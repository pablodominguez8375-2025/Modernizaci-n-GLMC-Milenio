"""Hermetic tests for the read-only pre-evidence diagnostic v2: the embedded Python runs against the REAL installed-launcher code
(git cfe4e657 copied to a root-owned stage dir) with fixture files at the fixed host paths (sandbox only, removed afterwards)."""
import hashlib
import json
import os
import re
import shutil
import subprocess
import tempfile
import time
import unittest
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
SCRIPT = REPO / "scripts/qa/operators/qa525-preevidence-diag-v2-20261104.sh"
TEXT = SCRIPT.read_text()
PY = TEXT.split("<<'PY'\n", 1)[1].split("\nPY\n", 1)[0]
SRC = "6eed4f00b7917da3cca36bbda70e16d16328648d"
DIGEST = "sha256:9bdeaa217f09f5d0e19e50d214a4abb936a2b89246ebbcf71fb71178e5574af2"
REVIEW = "https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/525#issuecomment-6604960072"
STAMP = time.strftime("%Y%m%dT%H%M%SZ", time.gmtime(time.time() - 3600))
ARCH = "centenario-qa-g6-CIFRADO-%s.tar.gz" % STAMP
REPORT = "isolated-restore-%s.report.json" % STAMP
G6 = "/etc/pmgm/qa525-g6-reviewed-%s.json" % STAMP
CFE4 = "cfe4e6573ece2ae81414cc252047d36cc56e5c73"


def have_cfe4():
    return subprocess.run(["git", "-C", str(REPO), "cat-file", "-e", CFE4 + ":scripts/qa/operators/qa525-update.py"], capture_output=True).returncode == 0


