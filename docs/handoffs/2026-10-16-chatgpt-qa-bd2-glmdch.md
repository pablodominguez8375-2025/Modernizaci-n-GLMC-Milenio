# Handoff — 16-10-2026 — ChatGPT — QA G4 PostgreSQL BD2 glmdch

**Issue:** #470, tablero #97, PR #520 (DRAFT) → `dev`.
**Base inicial:** dev@dc9bc9c8467b50febfbe22422ab6d0fc60c3c807. Main@6dfb9546a4873baff15955cf86abfd7d47e3d111 (no tocada).
**Alcance:** QA de datos sintéticos en glmdch. Silo reconstruido y firmado obligatorio. Sin servidor/SSH/DNS/TLS/secretos reales. **Despliegue QA pendiente; UAT no ejecutada.**

## Implementación en la PR
- `infrastructure/postgres/05-create-qa-db2.sh`: bootstrap solo volumen NUEVO, roles no-superusuario `pmgm_keycloak`, `pmgm_migrator`, `pmgm_runtime`, `pmgm_reports`; BD de Keycloak de propietario propio, aislamiento de CONNECT.
- `infrastructure/docker-compose.srv01-db2.yml`: overlay opt-in para Keycloak, migrador EF Core efímero, `db-grants` y API runtime limitado; reutiliza grants existentes del piloto sin modificar sus reservas.
- `scripts/qa/prepare-silo-archive.py`: combina overlay si `PMGM_QA_DB2=true` antes de emitir el Compose con imagen Silo verificada de mismo SHA, sin pull remoto alternativo.
- `scripts/preflight-srv01-qa.sh` / `scripts/qa/public-ingress-guards.sh` / `scripts/install-srv01-qa.sh`: fallo cerrado para HTTPS sin BD2, secretos no repetidos >=20 caracteres y rechazo a volumen preexistente; verificación BD2 previa al smoke.
- `scripts/restore-srv01-qa.sh`: restauración por propietario Keycloak/migrador, reaplicación de grants y comprobación; publicación HTTPS sigue en data-only/revisión.
- `scripts/check-qa-db2-roles.sh`: pruebas no destructivas con PostgreSQL de roles/propiedad/privilegios/negativas.
- `infrastructure/srv01.env.example`, `docs/qa/QA-BD2-GLMDCH.md`: variables de ejemplo SIN secretos y procedimiento.
- `tests/test_qa_db2_contract.py` + `scripts/build-qa-installable.sh`: pruebas shell, contrato, config Compose, gates estructurales obligatorias en empaquetado QA.

## Evidencia y límites
QA Installable sobre HEAD de implementación `ed7a9e4fec62933a6bf4d795e9c82bb2e70f4d1b` SUCCESS (run 38959539708); los restantes CI, Showcase, HTTPS/OIDC y Silo pueden estar todavía en curso y deben revisarse sobre **HEAD final** antes de merge. Ninguna verificación en glmdch. Probar runtime real DB2 con PostgreSQL 17/EF/Keycloak/Silo en runner o VM desechable y registrar negativas/recovery; el QA Installable prueba estructura, NO acredita operación G4.

## Riesgos / pasos concretos para la próxima IA
1. Revisar checks exact-head de PR #520, errores de runner e idempotencia; no declarar merge apto hasta cinco gates SUCCESS y pruebas PostgreSQL reales BD2.
2. Probar primer arranque aislado por SHA: roles, DML, migración, insert auditoría permitido y UPDATE/DELETE/DDL denegados, historia EF oculta; simular restauración de DOS BDs y regrants. No reutilizar volúmenes ya inicializados.
3. Si los gates y validación real satisfacen el criterio, reconciliar dev vivo, hacer merge a dev con autorización general del PO. Verificar nuevo SHA Pages/QA entregable. No tocar main.
4. Mantener abiertas G3 ingreso público host real, G7 canal glmdch (#501/#518 sin main), G8 identidades en backend, respaldo externo, G9/G10 smoke/regresión/UI/UAT.
5. Coordinar con Claude según GOV-003; no tocar sus archivos UI ni `infrastructure/docker-compose.pilot*` reservados #479.

Referencias: PR #519 incorporó HTTPS a dev@dc9bc9c; PR #506 cerró ingeniería de suministro firmado, pero NO acceso al host. #97 conserva tablero G1–G10. Dominio canónico QA: https://sistema.granlogiamixta.cl/.
