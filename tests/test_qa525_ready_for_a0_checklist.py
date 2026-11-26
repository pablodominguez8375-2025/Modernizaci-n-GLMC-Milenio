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


if __name__ == "__main__":
    unittest.main()
