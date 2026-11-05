"""Hermetic tests for the QA525 vNext state reader. No Docker, host or network."""
import copy
import importlib.util
from pathlib import Path
import re
import unittest

BASE = Path(__file__).resolve().parents[1] / "scripts" / "qa"


def load(name, filename):
    spec = importlib.util.spec_from_file_location(name, BASE / filename)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


reader = load("qa525_vnext_state_reader", "qa525-vnext-state-reader.py")
verify = load("qa525_vnext_verify_for_reader", "qa525-vnext-verify.py")

WEB_OLD, WEB_NEW = "a" * 40, "b" * 40
LAYERS_OLD = {"api": "/w/base.yml,/w/img-api-old.json", "web": "/w/base.yml,/w/img-web-old.json"}
LAYERS_NEW = {"api": "/w/base.yml,/w/dual-new-api.json", "web": "/w/base.yml,/w/dual-new-web.json"}


def cid(seed):
    return (seed * 64)[:64]


def row(name, seed, image, state="running", layers="/w/base.yml", exit_code=0):
    return [cid(seed), "sha256:" + image * 64, state, "2026-11-05T00:00:00Z", 0, name,
            "/w", layers, [], {}, {}, exit_code]


def rows(new=False, **override):
    data = {
        "api": row("api", "1", "5" if new else "1", layers=LAYERS_NEW["api"] if new else LAYERS_OLD["api"]),
        "web": row("web", "2", "6" if new else "2", layers=LAYERS_NEW["web"] if new else LAYERS_OLD["web"]),
        "keycloak": row("keycloak", "3", "3"), "postgres": row("postgres", "4", "4"),
        "minio": row("minio", "7", "7"), "gateway": row("gateway", "8", "8"),
        "clamav": row("clamav", "9", "9"),
        "db-grants": row("db-grants", "c", "c", "exited"),
        "migrator": row("migrator", "d", "d", "exited"),
        "minio-init": row("minio-init", "e", "e", "exited"),
    }
    data.update(override)
    return copy.deepcopy(data)


def make(state, web_sha=WEB_OLD, healthy=True):
    health = {"ok": healthy, "web_sha": web_sha}
    return reader.StateReader(lambda: copy.deepcopy(state["rows"]), lambda: WEB_OLD,
                              lambda: dict(health))


