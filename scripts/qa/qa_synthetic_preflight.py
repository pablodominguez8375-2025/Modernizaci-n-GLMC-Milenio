#!/usr/bin/env python3
"""Offline preflight for PMGM QA synthetic fixture batches. Never imports rows."""
from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
from pathlib import Path
from uuid import UUID, uuid5

import qa_operational_fixtures as synthetic_generator

SCHEMA = "PMGM-QA-OPERATIVE-DATASET-v1"
NAMESPACE = UUID("e2811d45-3efb-4d3b-a40a-a75e23a12d67")
MAX_INPUT_BYTES = 20 * 1024 * 1024
AREAS = (
    "workshops", "persons", "memberships", "offices", "quotas",
    "cash_receipts", "quota_allocations", "hospitalaria",
    "meetings", "attendance",
)
UNMAPPED = AREAS[3:]

# Structural contract only. Names describe the EF Core projection, never values.
# Do not map the fictitious "identity_number" to the legal Chilean RUT.
EF_STAGE_ONE = {
    "Organization": {
        "source": "workshops",
        "fields": {"Id": "id", "Name": "name", "Number": "number",
                   "Country": "country", "Type": "constant:workshop"},
        "excluded": ["currency"],
    },
    "Person": {
        "source": "persons",
        "fields": {"Id": "id", "FirstNames": "first_names",
                   "LastNames": "last_names", "Email": "email",
                   "Rut": "constant:null"},
        "excluded": ["identity_type", "identity_number"],
    },
    "Member": {
        "source": "memberships",
        "fields": {"Id": "id", "PersonId": "person_id",
                   "InstitutionalNumber": "institutional_number",
                   "CurrentDegree": "degree"},
        "excluded": ["category"],
    },
    "Membership": {
        "source": "memberships",
        "fields": {"Id": "derived:uuid5(membership-entity:member_id)",
                   "MemberId": "id", "OrganizationId": "workshop_id",
                   "MembershipType": "constant:regular", "StartDate": "start_date",
                   "Status": "status", "EndDate": "constant:null"},
        "excluded": ["category"],
    },
}

_EMAIL = re.compile(r"^persona\.qa\.\d{6}@pmgm\.invalid$")


class PreflightError(Exception):
    """Only hard-coded error codes are exposed. Never expose input values."""

    def __init__(self, code: str):
        self.code = code
        super().__init__(code)


def require(ok: bool, code: str) -> None:
    if not ok:
        raise PreflightError(code)


def _object_unique(pairs: list[tuple[str, object]]) -> dict:
    out = {}
    for k, v in pairs:
        require(k not in out, "DUPLICATE_JSON_KEY")
        out[k] = v
    return out


def load_fixture(path: Path) -> tuple[dict, str]:
    require(not path.is_symlink() and path.is_file(), "INPUT_NOT_REGULAR_FILE")
    try:
        with path.open("rb") as handle:
            raw = handle.read(MAX_INPUT_BYTES + 1)
    except OSError:
        raise PreflightError("INPUT_READ_FAILED") from None
    require(0 < len(raw) <= MAX_INPUT_BYTES, "INPUT_SIZE_INVALID")
    try:
        data = json.loads(raw, object_pairs_hook=_object_unique,
                          parse_constant=lambda _val: require(False, "NONFINITE_NUMBER"))
    except PreflightError:
        raise
    except (UnicodeError, ValueError, TypeError):
        raise PreflightError("INVALID_JSON") from None
    require(isinstance(data, dict), "INVALID_ROOT")
    return data, hashlib.sha256(raw).hexdigest()


def _uuid(value: object) -> UUID:
    require(isinstance(value, str), "INVALID_IDENTIFIER")
    try:
        parsed = UUID(value)
    except (ValueError, AttributeError):
        raise PreflightError("INVALID_IDENTIFIER") from None
    require(str(parsed) == value and parsed.version == 5, "INVALID_IDENTIFIER")
    return parsed


