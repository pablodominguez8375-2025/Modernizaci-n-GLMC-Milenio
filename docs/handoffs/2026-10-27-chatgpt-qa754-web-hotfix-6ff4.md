# #754: QA WEB hotfix HTTP 415 desde versión física 6ff4 (27-10-2026)

## Evidencia y control de alcance
- WEB real en glmdch reporta `/qa-version.json` `source_sha=6ff4ae0d092d4214df4ea31af484efbc969fde54`. En el propio GitHub commit hay un defecto en `frontend/src/api/pmgmApi.ts`: `createUserAccount()` hace `this.request(...,{method:'POST',body:JSON.stringify(request)})` sin `Content-Type`. El middleware ASP.NET requiere JSON, por lo que el Sponsor recibió `415 Unsupported Media Type` al intentar crear un usuario **sintético**.
- Corrección canonical ya aprobada por PR #762 (dev), pero QA WEB antigua `6ff4` no está sincronizada con los 2026 cambios dev. **No promover API ni aplicar el instalador global**.
- Rama especial `qa/hotfix-754-web-json-from-6ff4-20261027` desde `6ff4ae0d092d4214df4ea31af484efbc969fde54`; rama de referencia inmutable de trabajo `qa/baseline-web-6ff4-20261027` en el mismo SHA. Este hotfix NO es un cambio de dev ni main.
- Cambio exclusivo de una llamada en `frontend/src/api/pmgmApi.ts`: reemplazo por `this.postJson<UserAccountCreated>('/api/system/user-accounts',request)` que añade `Content-Type: application/json` junto al cuerpo correcto. Añadida prueba sintética `frontend/src/api/qa754-hotfix415.test.ts` copiada de la PR #762 probada en CI. No se toca API/Keycloak/SMTP, cuentas, bases, migraciones, Compose ni WEB física. Los archivos de test/docs no entran en bundle web de producción, solo el cambio de frontend.
- Verificar `git diff 6ff4..<hotfix SHA>` y tests exactos antes de build. **No declarar firmado el artefacto solamente por SHA de git, etiqueta OCI o éxito de CI**.

## Estado real actual de glmdch
- API identity activa (3 capas: compose.recovery.json, override API, stage privado /etc/pmgm/qa754-identity-overlay.stage.json root0600); WEB con 2 capas (base recovery + override imagen WEB). `qa-lite` no instalado, timer WEB inactivo. G6 cifrado posterior a SMTP en Drive SHA verificado `e4184cb798e3b44414c09d85735db6e6b2002e75027de67da8ed5b930a7ca8a6`; restauración integral PostgreSQL/Silo independiente **no certificada**.
- NO ejecutar `docker compose up` genérico, refresh WEB automático, ni `--apply` de identidad API. No reintentar crear usuarios hasta tener WEB hotfix desplegada.

## Para siguiente implementación QA
- Preparar build WEB aislado desde commit exacto de esta rama (digest de fuente, `frontend/package-lock.json`, `frontend/Dockerfile`), con build args WEB públicos idénticos al Compose físico. No volcar compose/env a stdout.
- Crear imagen OCI WEB por ID `sha256:`, registrar revisión real HOTFIX, retener ID y runtime env de imagen WEB previa. Verificar `npm ci`, `npm run build`, pruebas Vitest y contenido compilado.
- Desplegar exclusivamente `web` mediante procedimiento supervisor con rollback por imagen, que admita **WEB dos capas** y **API tres capas**, y asegure antes/después IDs de Keycloak/API/Postgres/MinIO y continuidad de etapa identity. Validar HTTP /health/web, /health/ready, OIDC y huella de origen/versión HOTFIX sin falsear `/qa-version.json`. Si falla, revertir WEB anterior sin tocar API.
- El estado `QA_LITE_INSTALLER=NOT_INSTALLED` invalida usar scripts `qa-lite --apply` actuales. No reactivar timer.
- UAT solo con ficha de hermano y buzón **exclusivamente sintéticos**: POST 201, correo de primera clave, cambio obligatorio al ingresar, permisos y auditoría, sin publicar contraseñas/tokens/PII. Mapper `pmgm-account-kind` y SMTP reset Keycloak pendientes separados. Issue #754 abierto hasta evidencias físicas.