@unittest.skipIf(os.geteuid() != 0 or not have_cfe4(), "needs root and the cfe4e657 commit in the clone")
class DiagV2(unittest.TestCase):
    created = []

    @classmethod
    def setUpClass(cls):
        cls.stage_root = Path("/root/qa525-preevidence-test-stage")
        shutil.rmtree(cls.stage_root, ignore_errors=True)
        ops = cls.stage_root / "scripts/qa/operators"
        ops.mkdir(parents=True)
        for rel in subprocess.run(["git", "-C", str(REPO), "ls-tree", "-r", "--name-only", CFE4, "scripts/qa/"], capture_output=True, text=True, check=True).stdout.split():
            if rel.endswith(".py") and rel.count("/") <= 3:
                dest = cls.stage_root / rel
                dest.parent.mkdir(parents=True, exist_ok=True)
                dest.write_bytes(subprocess.run(["git", "-C", str(REPO), "show", "%s:%s" % (CFE4, rel)], capture_output=True, check=True).stdout)
        for p in [cls.stage_root, *cls.stage_root.rglob("*")]:
            p.chmod(0o755 if p.is_dir() else 0o644)
        cls.ops = ops

    @classmethod
    def tearDownClass(cls):
        shutil.rmtree(cls.stage_root, ignore_errors=True)

    def mk(self, path, mode=0o755, directory=False):
        p = Path(path)
        if directory:
            if not p.exists():
                p.mkdir(parents=True)
                self.created.append(p)
                p.chmod(mode)
        return p

    def setUp(self):
        self.paths = []
        self.mk("/opt/centenario/qa525-signed-prepare", directory=True)
        os.chmod("/opt/centenario/qa525-signed-prepare", 0o700)
        self.mk("/opt/centenario/qa-g6", directory=True)
        self.mk("/etc/pmgm", directory=True)
        self.mk("/home/soporte", directory=True)
        self.archive = Path("/home/soporte") / ARCH
        self.archive.write_bytes(b"encrypted-archive-bytes")
        self.sha = hashlib.sha256(b"encrypted-archive-bytes").hexdigest()
        self.receipt = Path("/opt/centenario/qa-g6/archive.receipt.json")
        self.report = Path("/opt/centenario/qa-g6") / REPORT
        self.acta = Path(G6)
        self.completed = Path("/opt/centenario/qa525-signed-prepare/support00-completed.json")
        self.write(self.receipt, {"archive_name": ARCH, "sha256": self.sha, "status": "ENCRYPTED_LOCAL_ARCHIVE_READY_OFFHOST_PENDING"})
        self.write(self.report, {"result": "G6_SAME_HOST_ISOLATED_RESTORE_PASS_NOT_DISASTER_RECOVERY", "archive_filename": ARCH, "archive_sha256": self.sha,
                                 "postgres_tables_by_database": {"pmgm": 10, "pmgm_keycloak": 5}, "silo_objects_verified": 3, "production_touched": False})
        self.write(self.acta, {"g6_archive_name": ARCH, "g6_archive_sha256": self.sha, "g6_drive_file_id": "1dmptoKZORJT6R-mAPgpxRWzxrydvpk7-",
                               "g6_offhost_sha256_verified": True, "g6_restore_report_name": REPORT})
        gate_src = (self.ops / "qa525-support00-schema-gate.py").read_text()
        mid = re.search(r"MIGRATION_ID = '([^']+)'", gate_src).group(1)
        blob = re.search(r"MIGRATION_BLOB = '([^']+)'", gate_src).group(1)
        self.write(self.completed, {"status": "SUPPORT00_EF100_VERIFIED", "migration_id": mid, "migration_blob_sha": blob, "completed_at": int(time.time()) - 100})
        self.owned = [self.receipt, self.report, self.acta, self.completed, self.archive]

    def tearDown(self):
        for p in self.owned:
            p.unlink(missing_ok=True)
        for d in reversed(self.created):
            try:
                d.rmdir()
            except OSError:
                pass
        self.created.clear()

    def write(self, path, obj, mode=0o600):
        path = Path(path)
        path.write_text(obj if isinstance(obj, str) else json.dumps(obj))
        path.chmod(mode)

    def run_diag(self, mode="LOCAL_ONLY"):
        r = subprocess.run(["/usr/bin/python3", "-B", "-I", "-", str(self.ops), SRC, DIGEST, REVIEW, G6, mode, "45"], input=PY, capture_output=True, text=True, timeout=120)
        self.assertEqual(r.returncode, 0, r.stdout + r.stderr)
        return r.stdout

    def lines(self, out):
        return dict(re.findall(r"QA525_PREEVIDENCE (\w+)=(PASS|FAIL)", out))

    def run_diag(self, mode="LOCAL_ONLY"):
        r = subprocess.run(["/usr/bin/python3", "-B", "-I", "-", str(self.ops), SRC, DIGEST, REVIEW, G6, mode, "40"], input=PY, capture_output=True, text=True, timeout=120)
        self.assertEqual(r.returncode, 0, r.stdout + r.stderr)
        return r.stdout

    def test_local_only_has_no_network_steps_and_names_first_failure(self):
        out = self.run_diag()
        self.assertIn("L20_single_operator_runtime_flags=FAIL", out)  # sin Docker en el sandbox
        self.assertNotIn("N10_", out)
        self.assertNotIn("I01_", out)
        self.assertIn("QA525_PREEVIDENCE_RESULT=FIRST_FAILURE L20_single_operator_runtime_flags", out)

    def test_full_with_unreachable_quota_skips_network_and_install_replay(self):
        r = subprocess.run(["/usr/bin/python3", "-B", "-I", "-", str(self.ops), SRC, DIGEST, REVIEW, G6, "FULL", "1000000"], input=PY, capture_output=True, text=True, timeout=120)
        self.assertEqual(r.returncode, 0, r.stdout + r.stderr)
        self.assertNotIn("I01_", r.stdout)

    def test_install_replay_sandbox_redirects_paths_and_cleans_up(self):
        """Run install_replay with stubbed docker/metadata/net objects: records go to a private temp dir under /root, never to /etc/pmgm or STATE."""
        helper = PY.split("\nspec = importlib.util.spec_from_file_location", 1)[0]
        driver = helper + """
import types
spec = importlib.util.spec_from_file_location("upd", STAGE / "qa525-update.py")
upd = importlib.util.module_from_spec(spec); spec.loader.exec_module(upd)
entry = upd.load_entry()
modules = [entry.load("m%d" % i, p) for i, p in enumerate(entry.MODULES)]
gate = entry.load("g", entry.HERE / "qa525-support00-schema-gate.py")
core, build, approval, policy, signed, adapter, _g, _p = modules
live = {n: [None, "img", "running", None, None, None, None, "layer1,layer2"] for n in core.TARGETS}
adapter.docker = lambda *a, **k: ""
adapter.metadata = lambda: live
build.snapshot = lambda d: {n: {"running": True} for n in core.TARGETS}
seen = {}
entry.single_operator_runtime = lambda d: "fp"
entry.reviewed_layers = lambda *a, **k: None
entry.approved_review = lambda *a, **k: None
entry.validate_single_operator_review = lambda *a, **k: None
entry.validate_g6 = lambda *a, **k: True
def vpd(review, source, now, docker, gate_, probe, single_operator=False):
    seen["review"] = str(entry.REVIEW); seen["post"] = str(entry.SINGLE_OPERATOR_REVIEW); seen["cut"] = str(approval.APPROVAL)
    seen["exists"] = [Path(seen[k]).exists() for k in ("review", "post", "cut")]
entry.validate_post_db_dba = vpd
signed.gate.evaluate = lambda fetch, expected_sha=None: {"source_sha": SRC, "release_asset_sha256": DIGEST}
entry.current_change_review = lambda *a, **k: ({"reasons": ["x"]}, getattr(gate, "SCHEMA_REVIEW_STATE", "PR801_REVIEWED_ADDITIVE_NEEDS_DBA"))
orig = (entry.REVIEW, entry.SINGLE_OPERATOR_REVIEW, approval.APPROVAL)
g6 = {"g6_archive_name": "centenario-qa-g6-CIFRADO-20260101T000000Z.tar.gz", "g6_archive_sha256": "a" * 64, "g6_drive_file_id": "x", "g6_offhost_sha256_verified": True, "g6_restore_report_name": "r.json"}
entry.load = entry.load
import builtins
install_replay(entry, upd, modules, gate, {"source_sha": SRC, "release_asset_sha256": DIGEST}, {"reasons": ["x"]}, getattr(gate, "SCHEMA_REVIEW_STATE", "PR801_REVIEWED_ADDITIVE_NEEDS_DBA"), g6, [0], lambda *a, **k: {})
print("RESTORED=%s" % ((entry.REVIEW, entry.SINGLE_OPERATOR_REVIEW, approval.APPROVAL) == orig))
print("SEEN=" + json.dumps(seen))
"""
        before = sorted(str(p) for p in Path("/etc/pmgm").glob("*")) + sorted(str(p) for p in Path("/opt/centenario/qa525-signed-prepare").glob("*"))
        r = subprocess.run(["/usr/bin/python3", "-B", "-I", "-", str(self.ops), SRC, DIGEST, REVIEW, G6, "FULL", "40"], input=driver, capture_output=True, text=True, timeout=120)
        out = r.stdout + r.stderr
        self.assertIn("RESTORED=True", out)
        seen = json.loads(re.search(r"SEEN=(.*)", out).group(1))
        for k in ("review", "post", "cut"):
            self.assertTrue(seen[k].startswith("/root/.qa525-diag-"), seen)
        self.assertEqual(seen["exists"], [True, True, True])
        self.assertIn("sandbox_removed=True", out)
        self.assertFalse(list(Path("/root").glob(".qa525-diag-*")))
        after = sorted(str(p) for p in Path("/etc/pmgm").glob("*")) + sorted(str(p) for p in Path("/opt/centenario/qa525-signed-prepare").glob("*"))
        self.assertEqual(before, after)
        for step in ("I01_packet_contract", "I12_sandbox_write_three_records", "I13_validate_post_db_dba", "I15_runtime_unchanged", "P03_baseline_running"):
            self.assertRegex(out, step + "=PASS")

    def test_static_read_only_contract(self):
        for bad in ("--review-and-run", "--apply", "docker compose", "docker rm", "docker stop", "mv ", "rm -", "chmod", "chown", "install ", " > /etc", " > /opt", "tee "):
            self.assertNotIn(bad, TEXT, bad)
        self.assertIn("/usr/bin/python3 -B -I -", TEXT)
        # Only write sink: the private temp sandbox under /root, redirected module paths.
        for p in re.findall(r"os\.open\(([^,]+),", PY):
            self.assertEqual(p.strip(), "p", p)
        self.assertIn('dir="/root"', PY)
        self.assertNotIn("write_text", PY)
        self.assertNotIn("STATE /", PY.split("def write3")[1].split("step(\"I12")[0])
        subprocess.run(["bash", "-n", str(SCRIPT)], check=True)


if __name__ == "__main__":
    unittest.main()
