"""QA525 G6 v2 metadata contract: hermetic, no host, no Drive, no restic."""
import ast
import datetime as dt
import hashlib
import importlib.util
import json
from pathlib import Path
import unittest

SOURCE = Path(__file__).resolve().parents[1] / "scripts/qa/qa525-vnext-g6-v2-metadata.py"
spec = importlib.util.spec_from_file_location("qa525_vnext_g6_v2_metadata_test", SOURCE)
g6 = importlib.util.module_from_spec(spec)
spec.loader.exec_module(g6)

UTC = dt.timezone.utc
TAKEN = dt.datetime(2026, 11, 9, 8, 0, 0, tzinfo=UTC)
NOW = TAKEN + dt.timedelta(hours=2)
END = NOW + dt.timedelta(hours=3)
GEN = "g6v2-0123456789ab"
NAME = "centenario-qa-g6-v2-CIFRADO-20261109T080000Z.tar.gz"
RESTORE_NAME = "isolated-restore-v2-20261109T080500Z.report.json"
HEX = "a" * 64
ARCH = "sha256:" + HEX


def enc(doc):
    return json.dumps(doc, sort_keys=True).encode()


def pin(raw):
    return "sha256:" + hashlib.sha256(raw).hexdigest()


def docs(**over):
    review = {"schema": g6.SCHEMA, "generation": GEN, "key_generation": GEN,
              "g6_archive_name": NAME, "g6_archive_sha256": HEX,
              "drive_upload_sha256": HEX, "drive_download_sha256": HEX,
              "g6_restore_report_name": RESTORE_NAME, "g6_offhost_sha256_verified": True}
    receipt = {"schema": g6.SCHEMA, "generation": GEN, "key_generation": GEN,
               "archive_name": NAME, "sha256": HEX, "status": g6.RECEIPT_STATUS}
    restore = {"schema": g6.SCHEMA, "generation": GEN, "key_generation": GEN,
               "restored_with_key_generation": GEN, "result": g6.RESTORE_RESULT,
               "archive_filename": NAME, "archive_sha256": HEX,
               "postgres_tables_by_database": {"pmgm": 12, "pmgm_keycloak": 4},
               "silo_objects_verified": 3, "production_touched": False}
    out = {"review": review, "receipt": receipt, "restore": restore}
    for key, change in over.items():
        which, field = key.split("__")
        if change is KeyError:
            out[which].pop(field, None)
        else:
            out[which][field] = change
    return out