class StateReaderTests(unittest.TestCase):
    def test_baseline_feeds_the_verifier_for_applied_and_rolled_back(self):
        state = {"rows": rows()}
        baseline = make(state).capture_baseline()
        verify.validate_baseline(baseline)
        self.assertEqual(baseline["layers"]["api"], ["/w/base.yml", "/w/img-api-old.json"])
        self.assertEqual(baseline["oneshots"]["migrator"], {"id": cid("d"), "exit_code": 0})
        state["rows"] = rows(new=True)
        target = {"images": {"api": "sha256:" + "5" * 64, "web": "sha256:" + "6" * 64},
                  "layers": {"api": ["/w/base.yml", "/w/dual-new-api.json"],
                             "web": ["/w/base.yml", "/w/dual-new-web.json"]}, "web_sha": WEB_NEW}
        applied = verify.verify(make(state, WEB_NEW), baseline, "APPLIED", target)
        self.assertTrue(applied["ok"], applied)
        state["rows"] = rows()
        rolled = verify.verify(make(state, WEB_OLD), baseline, "ROLLED_BACK")
        self.assertTrue(rolled["ok"], rolled)

    def test_the_6eed4_orphan_layer_is_not_ok_through_the_whole_chain(self):
        state = {"rows": rows()}
        baseline = make(state).capture_baseline()
        orphan = LAYERS_NEW["api"] + ",/w/qa525-api-6eed4-orphan.json"
        state["rows"] = rows(new=True, api=row("api", "1", "5", layers=orphan))
        target = {"images": {"api": "sha256:" + "5" * 64, "web": "sha256:" + "6" * 64},
                  "layers": {"api": ["/w/base.yml", "/w/dual-new-api.json"],
                             "web": ["/w/base.yml", "/w/dual-new-web.json"]}, "web_sha": WEB_NEW}
        result = verify.verify(make(state, WEB_NEW), baseline, "APPLIED", target)
        self.assertEqual(result["code"], "LAYERS_MISMATCH_API")

    def test_changed_dependency_oneshot_or_stopped_service_is_visible_to_the_verifier(self):
        base_state = {"rows": rows()}
        baseline = make(base_state).capture_baseline()
        cases = {
            "DEPENDENCY_CHANGED": rows(postgres=row("postgres", "f", "4")),
            "ONESHOT_CHANGED": rows(migrator=row("migrator", "d", "d", "exited", exit_code=1)),
            "SERVICE_NOT_RUNNING_WEB": rows(web=row("web", "2", "2", "exited", layers=LAYERS_OLD["web"])),
        }
        for code, state_rows in cases.items():
            with self.subTest(code):
                result = verify.verify(make({"rows": state_rows}), baseline, "ROLLED_BACK")
                self.assertEqual(result["code"], code)

    def test_malformed_rows_fail_closed(self):
        broken = {
            "missing service": lambda d: d.pop("clamav"),
            "extra service": lambda d: d.update(extra=row("extra", "1", "1")),
            "wrong name inside row": lambda d: d["api"].__setitem__(5, "web"),
            "short row": lambda d: d["api"].pop(),
            "bad id": lambda d: d["api"].__setitem__(0, "xyz"),
            "short id": lambda d: d["keycloak"].__setitem__(0, "ab12"),
            "bad image": lambda d: d["api"].__setitem__(1, "latest"),
            "exit code is bool": lambda d: d["migrator"].__setitem__(11, True),
            "state not str": lambda d: d["api"].__setitem__(2, None),
            "no layers": lambda d: d["api"].__setitem__(7, ""),
            "relative layer": lambda d: d["api"].__setitem__(7, "rel.yml"),
            "dotdot layer": lambda d: d["api"].__setitem__(7, "/w/../x.yml"),
            "duplicate layer": lambda d: d["api"].__setitem__(7, "/w/a.yml,/w/a.yml"),
            "trailing slash": lambda d: d["web"].__setitem__(7, "/w/a.yml/"),
        }
        for name, mutate in broken.items():
            with self.subTest(name):
                data = rows()
                mutate(data)
                with self.assertRaises(reader.Stop):
                    reader.baseline_from_rows(data, WEB_OLD)
                with self.assertRaises(reader.Stop):
                    reader.observation_from_rows(data, {"ok": True, "web_sha": WEB_OLD})
        with self.assertRaises(reader.Stop):
            reader.baseline_from_rows([], WEB_OLD)

    def test_baseline_requires_a_clean_live_system(self):
        for name, change in {
            "api stopped": {"api": row("api", "1", "1", "exited", layers=LAYERS_OLD["api"])},
            "oneshot still running": {"migrator": row("migrator", "d", "d", "running")},
            "oneshot failed": {"minio-init": row("minio-init", "e", "e", "exited", exit_code=2)},
        }.items():
            with self.subTest(name), self.assertRaises(reader.Stop):
                reader.baseline_from_rows(rows(**change), WEB_OLD)
        for bad in ("short", "A" * 40, None, 7):
            with self.subTest(sha=bad), self.assertRaises(reader.Stop):
                reader.baseline_from_rows(rows(), bad)
        # An observation may describe a stopped service; the verifier decides.
        obs = reader.observation_from_rows(
            rows(api=row("api", "1", "1", "exited", layers=LAYERS_OLD["api"])),
            {"ok": False, "web_sha": WEB_OLD})
        self.assertIs(obs["running"]["api"], False)

    def test_health_shape_and_providers_are_strict(self):
        for health in ({"ok": 1, "web_sha": WEB_OLD}, {"ok": True}, {"ok": True, "web_sha": 5},
                       {"ok": True, "web_sha": WEB_OLD, "x": 1}, [], None):
            with self.subTest(health=str(health)), self.assertRaises(reader.Stop):
                reader.observation_from_rows(rows(), health)
        with self.assertRaises(reader.Stop):
            reader.StateReader(None, lambda: WEB_OLD, lambda: {})

    def test_provider_failures_propagate_and_never_become_an_observation(self):
        def boom():
            raise OSError("docker unreachable")
        r = reader.StateReader(boom, lambda: WEB_OLD, lambda: {"ok": True, "web_sha": WEB_OLD})
        with self.assertRaises(OSError):
            r.observe()
        # Through the verifier an unreadable runtime is indeterminate, never OK.
        base = make({"rows": rows()}).capture_baseline()
        self.assertEqual(verify.verify(r, base, "ROLLED_BACK")["code"], "VERIFY_INDETERMINATE")

    def test_module_has_no_io_and_never_touches_secrets(self):
        source = (BASE / "qa525-vnext-state-reader.py").read_text(encoding="utf-8")
        for forbidden in ("import subprocess", "import socket", "import os", "import sys",
                          "open(", "Popen", "os.system", "Config.Env", ".Env", "logs"):
            self.assertNotIn(forbidden, source, forbidden)
        self.assertIsNone(re.search(r"\bdocker\b\s*\(", source))


if __name__ == "__main__":
    unittest.main()
