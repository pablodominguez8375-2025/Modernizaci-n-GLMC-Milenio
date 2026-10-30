#!/usr/bin/env python3
"""Exact read-only compatibility contract for PR801 member-history schema."""
from __future__ import annotations

import re

BASELINE_MIGRATIONS = 98
MIGRATION_ID = "20261029120000_AddMemberHistoryProposals"
MIGRATION_PATH = "backend/src/PMGM.Api/Migrations/" + MIGRATION_ID + ".cs"
CONTEXT_PATH = "backend/src/PMGM.Api/Data/PmgmDbContext.cs"
MIGRATION_BLOB = "f3d29c00fb8b153a3dee4a2aa954a70879826f23"
CONTEXT_BLOB = "9de850e92d9addce7f47de99c67fa052b99408c5"
TABLES = ("core.member_history_proposal_items", "core.member_history_proposals")
INDEXES = (
    "IX_member_history_proposals_MemberId_Status",
    "IX_member_history_proposals_OrganizationId_Status_CreatedAtUtc",
    "IX_member_history_proposal_items_ProposalId",
)
INDEX_DEFINITIONS = (
    "member_history_proposal_items|IX_member_history_proposal_items_ProposalId|f|ProposalId",
    "member_history_proposals|IX_member_history_proposals_MemberId_Status|f|MemberId,Status",
    "member_history_proposals|IX_member_history_proposals_OrganizationId_Status_CreatedAtUtc|f|OrganizationId,Status,CreatedAtUtc",
)
EXPECTED_COLUMNS = (
    "member_history_proposal_items|1|Id|uuid|t",
    "member_history_proposal_items|2|ProposalId|uuid|t",
    "member_history_proposal_items|3|Kind|character varying(20)|t",
    "member_history_proposal_items|4|Action|character varying(20)|t",
    "member_history_proposal_items|5|TargetRecordId|uuid|f",
    "member_history_proposal_items|6|OfficeType|character varying(120)|f",
    "member_history_proposal_items|7|Period|character varying(80)|f",
    "member_history_proposal_items|8|StartDate|date|f",
    "member_history_proposal_items|9|EndDate|date|f",
    "member_history_proposal_items|10|EventType|character varying(80)|f",
    "member_history_proposal_items|11|EventDate|date|f",
    "member_history_proposals|1|Id|uuid|t",
    "member_history_proposals|2|OrganizationId|uuid|t",
    "member_history_proposals|3|MemberId|uuid|t",
    "member_history_proposals|4|Status|character varying(40)|t",
    "member_history_proposals|5|EvidenceReference|character varying(500)|t",
    "member_history_proposals|6|Reason|character varying(1000)|t",
    "member_history_proposals|7|CreatedBySubject|character varying(320)|t",
    "member_history_proposals|8|CreatedAtUtc|timestamp with time zone|t",
    "member_history_proposals|9|ReviewedBySubject|character varying(320)|f",
    "member_history_proposals|10|ReviewedAtUtc|timestamp with time zone|f",
    "member_history_proposals|11|ReviewNotes|character varying(2000)|f",
)
EXPECTED_CONSTRAINTS = (
    "member_history_proposal_items|f|FK_member_history_proposal_items_member_history_proposals_ProposalId|ProposalId|core.member_history_proposals|Id|c",
    "member_history_proposal_items|p|PK_member_history_proposal_items|Id|||",
    "member_history_proposals|p|PK_member_history_proposals|Id|||",
)
DDL_IN_UP = re.compile(r"\bmigrationBuilder\.(\w+)\s*\(")


class Blocked(Exception):
    pass


def require(value, code):
    if not value:
        raise Blocked(code)


def reviewed_diff(files, blobs):
    require(type(files) is list and 0 < len(files) < 300 and
            all(type(f) is dict and type(f.get("filename")) is str for f in files),
            "MIGRATION_DIFF_INVALID")
    sensitive = set()
    for file in files:
        names = [file["filename"]]
        if file.get("status") == "renamed":
            require(type(file.get("previous_filename")) is str, "MIGRATION_RENAME_UNKNOWN")
            names.append(file["previous_filename"])
        sensitive.update(name for name in names if
                         "/migrations/" in name.lower() or "dbcontext" in name.lower())
    require(sensitive == {MIGRATION_PATH, CONTEXT_PATH},
            "MIGRATIONS_REQUIRE_SEPARATE_DBA_REVIEW")
    require(blobs == {MIGRATION_PATH: MIGRATION_BLOB, CONTEXT_PATH: CONTEXT_BLOB},
            "MIGRATION_OR_MODEL_NOT_REVIEWED_BLOB")
    return {"status": "KNOWN_ADDITIVE_SCHEMA_DIFF_REVIEWED_NO_MIGRATOR_AUTH",
            "migration_id": MIGRATION_ID, "may_apply": False}


def reviewed_up_source(content):
    require(type(content) is str and content.count("protected override void Up") == 1
            and content.count("protected override void Down") == 1,
            "EF_MIGRATION_SOURCE_INVALID")
    up = content.split("protected override void Up", 1)[1].split(
        "protected override void Down", 1)[0]
    operations = DDL_IN_UP.findall(up)
    require(operations.count("CreateTable") == 2 and operations.count("CreateIndex") == 3
            and len(operations) == 5, "EF_MIGRATION_UP_NOT_STRICTLY_ADDITIVE")
    require(all(name in up for name in
                ("member_history_proposals", "member_history_proposal_items", *INDEXES)),
            "EF_MIGRATION_EXPECTED_TABLES_MISSING")
    require(not re.search(r"\b(DropTable|DropColumn|DeleteData|Sql|RenameTable|"
                          r"RenameColumn|AlterColumn|UpdateData|EnsureSchema)\s*\(", up),
            "EF_MIGRATION_UP_UNEXPECTED_OPERATION")
    return True


def exact_post_migration_history(receipt, live):
    require(type(receipt) is list and len(receipt) == BASELINE_MIGRATIONS
            and all(type(x) is str and x for x in receipt)
            and len(set(receipt)) == BASELINE_MIGRATIONS and MIGRATION_ID not in receipt,
            "EF_BASELINE_RECEIPT_INVALID")
    require(type(live) is list and len(live) == BASELINE_MIGRATIONS + 1
            and len(set(live)) == len(live)
            and set(live) == set(receipt) | {MIGRATION_ID},
            "EF_POST_MIGRATION_HISTORY_MISMATCH")
    return {"status": "EF_HISTORY_99_READ_ONLY_VERIFIED_NOT_DEPLOYMENT_AUTHORITY",
            "count": len(live), "migration_id": MIGRATION_ID, "may_apply": False}


def schema_objects(tables, indexes, columns, constraints):
    """Require the complete pinned physical shape returned by fixed catalog queries."""
    inventories = (tables, indexes, columns, constraints)
    require(all(type(rows) is list and all(type(x) is str for x in rows)
                for rows in inventories), "SCHEMA_INVENTORY_INVALID")
    require(tuple(tables) == TABLES, "EF_EXPECTED_TABLES_MISMATCH")
    require(tuple(indexes) == INDEX_DEFINITIONS, "EF_EXPECTED_INDEXES_MISMATCH")
    require(tuple(columns) == EXPECTED_COLUMNS, "EF_EXPECTED_COLUMNS_MISMATCH")
    require(tuple(constraints) == EXPECTED_CONSTRAINTS,
            "EF_EXPECTED_CONSTRAINTS_MISMATCH")
    return True


if __name__ == "__main__":
    raise SystemExit("NO_CLI_DBA_REVIEW_REQUIRED")
