#!/usr/bin/env python3
import argparse
from datetime import datetime
import json
from pathlib import Path
import re
import sys
from urllib.parse import urlparse

EXPECTED_RELEASE = "1.0.0-rc1"
SHA40 = re.compile(r"^[0-9a-f]{40}$", re.IGNORECASE)
ALLOWED_STATUSES = {"pending", "pass", "fail", "not_applicable"}
REQUIRED_CASE_IDS = {f"UAT-{index:03d}" for index in range(1, 21)}
FORBIDDEN_EVIDENCE_MARKERS = {
    "password=",
    "authorization: bearer",
    "client_secret",
    "private_key",
    "access_token",
    "refresh_token",
}


def fail(message: str) -> None:
    print(f"UAT EVIDENCE GATE FAILED: {message}")
    raise SystemExit(1)


def load(path: Path) -> dict:
    if not path.exists():
        fail(f"No existe el archivo de evidencia {path}.")
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        fail(f"No fue posible leer {path}: {exc}")
    if not isinstance(value, dict):
        fail("La raíz del archivo UAT debe ser un objeto JSON.")
    return value


def valid_utc(value: str) -> bool:
    try:
        parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
    except (TypeError, ValueError):
        return False
    return parsed.tzinfo is not None


def validate_evidence_strings(case_id: str, evidence) -> None:
    if not isinstance(evidence, list):
        fail(f"{case_id}: evidence debe ser una lista.")
    for item in evidence:
        if not isinstance(item, str) or not item.strip():
            fail(f"{case_id}: cada evidencia debe ser una referencia textual no vacía.")
        lowered = item.lower()
        for marker in FORBIDDEN_EVIDENCE_MARKERS:
            if marker in lowered:
                fail(f"{case_id}: la evidencia parece contener un secreto ({marker}).")


