"""QA525 API_WEB host assembly + effects: real reader/plan/b7/adapters modules, fake docker/exec/fetch. No authority."""
import hashlib
import importlib.util
import json
import os
import pathlib
import stat
import tempfile
import types
import unittest
from pathlib import Path

BASE = pathlib.Path(__file__).resolve().parents[1] / "scripts" / "qa"


def load(name):
    spec = importlib.util.spec_from_file_location("qa525_t_" + name[:-3].replace("-", "_"), BASE / name)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


host = load("qa525-vnext-root-apiweb-host.py")
fx = load("qa525-vnext-root-apiweb-effects.py")
M = {k: load(v) for k, v in {
    "host_adapters": "qa525-vnext-host-adapters.py", "state_reader": "qa525-vnext-state-reader.py",
    "health": "qa525-vnext-health.py", "plan": "qa525-vnext-baseline-plan.py",
    "b7": "qa525-vnext-b7-held-readonly.py", "containment": "qa525-vnext-containment.py",
    "root_locks": "qa525-vnext-root-locks.py", "boundary": "qa525-vnext-b7-root-boundary.py",
    "scopes": "qa525-vnext-scope-store.py", "physical_runner": "qa525-vnext-held-physical-runner.py"}.items()}
host.ROOT_UID = fx.ROOT_UID = os.geteuid()
D = lambda c: "sha256:" + c * 64
SERVICES = ["api", "web", "keycloak", "postgres", "minio", "gateway", "clamav", "db-grants", "migrator", "minio-init"]
ONESHOT = {"db-grants", "migrator", "minio-init"}
API_LAYERS = ",".join("/opt/q/api%d.yml" % i for i in range(6))
WEB_LAYERS = ",".join("/opt/q/web%d.yml" % i for i in range(5))


def cid(i):
    return ("%02x" % i) * 32


def row(i, name, state="running"):
    layers = API_LAYERS if name == "api" else WEB_LAYERS if name == "web" else "/opt/q/c.yml"
    return [cid(i), "sha256:" + ("%02x" % (100 + i)) * 32, "exited" if name in ONESHOT else state, "t", 0, name,
            "/opt/q", layers, [], {}, {}, 0]


class ApiwebHostFakeDocker:
    def __init__(self):
        self.calls = []
        self.rows = {n: row(i + 1, n) for i, n in enumerate(SERVICES)}

    def __call__(self, args):
        self.calls.append(list(args))
        if args[0] == "ps":
            return "\n".join(r[0] for r in self.rows.values())
        if args[0] == "inspect":
            return json.dumps(next(r for r in self.rows.values() if r[0] == args[3]))
        if args[:2] == ["image", "inspect"]:
            return "a" * 40
        raise AssertionError(args)


class ApiwebHostFx:
    def __init__(self, docker):
        self.docker, self.log = docker, []

    def docker_reader(self):
        return self.docker

    def executor(self):
        return lambda argv: (self.log.append(("exec", argv)), 0)[1]

    def pin_verifier(self, paths):
        self.paths = paths
        return lambda expected: True

    def https_fetch(self):
        def fetch(url):
            if url.endswith("/qa-version.json"):
                return 200, url, json.dumps({"environment": "QA_SYNTHETIC", "source_sha": "a" * 40}).encode()
            if url.endswith("/health/ready"):
                return 200, url, b'{"status":"ready","database":"postgresql"}'
            if url.endswith("/health/web"):
                return 200, url, b"ok"
            return 200, url, b"<html>" + b"x" * 10 + b"</html>"
        return fetch


