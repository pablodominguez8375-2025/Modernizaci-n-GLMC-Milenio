# Handoff QA #754 — WEB hotfix 415 aislado y reversible

Fecha: 27-10-2026. Sponsor solicitó continuar tras V2 física (QA 17:02 UTC).

## Estado de las fuentes
- Proyecto QA `centenario-qa-lab2` servidor glmdch, WEB publicada `6ff4ae0d092d4214df4ea31af484efbc969fde54` antes del hotfix.
- API conserva tres capas Compose: recovery base, `api-<sha>.json` e identidad privada root0600; estado identity active confirmado y SMTP Google recibido. WEB tiene DOS capas: base compartida + `web-<imageDigest>.json` root0600. MinIO, PostgreSQL, Keycloak permanecen operativos, timer WEB inactivo.
- POST del formulario de alta sintética devolvió HTTP 415 porque falta `Content-Type: application/json`. PR #762 fusionó fix a dev; WEB física no actualizada.
- G6 cifrado corte 27oct 11:59Z, 1.027.549 bytes; SHA256 remoto/local **e4184cb798e3b44414c09d85735db6e6b2002e75027de67da8ed5b930a7ca8a6**; restauración independiente PostgreSQL/Silo NO certificada.
- `main@6dfb9546a4873baff15955cf86abfd7d47e3d111` protegida, srv01 pausado, solo QA sintética.

## Fuente WEB hotfix especial: PR #765
Rama `qa/hotfix-754-web-json-from-6ff4-20261027`, HEAD fijo **424153c3a06c2b0bc0f8c2e8599eed4bb9ac6786** (comparación con QA original **6ff4ae0d092d4214df4ea31af484efbc969fde54**, status ahead 3 commits). Cambios EXACTOS: una línea `frontend/src/api/pmgmApi.ts` reemplazada por método `postJson`, test sintético `frontend/src/api/qa754-hotfix415.test.ts`, este handoff QA basado en su rama. Dockerfile frontend, nginx.conf y package-lock permanecen de la versión física 6ff4. **No fusionar PR #765 a dev/main** (fix canónico PR #762 ya integrado dev).

## Entrega operador (en PR de desarrollo independiente)
- `scripts/qa/operators/pmgm-qa754-web-hotfix.py` SHA256 **b78c445c6e6f8c57de53993c6263db672bf5a263fef5a41caf37139f9e653151** y prueba `tests/test_qa754_web_hotfix.py` (20/20 unittest sintéticos y py_compile PASS). Cotejados bytes con GitHub antes de publicar.
- Modo `--check`: lee labels Compose WEB/API, solo acepta 2 capas WEB y 3 API con stage privado, imagen web OCI digest, estado original, G6 cifrado y timers, health HTTPS. NO crea usuarios, imágenes ni reinicia servicios.
- `--prepare`: única etapa que crea artefactos: `git fetch` desde GitHub, verifica commit fijo, ancestro y diff *exactamente de 3 archivos*, coteja sustitución de una línea, comprueba Dockerfile base y build args públicos OIDC, compila `npm run build` y ejecuta regresión `npm test -- src/api/qa754-hotfix415.test.ts` DENTRO de Docker build, coloca `qa-version.json` veraz con SHA hotfix y obtiene ID OCI sha256. No recrea contenedores ni hace cambios de base de datos. No es firma OCI/atestado Sigstore; procedencia QA = Git SHA pinneado+diff/test+CI de la PR de operador. Requiere >=8 GiB libres para builder.
- `--apply --confirm QA754-APLICAR-SOLO-WEB`: cambia exclusivamente servicio web usando base Compose + nuevo `web-<digest>.json` root0600 e imagen OCI preparada, `up -d --no-deps --no-build --pull never --force-recreate --wait web` bajo unidad systemd aislada. Captura IDs originales de api, keycloak, postgres, minio, y exige IDs inalterados tras el cambio; OIDC/public health, version con SHA exacto. Journal root0600 antes de mutación e intento de rollback web-only automático ante fallo. **El rollback no garantiza recuperación si Docker/host/disco fallan**.
- `--rollback --confirm QA754-REVERTIR-SOLO-WEB`: solo si existe journal privado consistente y actual WEB sigue en imagen hotfix; restituye web override anterior y salud, sin tocar la API.
- No reactivar timer, no repetir `--apply` API #761, no usar qa-lite no instalado, ni `docker compose down/up` general. Operador futuro NO ejecutado aún en glmdch: primero ejecutar `--check`, verificar salida y autorización de ventana supervisada; solo después `--prepare`, `--apply` y UAT.

## Instalación futura controlada (pendiente de CI y auditoría de QA)
Verificar GitHub HEAD exacto y SHA del script; descargar por Termius mediante raw URL con SHA pinneado; comparar sha256sum, instalar root:root 0700 en `/opt/centenario/qa-tools/pmgm-qa754-web-hotfix.py`. Preparar directorio estado `sudo install -d -o root -g root -m 700 /opt/centenario/qa-tools/qa754-web-hotfix`. Solo lectura primera vez: `sudo python3 /opt/centenario/qa-tools/pmgm-qa754-web-hotfix.py --check`. Si BLOCKED detener; no enviar output crudo de inspect, config/env, JWT, ni PII.

UAT: después de WEB hotfix instalado validar solo una cuenta y email **sintéticos**, POST 201, correo inicial, cambio obligado, no duplicidad, permisos. Mapper pmgm-account-kind #757 y SMTP reset Keycloak aún son otro flujo. No cerrar #754 antes de UAT.

## Precauciones adicionales
Este operador es código de QA sujeto a revisión. Proteger against cross-service compose drift. Un supuesto fijo en WEB/API, imagen/base y health debe reportar BLOCKED; **jamás** forzar con compose o alterar secretos para evitar bloqueo.