def _money(value: object) -> bool:
    return type(value) is int and 0 < value <= 100_000_000


def _reference(row: dict, key: str, identifiers: set[str]) -> None:
    require(isinstance(row.get(key), str) and row[key] in identifiers, "INVALID_REFERENCE")


def preflight(data: dict, digest: str) -> dict:
    require(set(data) == {"manifest", *AREAS}, "UNEXPECTED_SECTIONS")
    manifest = data.get("manifest")
    require(isinstance(manifest, dict), "INVALID_MANIFEST")
    require(manifest.get("schema") == SCHEMA, "SCHEMA_UNSUPPORTED")
    require(manifest.get("classification") == "SYNTHETIC_ONLY", "NON_SYNTHETIC_DATA")
    require(manifest.get("import_status") == "NOT_IMPORTED", "IMPORT_STATUS_UNSAFE")
    _uuid(manifest.get("dataset_id"))
    declared = manifest.get("fixture_parameters")
    require(isinstance(declared, dict), "INVALID_PARAMETERS")

    seen: set[str] = set()
    tables: dict[str, dict[str, dict]] = {}
    for name in AREAS:
        rows = data[name]
        require(isinstance(rows, list), "INVALID_SECTION")
        require(len(rows) <= 100_000, "SECTION_TOO_LARGE")
        table: dict[str, dict] = {}
        for row in rows:
            require(isinstance(row, dict), "INVALID_ROW")
            identifier = row.get("id")
            _uuid(identifier)
            require(identifier not in seen, "DUPLICATE_IDENTIFIER")
            seen.add(identifier)
            table[identifier] = row
        tables[name] = table

    workshops = tables["workshops"]
    persons = tables["persons"]
    memberships = tables["memberships"]
    require(2 <= len(workshops) <= 30, "WORKSHOP_LIMIT")
    require(1 <= len(persons) <= 2000, "PERSON_LIMIT")
    require(len(persons) == len(memberships), "MEMBER_COUNT_MISMATCH")
    require(type(declared.get("members")) is int and
            type(declared.get("workshops")) is int and
            declared["members"] == len(persons) and
            declared["workshops"] == len(workshops), "PARAMETER_MISMATCH")
    require(type(declared.get("months")) is int and
            1 <= declared["months"] <= 24, "MONTH_LIMIT")

    for row in workshops.values():
        country, currency = row.get("country"), row.get("currency")
        require(isinstance(country, str) and isinstance(currency, str) and
                (country, currency) in {("CL", "CLP"), ("PE", "USD")},
                "WORKSHOP_CURRENCY_INVALID")
        require(isinstance(row.get("name"), str) and
                row["name"].startswith("Taller ficticio QA ") and
                isinstance(row.get("number"), str) and
                row["number"].startswith("QA-"), "NON_SYNTHETIC_WORKSHOP")

    for row in persons.values():
        require(row.get("first_names") == "Persona" and
                isinstance(row.get("last_names"), str) and
                row["last_names"].startswith("Ficticia QA ") and
                row.get("identity_type") == "other" and
                isinstance(row.get("identity_number"), str) and
                row["identity_number"].startswith("QA-NO-REAL-") and
                isinstance(row.get("email"), str) and
                bool(_EMAIL.fullmatch(row["email"])),
                "NON_SYNTHETIC_PERSON")

    person_seen: set[str] = set()
    for row in memberships.values():
        _reference(row, "person_id", set(persons))
        _reference(row, "workshop_id", set(workshops))
        require(row["person_id"] not in person_seen, "PERSON_MEMBERSHIP_DUPLICATE")
        person_seen.add(row["person_id"])
        require(isinstance(row.get("degree"), str) and
                row["degree"] in {"apprentice", "fellowcraft", "master"} and
                isinstance(row.get("category"), str) and
                row["category"] in {"normal", "spouse", "student", "senior", "past_active"} and
                isinstance(row.get("status"), str) and
                row["status"] in {"active", "inactive"} and
                isinstance(row.get("institutional_number"), str) and
                row["institutional_number"].startswith("QA-M-"),
                "INVALID_MEMBERSHIP")
        # The generator's memberships[].id denotes Member.Id. Membership.Id
        # is a DIFFERENT EF entity, so derive a separate deterministic UUID.
        membership_id = str(uuid5(NAMESPACE, "membership-entity:" + row["id"]))
        require(membership_id not in seen, "MEMBERSHIP_ID_COLLISION")
        seen.add(membership_id)

    for area in ("offices", "quotas", "cash_receipts", "hospitalaria"):
        for row in tables[area].values():
            _reference(row, "member_id", set(memberships))
            _reference(row, "workshop_id", set(workshops))
            require(memberships[row["member_id"]]["workshop_id"] == row["workshop_id"],
                    "CROSS_WORKSHOP_REFERENCE")
            if area in ("quotas", "cash_receipts", "hospitalaria"):
                require(row.get("currency") == workshops[row["workshop_id"]]["currency"],
                        "CROSS_CURRENCY_REFERENCE")
                require(_money(row.get("amount")), "INVALID_AMOUNT")

    for row in tables["quota_allocations"].values():
        _reference(row, "quota_id", set(tables["quotas"]))
        _reference(row, "receipt_id", set(tables["cash_receipts"]))
        quota = tables["quotas"][row["quota_id"]]
        receipt = tables["cash_receipts"][row["receipt_id"]]
        require(quota["member_id"] == receipt["member_id"] and
                quota["workshop_id"] == receipt["workshop_id"] and
                quota["currency"] == receipt["currency"] and
                row.get("amount") == quota["amount"] == receipt["amount"],
                "ALLOCATION_MISMATCH")

    for row in tables["meetings"].values():
        _reference(row, "workshop_id", set(workshops))
    for row in tables["attendance"].values():
        _reference(row, "meeting_id", set(tables["meetings"]))
        _reference(row, "member_id", set(memberships))
        require(memberships[row["member_id"]]["workshop_id"] ==
                tables["meetings"][row["meeting_id"]]["workshop_id"],
                "ATTENDANCE_WORKSHOP_MISMATCH")

    # A closed generator contract rejects undeclared columns, arbitrary text,
    # synthetic-looking personal data, or injected extra fields in any section.
    # No input-derived data is logged, persisted, or returned by the preflight.
    canonical = synthetic_generator.make_dataset(
        member_count=len(persons), workshop_count=len(workshops),
        month_count=declared["months"])
    require(data == canonical, "NON_CANONICAL_SYNTHETIC_DATA")

    # Output has no row identifiers, data or sample values. Mapping is a
    # CONTRACT/PLAN only; unsupported entities remain explicitly unmapped.
    return {
        "status": "READY_FOR_MAPPING_ONLY",
        "classification": "SYNTHETIC_ONLY",
        "import_status": "NOT_IMPORTED",
        "writes_performed": 0,
        "source_sha256": digest,
        "first_stage_ef_mapping": {
            "Organization": len(workshops),
            "Person": len(persons),
            "Member": len(memberships),
            "Membership": len(memberships),
        },
        "ef_schema_contract": EF_STAGE_ONE,
        "not_mapped": {name: len(tables[name]) for name in UNMAPPED},
        "note": "Offline contract only. No records were imported or persisted.",
    }


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input", type=Path, required=True,
                        help="Path to a private, existing, synthetic QA JSON file")
    args = parser.parse_args(argv)
    try:
        data, digest = load_fixture(args.input)
        result = preflight(data, digest)
    except PreflightError as exc:
        result = {"status": "REJECTED", "error_code": exc.code,
                  "writes_performed": 0}
        print(json.dumps(result, sort_keys=True))
        return 2
    print(json.dumps(result, sort_keys=True))
    return 0


if __name__ == "__main__":
    sys.exit(main())