class ApiwebHostBase(unittest.TestCase):
    def setUp(self):
        self.dir = tempfile.mkdtemp()
        self.addCleanup(lambda: __import__("shutil").rmtree(self.dir, ignore_errors=True))
        os.chmod(self.dir, 0o700)
        self.files = {}
        for key, name in host.EVIDENCE.items():
            self.write(name, ("evidence-" + key).encode())
        self.docker = ApiwebHostFakeDocker()
        self.fx = ApiwebHostFx(self.docker)
        self.authority = self.make_authority()

    def write(self, name, data):
        path = os.path.join(self.dir, name)
        if os.path.lexists(path):
            os.unlink(path)
        Path(path).write_bytes(data)
        os.chmod(path, 0o600)
        self.files[name] = data

    def make_authority(self):
        sha = lambda k: "sha256:" + hashlib.sha256(("evidence-" + k).encode()).hexdigest()
        return {
            "g6": {"pinned_admission_sha256": sha("admission"), "pinned_approval_sha256": sha("approval"),
                   "pinned_review_sha256": sha("review"), "pinned_receipt_sha256": sha("receipt"),
                   "pinned_restore_sha256": sha("restore"), "observed_archive_sha256": D("1"),
                   "downloaded_offhost_sha256": D("1"), "expected_restic_repo_id": "b" * 64,
                   "expected_key_epoch": "c" * 32},
            "exec_pins": {k: D("2") for k in host.EXEC_KEYS},
            "exec_paths": {k: "/usr/bin/" + k for k in host.EXEC_KEYS},
            "target": {"images": {"api": D("7"), "web": D("8")},
                       "layers": {"api": ["/opt/q/api%d.yml" % i for i in range(6)],
                                  "web": ["/opt/q/web%d.yml" % i for i in range(5)]}, "web_sha": "d" * 40},
            "baseline_sha256": D("3"), "admission_sha256": D("4"), "policy_hash": D("5"),
            "journal_root": "/var/lib/qa525/journal",
            "host": {"project": "centenario-qa-lab2", "workdir": "/opt/q", "env_file": "/etc/q.env",
                     "evidence_dir": self.dir}}

    def build(self, **over):
        kw = dict(archive_bytes=b"zip", claims={"expires_at": 1800000300}, authority=self.authority, modules=M,
                  effects=self.fx, now=None)
        kw.update(over)
        return host.build(kw["archive_bytes"], kw["claims"], kw["authority"], kw["modules"], kw["effects"], kw["now"])

    def stop(self, code, **over):
        with self.assertRaises(host.Stop) as err:
            self.build(**over)
        self.assertEqual(str(err.exception), code)


class ApiwebHostAuthority(ApiwebHostBase):
    def test_every_missing_extra_or_malformed_authority_field_is_refused(self):
        def mutate(fn):
            a = json.loads(json.dumps(self.authority))
            fn(a)
            return a
        for fn in (lambda a: a.pop("g6"), lambda a: a.update(extra=1), lambda a: a["g6"].update(extra=1),
                   lambda a: a["g6"].update(pinned_review_sha256="x"), lambda a: a["g6"].update(expected_key_epoch="c" * 31),
                   lambda a: a["exec_pins"].pop("docker_sha256"), lambda a: a["exec_paths"].update(docker_sha256="docker"),
                   lambda a: a["target"].update(web_sha="d" * 39), lambda a: a["target"]["layers"].update(api=[]),
                   lambda a: a["target"]["layers"]["api"].append("../x"), lambda a: a["target"]["images"].pop("web"),
                   lambda a: a.update(baseline_sha256="3" * 64), lambda a: a["host"].update(workdir="rel"),
                   lambda a: a.update(policy_hash=None), lambda a: a.update(journal_root="j")):
            with self.subTest(fn=fn):
                self.stop("APIWEB_HOST_AUTHORITY_INVALID", authority=mutate(fn))
        self.stop("APIWEB_HOST_AUTHORITY_INVALID", authority=None)
        self.assertEqual(self.docker.calls, [])

    def test_missing_modules_or_effects_stop_first(self):
        self.stop("APIWEB_HOST_BINDING_INVALID", modules={k: v for k, v in M.items() if k != "b7"})
        self.stop("APIWEB_HOST_BINDING_INVALID", effects=types.SimpleNamespace())
        self.stop("APIWEB_HOST_BINDING_INVALID", archive_bytes="zip")


class ApiwebHostEvidence(ApiwebHostBase):
    def test_evidence_must_match_its_pin_and_be_root_private_and_present(self):
        self.write("g6-review.json", b"tampered")
        self.stop("APIWEB_EVIDENCE_PIN_MISMATCH")
        self.write("g6-review.json", b"evidence-review")
        os.chmod(os.path.join(self.dir, "g6-review.json"), 0o644)
        self.stop("APIWEB_EVIDENCE_UNSAFE")
        os.chmod(os.path.join(self.dir, "g6-review.json"), 0o600)
        os.unlink(os.path.join(self.dir, "g6-receipt.json"))
        os.symlink(os.path.join(self.dir, "g6-review.json"), os.path.join(self.dir, "g6-receipt.json"))
        self.stop("APIWEB_EVIDENCE_MISSING")  # O_NOFOLLOW: a symlink is never followed
        self.assertEqual(self.docker.calls, [])

    def test_evidence_directory_writable_by_others_is_refused(self):
        os.chmod(self.dir, 0o777)
        self.stop("APIWEB_EVIDENCE_UNSAFE")

    def test_the_zip_never_supplies_authority(self):
        a = self.build(archive_bytes=b"PK" + b"\0" * 10)
        b = self.build(archive_bytes=b"completely different bytes")
        self.assertEqual(a["inputs"]["target"], b["inputs"]["target"])
        self.assertEqual(a["g6_evidence"], b["g6_evidence"])


