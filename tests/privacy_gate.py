#!/usr/bin/env python3
import json
from pathlib import Path
import sys

MANIFEST = Path("docs/seguridad/PMGM-PRIVACY-MANIFEST.json")
REQUIRED_FIELDS = {
    "code",
    "module",
    "purpose",
    "lawfulBasisStatus",
    "retentionPolicyStatus",
    "accessControlStatus",
    "containsSensitiveData",
    "owner",
    "legalReviewRequired",
}
FORBIDDEN_PLACEHOLDERS = {"", "undefined", "not_defined", "n/a", "todo", "tbd"}


def fail(message: str) -> None:
    print(f"PRIVACY GATE FAILED: {message}")
    raise SystemExit(1)


def main() -> int:
    if not MANIFEST.exists():
        fail(f"No existe el manifiesto {MANIFEST}.")

    try:
        payload = json.loads(MANIFEST.read_text(encoding="utf-8"))
    except (json.JSONDecodeError, OSError) as exc:
        fail(f"No fue posible leer el manifiesto: {exc}")

    treatments = payload.get("treatments")
    if not isinstance(treatments, list) or not treatments:
        fail("El manifiesto debe contener al menos una actividad de tratamiento.")

    seen_codes: set[str] = set()

    for index, treatment in enumerate(treatments, start=1):
        if not isinstance(treatment, dict):
            fail(f"Tratamiento #{index} no es un objeto JSON válido.")

        missing = sorted(REQUIRED_FIELDS - treatment.keys())
        if missing:
            fail(f"{treatment.get('code', f'Tratamiento #{index}')} omite campos: {', '.join(missing)}")

        code = str(treatment["code"]).strip()
        if code in seen_codes:
            fail(f"Código duplicado: {code}")
        seen_codes.add(code)

        for field in REQUIRED_FIELDS - {"containsSensitiveData", "legalReviewRequired"}:
            value = str(treatment[field]).strip()
            if value.lower() in FORBIDDEN_PLACEHOLDERS:
                fail(f"{code}: {field} no puede quedar sin definir.")

        if not isinstance(treatment["containsSensitiveData"], bool):
            fail(f"{code}: containsSensitiveData debe ser booleano.")
        if not isinstance(treatment["legalReviewRequired"], bool):
            fail(f"{code}: legalReviewRequired debe ser booleano.")

    print(f"Privacy gate OK: {len(treatments)} tratamientos estructuralmente completos.")
    print("Nota: pending_legal_review es válido en esta fase; el gate de producción será más estricto.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
