# #669 C5 — preflight SMTP Google seguro y de solo lectura
23-10-2026. Base dev@84ddb69fe2d9b7500fcf2e1021e75d793ee61fcd. C1 PR#670, C2 PR#672, C3 PR#674 fusionadas. Issue #669 reservado a agente:chatgpt.

Motivo: no declarar el correo operativo por haber compilado o instalado Compose. Antes de ingresar credenciales, un administrador validará localmente formato y permisos de clave cifrado, y mapeo Compose, sin imprimir secretos.

Alcance propio, sin solapar PR #675 C4: scripts/qa/check-google-smtp-runtime.py (nuevo), tests/test_google_smtp_runtime_preflight.py (nuevo), docs/operaciones/qa-google-smtp-preflight.md (nuevo), docs/modelo-datos/registros/2026-10-23-chatgpt-google-smtp-preflight-c5-669.md (nuevo), este handoff y un paso de unittest en .github/workflows/ci.yml.

No hace: SSH, conectar Google, editar /etc/pmgm/srv01.env, Docker up/restart, captar contraseñas, deploy QA/main ni UAT. Gates 3/3 HEAD exacto.
