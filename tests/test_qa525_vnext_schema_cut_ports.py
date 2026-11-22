import datetime as dt
import hashlib
import importlib.util
from pathlib import Path
import unittest

SCRIPTS = Path(__file__).resolve().parents[1] / "scripts" / "qa"


def load(name, filename):
    spec = importlib.util.spec_from_file_location(name, SCRIPTS / filename)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


pt = load("schema_cut_ports", "qa525-vnext-schema-cut-ports.py")
ad = load("schema_adapter_ports", "qa525-vnext-schema-adapter.py")
dg = lambda b: "sha256:" + hashlib.sha256(b).hexdigest()
IDS100 = sorted("20200101000000_Synthetic%03d" % i for i in range(100))
IDS104 = sorted(IDS100 + list(ad.REQUIRED))
API, WEB, MIG = "sha256:" + "a" * 64, "sha256:" + "b" * 64, "sha256:" + "c" * 64
REV = {"api": "1" * 40, "web": "2" * 40}
NOW = 1_800_000_000
CID = "d" * 64


def clock(t=NOW):
    return lambda: dt.datetime.fromtimestamp(t, dt.timezone.utc)


def spec(**o):
    s = {"pg_container": "pg", "expires_at": NOW + 600, "images": {"api": API, "web": WEB}, "revisions": dict(REV),
         "migrator_image_id": MIG, "baseline_sha256": dg("\n".join(IDS100).encode())}
    s.update(o)
    return s


class Runner:
    def __init__(self, history=IDS100, **bad):
        self.calls, self.history, self.bad = [], history, bad

    def __call__(self, argv, timeout, stdin):
        self.calls.append(argv)
        text = " ".join(argv)
        if "stop" in argv:
            return self.bad.get("stop", (0, b"x\n"))
        if "{{.State.Running}}" in argv:
            return self.bad.get("running", (0, b"false\n"))
        if "count(*)" in text:
            return self.bad.get("sessions", (0, b"0\n"))
        if "__EFMigrationsHistory" in text:
            return self.bad.get("history", (0, ("\n".join(self.history) + "\n").encode()))
        if "{{.Id}}" in argv:
            return self.bad.get("migrator", (0, (MIG + "\n").encode()))
        if "image" in argv and "inspect" in argv:
            image = argv[-1]
            rev = REV["api"] if image == API else REV["web"]
            return self.bad.get("image_" + ("api" if image == API else "web"), (0, (rev + "\n").encode()))
        raise AssertionError(argv)


