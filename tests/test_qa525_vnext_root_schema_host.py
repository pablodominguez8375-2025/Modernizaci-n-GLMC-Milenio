"""Schema host assembly (offline): real plan gate + ports + adapter + sequencer, scripted effects, synthetic data."""
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import unittest
import zipfile

HERE = Path(__file__).resolve().parent


def load(name, relative):
    spec = importlib.util.spec_from_file_location(name, HERE.parent / "scripts" / "qa" / relative)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


host = load("sh_host", "qa525-vnext-root-schema-host.py")
ports = load("sh_ports", "qa525-vnext-schema-cut-ports.py")
ad = load("sh_adapter", "qa525-vnext-schema-adapter.py")
plan_mod = load("sh_plan", "qa525-vnext-schema-cut-plan.py")
ex = load("sh_exec", "qa525-vnext-schema-cut-executor.py")
adapter_tests = load("sh_adapter_tests", "../../tests/test_qa525_vnext_schema_adapter.py")
MODULES = {"ports": ports, "adapter": ad, "plan": plan_mod}


def dg(b):
    return "sha256:" + hashlib.sha256(b).hexdigest()


def fixed_params():
    return adapter_tests.params(**{k: host.FIXED[k] for k in ("network", "pg_container", "migrator_env_file", "grants_env_file")})


def case():
    p = fixed_params()
    src = "a" * 40
    api, web, arch = "sha256:" + "1" * 64, "sha256:" + "2" * 64, "sha256:" + "3" * 64
    plan = {"schema": "qa525-schema-cut-plan-v1", "host": "glmdch", "environment": "QA_SYNTHETIC",
            "app_source_sha": src, "app_archive_sha256": arch, "migrator_image_digest": p["migrator_image_id"],
            "api_image_id": api, "web_image_id": web, "history_ids": p["baseline_history_ids"],
            "pending_ids": list(plan_mod.REQUIRED), "grant_script_sha256": p["grant_script_sha256"],
            "postgate_sha256": p["postgate_sha256"]}
    raw = (json.dumps(plan, sort_keys=True, separators=(",", ":")) + "\n").encode()
    buf = io.BytesIO()
    with zipfile.ZipFile(buf, "w") as z:
        z.writestr(host.FIXED["grant_script_member"], p["grant_script"])
        z.writestr(host.FIXED["gate_sql_member"], p["gate_sql"])
    pins = {"migrator_image_digest": p["migrator_image_id"], "baseline_history_sha256":
            dg("\n".join(p["baseline_history_ids"]).encode()), "grant_script_sha256": p["grant_script_sha256"],
            "postgate_sha256": p["postgate_sha256"], "api_image_id": api, "web_image_id": web}
    claims = {"app_source_sha": src, "app_archive_sha256": arch, "plan_sha256": dg(raw), "expires_at": 2000000000}
    authority = {"g6": {v: dg(k.encode()) for k, v in host.G6_MAP.items()}, "target": {"web_sha": src}}
    return p, raw, buf.getvalue(), pins, claims, authority


class Plane:
    def assert_held(self):
        pass


class Effects:
    def __init__(self, **over):
        self.calls = []
        self.runner = lambda argv, t, s: (0, b"")
        self.clock = lambda: __import__("datetime").datetime.fromtimestamp(1900000000)
        self.abort_requested = lambda: False
        self.read_evidence = lambda n: n.encode()
        self.verifier = lambda d: True
        self.flight = lambda: {"journal": "APPLIED", "health": True, "served_sha": "a" * 40}
        self.uat_checks = (lambda: True,)
        self.archive_epoch = 1900000000 - 3600
        self.api_container_id = "b" * 12
        for k, v in over.items():
            setattr(self, k, v)


class EffModule:
    def __init__(self, effects):
        self.effects = effects

    def provide(self, authority, claims):
        if isinstance(self.effects, Exception):
            raise self.effects
        return self.effects


def bind(effects=None):
    host.bind_dependencies(dict(MODULES, effects=EffModule(effects if effects is not None else Effects())))


def case_with_member(p):
    return p


def build(effects=None, **kw):
    p, raw, arch, pins, claims, authority = case()
    bind(effects)
    args = dict(archive_bytes=arch, plan_bytes=raw, claims=claims, plane=Plane(), authority=authority,
                schema_pins=pins)
    args.update(kw)
    return host.build_steps(**args), args


