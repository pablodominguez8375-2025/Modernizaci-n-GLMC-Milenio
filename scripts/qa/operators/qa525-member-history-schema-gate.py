#!/usr/bin/env python3
"""QA525 read-only compatibility contract for PR801 member-history schema.

This module deliberately has no CLI or database writes. It does NOT authorize
running EF migrations or lifting the QA525 apply gate; it supplies an exact
post-migration acceptance test for a separate, approved DBA change.
"""
from __future__ import annotations

import re

BASELINE_MIGRATIONS = 98
MIGRATION_ID = "20261029120000_AddMemberHistoryProposals"
MIGRATION_PATH = "backend/src/PMGM.Api/Migrations/" + MIGRATION_ID + ".cs"
CONTEXT_PATH = "backend/src/PMGM.Api/Data/PmgmDbContext.cs"
# Immutable reviewed Git blobs at dev@b27509d935516c6e7aa08a7f3cb76dc731b97e72.
MIGRATION_BLOB = "f3d29c00fb8b153a3dee4a2aa954a70879826f23"
CONTEXT_BLOB = "9de850e92d9addce7f47de99c67fa052b99408c5"
TABLES = ("core.member_history_proposals", "core.member_history_proposal_items")
INDEXES = (
    "IX_member_history_proposals_MemberId_Status",
    "IX_member_history_proposals_OrganizationId_Status_CreatedAtUtc",
    "IX_member_history_proposal_items_ProposalId",
)
DDL_IN_UP = re.compile(r"\bmigrationBuilder\.(\w+)\s*\(")


class Blocked(Exception):
    pass


def require(value, code):
    if not value:
        raise Blocked(code)


def reviewed_diff(files, blobs):
    """Reject any other migration or DbContext delta; never infer safety from filename."""
    require(type(files) is list and 0 < len(files) < 300 and
            all(type(f) is dict and type(f.get("filename")) is str
                for f in files), "MIGRATION_DIFF_INVALID")
    sensitive = set()
    for file in files:
        names = [file["filename"]]
        if file.get("status") == "renamed":
            require(type(file.get("previous_filename")) is str,
                    "MIGRATION_RENAME_UNKNOWN")
            names.append(file["previous_filename"])
        sensitive.update(name for name in names if
                         "/migrations/" in name.lower() or "dbcontext" in name.lower())
    require(sensitive == {MIGRATION_PATH, CONTEXT_PATH},
            "MIGRATIONS_REQUIRE_SEPARATE_DBA_REVIEW")
    require(type(blobs) is dict
            and blobs == {MIGRATION_PATH: MIGRATION_BLOB,
                          CONTEXT_PATH: CONTEXT_BLOB},
            "MIGRATION_OR_MODEL_NOT_REVIEWED_BLOB")
    return {"status": "KNOWN_ADDITIVE_SCHEMA_DIFF_REVIEWED_NO_MIGRATOR_AUTH",
            "migration_id": MIGRATION_ID, "may_apply": False}


def reviewed_up_source(content):
    """Review aid, not a C# parser: exact blob pins remain mandatory."""
    require(type(content) is str
            and content.count("protected override void Up") == 1
            and content.count("protected override void Down") == 1,
            "EF_MIGRATION_SOURCE_INVALID")
    up = content.split("protected override void Up", 1)[1].split(
        "protected override void Down", 1)[0]
    operations = DDL_IN_UP.findall(up)
    require(operations.count("CreateTable") == 2
            and operations.count("CreateIndex") == 3
            and len(operations) == 5,
            "EF_MIGRATION_UP_NOT_STRICTLY_ADDITIVE")
    require(all(name in up for name in
                ("member_history_proposals", "member_history_proposal_items", *INDEXES)),
            "EF_MIGRATION_EXPECTED_TABLES_MISSING")
    require(not re.search(r"\b(DropTable|DropColumn|DeleteData|Sql|RenameTable|"
                          r"RenameColumn|AlterColumn|UpdateData|EnsureSchema)\s*\(", up),
            "EF_MIGRATION_UP_UNEXPECTED_OPERATION")
    return True


def exact_post_migration_history(receipt, live):
    """Pre-existing 98 migrations plus the ONE approved new migration, exactly."""
    require(type(receipt) is list and len(receipt) == BASELINE_MIGRATIONS
            and all(type(x) is str and x for x in receipt)
            and len(set(receipt)) == BASELINE_MIGRATIONS
            and MIGRATION_ID not in receipt, "EF_BASELINE_RECEIPT_INVALID")
    require(type(live) is list and len(live) == BASELINE_MIGRATIONS + 1
            and len(set(live)) == len(live)
            and set(live) == set(receipt) | {MIGRATION_ID},
            "EF_POST_MIGRATION_HISTORY_MISMATCH")
    return {"status": "EF_HISTORY_99_READ_ONLY_VERIFIED_NOT_DEPLOYMENT_AUTHORITY",
            "count": len(live), "migration_id": MIGRATION_ID, "may_apply": False}


def schema_objects(tables, indexes):
    """Use lists from an independently read-only pg_catalog inventory."""
    require(type(tables) is list and type(indexes) is list
            and all(type(x) is str for x in tables + indexes),
            "SCHEMA_INVENTORY_INVALID")
    require(set(TABLES) <= set(tables) and set(INDEXES) <= set(indexes),
            "EF_EXPECTED_TABLES_OR_INDEXES_MISSING")
    return True


if __name__ == "__main__":
    raise SystemExit("NO_CLI_DBA_REVIEW_REQUIRED")