class ApiwebHostAssembled(ApiwebHostBase):
    def test_inputs_cover_exactly_what_run_held_requires_and_nothing_executes_at_build(self):
        held = load("qa525-vnext-held-flight-bridge.py")
        built = self.build()
        required = {"clock", "external_gate", "runner_factory", "scope_factory", "settle", "inspector", "reader",
                    "port", "root", "admission", "admission_sha256", "admission_bytes", "approval_bytes",
                    "policy_hash", "baseline_sha256", "target", "project", "workdir", "env_file"}
        self.assertEqual(set(built["inputs"]) | {"port"}, required)
        self.assertEqual(set(built["g6_evidence"]), held.G6_BYTES | held.G6_PINS | set(held.G6_IDS))
        i = built["inputs"]
        self.assertEqual((i["policy_hash"], i["admission_sha256"], i["baseline_sha256"]), (D("5"), D("4"), D("3")))
        self.assertEqual(i["admission"], Path(self.dir) / "admission.json")
        self.assertEqual(self.docker.calls + self.fx.log, [])
        i["target"]["layers"]["api"].append("/x")
        self.assertEqual(len(self.authority["target"]["layers"]["api"]), 6)  # detached copy

    def test_inspector_reports_the_real_baseline_digest_through_the_read_only_guard(self):
        i = self.build()["inputs"]
        report = i["inspector"]()
        held_b7 = M["b7"]
        base = i["reader"].capture_baseline()
        self.assertEqual(report["baseline_sha256"], M["plan"].digest(base))
        held_b7._shape(report, report["baseline_sha256"])  # exact B7 shape (7 running, 3 one-shots, layers, samples)
        verbs = {c[0] if c[0] != "image" else "image inspect" for c in self.docker.calls}
        self.assertEqual(verbs, {"ps", "inspect", "image inspect"})  # nothing but read verbs ever issued

    def test_inspector_refuses_an_unstable_or_degraded_runtime(self):
        i = self.build()["inputs"]
        self.docker.rows["postgres"][2] = "exited"
        with self.assertRaises(Exception):
            i["inspector"]()

    def test_gate_scope_settle_and_clock_are_the_reviewed_adapters(self):
        i = self.build()["inputs"]
        self.assertIs(i["scope_factory"], M["host_adapters"].cgroup_scope)
        self.assertEqual(i["clock"]().tzinfo is not None, True)

    def test_expiry_closes_forward_stages_only_and_never_blocks_rollback(self):
        import datetime as dt
        at = {"t": 1800000000}
        clock = lambda: dt.datetime.fromtimestamp(at["t"], dt.timezone.utc)
        gate = self.build(now=clock)["inputs"]["external_gate"]
        self.assertTrue(gate("API_SWITCHING"))
        self.assertTrue(gate("WEB_SWITCHING"))
        for stage in (None, "API_VERIFIED", "VERIFYING", "RECOVERY_REQUIRED", "x", 5):
            self.assertFalse(gate(stage))
        at["t"] = 1800000299
        self.assertTrue(gate("WEB_SWITCHING"))
        at["t"] = 1800000300  # exactly at expiry: forward closed, recovery still open
        self.assertFalse(gate("API_SWITCHING"))
        self.assertFalse(gate("WEB_SWITCHING"))
        self.assertTrue(gate("ROLLING_BACK"))
        at["t"] = 9_999_999_999
        self.assertTrue(gate("ROLLING_BACK"))

    def test_a_clock_fault_closes_forward_but_not_recovery(self):
        bad = lambda: (_ for _ in ()).throw(RuntimeError("clock"))
        gate = self.build(now=bad)["inputs"]["external_gate"]
        self.assertFalse(gate("API_SWITCHING"))
        self.assertFalse(gate("WEB_SWITCHING"))
        self.assertTrue(gate("ROLLING_BACK"))

    def test_claims_without_a_valid_expiry_never_build(self):
        for claims in ({}, {"expires_at": "9"}, {"expires_at": 0}, {"expires_at": True}):
            self.stop("APIWEB_HOST_BINDING_INVALID", claims=claims)

    def test_no_compiled_python_is_tracked_by_git(self):
        import subprocess
        try:
            out = subprocess.run(["git", "ls-files"], cwd=BASE, capture_output=True, text=True, timeout=30)
        except (OSError, subprocess.SubprocessError):
            self.skipTest("git unavailable")
        if out.returncode != 0:
            self.skipTest("not a git checkout")
        bad = [n for n in out.stdout.splitlines() if "__pycache__" in n or n.endswith((".pyc", ".pyo"))]
        self.assertEqual(bad, [])

    def test_runner_factory_binds_the_four_command_runner_with_pinned_exec_effects(self):
        calls = []
        physical = types.SimpleNamespace(bind=lambda **kw: (calls.append(kw), "RUNNER")[1])
        i = self.build(modules=dict(M, physical_runner=physical))["inputs"]
        plane = types.SimpleNamespace(lease=types.SimpleNamespace(nonce="n" * 32))
        plan = {"baseline": {"layers": {"api": ["/a"], "web": ["/w"]}}, "target": {"layers": {"api": ["/b"], "web": ["/v"]}}}
        self.assertEqual(i["runner_factory"](plane, "HELD", "REPORT", plan), "RUNNER")
        kw = calls[0]
        self.assertEqual(kw["spec"]["admission_sha256"], D("4"))
        self.assertEqual(kw["spec"]["baseline_layers"], plan["baseline"]["layers"])
        self.assertEqual(kw["spec"]["target_layers"], plan["target"]["layers"])
        self.assertEqual(kw["pins"], self.authority["exec_pins"])
        self.assertEqual(self.fx.paths, self.authority["exec_paths"])
        self.assertEqual((kw["plane"], kw["boundary"], kw["report"]), (plane, "HELD", "REPORT"))