def main() -> int:
    parser = argparse.ArgumentParser(description="Valida evidencia UAT de Proyecto Milenio v1.0.0-rc1.")
    parser.add_argument("path", type=Path)
    parser.add_argument(
        "--allow-pending",
        action="store_true",
        help="Valida sólo la estructura de una plantilla todavía no ejecutada.",
    )
    args = parser.parse_args()

    payload = load(args.path)
    for field in ("schemaVersion", "releaseVersion", "executionId", "rcHeadSha", "environment", "dataPolicy", "cases", "approval"):
        if field not in payload:
            fail(f"Falta el campo obligatorio {field}.")

    if payload["schemaVersion"] != 1:
        fail("schemaVersion debe ser 1.")
    if payload["releaseVersion"] != EXPECTED_RELEASE:
        fail(f"releaseVersion debe ser {EXPECTED_RELEASE}.")
    if not isinstance(payload["executionId"], str) or not payload["executionId"].strip():
        fail("executionId debe ser un texto no vacío.")

    rc_sha = payload["rcHeadSha"]
    if args.allow_pending:
        if rc_sha is not None and not (isinstance(rc_sha, str) and SHA40.fullmatch(rc_sha)):
            fail("rcHeadSha debe ser null en plantilla o un SHA Git de 40 caracteres.")
    elif not isinstance(rc_sha, str) or not SHA40.fullmatch(rc_sha):
        fail("Una UAT aprobable debe registrar el SHA exacto de 40 caracteres del candidato probado.")

    environment = payload["environment"]
    if not isinstance(environment, dict):
        fail("environment debe ser un objeto.")
    if environment.get("name") != "pilot-operational":
        fail("environment.name debe ser pilot-operational.")
    base_url = environment.get("baseUrl")
    executed_at = environment.get("executedAtUtc")
    if args.allow_pending:
        if base_url is not None and (not isinstance(base_url, str) or not base_url.strip()):
            fail("environment.baseUrl debe ser null o una URL no vacía.")
        if executed_at is not None and (not isinstance(executed_at, str) or not valid_utc(executed_at)):
            fail("environment.executedAtUtc debe ser null o un timestamp UTC/offset válido.")
    else:
        if not isinstance(base_url, str):
            fail("Una UAT aprobable debe registrar environment.baseUrl.")
        parsed = urlparse(base_url)
        if parsed.scheme != "https" or not parsed.netloc or parsed.username or parsed.password:
            fail("environment.baseUrl debe ser HTTPS y no puede contener credenciales.")
        if not isinstance(executed_at, str) or not valid_utc(executed_at):
            fail("Una UAT aprobable debe registrar executedAtUtc con zona horaria.")

    policy = payload["dataPolicy"]
    expected_policy = {
        "fictionalOrAuthorizedTestDataOnly": True,
        "noSecretsInEvidence": True,
        "noPersonalDataCommittedToGit": True,
    }
    if policy != expected_policy:
        fail("dataPolicy debe conservar los tres guardrails de datos/evidencia activados.")

    cases = payload["cases"]
    if not isinstance(cases, list) or len(cases) != 20:
        fail("La UAT debe contener exactamente los 20 casos de aceptación definidos para v1.0.0-rc1.")

    seen = set()
    failed_cases = []
    pending_cases = []
    for item in cases:
        if not isinstance(item, dict):
            fail("Cada caso UAT debe ser un objeto JSON.")
        required_fields = {"id", "category", "title", "required", "status", "expected", "evidence", "notes"}
        missing = sorted(required_fields - item.keys())
        if missing:
            fail(f"Caso {item.get('id', '?')} omite: {', '.join(missing)}")
        case_id = item["id"]
        if case_id in seen:
            fail(f"ID UAT duplicado: {case_id}")
        seen.add(case_id)
        if case_id not in REQUIRED_CASE_IDS:
            fail(f"Caso inesperado para esta RC: {case_id}")
        if item["required"] is not True:
            fail(f"{case_id}: los 20 casos base son obligatorios.")
        if not isinstance(item["category"], str) or not item["category"].strip():
            fail(f"{case_id}: category es obligatorio.")
        if not isinstance(item["title"], str) or not item["title"].strip():
            fail(f"{case_id}: title es obligatorio.")
        if not isinstance(item["expected"], str) or len(item["expected"].strip()) < 20:
            fail(f"{case_id}: expected debe describir un resultado verificable.")

        status = item["status"]
        if status not in ALLOWED_STATUSES:
            fail(f"{case_id}: status inválido: {status}")
        validate_evidence_strings(case_id, item["evidence"])

        if status == "fail":
            failed_cases.append(case_id)
        elif status in {"pending", "not_applicable"}:
            pending_cases.append(case_id)

        if status == "pass" and not item["evidence"]:
            fail(f"{case_id}: un resultado pass debe registrar al menos una referencia de evidencia.")
        if status == "fail" and (not isinstance(item["notes"], str) or not item["notes"].strip()):
            fail(f"{case_id}: un fail debe explicar el hallazgo en notes.")

    if seen != REQUIRED_CASE_IDS:
        missing = sorted(REQUIRED_CASE_IDS - seen)
        fail(f"Faltan casos UAT obligatorios: {', '.join(missing)}")

    approval = payload["approval"]
    if not isinstance(approval, dict):
        fail("approval debe ser un objeto.")
    for field in ("decision", "approvedByRole", "approvedAtUtc", "comments"):
        if field not in approval:
            fail(f"approval omite {field}.")
    if approval["approvedByRole"] != "Sponsor/Product Owner":
        fail("approvedByRole debe permanecer como Sponsor/Product Owner.")

    if args.allow_pending:
        if approval["decision"] not in {"pending", "approved", "rejected"}:
            fail("approval.decision inválido.")
        print(
            f"UAT template gate OK: {len(cases)} casos estructurados para {EXPECTED_RELEASE}; "
            f"pendientes={len(pending_cases)}, fallidos={len(failed_cases)}."
        )
        return 0

    if failed_cases:
        fail(f"Existen casos fallidos: {', '.join(failed_cases)}")
    if pending_cases:
        fail(f"Existen casos no aprobados: {', '.join(pending_cases)}")
    if approval["decision"] != "approved":
        fail("La evidencia completa requiere approval.decision=approved.")
    approved_at = approval["approvedAtUtc"]
    if not isinstance(approved_at, str) or not valid_utc(approved_at):
        fail("La aprobación requiere approvedAtUtc con zona horaria.")

    print(f"UAT EVIDENCE APPROVED: {EXPECTED_RELEASE}, 20/20 casos pass y aprobación formal registrada.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