class HostTests(unittest.TestCase):
    def test_builds_exactly_the_sequencer_steps(self):
        steps, _ = build()
        self.assertEqual(set(steps), ex.STEPS)
        self.assertTrue(all(callable(v) for v in steps.values()))

    def test_precheck_ok_and_fails_closed_without_uat_or_effects(self):
        _, a = build()
        ok = dict(plan_bytes=a["plan_bytes"], claims=a["claims"], authority=a["authority"], schema_pins=a["schema_pins"])
        bind()
        self.assertTrue(host.precheck(**ok))
        for bad in (Effects(uat_checks=()), Effects(uat_checks=None), Effects(verifier=None), Effects(api_container_id=None),
                    RuntimeError("no credential")):
            bind(bad)
            with self.assertRaises(host.Stop) as c:
                host.precheck(**ok)
            self.assertRegex(str(c.exception), r"^SCHEMA_HOST_[A-Z_]+$")
        bind()
        with self.assertRaises(host.Stop):
            host.precheck(**dict(ok, plan_bytes=ok["plan_bytes"] + b" "))
        host._DEPS.clear()
        with self.assertRaises(host.Stop):  # unbound siblings
            host.precheck(**ok)

    def test_host_constants_are_fixed_and_roles_exact(self):
        self.assertEqual(set(host.FIXED["write_roles"]), {"pmgm_runtime", "pmgm_app", "pmgm_keycloak"})
        _, a = build()
        for extra in ({"schema_host": {}}, {"pg_container": "x"}):
            auth = dict(a["authority"], **extra)  # extra keys never change the fixed constants
            steps, _ = build(authority=auth)
            self.assertEqual(set(steps), ex.STEPS)

    def test_plan_or_archive_drift_is_refused_with_fixed_code(self):
        p, raw, arch, pins, claims, authority = case()
        buf = io.BytesIO()
        with zipfile.ZipFile(buf, "w") as z:
            z.writestr(host.FIXED["grant_script_member"], b"evil")
            z.writestr(host.FIXED["gate_sql_member"], p["gate_sql"])
        for kw in ({"plan_bytes": raw + b" "}, {"archive_bytes": buf.getvalue()}, {"archive_bytes": b"nozip"}):
            with self.assertRaises(host.Stop) as c:
                build(**kw)
            self.assertRegex(str(c.exception), r"^SCHEMA_HOST_[A-Z_]+$")

    def test_duplicate_or_missing_archive_member_is_refused(self):
        p, raw, arch, pins, claims, authority = case()
        buf = io.BytesIO()
        with zipfile.ZipFile(buf, "w") as z:
            z.writestr(host.FIXED["grant_script_member"], p["grant_script"])
        with self.assertRaises(host.Stop):
            build(archive_bytes=buf.getvalue())

    def test_pin_mismatch_between_go_and_plan_is_refused(self):
        _, a = build()
        pins = dict(a["schema_pins"], api_image_id="sha256:" + "9" * 64)
        with self.assertRaises(host.Stop):
            build(schema_pins=pins)

    def test_full_sequence_with_real_sequencer_and_scripted_effects(self):
        p = fixed_params()
        fake = adapter_tests.Fake(p)
        revs = ("a" * 40 + "\n").encode()

        def runner(argv, t, s):
            if tuple(argv) == fake.cmds["history"] and not self.state:
                return 0, ("\n".join(p["baseline_history_ids"]) + "\n").encode()
            if tuple(argv) == fake.cmds["migrate"]:
                self.state = True
            try:
                return fake(argv, t, s)
            except AssertionError:
                a = tuple(argv)
                if "image" in a and "--format" in a and "{{.Id}}" in a:
                    return 0, (a[-1] + "\n").encode()
                if "image" in a:
                    return 0, revs
                if a[1] == "stop":
                    return 0, b""
                if a[1] == "inspect":
                    return 0, b"false\n"
                sql = a[-1]
                if "pg_stat_activity" in sql:
                    return 0, b"0\n"
                hist = p["baseline_history_ids"] if not self.state else ad.expected_history_after(p["baseline_history_ids"])
                return 0, ("\n".join(hist) + "\n").encode()
        self.state = False
        phases = []

        def journal(phase):
            phases.append(phase)
            if phase == "POSTGATE_PASS":
                self.state = True
            return {"phase": phase, "failure_class_if_interrupted": "PARTIAL_SCHEMA_STOP" if len(phases) > 3 else "PRE_DDL_STOP"}
        steps, _ = build(Effects(runner=runner))
        out = ex.run(journal, Plane(), steps)
        self.assertEqual(tuple(out["phases"]), ex.PHASES_OK)

    def test_flight_not_applied_stops_before_uat(self):
        steps, _ = build(Effects(flight=lambda: {"journal": "FAILED", "health": True, "served_sha": "a" * 40}))
        self.assertEqual(steps["apply_api_web"](), {"terminal": "FAILED"})


if __name__ == "__main__":
    unittest.main()
