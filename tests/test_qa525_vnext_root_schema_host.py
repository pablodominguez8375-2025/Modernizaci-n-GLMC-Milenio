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
class EnvRender:
    @staticmethod
    def render(source, history):
        return b"synthetic", b"synthetic"

class EnvFiles:
    @staticmethod
    def ephemeral_env_files(*, base_dir, source_reader, history, render, verify_tmpfs, uid):
        from contextlib import contextmanager
        @contextmanager
        def scope():
            assert base_dir == "/run/qa525-schema-env"
            assert uid == 0
            assert verify_tmpfs("/run") is True
            yield (base_dir + "/migrator.env", base_dir + "/grants.env")
        return scope()

MODULES = {"ports": ports, "adapter": ad, "plan": plan_mod,
           "env_render": EnvRender, "env_files": EnvFiles}


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
            "api_image_id": api, "web_image_id": web, "api_container_id": "b" * 64, "history_ids": p["baseline_history_ids"],
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
    authority = {"g6": {v: dg(k.encode()) for k, v in host.G6_MAP.items()},
                 "target": {"web_sha": src}, "baseline_sha256": dg(b"baseline")}
    return p, raw, buf.getvalue(), pins, claims, authority


class Plane:
    def assert_held(self):
        pass


class Effects:
    def __init__(self, **over):
        self.calls = []
        self.runner = lambda argv, t, s: (0, (("sha256:" + "1" * 64) + "\n").encode()) if "{{.Image}}" in argv else (0, b"")
        self.clock = lambda: __import__("datetime").datetime.fromtimestamp(1900000000)
        self.abort_requested = lambda: False
        self.read_evidence = lambda n: n.encode()
        self.verifier = lambda d: True
        self.uat_checks = (lambda: True,)
        self.archive_epoch = 1900000000 - 3600
        self.api_container_id = "b" * 64
        self.read_schema_source = lambda: b"synthetic"
        self.verify_schema_tmpfs = lambda p: p == "/run"
        self.schema_env_dir_exists = lambda: False
        for k, v in over.items():
            setattr(self, k, v)


class FlightAdapter:
    @staticmethod
    def preflight(*, archive_bytes, claims, authority, plane):
        return authority["baseline_sha256"]

    @staticmethod
    def precheck_schema_transition():
        return True

    result = {"journal": "APPLIED", "health": True, "served_sha": "a" * 40}
    calls = []

    @classmethod
    def make(cls, **kw):
        cls.calls.append(kw)
        return lambda: cls.result


class EffModule:
    def __init__(self, effects):
        self.effects = effects

    def provide(self, authority, claims):
        if isinstance(self.effects, Exception):
            raise self.effects
        return self.effects


def bind(effects=None):
    host.bind_dependencies(dict(MODULES, effects=EffModule(effects if effects is not None else Effects()),
                                flight_adapter=FlightAdapter))


def case_with_member(p):
    return p


def build(effects=None, **kw):
    p, raw, arch, pins, claims, authority = case()
    bind(effects)
    args = dict(archive_bytes=arch, plan_bytes=raw, claims=claims, plane=Plane(), authority=authority,
                schema_pins=pins)
    args.update(kw)
    return host.build_steps(**args), args


class SchemaCutHostTests(unittest.TestCase):
    def test_builds_exactly_the_sequencer_steps(self):
        steps, _ = build()
        self.assertEqual(set(steps), ex.STEPS)
        self.assertTrue(all(callable(v) for v in steps.values()))
        self.assertEqual(FlightAdapter.calls[-1]["claims"]["app_source_sha"], "a" * 40)

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

    def test_stale_env_directory_stops_before_flight_without_cleanup(self):
        _, a = build()
        ok = {k: a[k] for k in ("plan_bytes", "claims", "authority", "schema_pins")}
        events = []
        class FlightGuard:
            @staticmethod
            def precheck_schema_transition():
                events.append("flight")
        class NoCleanup:
            @staticmethod
            def ephemeral_env_files(**kwargs):
                events.append("cleanup_or_scope")
                raise AssertionError("must not enter scope")
        stale = Effects(schema_env_dir_exists=lambda: events.append("exists") or True,
                        read_schema_source=lambda: events.append("source") or b"synthetic")
        bind(stale)
        host._DEPS["flight_adapter"] = FlightGuard
        host._DEPS["env_files"] = NoCleanup
        with self.assertRaises(host.Stop) as caught:
            host.precheck(**ok)
        self.assertEqual(str(caught.exception), "SCHEMA_HOST_ENV_DIR_EXISTS")
        self.assertEqual(events, ["exists"])

    def test_container_must_match_plan_pin_and_pinned_image(self):
        _, a = build()
        ok = dict(plan_bytes=a["plan_bytes"], claims=a["claims"], authority=a["authority"], schema_pins=a["schema_pins"])
        bind(Effects(api_container_id="c" * 64))  # discovered by label but NOT the plan's pin
        with self.assertRaises(host.Stop) as c:
            host.precheck(**ok)
        self.assertEqual(str(c.exception), "SCHEMA_HOST_INPUT_INVALID")
        with self.assertRaises(host.Stop):
            build(Effects(api_container_id="c" * 64))
        wrong_image = Effects(runner=lambda argv, t, s: (0, ("sha256:" + "9" * 64 + "\n").encode()))
        bind(wrong_image)
        with self.assertRaises(host.Stop) as c:
            host.precheck(**ok)
        self.assertEqual(str(c.exception), "SCHEMA_HOST_CONTAINER_MISMATCH")
        bind(Effects(runner=lambda argv, t, s: (1, b"")))
        with self.assertRaises(host.Stop):
            host.precheck(**ok)
        bind()
        self.assertTrue(host.precheck(**ok))

    def test_host_constants_are_fixed_and_roles_exact(self):
        self.assertEqual(host.FIXED["network"], "centenario-qa-lab2_pmgm_srv01_internal")
        self.assertNotEqual(host.FIXED["network"], "centenario-qa-lab2_default")
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
            # Ephemeral files are a security boundary, not a change to Docker's
            # schema command contract. Normalize only the two generated paths
            # for the legacy scripted adapter fake.
            argv = tuple(argv)
            if argv[:2] == ("/usr/bin/docker", "run") and "--env-file" in argv:
                idx = argv.index("--env-file")
                argv = argv[:idx+1] + (p["migrator_env_file"],) + argv[idx+2:]
            if argv[:2] == ("/usr/bin/docker", "exec") and "--env-file" in argv:
                idx = argv.index("--env-file")
                argv = argv[:idx+1] + (p["grants_env_file"],) + argv[idx+2:]
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
                if "{{.Image}}" in a:
                    return 0, (("sha256:" + "1" * 64) + "\n").encode()
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
        previous = FlightAdapter.result
        try:
            FlightAdapter.result = {"journal": "FAILED", "health": True, "served_sha": "a" * 40}
            steps, _ = build()
            self.assertEqual(steps["apply_api_web"](), {"terminal": "FAILED"})
        finally:
            FlightAdapter.result = previous


if __name__ == "__main__":
    unittest.main()
