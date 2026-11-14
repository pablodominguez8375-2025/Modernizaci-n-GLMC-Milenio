import re
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DOC = (ROOT / "docs/qa/QA525-APIWEB-IMAGE-STAGING.md").read_text(encoding="utf-8")
UPDATE = (ROOT / "scripts/qa/operators/qa525-update.py").read_text(encoding="utf-8")


class ApiwebStagingDocGuard(unittest.TestCase):
    def test_no_manual_move_of_prepared(self):
        self.assertIsNone(re.search(r"\bmv\s+\S*prepared", DOC))
        self.assertNotIn("Se **mueve**", DOC)

    def test_doc_requires_authorized_archive_and_reconciliation(self):
        for needle in ("--archive-expired-prepared", "--inspect-pending-archive",
                       "--reconcile-pending-dual", "expired-prepared-archive-pending.json",
                       "Una sola preparación target por SHA X"):
            self.assertIn(needle, DOC)

    def test_referenced_operator_contract_exists(self):
        for needle in ("def archive_expired_prepared", "expired-prepared-archive-pending.json",
                       "RECOVERY_ARCHIVE_COMMIT_SYNC_UNCERTAIN", "--inspect-pending-archive",
                       "--reconcile-pending-dual", "def require_reconciled_archive"):
            self.assertIn(needle, UPDATE)


if __name__ == "__main__":
    unittest.main()
