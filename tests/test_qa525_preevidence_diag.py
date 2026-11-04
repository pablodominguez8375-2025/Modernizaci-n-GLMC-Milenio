"""Hermetic tests for the read-only pre-evidence diagnostic: the embedded Python runs against the REAL installed-launcher code
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
SCRIPT = REPO / "scripts/qa/operators/qa525-preevidence-diag-20261104.sh"
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
class Diag(unittest.TestCase):
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

    def test_local_steps_pass_with_wellformed_g6(self):
        out = self.run_diag()
        s = self.lines(out)
        for k in ("L00_load_entry_and_trust", "L01_state_dir_private", "L02_load_modules", "L03_load_support00_gate", "L10_g6_acta_private_json",
                  "L11_g6_fields_exact", "L12_validate_g6_archive_receipt_report_age", "L13_g6_offhost_true", "L30_support00_completed_record", "L40_archive_marker_absent"):
            self.assertEqual(s.get(k), "PASS", out)
        self.assertEqual(s.get("L20_single_operator_runtime_flags"), "FAIL")  # sin Docker en el sandbox: debe reportar el código fijo
        self.assertIn("DOCKER_OPERATION_FAILED", out)
        self.assertNotIn("N10_", out)  # LOCAL_ONLY: sin pasos de red

    def expect_fail(self, step, code):
        out = self.run_diag()
        self.assertEqual(self.lines(out).get(step), "FAIL", out)
        self.assertRegex(out, r"%s=FAIL .*detail=%s " % (step, code))
        return out

    def test_receipt_status_changed_after_reconcile(self):
        self.write(self.receipt, {"archive_name": ARCH, "sha256": self.sha, "status": "RECONCILED_LOCAL_BACKUP"})
        self.expect_fail("L12_validate_g6_archive_receipt_report_age", "G6_LOCAL_RECEIPT_MISMATCH")

    def test_report_result_wrong(self):
        r = json.loads(self.report.read_text()); r["result"] = "G6 SAME-HOST ISOLATED RESTORE PASS"; self.write(self.report, r)
        self.expect_fail("L12_validate_g6_archive_receipt_report_age", "G6_RESTORATION_UNVERIFIED")

    def test_archive_missing_and_hash_changed(self):
        self.archive.write_bytes(b"other")
        self.expect_fail("L12_validate_g6_archive_receipt_report_age", "G6_LOCAL_ARCHIVE_HASH_CHANGED")
        self.archive.unlink()
        self.expect_fail("L12_validate_g6_archive_receipt_report_age", "G6_LOCAL_ARCHIVE_MISSING")

    def test_acta_not_private_and_missing_field(self):
        os.chmod(self.acta, 0o644)
        self.expect_fail("L10_g6_acta_private_json", "PRIVATE_EVIDENCE_UNTRUSTED")
        os.chmod(self.acta, 0o600)
        a = json.loads(self.acta.read_text()); del a["g6_restore_report_name"]; self.write(self.acta, a)
        out = self.run_diag()
        self.assertEqual(self.lines(out).get("L11_g6_fields_exact"), "FAIL", out)
        self.assertIn("G6_FIELD_MISSING", out)
        self.assertRegex(out, r"L12_validate_g6_archive_receipt_report_age=FAIL class=builtins\.KeyError")  # excepción no-Blocked: clase y lugar exacto
        self.assertRegex(out, r"at=qa525-supervised-dual-entrypoint\.py:\d+ in validate_g6")

    def test_support00_completion_missing(self):
        self.completed.unlink()
        out = self.run_diag()
        self.assertEqual(self.lines(out).get("L30_support00_completed_record"), "FAIL", out)

    def test_final_result_names_first_failure_and_never_writes(self):
        before = sorted(str(p) for p in Path("/opt/centenario").rglob("*")) + sorted(str(p) for p in Path("/etc/pmgm").glob("*"))
        out = self.run_diag()
        self.assertIn("QA525_PREEVIDENCE_RESULT=FIRST_FAILURE L20_single_operator_runtime_flags", out)
        after = sorted(str(p) for p in Path("/opt/centenario").rglob("*")) + sorted(str(p) for p in Path("/etc/pmgm").glob("*"))
        self.assertEqual(before, after)

    def test_static_read_only_contract(self):
        for bad in ("--review-and-run", "--apply", "docker compose", "docker rm", "docker stop", "mv ", "rm -", "chmod", "chown", "install ", " > /etc", " > /opt", "tee "):
            self.assertNotIn(bad, TEXT, bad)
        self.assertIn("/usr/bin/python3 -B -I -", TEXT)
        self.assertNotIn("write_text", PY)
        self.assertNotIn("open(", PY.replace("urlopen(", ""))
        subprocess.run(["bash", "-n", str(SCRIPT)], check=True)


if __name__ == "__main__":
    unittest.main()
