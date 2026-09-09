#!/usr/bin/env python3
import json
from pathlib import Path
import sys

CATALOGS = [
    Path("docs/seguridad/PMGM-DATA-CLASSIFICATION-CATALOG.json"),
    Path("docs/seguridad/PMGM-DATA-CLASSIFICATION-LODGE-MANAGEMENT.json"),
    Path("docs/seguridad/PMGM-DATA-CLASSIFICATION-NOTIFICATIONS.json"),
]
ALLOWED_CLASSIFICATIONS = {
    "public_projection",
    "internal",
    "confidential",
    "sensitive",
    "restricted",
}
REQUIRED_FIELDS = {
    "code",
    "module",
    "entity",
    "field",
    "classification",
    "purpose",
    "containsSensitiveData",
    "allowInLogs",
    "allowExport",
    "publicProjection",
    "masking",
    "retentionPolicyCode",
    "owner",
    "status",
}
FORBIDDEN_PLACEHOLDERS = {"", "undefined", "not_defined", "n/a", "todo", "tbd"}


def fail(message: str) -> None:
    print(f"DATA CLASSIFICATION GATE FAILED: {message}")
    raise SystemExit(1)


def load_rows() -> list[dict]:
    rows: list[dict] = []
    for catalog in CATALOGS:
        if not catalog.exists():
            fail(f"No existe el catálogo {catalog}.")
        try:
            payload = json.loads(catalog.read_text(encoding="utf-8"))
        except (json.JSONDecodeError, OSError) as exc:
            fail(f"No fue posible leer {catalog}: {exc}")
        catalog_rows = payload.get("classifications")
        if not isinstance(catalog_rows, list) or not catalog_rows:
            fail(f"{catalog} debe contener al menos una clasificación de campo.")
        rows.extend(catalog_rows)
    return rows


def main() -> int:
    rows = load_rows()
    seen_codes: set[str] = set()
    seen_fields: set[tuple[str, str, str]] = set()

    for index, row in enumerate(rows, start=1):
        if not isinstance(row, dict):
            fail(f"Clasificación #{index} no es un objeto JSON válido.")

        missing = sorted(REQUIRED_FIELDS - row.keys())
        if missing:
            fail(f"{row.get('code', f'Clasificación #{index}')} omite campos: {', '.join(missing)}")

        code = str(row["code"]).strip()
        if code in seen_codes:
            fail(f"Código duplicado: {code}")
        seen_codes.add(code)

        for field in REQUIRED_FIELDS - {
            "containsSensitiveData",
            "allowInLogs",
            "allowExport",
            "publicProjection",
        }:
            value = str(row[field]).strip()
            if value.lower() in FORBIDDEN_PLACEHOLDERS:
                fail(f"{code}: {field} no puede quedar sin definir.")

        for field in ("containsSensitiveData", "allowInLogs", "allowExport", "publicProjection"):
            if not isinstance(row[field], bool):
                fail(f"{code}: {field} debe ser booleano.")

        classification = str(row["classification"]).strip()
        if classification not in ALLOWED_CLASSIFICATIONS:
            fail(f"{code}: clasificación no permitida: {classification}")

        key = (str(row["module"]).strip(), str(row["entity"]).strip(), str(row["field"]).strip())
        if key in seen_fields:
            fail(f"{code}: campo duplicado dentro del mismo módulo: {'/'.join(key)}")
        seen_fields.add(key)

        if classification in {"sensitive", "restricted"} and row["allowInLogs"]:
            fail(f"{code}: datos sensibles/restringidos no pueden quedar habilitados en logs.")

        if row["containsSensitiveData"] and row["allowInLogs"]:
            fail(f"{code}: un campo marcado como sensible no puede registrarse en logs.")

        if row["publicProjection"] and classification != "public_projection":
            fail(f"{code}: sólo public_projection puede exponerse como proyección pública.")

        if classification == "public_projection" and not row["publicProjection"]:
            fail(f"{code}: public_projection debe declarar publicProjection=true.")

        if row["publicProjection"] and row["allowExport"]:
            fail(f"{code}: la proyección pública no habilita exportación masiva por defecto.")

    print(f"Data classification gate OK: {len(rows)} campos clasificados en {len(CATALOGS)} catálogos con guardrails estructurales.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
