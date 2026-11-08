"""QA525 G6 v2 admission CONTRACT: what the real producer writes vs what each consumer reads.

Hermetic: fake Restic, tmp dirs. Pure test, no host, no root, no GO. It documents the gap that a
future, separate G6 v2 admission adapter must close; it does not close it and does not touch
the legacy ``validate_g6``.
"""
import datetime as dt
import hashlib
import importlib.util
import os
from pathlib import Path
import shutil
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]


def load(name, rel):
    spec = importlib.util.spec_from_file_location(name, ROOT / rel)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


evid = load("qa525_g6adm_evidence", "scripts/qa/qa525-vnext-g6-v2-evidence.py")
prod = load("qa525_g6adm_producer", "scripts/qa/qa525-vnext-g6-v2-producer.py")
legacy = load("qa525_g6adm_legacy", "scripts/qa/operators/qa525-supervised-dual-entrypoint.py")
ptest = load("qa525_g6adm_ptest", "tests/test_qa525_vnext_g6_v2_producer.py")
UID = os.getuid()
NOW = ptest.NOW


class G6V2AdmissionContractTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = os.path.join(os.path.realpath(self.tmp.name), "g6")
        os.mkdir(self.root, 0o700)
        self.fake = ptest.G6V2ProducerFakeRestic()
        epoch = prod.key_epoch(ptest.KEY)
        self.pins = dict(expected_repo_id=ptest.REPO_ID, expected_key_epoch=epoch)
        prod.init_repo(self.root, runner=self.fake, key_bytes=ptest.KEY, uid=UID, now=NOW)
        self.out = prod.backup(self.root, runner=self.fake, captor=ptest.good_captor, uid=UID,
                               now=NOW, **self.pins)
        self.name = self.out["archive_name"]
        downloads = os.path.join(self.root, "downloads-v2")
        os.mkdir(downloads, 0o700)
        shutil.copyfile(os.path.join(self.root, "exports-v2", self.name),
                        os.path.join(downloads, self.name))
        os.chmod(os.path.join(downloads, self.name), 0o600)
        prod.restore(self.root, runner=self.fake, verifier=ptest.verifier_ok,
                     archive_name=self.name, archive_sha256=self.out["archive_sha256"],
                     snapshot_id=self.out["snapshot_id"],
                     expected_manifest_sha256=self.out["manifest_sha256"],
                     forbidden_prefixes=[os.path.join(self.root, "prod")], uid=UID,
                     now=NOW + dt.timedelta(hours=2), **self.pins)
        self.review = prod.finalize_review(self.root, archive_name=self.name,
                                           drive_file_id="D" * 20, uid=UID, **self.pins)
        self.state = os.path.join(self.root, "state-v2")
        self.stamp = self.name.split("-")[-1].split(".")[0]

    def files(self):
        return {kind: Path(self.state, fname).read_bytes() for kind, fname in (
            ("review", "review-%s.json" % self.stamp),
            ("receipt", "receipt-%s.json" % self.stamp),
            ("restore", prod.RESTORE_FMT.format(stamp=self.stamp)))}

    def inspect(self, **over):
        raw = self.files()
        pin = lambda b: "sha256:" + hashlib.sha256(b).hexdigest()
        args = dict(review_bytes=raw["review"], receipt_bytes=raw["receipt"],
                    restore_bytes=raw["restore"],
                    pins={"review_sha256": pin(raw["review"]),
                          "receipt_sha256": pin(raw["receipt"]),
                          "restore_sha256": pin(raw["restore"])},
                    observed_archive_sha256="sha256:" + self.out["archive_sha256"],
                    downloaded_offhost_sha256="sha256:" + self.out["archive_sha256"],
                    expected_restic_repo_id=ptest.REPO_ID,
                    expected_key_epoch=self.pins["expected_key_epoch"],
                    now=NOW + dt.timedelta(hours=3),
                    admission_expires_at=NOW + dt.timedelta(hours=4))
        args.update(over)
        return evid.inspect(**args)

    def test_real_producer_output_is_coherent_for_the_v2_evidence_module_but_never_authority(self):
        result = self.inspect()
        self.assertEqual(result["status"], evid.STATUS)
        self.assertIs(result["may_apply"], False)
        self.assertIs(result["may_install"], False)
        self.assertIs(result["sponsor_authorized_here"], False)

    def test_each_pin_the_adapter_must_supply_is_individually_load_bearing(self):
        raw = self.files()
        for kind in ("review", "receipt", "restore"):
            tampered = dict(raw)
            tampered[kind] = raw[kind].replace(b"a", b"b", 1) if b"a" in raw[kind] \
                else raw[kind] + b" "
            kwargs = {kind + "_bytes": tampered[kind]}
            with self.assertRaises(evid.Stop) as err:
                self.inspect(**kwargs)
            self.assertTrue(str(err.exception).startswith("G6_V2_"), kind)

    def test_wrong_name_sha_or_generation_is_refused(self):
        for over, code in (
                ({"observed_archive_sha256": "sha256:" + "0" * 64},
                 "G6_V2_ARCHIVE_OR_OFFHOST_HASH_MISMATCH"),
                ({"downloaded_offhost_sha256": "sha256:" + "0" * 64},
                 "G6_V2_ARCHIVE_OR_OFFHOST_HASH_MISMATCH"),
                ({"expected_restic_repo_id": "f" * 64}, "G6_V2_GENERATION_MISMATCH"),
                ({"expected_key_epoch": "f" * 32}, "G6_V2_GENERATION_MISMATCH"),
                ({"now": NOW + dt.timedelta(hours=49)}, "G6_V2_WINDOW_EXPIRED_OR_UNCOVERED")):
            with self.assertRaises(evid.Stop) as err:
                self.inspect(**over)
            self.assertEqual(str(err.exception), code, over)

    def test_the_gap_v2_writes_where_the_legacy_validator_does_not_read(self):
        legacy_reads = {str(legacy.G6_DIR), str(legacy.ARCHIVE_RECEIPT),
                        "/home/soporte/" + self.name}
        v2_dir = os.path.join(self.root, "state-v2")
        written = {os.path.join(v2_dir, f) for f in os.listdir(v2_dir)}
        written |= {os.path.join(self.root, "exports-v2", self.name)}
        self.assertFalse(legacy_reads & written)
        self.assertNotEqual(os.path.basename(str(legacy.ARCHIVE_RECEIPT)),
                            "receipt-%s.json" % self.stamp)

    def test_legacy_validate_g6_cannot_accept_the_v2_review_and_nothing_here_makes_it(self):
        # validate_g6 fails first on /home/soporte/<v2 name>, which no host has; this does not
        # depend on the historical legacy receipt and is NOT an authorization condition.
        with self.assertRaises(Exception) as err:
            legacy.validate_g6(self.review, int((NOW + dt.timedelta(hours=3)).timestamp()))
        self.assertIn("G6_LOCAL_ARCHIVE_MISSING", str(err.exception))

    def test_legacy_age_check_reads_a_different_name_layout_than_v2(self):
        # validate_g6 slices name[25:40] for the cut time; that is not the v2 stamp.
        self.assertNotEqual(self.name[25:40], self.stamp)
        self.assertNotRegex(self.name[25:40], r"^\d{8}T\d{6}$")

    def test_adapter_must_not_write_the_legacy_receipt_path(self):
        # Guard for the future adapter: the legacy receipt path is outside any v2 tree.
        self.assertTrue(str(legacy.ARCHIVE_RECEIPT).startswith("/opt/centenario/qa-g6/"))
        self.assertFalse(str(legacy.ARCHIVE_RECEIPT).startswith(self.root))


if __name__ == "__main__":
    unittest.main()