class SchemaCutPortsTests(unittest.TestCase):
    def test_preflight_ok_and_each_failure_stops(self):
        self.assertIsNone(pt.make_preflight(Runner(), clock(), lambda: False, spec())())
        for bad in ({"migrator": (0, b"sha256:" + b"0" * 64 + b"\n")}, {"image_api": (0, b"0" * 40 + b"\n")},
                    {"image_web": (1, b"")}, {"history": (1, b"")}):
            with self.assertRaises(pt.Stop):
                pt.make_preflight(Runner(**bad), clock(), lambda: False, spec())()
        with self.assertRaises(pt.Stop):  # history differs from the plan
            pt.make_preflight(Runner(history=IDS100[:-1] + ["20200101000000_Other"]), clock(), lambda: False, spec())()
        with self.assertRaises(pt.Stop):  # 104 rows is not the baseline
            pt.make_preflight(Runner(history=IDS104), clock(), lambda: False, spec())()

    def test_gate_closes_on_expiry_abort_or_clock_fault(self):
        for c, a in ((clock(NOW + 601), lambda: False), (clock(), lambda: True), (clock(), lambda: None),
                     (lambda: 1 / 0, lambda: False)):
            with self.assertRaises(pt.Stop) as caught:
                pt.make_preflight(Runner(), c, a, spec())()
            self.assertEqual("SCHEMA_PORT_GATE_CLOSED", str(caught.exception))

    def test_verify_backup_needs_pins_age_and_verifier(self):
        files = {n: ("evidence-" + n).encode() for n in pt.EVIDENCE}
        pins = {n: dg(b) for n, b in files.items()}
        ok = pt.make_verify_backup(files.__getitem__, lambda d: True, clock(), pins, NOW - 3600)
        self.assertIs(ok(), True)
        for kwargs in ({"verifier": lambda d: False}, {"verifier": lambda d: "yes"}, {"epoch": NOW - 49 * 3600},
                       {"epoch": NOW + 5}):
            port = pt.make_verify_backup(files.__getitem__, kwargs.get("verifier", lambda d: True), clock(), pins,
                                         kwargs.get("epoch", NOW - 3600))
            with self.assertRaises(pt.Stop):
                port()
        tampered = dict(files, receipt=b"other")
        with self.assertRaises(pt.Stop):
            pt.make_verify_backup(tampered.__getitem__, lambda d: True, clock(), pins, NOW - 3600)()

    def test_quiesce_stops_the_exact_api_container_and_requires_zero_sessions(self):
        s = {"pg_container": "pg", "api_container_id": CID, "write_roles": ("pmgm_runtime", "pmgm_app")}
        runner = Runner()
        self.assertIs(pt.make_quiesce_writes(runner, s)(), True)
        self.assertEqual(("/usr/bin/docker", "stop", "--time", "30", CID), runner.calls[0])
        self.assertIn("usename IN ('pmgm_runtime','pmgm_app')", " ".join(runner.calls[2]))
        for bad in ({"stop": (1, b"")}, {"running": (0, b"true\n")}, {"sessions": (0, b"2\n")},
                    {"sessions": (1, b"")}):
            with self.assertRaises(pt.Stop):
                pt.make_quiesce_writes(Runner(**bad), s)()

    def test_quiesce_rejects_injectable_roles_and_ids(self):
        for roles in (("pmgm_x'; DROP TABLE a;--",), (), ("pmgm_runtime", "pmgm_runtime"), ["pmgm_runtime"]):
            with self.assertRaises(pt.Stop):
                pt.make_quiesce_writes(Runner(), {"pg_container": "pg", "api_container_id": CID, "write_roles": roles})
        with self.assertRaises(pt.Stop):
            pt.make_quiesce_writes(Runner(), {"pg_container": "pg", "api_container_id": "../x", "write_roles": ("pmgm_app",)})

    def test_revalidate_requires_104_and_images(self):
        sha104 = dg("\n".join(IDS104).encode())
        self.assertIs(pt.make_revalidate(Runner(history=IDS104), clock(), lambda: False, spec(), sha104)(), True)
        for runner in (Runner(history=IDS100), Runner(history=IDS104, image_web=(1, b""))):
            with self.assertRaises(pt.Stop):
                pt.make_revalidate(runner, clock(), lambda: False, spec(), sha104)()

    def test_apply_wrapper_is_applied_only_on_the_exact_triple(self):
        sha = "2" * 40
        good = {"journal": "APPLIED", "health": True, "served_sha": sha}
        self.assertEqual({"terminal": "APPLIED"}, pt.make_apply_api_web(lambda: good, sha)())
        for bad in (dict(good, journal="ROLLED_BACK"), dict(good, health=False), dict(good, health="yes"),
                    dict(good, served_sha="3" * 40), dict(good, extra=1), None, "APPLIED"):
            self.assertEqual({"terminal": "FAILED"}, pt.make_apply_api_web(lambda b=bad: b, sha)())

    def test_uat_stops_at_the_first_failure_and_never_retries(self):
        seen = []
        checks = (lambda: seen.append(1) or True, lambda: seen.append(2) or False, lambda: seen.append(3) or True)
        self.assertIs(pt.make_uat(checks)(), False)
        self.assertEqual([1, 2], seen)
        self.assertIs(pt.make_uat((lambda: True, lambda: True))(), True)
        self.assertIs(pt.make_uat((lambda: "yes",))(), False)

    def test_schema_port_binds_the_adapter_and_validates_the_plan_early(self):
        class Fake:
            def __init__(self):
                self.args = None

            def plan_commands(self, p):
                if p.get("bad"):
                    raise ad.Stop("SCHEMA_ADAPTER_INPUT_INVALID")

            def run_phases(self, journal, runner, params):
                self.args = (journal, runner, params)
                return "done"
        fake = Fake()
        runner = lambda argv, timeout, stdin: (0, b"")
        port = pt.make_schema(fake, {"x": 1}, runner)
        self.assertEqual("done", port("journal"))
        self.assertEqual(("journal", runner, {"x": 1}), fake.args)
        with self.assertRaises(ad.Stop):
            pt.make_schema(Fake(), {"bad": 1}, runner)

    def test_invalid_specs_are_refused_without_effects(self):
        runner = Runner()
        for bad in (spec(expires_at=0), spec(expires_at=True), spec(images={"api": API}), spec(pg_container="a b"),
                    spec(revisions={"api": "x", "web": "y"}), None):
            with self.assertRaises(pt.Stop):
                pt.make_preflight(runner, clock(), lambda: False, bad)
        self.assertEqual([], runner.calls)

    def test_runner_errors_are_opaque(self):
        def boom(argv, timeout, stdin):
            raise RuntimeError("password=hunter2")
        with self.assertRaises(pt.Stop) as caught:
            pt.make_preflight(boom, clock(), lambda: False, spec())()
        self.assertEqual("SCHEMA_PORT_EFFECT_FAILED", str(caught.exception))
        self.assertNotIn("hunter2", repr(caught.exception))


if __name__ == "__main__":
    unittest.main()
