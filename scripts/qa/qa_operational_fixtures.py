#!/usr/bin/env python3
"""Build reproducible fictitious records for QA; never connects to a database.

The output is a staging/interchange dataset, NOT an EF Core database dump and
NOT an import performed against glmdch. No personal data or credentials.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
from uuid import UUID, uuid5

NAMESPACE = UUID("e2811d45-3efb-4d3b-a40a-a75e23a12d67")
SCHEMA = "PMGM-QA-OPERATIVE-DATASET-v1"
CATEGORIES = ("normal", "spouse", "student", "senior", "past_active")
DEGREES = ("apprentice", "fellowcraft", "master")
OFFICES = ("venerable_master", "secretary", "treasurer", "hospitalarian")
TARIFF_CLP = {"normal": 20000, "spouse": 16000, "student": 8000, "senior": 12000}
TARIFF_USD = {"normal": 25, "spouse": 20, "student": 10, "senior": 15}


def uid(*parts: object) -> str:
    return str(uuid5(NAMESPACE, ":".join(map(str, parts))))


def period_at(offset: int) -> str:
    """0=2025-11; 11=2026-10; no dependency on today's clock."""
    count = 2025 * 12 + 10 + offset
    return f"{count // 12:04d}-{count % 12 + 1:02d}"


def make_dataset(member_count: int = 120, workshop_count: int = 4,
                 month_count: int = 12) -> dict:
    if not (1 <= member_count <= 2000 and
            2 <= workshop_count <= 30 and 1 <= month_count <= 24):
        raise ValueError("Allowed: members 1..2000, workshops 2..30, months 1..24")
    countries = ("CL",) * (workshop_count - 1) + ("PE",)
    workshops = [
        {"id": uid("w", n), "number": f"QA-{9000+n}",
         "name": f"Taller ficticio QA {n:02d}",
         "country": countries[n-1],
         "currency": "CLP" if countries[n-1] == "CL" else "USD"}
        for n in range(1, workshop_count + 1)
    ]
    persons, memberships, offices, quotas = [], [], [], []
    receipts, imputations, hospitalaria, tenidas, asistencia = [], [], [], [], []
    active_by_workshop = {w["id"]: [] for w in workshops}

    for n in range(1, member_count + 1):
        workshop = workshops[(n-1) % workshop_count]
        person_id, member_id = uid("p", n), uid("m", n)
        degree = DEGREES[(n-1) % len(DEGREES)]
        category = CATEGORIES[(n-1) % len(CATEGORIES)]
        state = "inactive" if n % 19 == 0 else "active"
        persons.append({
            "id": person_id, "first_names": "Persona",
            "last_names": f"Ficticia QA {n:05d}",
            "identity_type": "other",
            "identity_number": f"QA-NO-REAL-{n:06d}",
            "email": f"persona.qa.{n:06d}@pmgm.invalid"})
        memberships.append({
            "id": member_id, "person_id": person_id,
            "workshop_id": workshop["id"], "degree": degree,
            "category": category, "status": state,
            "institutional_number": f"QA-M-{n:06d}",
            "start_date": "2025-01-01"})
        if state == "inactive":
            continue
        active_by_workshop[workshop["id"]].append(member_id)
        if degree == "master" and n <= 4 * workshop_count:
            offices.append({
                "id": uid("office", n), "member_id": member_id,
                "workshop_id": workshop["id"],
                "type": OFFICES[(n-1) % len(OFFICES)],
                "starts_on": "2026-01-01", "ends_on": "2026-12-31"})
        if category == "past_active":
            hospitalaria.append({
                "id": uid("h", n), "member_id": member_id,
                "workshop_id": workshop["id"], "period": "2026",
                "kind": "annual_reposition",
                "currency": workshop["currency"],
                "amount": 1500 if workshop["currency"] == "CLP" else 3})
            continue

        for offset in range(month_count):
            month = period_at(offset)
            amount = (TARIFF_CLP if workshop["currency"] == "CLP"
                      else TARIFF_USD)[category]
            settled = (n + offset) % 4 != 0
            due_id = uid("due", n, month)
            quotas.append({
                "id": due_id, "member_id": member_id,
                "workshop_id": workshop["id"], "period": month,
                "currency": workshop["currency"], "amount": amount,
                "control_status": "up_to_date" if settled else "unpaid"})
            if settled:
                receipt_id = uid("receipt", n, month)
                # Payment date is not the quota period: tests two parallel views.
                received = "2026-10-20" if (n + offset) % 3 == 0 else "2026-09-20"
                receipts.append({
                    "id": receipt_id, "member_id": member_id,
                    "workshop_id": workshop["id"], "received_on": received,
                    "currency": workshop["currency"], "amount": amount,
                    "kind": "test_cash_receipt"})
                imputations.append({
                    "id": uid("allocation", n, month), "receipt_id": receipt_id,
                    "quota_id": due_id, "amount": amount})
    for workshop in workshops:
        members = active_by_workshop[workshop["id"]]
        for quarter in range(4):
            meeting_id = uid("tenida", workshop["id"], quarter)
            tenidas.append({
                "id": meeting_id, "workshop_id": workshop["id"],
                "date": f"2026-{quarter * 3 + 1:02d}-15",
                "degree": DEGREES[quarter % 3], "type": "regular"})
            for member_id in members[:25]:
                asistencia.append({
                    "id": uid("att", meeting_id, member_id),
                    "meeting_id": meeting_id, "member_id": member_id,
                    "status": "present" if
                    int(member_id.replace("-", "")[-2:], 16) % 4 else "excused"})
    dataset = {
        "manifest": {
            "schema": SCHEMA, "classification": "SYNTHETIC_ONLY",
            "import_status": "NOT_IMPORTED",
            "dataset_id": uid("dataset", member_count, workshop_count, month_count),
            "reference_date": "2026-10-23",
            "fixture_parameters": {
                "members": member_count, "workshops": workshop_count,
                "months": month_count},
            "warnings": [
                "Fictitious records only, not a copy of institutional data",
                "Interchange format; does not match database entities directly",
                "No API, Keycloak, PostgreSQL or document store was modified"]},
        "workshops": workshops, "persons": persons,
        "memberships": memberships, "offices": offices,
        "quotas": quotas, "cash_receipts": receipts,
        "quota_allocations": imputations, "hospitalaria": hospitalaria,
        "meetings": tenidas, "attendance": asistencia
    }
    validate(dataset)
    return dataset


