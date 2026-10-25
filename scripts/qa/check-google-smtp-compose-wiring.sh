#!/usr/bin/env bash
# Prueba estática sin secretos del cableado SystemMail:EncryptionKey entre Compose y API.
set -euo pipefail
root="$(CDPATH= cd -- "$(dirname -- "$0")/../.." && pwd)"
cd "$root"
python3 - <<'PY'
from pathlib import Path

compose = (
    "infrastructure/docker-compose.srv01.yml",
    "infrastructure/docker-compose.pilot.yml",
    "infrastructure/docker-compose.qa.yml",
)
mapping = "SystemMail__EncryptionKey: ${PMGM_SMTP_ENCRYPTION_KEY:-}"
for file in compose:
    body = Path(file).read_text(encoding="utf-8")
    if body.count(mapping) != 1:
        raise SystemExit(f"{file}: mapping SMTP cifrado ausente o duplicado")
    if "  api:\n" not in body:
        raise SystemExit(f"{file}: sin servicio api")
    _, api = body.split("  api:\n", 1)
    # Solo el servicio API tiene acceso al secreto. Lo que sigue es la
    # primera definición de otro servicio al mismo nivel.
    import re
    api = re.split(r"(?m)^  [a-zA-Z0-9_-]+:\s*$", api, maxsplit=1)[0]
    if mapping not in api:
        raise SystemExit(f"{file}: variable SMTP no existe en el servicio api")
    forbidden = body.replace(api, "", 1)
    if mapping in forbidden:
        raise SystemExit(f"{file}: secreto SMTP en servicio no autorizado")
    print(f"PASS {file}: clave opcional sólo API")
for file in ("infrastructure/srv01.env.example", "infrastructure/pilot.env.example", ".env.qa.example"):
    body=Path(file).read_text(encoding="utf-8")
    matches=[line for line in body.splitlines() if line.startswith("PMGM_SMTP_ENCRYPTION_KEY=")]
    if matches != ["PMGM_SMTP_ENCRYPTION_KEY="]:
        raise SystemExit(f"{file}: plantilla secreta debe estar VACÍA (sin valores)")
    print(f"PASS {file}: valor no se incluye en el repositorio")
PY