class ApiwebHostEffects(unittest.TestCase):
    def setUp(self):
        self.dir = tempfile.mkdtemp()
        self.addCleanup(lambda: __import__("shutil").rmtree(self.dir, ignore_errors=True))
        self.paths = {}
        for k in fx.PIN_FILES:
            p = os.path.join(self.dir, k)
            Path(p).write_bytes(k.encode())
            os.chmod(p, 0o755)
            self.paths[k] = p
        self.expected = {k: "sha256:" + hashlib.sha256(k.encode()).hexdigest() for k in fx.PIN_FILES}

    def test_pin_verifier_rehashes_every_time_and_detects_swap_symlink_or_loose_mode(self):
        verify = fx.pin_verifier(self.paths)
        self.assertTrue(verify(self.expected))
        Path(self.paths["docker_sha256"]).write_bytes(b"swapped")
        self.assertFalse(verify(self.expected))
        Path(self.paths["docker_sha256"]).write_bytes(b"docker_sha256")
        self.assertTrue(verify(self.expected))
        os.chmod(self.paths["docker_sha256"], 0o777)
        self.assertFalse(verify(self.expected))
        os.chmod(self.paths["docker_sha256"], 0o755)
        os.unlink(self.paths["child_sha256"])
        os.symlink(self.paths["docker_sha256"], self.paths["child_sha256"])
        self.assertFalse(verify(self.expected))
        self.assertFalse(verify(dict(self.expected, extra=1)))
        self.assertFalse(verify(None))

    def test_pin_verifier_paths_are_closed(self):
        for bad in (None, {}, dict(self.paths, extra="/x"), dict(self.paths, docker_sha256="rel")):
            with self.assertRaises(fx.Stop):
                fx.pin_verifier(bad)

    def test_executor_rejects_non_list_and_returns_the_exit_code_without_output(self):
        run = fx.executor()
        self.assertEqual(run(["/bin/true"]), 0)
        self.assertEqual(run(["/bin/false"]), 1)
        for bad in ("true", [], ["a", 1], ["a\0b"]):
            with self.assertRaises(fx.Stop):
                run(bad)

    def test_fetch_only_https(self):
        with self.assertRaises(fx.Stop):
            fx.https_fetch()("http://example.invalid/")


if __name__ == "__main__":
    unittest.main()
