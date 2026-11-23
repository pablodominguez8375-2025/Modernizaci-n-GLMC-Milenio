"""G6 evidence wiring for the schema cut (offline, synthetic canonical evidence, real v2 evidence module)."""
import datetime as dt
import hashlib
import importlib.util
import json
from pathlib import Path
import unittest

HERE = Path(__file__).resolve().parent


def load(name, relative):
    spec = importlib.util.spec_from_file_location(name, HERE.parent / "scripts" / "qa" / relative)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


g6 = load("sg6", "qa525-vnext-root-schema-g6.py")
ev = load("sg6_ev", "qa525-vnext-g6-v2-evidence.py")
ports = load("sg6_ports", "qa525-vnext-schema-cut-ports.py")
STAMP = "20261122T100000Z"
START = dt.datetime(2026, 11, 22, 10, 0, 0, tzinfo=dt.timezone.utc)
ARCH = "a" * 64
REPO, EPOCH = "b" * 64, "c" * 32


def canon(obj):
    return (json.dumps(obj, sort_keys=True, separators=(",", ":"), ensure_ascii=True) + "\n").encode()


def dg(raw):
    return "sha256:" + hashlib.sha256(raw).hexdigest()


def fixture(**over):
    name = "centenario-qa-g6-v2-CIFRADO-%s.tar.gz" % STAMP
    review = canon({"schema": "qa525-g6-v2-review", "g6_archive_name": name, "g6_archive_sha256": ARCH,
                    "g6_restore_report_name": "isolated-restore-v2-%s.report.json" % STAMP,
                    "g6_offhost_sha256_verified": True, "g6_drive_file_id": "d" * 20,
                    "restic_repo_id": REPO, "key_epoch": EPOCH})
    receipt = canon({"schema": "qa525-g6-v2-receipt", "archive_name": name, "sha256": ARCH,
                     "status": "ENCRYPTED_LOCAL_ARCHIVE_READY_OFFHOST_PENDING", "restic_repo_id": REPO,
                     "key_epoch": EPOCH})
    restore = canon({"schema": "qa525-g6-v2-restore", "result": "G6_SAME_HOST_ISOLATED_RESTORE_PASS_NOT_DISASTER_RECOVERY",
                     "archive_filename": name, "archive_sha256": ARCH, "restic_repo_id": REPO, "key_epoch": EPOCH,
                     "postgres_tables_by_database": {"pmgm": 119, "pmgm_keycloak": 20}, "silo_objects_verified": 3,
                     "production_touched": False})
    files = {"review": review, "receipt": receipt, "restore": restore, "admission": b"a", "approval": b"b"}
    authority = {"g6": {"pinned_review_sha256": dg(review), "pinned_receipt_sha256": dg(receipt),
                        "pinned_restore_sha256": dg(restore), "observed_archive_sha256": "sha256:" + ARCH,
                        "downloaded_offhost_sha256": "sha256:" + ARCH, "expected_restic_repo_id": REPO,
                        "expected_key_epoch": EPOCH}}
    authority["g6"].update(over)
    return files, authority


NOW = START + dt.timedelta(hours=2)
CLAIMS = {"expires_at": int((NOW + dt.timedelta(minutes=15)).timestamp())}


def setup(files=None, authority=None, now=NOW):
    f, a = fixture()
    g6.bind_dependencies({"evidence": ev})
    return g6.make(authority or a, CLAIMS, lambda n: (files or f)[n], lambda: now), f, a


class SchemaG6Tests(unittest.TestCase):
    def test_verifier_accepts_consistent_evidence_and_epoch_is_archive_start(self):
        (verifier, epoch), files, _ = setup()
        self.assertEqual(epoch, int(START.timestamp()))
        self.assertIs(verifier(files), True)

    def test_works_with_the_reviewed_backup_port(self):
        (verifier, epoch), files, a = setup()
        pins = {k: dg(files[k]) for k in ports.EVIDENCE}
        step = ports.make_verify_backup(lambda n: files[n], verifier, lambda: NOW, pins, epoch)
        self.assertIs(step(), True)

    def test_every_pin_comes_from_authority_and_drift_is_rejected(self):
        (verifier, _), files, _ = setup()
        for key in ("review", "receipt", "restore"):
            bad = dict(files, **{key: files[key] + b" "})
            self.assertIs(verifier(bad), False)
        (v2, _), f2, _ = setup(authority=fixture(observed_archive_sha256="sha256:" + "e" * 64)[1])
        self.assertIs(v2(f2), False)
        (v3, _), f3, _ = setup(authority=fixture(expected_key_epoch="f" * 32)[1])
        self.assertIs(v3(f3), False)

    def test_expired_or_future_window_is_rejected(self):
        for now in (START + dt.timedelta(hours=49), START - dt.timedelta(minutes=1)):
            (verifier, _), files, _ = setup(now=now)
            self.assertIs(verifier(files), False)

    def test_authority_rejects_unknown_g6_keys_and_bad_identity_pins(self):
        _, authority = fixture()
        g6_data = authority["g6"]
        for extra in ({"unexpected": "value"}, {"g6_operation_sha256": "sha256:" + "a" * 64}):
            with self.subTest(extra=extra):
                altered = dict(g6_data, **extra)
                with self.assertRaises(g6.Stop):
                    g6.make({"g6": altered}, CLAIMS, lambda n: fixture()[0][n], lambda: NOW)
        for key, value in (("expected_restic_repo_id", "not-hex"),
                           ("expected_key_epoch", "not-epoch")):
            with self.subTest(key=key):
                altered = dict(g6_data, **{key: value})
                with self.assertRaises(g6.Stop):
                    g6.make({"g6": altered}, CLAIMS, lambda n: fixture()[0][n], lambda: NOW)

    def test_bad_inputs_fail_closed_with_fixed_codes(self):
        f, a = fixture()
        g6.bind_dependencies({"evidence": ev})
        for authority in ({}, {"g6": {}}, {"g6": dict(a["g6"], observed_archive_sha256="x")}):
            with self.assertRaises(g6.Stop):
                g6.make(authority, CLAIMS, lambda n: f[n], lambda: NOW)
        with self.assertRaises(g6.Stop):
            g6.make(a, CLAIMS, lambda n: b"not json", lambda: NOW)
        with self.assertRaises(g6.Stop):
            g6.make(a, {"expires_at": True}, lambda n: f[n], lambda: NOW)
        g6._DEPS.clear()
        with self.assertRaises(g6.Stop):
            g6.make(a, CLAIMS, lambda n: f[n], lambda: NOW)


if __name__ == "__main__":
    unittest.main()
