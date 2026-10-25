# 25-10-2026 — #548 Reconciliación G8: LAB2 histórico y checker QA vigente

## Fuente de verdad
Dev actual antes de integración: `d34a8faefbad1359931d6c74a94cfa4c92a7f938` (Claude #730 incluida). `main@6dfb9546a4873baff15955cf86abfd7d47e3d111` intacta. PR #548 original HEAD `caf52c8e963a8d078112cdaa388f943c65698d55`, detrás del dev nuevo. No hay ejecución física nueva.

## Cambio exacto
- `scripts/qa/check-g8-identities.sh`: mantiene los conteos de fixtures **20 miembros y 21 usuarios Keycloak** que ya tenía el código de dev; corrige exclusivamente la serialización del booleano SQL `SELECT (expr)::text` que devuelve `true`, no `t`.
- `scripts/qa/g8-lab2-identity-rbac-probe.py` y `tests/test_qa_g8_lab2_probe.py` provienen del PR histórico con SHA de LAB2 **`4ef456fee3a578a9e6418f3f7072af5dd79eb045`**, separado del checker canónico del nuevo dev. Sus cifras 18/19 son válidas SOLO para ese laboratorio histórico y no deben aplicarse a un candidato QA nuevo.
- No cambios de identidades, permisos, cuentas, roles, base de datos, migraciones, DNS, credenciales o Docker. Sin datos personales reales.

## Gates y criterios
CI exact HEAD, Showcase y QA Installable 3/3 SUCCESS sobre base dev fresca antes de fusionar a dev. Test offline `test_qa_g8_lab2_probe.py`, shell checker actualizado con `true` y contract check de conteos 20/21. Una PR verde valida el código, NO G8 físico/UAT de la instancia actual. QA #97 permanece abierta para ejecución autorizada con nuevas fixtures y roles, identidad y RBAC reales.

Concurrencia Multi-IA: no tocar UI ni Keycloak de Claude; PR #548 es exclusiva de herramientas QA y pruebas del propietario.