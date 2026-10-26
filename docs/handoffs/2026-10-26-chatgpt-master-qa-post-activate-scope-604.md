# #604 — Confirmación posactivación de scope del Máster QA — 2026-10-26

**Origen:** rama desde dev@07864aab4be1df58dc9298044b0fa7003d5d3fc5 (PR #740 fusionada y 6/6 gates de PR aprobadas). main estable, sin promoción.

## Riesgo
`prepare-master-qa.py` verifica el atributo `pmgm_scope=["order"]` al preparar y justo antes de `PUT /users/{id}` para habilitar `admin`. Pero después del PUT solo comprobaba que el usuario estuviera habilitado y que tuviera el rol. Keycloak puede devolver una representación abreviada en los listados y un PUT parcial podría alterar atributos. Así se podría reportar `PASS ENABLED_FOR_AUTHENTICATED_UAT_NOT_CERTIFIED` sin evidenciar que el usuario habilitado retuvo el scope institucional requerido por el backend.

## Corrección acotada
- Tras habilitar y verificar inventario del Máster único, volver a leer su representación completa desde Admin API y exigir sujeto exacto, nombre admin, `pmgm_scope=order` y marcador `pmgm_master_qa_bootstrap=issue-604`. Usar la misma función `require_master_scope` que ya emplean prepare y status.
- Test de proveedor sintético que elimina el atributo DURANTE PUT: debe emitir solo `BLOCKED MASTER_SCOPE_NOT_PROVISIONED`, nunca PASS; status posterior también bloqueado.
- No se realiza nueva escritura automática ante ese estado ambiguo: el operador con respaldo debe investigar la identidad y deshabilitarla/revertir de forma supervisada, sin reintentos ciegos.
- No se accede al servidor, no se crea/modifica ningún Máster real, ni claves ni datos privados ni producción.

**No cerrar issue #604** hasta lectura del mapper de Keycloak, nueva sesión JWT autenticada, roles y ámbito real, QA 26/26 y UAT. El operador #737 mantiene su propia reserva de archivos, sin modificaciones aquí. Registro espejo GitHub/Drive.
