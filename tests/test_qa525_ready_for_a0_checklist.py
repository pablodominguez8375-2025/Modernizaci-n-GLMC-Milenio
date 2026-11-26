import importlib.util
import pathlib
import unittest

SRC = pathlib.Path(__file__).resolve().parent.parent / "scripts/qa/operators/qa525-ready-for-a0-checklist.py"
spec = importlib.util.spec_from_file_location("chk", SRC)
chk = importlib.util.module_from_spec(spec)
spec.loader.exec_module(chk)
H = "a" * 40
INV = "\n".join([
    "A0G6_EVIDENCE_qa525-a0-g6-params-v1.json=sha256=%s size=9" % ("1" * 64),
    "A0G6_EVIDENCE_receipt-20261126T100000Z.json=sha256=%s size=9 restic_repo_id=%s key_epoch=%s" % ("2" * 64, "b" * 64, "c" * 32),
    "A0G6_EVIDENCE_review-20261126T100000Z.json=sha256=%s size=9 restic_repo_id=%s key_epoch=%s" % ("3" * 64, "b" * 64, "c" * 32),
    "A0G6_EVIDENCE_isolated-restore-v2-20261126T100000Z.report.json=sha256=%s size=9" % ("4" * 64),
    "A0G6_ARCHIVE_centenario-qa-g6-v2-CIFRADO-20261126T100000Z.tar.gz=file uid=0 mode=0600 nlink=1 size=4",
    "A0G6_INVENTORY_COMPLETE=1"]) + "\n"
INPUTS = {"ci_head_sha": H, "expected_head_sha": H, "ci_all_green": True, "migrator_digest": "d" * 64,
          "image_a_config_digest": "e" * 64, "migrator_provenance_verified": True, "offhost_sha256": "f" * 64,
          "offhost_matches_local": True, "g6_window_valid": True, "final_backup_under_quiesce": True,
          "base_adelantada_runbook_reviewed": True, "uat_postflight_defined": True}


class Checklist(unittest.TestCase):
    def test_all_pass_is_ready(self):
        g, rows = chk.evaluate(INV, INPUTS)
        self.assertEqual(g, "READY_FOR_A0", rows)
        self.assertEqual(len(rows), 10)

    def test_missing_inventory_or_params_is_stop(self):
        self.assertEqual(chk.evaluate("", INPUTS)[0], "STOP")
        self.assertEqual(chk.evaluate(INV.replace("qa525-a0-g6-params-v1", "other"), INPUTS)[0], "STOP")
        self.assertEqual(chk.evaluate(INV + "A0G6_STOP:X\n", INPUTS)[0], "STOP")

    def test_unknown_field_never_pass(self):
        for k in INPUTS:
            bad = dict(INPUTS)
            del bad[k]
            self.assertEqual(chk.evaluate(INV, bad)[0], "STOP", k)
        self.assertEqual(chk.evaluate(INV, {})[0], "STOP")
        self.assertEqual(chk.evaluate(INV, None)[0], "STOP")
        self.assertEqual(chk.evaluate(None, INPUTS)[0], "STOP")

    def test_ci_head_mismatch_and_epoch_ambiguity(self):
        self.assertEqual(chk.evaluate(INV, dict(INPUTS, expected_head_sha="9" * 40))[0], "STOP")
        two = INV.replace("c" * 32, "d" * 32, 1)
        self.assertEqual(chk.evaluate(two, INPUTS)[0], "STOP")

    def test_render_closed_and_no_free_text(self):
        g, rows = chk.evaluate(INV, dict(INPUTS, ci_all_green=False))
        text = chk.render(g, rows)
        self.assertIn("READY_FOR_A0_01_CI_EXACT_HEAD=STOP ref=NO_EVIDENCE", text)
        self.assertTrue(text.endswith("READY_FOR_A0_GLOBAL=STOP\n"))

    def assert_all_stop(self, text, inputs=INPUTS):
        g, rows = chk.evaluate(text, inputs)
        self.assertEqual(g, "STOP")
        self.assertEqual(len(rows), 10)
        self.assertTrue(all(r[2] == "STOP" and r[3] == "NO_EVIDENCE" for r in rows))
        self.assertTrue(chk.render(g, rows).endswith("READY_FOR_A0_GLOBAL=STOP\n"))

    def test_duplicate_key_rejected(self):
        self.assert_all_stop(INV + "A0G6_INVENTORY_COMPLETE=1\n")

    def test_malformed_repo_or_epoch_or_evidence(self):
        self.assert_all_stop(INV.replace("b" * 64, "b" * 63, 1))
        self.assert_all_stop(INV.replace("c" * 32, "zz" * 16, 1))
        self.assert_all_stop(INV.replace("size=9 restic", "size=-9 restic", 1))
        self.assert_all_stop(INV.replace("sha256=" + "2" * 64, "sha256=short", 1))

    def test_contaminated_text(self):
        self.assert_all_stop(INV + "free text line\n")
        self.assert_all_stop("garbage\n" + INV)
        self.assert_all_stop(INV + "A0G6_X=\x00bad\n")
        self.assert_all_stop(INV + "A0G6_STOP:IO\n")

    def test_parse_exceptions_become_closed_stop(self):
        self.assert_all_stop(object())
        self.assert_all_stop("A" * (1 << 21))
        g, rows = chk.evaluate(INV, {"ci_head_sha": object()})  # bad input: no exception, global STOP
        self.assertEqual(g, "STOP")
        self.assertEqual(rows[0][2], "STOP")


if __name__ == "__main__":
    unittest.main()
