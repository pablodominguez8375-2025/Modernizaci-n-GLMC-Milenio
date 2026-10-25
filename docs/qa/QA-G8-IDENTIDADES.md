# QA G8 — identidades sintéticas OIDC, grados y cargos

Fecha: 16-10-2026. Issue #517, PR #521. Destino exclusivo: `glmdch`. Dominio canónico: `https://sistema.granlogiamixta.cl/`. **No se ha desplegado el host**; ningún resultado de CI lo acredita.

## Qué prepara

`infrastructure/keycloak/pmgm-realm.json` y su derivado público contienen 21 cuentas ficticias `qa.*`, con `id`/claim `sub` UUID estable. La cuenta `qa.admin` es una excepción de administración de plataforma y no se vincula a un hermano. Las otras 20 tienen ficha sintética independiente en Taller QA Nº 23, membresía activa, un grado efectivo, y vínculo exacto `Issuer+Subject` con `core.member_identity_links`. Cuentas sin cargo: 1°, 2° y 3°. Incluye cargo vigente y cargo vencido; perfiles propios de Gran Secretaría, Régimen Interior, Gran Archivero y dos cuentas de Biblioteca. Por ahora las dos cuentas de Biblioteca tienen rol técnico `document_manager`, por lo que **no demuestra aún una separación funcional editor/revisor**; crear un perfil explícito solo mediante definición institucional y tests de autorización, nunca inventarlo. Desde #635 (decisión del PO, 22-10-2026) existen además dos perfiles institucionales explícitos: `qa.bibliotecario` (rol `librarian`, Biblioteca Virtual) y `qa.comunicaciones` (rol `communications`, publica la revista y el Boletín mensual en PDF; sin Biblioteca general ni Gestión Documental).

El servicio `QaIdentitySeedService` se activa únicamente mediante `PMGM_QA_G8_ENABLED=true`, `ASPNETCORE_ENVIRONMENT=Staging`, `PMGM_QA_HTTPS=true`, `PMGM_QA_DB2=true` y datos ficticios habilitados en el QA histórico. No crea usuarios administrativos productivos ni lee datos institucionales. Si encuentra un vínculo activo `issuer+sub` a otro miembro, falla; la carga es idempotente con los identificadores de prueba. Esas fichas son sólo para validación del sistema.

## Primer montaje, no certificado

1. Utilizar exclusivamente el instalable completo firmado del mismo SHA que `dev`, nunca el ZIP de Pages ni artefactos parciales; verificar `SOURCE_SHA`, firma, digest y `MANIFEST`.
2. Obtener vía canal restringido `glmdch` inspección host/DNS/firewall y secretos seguros en `/etc/pmgm/srv01.env` (600/640); no subir credenciales ni huellas sin verificar a GitHub/Drive.
3. Usar volumen PostgreSQL **nuevo** y `PMGM_QA_DB2=true`; nunca borrar ni reutilizar volúmenes existentes automáticamente.
4. Configurar `PMGM_QA_HTTPS=true`, `PMGM_QA_G8_ENABLED=true`, URL web `https://sistema.granlogiamixta.cl` y URL OIDC `https://sistema.granlogiamixta.cl/identity`. Ejecutar preflight, validar restauración y backup fuera del host antes de publicar.
5. Instalar desde candidato firmado. El instalador exige roles G4 y ejecuta `scripts/qa/check-g8-identities.sh` **antes** del smoke: verifica 21 cuentas Keycloak importadas, 20 vínculos issuer+sub/miembros/grado y un cargo vigente/vencido. Si falla, NO se declara QA operativo.
6. Probar tokens OIDC reales, menú/Agenda por grado, aislamiento de documentos/datos, smoke autenticado, regresión y UAT. Registrar evidencia SHA y capturas sin PII en #97; mantener G9/G10 abiertos hasta pruebas físicas.

## Evidencia automatizada

- `tests/test_qa_g8_contract.py`: 21 IDs exactos, roles restringidos, matriz de grados, links y bloqueo de publicación sin fixture.
- `tests/qa_public_ingress_runtime.py`: Keycloak verdadero tras Caddy/TLS aislado; valida identidad estable de `qa.admin` además de issuer, PKCE, callbacks, consola de administración denegada y headers HTTPS.
- `scripts/build-qa-installable.sh`: bloquea empaquetado si falla contrato G8.
- `scripts/qa/check-g8-identities.sh`: comprobación operativa en PostgreSQL y Keycloak reales; no devuelve secretos.

### Límites y decisión

No hay canal SSH verificado hacia glmdch desde el asistente, ni aprovisionamiento de cuenta restringida. La PR #518 hacia `main` sigue en borrador y no puede fusionarse sin orden expresa del Sponsor. El dominio configurado en archivos no demuestra DNS, certificado o firewall real. `srv01` sigue pausado; no se promueve `main`.

El Sponsor aprobó Silo exclusivamente para QA sintético, nunca para almacenamiento productivo. La certificación física y el UAT requieren pruebas posteriores sobre `glmdch`.