def validate(dataset: dict) -> None:
    tables = ("workshops", "persons", "memberships", "offices", "quotas",
              "cash_receipts", "quota_allocations", "hospitalaria",
              "meetings", "attendance")
    ids = [item["id"] for table in tables for item in dataset[table]]
    if len(ids) != len(set(ids)):
        raise ValueError("Duplicate fixture ID")
    members = {m["id"] for m in dataset["memberships"]}
    people = {p["id"] for p in dataset["persons"]}
    workshops = {w["id"] for w in dataset["workshops"]}
    quotas = {q["id"]: q for q in dataset["quotas"]}
    receipts = {r["id"]: r for r in dataset["cash_receipts"]}
    meetings = {m["id"]: m for m in dataset["meetings"]}
    for m in dataset["memberships"]:
        if m["person_id"] not in people or m["workshop_id"] not in workshops:
            raise ValueError("Invalid membership foreign key")
    for table in ("offices", "quotas", "cash_receipts", "hospitalaria"):
        for row in dataset[table]:
            if row["member_id"] not in members or row["workshop_id"] not in workshops:
                raise ValueError(f"Invalid {table} foreign key")
    for a in dataset["quota_allocations"]:
        q, r = quotas.get(a["quota_id"]), receipts.get(a["receipt_id"])
        if not q or not r or a["amount"] != q["amount"] or q["amount"] != r["amount"]:
            raise ValueError("Invalid allocation amount")
        if (q["member_id"] != r["member_id"] or
                q["workshop_id"] != r["workshop_id"] or
                q["currency"] != r["currency"]):
            raise ValueError("Cross-member or cross-currency allocation")
    for a in dataset["attendance"]:
        if a["meeting_id"] not in meetings or a["member_id"] not in members:
            raise ValueError("Invalid attendance foreign key")
    if any(not p["email"].endswith("@pmgm.invalid") or
           not p["identity_number"].startswith("QA-NO-REAL-")
           for p in dataset["persons"]):
        raise ValueError("Non-synthetic personal identifier")


def serialize(dataset: dict) -> bytes:
    return (json.dumps(dataset, ensure_ascii=False,
                       sort_keys=True, indent=2) + "\n").encode("utf-8")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--members", type=int, default=120)
    parser.add_argument("--workshops", type=int, default=4)
    parser.add_argument("--months", type=int, default=12)
    parser.add_argument("--output", type=Path,
                        help="New local private JSON file; never overwrites")
    args = parser.parse_args()
    dataset = make_dataset(args.members, args.workshops, args.months)
    content = serialize(dataset)
    if args.output:
        # O_EXCL prevents accidentally clobbering a real QA export. Mode 0600.
        fd = os.open(args.output, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
        with os.fdopen(fd, "wb") as handle:
            handle.write(content)
    print(json.dumps({
        "classification": "SYNTHETIC_ONLY", "status": "NOT_IMPORTED",
        "schema": SCHEMA,
        "sha256": hashlib.sha256(content).hexdigest(),
        "records": {k: len(v) for k, v in dataset.items() if isinstance(v, list)},
        "written": bool(args.output),
    }, sort_keys=True))


if __name__ == "__main__":
    main()