class G6V2MetadataTests(unittest.TestCase):
    def call(self, raw_docs=None, **kw):
        d = raw_docs if raw_docs is not None else docs()
        raws = {k: (v if isinstance(v, bytes) else enc(v)) for k, v in d.items()}
        args = dict(
            pinned_review_sha256=pin(raws["review"]), pinned_receipt_sha256=pin(raws["receipt"]),
            pinned_restore_sha256=pin(raws["restore"]), observed_archive_sha256=ARCH,
            observed_drive_download_sha256=ARCH, window_end=END, now=NOW)
        args.update(kw)
        return g6.inspect(raws["review"], raws["receipt"], raws["restore"], **args)

    def code(self, raw_docs=None, **kw):
        with self.assertRaises(g6.Stop) as c:
            self.call(raw_docs, **kw)
        self.assertIsNone(c.exception.__cause__)
        return str(c.exception)

    def test_coherent_v2_passes_and_grants_nothing(self):
        out = self.call()
        self.assertEqual(out["status"], g6.STATUS)
        self.assertEqual(out["generation"], GEN)
        for flag in ("may_apply", "may_install", "signature_verified_here",
                     "physical_archive_verified_here", "offhost_custody_authenticated_here",
                     "independent_restore_verified_here", "sponsor_approved_here"):
            self.assertIs(out[flag], False)

    def test_v1_documents_are_never_v2(self):
        for which in ("review", "receipt", "restore"):
            with self.subTest(which=which):
                d = docs(**{which + "__schema": KeyError})
                self.assertEqual(self.code(d), "G6V2_%s_SCHEMA_NOT_V2" % which.upper())
        legacy = docs(review__schema="qa525-g6-v1")
        self.assertEqual(self.code(legacy), "G6V2_REVIEW_SCHEMA_NOT_V2")

    def test_v1_archive_names_are_rejected(self):
        old = "centenario-qa-g6-CIFRADO-20261109T080000Z.tar.gz"
        d = docs(review__g6_archive_name=old, receipt__archive_name=old,
                 restore__archive_filename=old)
        self.assertEqual(self.code(d), "G6V2_ARCHIVE_NAME_INVALID")
        d = docs(review__g6_restore_report_name="isolated-restore-20261109T080500Z.report.json")
        self.assertEqual(self.code(d), "G6V2_RESTORE_NAME_INVALID")

    def test_generation_must_match_in_every_document_and_restore_must_use_new_key(self):
        self.assertEqual(self.code(docs(review__generation="g6v2-zzzz")), "G6V2_GENERATION_INVALID")
        for which in ("receipt", "restore"):
            with self.subTest(which=which):
                d = docs(**{which + "__generation": "g6v2-fedcba987654"})
                self.assertEqual(self.code(d), "G6V2_%s_GENERATION_MISMATCH" % which.upper())
        d = docs(restore__key_generation="g6v2-fedcba987654")
        self.assertEqual(self.code(d), "G6V2_RESTORE_GENERATION_MISMATCH")
        d = docs(restore__restored_with_key_generation="g6v2-fedcba987654")
        self.assertEqual(self.code(d), "G6V2_RESTORE_NOT_WITH_NEW_KEY")
        d = docs(restore__restored_with_key_generation=KeyError)
        self.assertEqual(self.code(d), "G6V2_RESTORE_NOT_WITH_NEW_KEY")

    def test_age_future_and_window_coverage(self):
        self.assertEqual(self.code(now=TAKEN - dt.timedelta(seconds=1),
                                   window_end=END), "G6V2_CHECKPOINT_FUTURE")
        late = TAKEN + dt.timedelta(hours=48)
        self.assertEqual(self.code(now=late, window_end=late + dt.timedelta(hours=1)),
                         "G6V2_CHECKPOINT_EXPIRED")
        just = TAKEN + dt.timedelta(hours=47, minutes=59)
        self.assertEqual(self.call(now=just, window_end=just)["generation"], GEN)
        self.assertEqual(self.code(now=NOW, window_end=TAKEN + dt.timedelta(hours=48)),
                         "G6V2_CHECKPOINT_DOES_NOT_COVER_WINDOW")
        self.assertEqual(self.code(now=NOW, window_end=TAKEN + dt.timedelta(hours=49)),
                         "G6V2_CHECKPOINT_DOES_NOT_COVER_WINDOW")
        self.assertEqual(self.code(window_end=NOW - dt.timedelta(seconds=1)), "G6V2_WINDOW_INVALID")

    def test_clock_and_window_types(self):
        for bad in (None, "2026-11-09", dt.datetime(2026, 11, 9, 10), 1.0):
            with self.subTest(bad=bad):
                self.assertEqual(self.code(now=bad), "G6V2_CLOCK_INVALID")
                self.assertEqual(self.code(window_end=bad), "G6V2_WINDOW_INVALID")

    def test_drive_upload_download_and_observed_hashes_must_all_match(self):
        other = "b" * 64
        self.assertEqual(self.code(docs(review__drive_upload_sha256=other)), "G6V2_DRIVE_DIGEST_MISMATCH")
        self.assertEqual(self.code(docs(review__drive_download_sha256=other)), "G6V2_DRIVE_DIGEST_MISMATCH")
        self.assertEqual(self.code(docs(review__drive_download_sha256=KeyError)), "G6V2_DRIVE_DIGEST_MISMATCH")
        self.assertEqual(self.code(observed_drive_download_sha256="sha256:" + other),
                         "G6V2_DRIVE_DIGEST_MISMATCH")
        self.assertEqual(self.code(observed_archive_sha256="sha256:" + other),
                         "G6V2_ARCHIVE_DIGEST_MISMATCH")
        for bad in ("x", None, "sha256:" + "A" * 64, ARCH.upper()):
            with self.subTest(bad=bad):
                self.assertEqual(self.code(observed_archive_sha256=bad), "G6V2_OBSERVED_HASH_INVALID")
                self.assertEqual(self.code(observed_drive_download_sha256=bad), "G6V2_OBSERVED_HASH_INVALID")

    def test_claims_are_strict_booleans(self):
        for bad in (1, "true", None, False, KeyError):
            with self.subTest(bad=bad):
                self.assertEqual(self.code(docs(review__g6_offhost_sha256_verified=bad)),
                                 "G6V2_OFFHOST_CLAIM_MISSING")
        self.assertEqual(self.code(docs(restore__production_touched=0)), "G6V2_RESTORE_MISMATCH")
        self.assertEqual(self.code(docs(restore__production_touched=KeyError)), "G6V2_RESTORE_MISMATCH")

    def test_restore_and_receipt_must_describe_the_same_archive(self):
        self.assertEqual(self.code(docs(receipt__sha256="b" * 64)), "G6V2_RECEIPT_MISMATCH")
        self.assertEqual(self.code(docs(receipt__status="OTHER")), "G6V2_RECEIPT_MISMATCH")
        self.assertEqual(self.code(docs(receipt__archive_name="x")), "G6V2_RECEIPT_MISMATCH")
        self.assertEqual(self.code(docs(restore__archive_sha256="b" * 64)), "G6V2_RESTORE_MISMATCH")
        self.assertEqual(self.code(docs(restore__result="G6_PASS")), "G6V2_RESTORE_MISMATCH")
        for tables in ({"pmgm": 1}, {"pmgm": 1, "pmgm_keycloak": 0}, {"pmgm": True, "pmgm_keycloak": 1},
                       {"pmgm": 1, "pmgm_keycloak": 1, "x": 1}, []):
            with self.subTest(tables=tables):
                self.assertEqual(self.code(docs(restore__postgres_tables_by_database=tables)),
                                 "G6V2_RESTORE_MISMATCH")
        for silo in (True, -1, "3", None):
            with self.subTest(silo=silo):
                self.assertEqual(self.code(docs(restore__silo_objects_verified=silo)), "G6V2_RESTORE_MISMATCH")

    def test_pins_bytes_and_json_strictness(self):
        d = docs()
        raws = {k: enc(v) for k, v in d.items()}
        with self.assertRaises(g6.Stop) as c:
            g6.inspect(raws["review"], raws["receipt"], raws["restore"],
                       pinned_review_sha256="sha256:" + "0" * 64,
                       pinned_receipt_sha256=pin(raws["receipt"]),
                       pinned_restore_sha256=pin(raws["restore"]),
                       observed_archive_sha256=ARCH, observed_drive_download_sha256=ARCH,
                       window_end=END, now=NOW)
        self.assertEqual(str(c.exception), "G6V2_REVIEW_DIGEST_MISMATCH")
        dup = b'{"schema":"qa525-g6-v2","schema":"qa525-g6-v2"}'
        for bad in (dup, b"[]", b"\xff", b"not json", b'{"schema":NaN}', b""):
            with self.subTest(bad=bad):
                self.assertIn(self.code({"review": bad, "receipt": raws["receipt"],
                                         "restore": raws["restore"]},
                                        pinned_review_sha256=pin(bad)),
                              {"G6V2_DUPLICATE_JSON_KEY", "G6V2_REVIEW_JSON_INVALID",
                               "G6V2_REVIEW_BYTES_INVALID", "G6V2_JSON_NONFINITE"})
        with self.assertRaises(g6.Stop) as c:
            g6.inspect("text", raws["receipt"], raws["restore"],
                       pinned_review_sha256=ARCH, pinned_receipt_sha256=pin(raws["receipt"]),
                       pinned_restore_sha256=pin(raws["restore"]), observed_archive_sha256=ARCH,
                       observed_drive_download_sha256=ARCH, window_end=END, now=NOW)
        self.assertEqual(str(c.exception), "G6V2_REVIEW_BYTES_INVALID")


class G6V2SurfaceTests(unittest.TestCase):
    def test_library_has_no_process_file_or_network_surface(self):
        tree = ast.parse(SOURCE.read_text())
        imported = {a.name.split(".")[0] for n in ast.walk(tree)
                    if isinstance(n, (ast.Import, ast.ImportFrom))
                    for a in (n.names if isinstance(n, ast.Import) else [ast.alias(name=n.module)])}
        self.assertFalse(imported & {"subprocess", "socket", "os", "pathlib", "urllib", "http",
                                     "shutil", "ssl", "ctypes"})
        names = {n.id for n in ast.walk(tree) if isinstance(n, ast.Name)}
        self.assertFalse(names & {"open", "exec", "eval", "compile", "__import__"})

    def test_module_is_not_runnable_and_never_mentions_the_v1_receipt_path(self):
        text = SOURCE.read_text()
        self.assertNotIn("/opt/centenario/qa-g6", text)
        self.assertNotIn("archive.receipt.json", text)
        self.assertFalse(callable(getattr(g6, "main", None)))


if __name__ == "__main__":
    unittest.main()
