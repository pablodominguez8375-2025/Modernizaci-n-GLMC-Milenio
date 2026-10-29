"""Hermetic no-host QA525 migration compatibility tests for PR801/PR802."""
import importlib.util
from pathlib import Path
import unittest

FILE = Path(__file__).resolve().parents[1] / "scripts/qa/operators/qa525-member-history-schema-gate.py"
spec = importlib.util.spec_from_file_location("qa525_schema_gate_test", FILE)
gate = importlib.util.module_from_spec(spec)
spec.loader.exec_module(gate)


def files():
    return [{"filename": gate.MIGRATION_PATH, "status": "added"},
            {"filename": gate.CONTEXT_PATH, "status": "modified"},
            {"filename": "frontend/src/help/helpGuides.ts", "status": "modified"}]


def blobs():
    return {gate.MIGRATION_PATH: gate.MIGRATION_BLOB,
            gate.CONTEXT_PATH: gate.CONTEXT_BLOB}


def synthetic_up():
    return """protected override void Up(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.CreateTable(name: "member_history_proposals");
      migrationBuilder.CreateIndex(name: "IX_member_history_proposals_MemberId_Status");
      migrationBuilder.CreateIndex(name: "IX_member_history_proposals_OrganizationId_Status_CreatedAtUtc");
      migrationBuilder.CreateTable(name: "member_history_proposal_items");
      migrationBuilder.CreateIndex(name: "IX_member_history_proposal_items_ProposalId");
    }
    protected override void Down(MigrationBuilder migrationBuilder) {
      migrationBuilder.DropTable(name: "member_history_proposal_items");
      migrationBuilder.DropTable(name: "member_history_proposals");
    }"""


class SchemaAcceptanceTests(unittest.TestCase):
    def test_exact_two_reviewed_blobs_and_only_one_migration(self):
        self.assertFalse(gate.reviewed_diff(files(), blobs())["may_apply"])

    def test_unknown_migration_denied(self):
        rows = files() + [{"filename": "backend/src/PMGM.Api/Migrations/Unexpected.cs",
                           "status": "added"}]
        with self.assertRaises(gate.Blocked):
            gate.reviewed_diff(rows, blobs())

    def test_context_blob_changed_denied(self):
        changed = blobs()
        changed[gate.CONTEXT_PATH] = "a" * 40
        with self.assertRaisesRegex(gate.Blocked, "MIGRATION_OR_MODEL"):
            gate.reviewed_diff(files(), changed)

    def test_unexpected_renamed_context_denied(self):
        rows = files() + [{"filename": "backend/src/new.cs",
                           "previous_filename": "backend/src/OldDbContext.cs",
                           "status": "renamed"}]
        with self.assertRaises(gate.Blocked):
            gate.reviewed_diff(rows, blobs())

    def test_positive_add_only_up_operations(self):
        self.assertTrue(gate.reviewed_up_source(synthetic_up()))

    def test_drop_in_up_denied(self):
        attack = synthetic_up().replace('migrationBuilder.CreateTable(name: "member_history_proposals");',
                                       'migrationBuilder.DropTable(name: "core.member_history_proposals");')
        with self.assertRaisesRegex(gate.Blocked, "NOT_STRICTLY_ADDITIVE"):
            gate.reviewed_up_source(attack)

    def test_sql_in_up_denied(self):
        attack = synthetic_up().replace('migrationBuilder.CreateTable(name: "member_history_proposals");',
                                       'migrationBuilder.CreateTable(name: "member_history_proposals");'
                                       'migrationBuilder.Sql("DELETE FROM core.members");')
        with self.assertRaisesRegex(gate.Blocked, "NOT_STRICTLY_ADDITIVE"):
            gate.reviewed_up_source(attack)

    def test_baseline_98_to_exact_99(self):
        original = [f"2026_Original_{i:03}" for i in range(98)]
        live = original + [gate.MIGRATION_ID]
        result = gate.exact_post_migration_history(original, live)
        self.assertEqual(result["count"], 99)
        self.assertFalse(result["may_apply"])

    def test_duplicate_or_other_migration_denied(self):
        original = [f"2026_Original_{i:03}" for i in range(98)]
        with self.assertRaises(gate.Blocked):
            gate.exact_post_migration_history(original, original + [original[0]])
        with self.assertRaises(gate.Blocked):
            gate.exact_post_migration_history(original, original + ["OtherMigration"])

    def test_schema_inventory_two_tables_three_indexes(self):
        self.assertTrue(gate.schema_objects(list(gate.TABLES), list(gate.INDEXES)))
        with self.assertRaises(gate.Blocked):
            gate.schema_objects(list(gate.TABLES[:1]), list(gate.INDEXES))


if __name__ == "__main__":
    unittest.main()
