# #669 C3 — cableado de clave cifrado SMTP en Compose/QA

Inicio 23-10-2026, ChatGPT. Base vivo `dev@9b9bc23edad98e2bbe6d1982271a3531f7f0953b`, PR #670 y #672 integradas, C1/C2 con gates 3/3 exact-head SUCCESS. Issue #669 sigue abierto por habilitación física y verificación real.

## Hallazgo urgente
El backend exige `SystemMail:EncryptionKey` en configuración (32 bytes base64), pero los tres Compose de QA/srv01/piloto **no la suministran al contenedor api**. La pantalla respondería falta clave de cifrado aun teniendo clave en `/etc/pmgm/srv01.env`.

## Reclamo de archivos
- `infrastructure/docker-compose.srv01.yml`
- `infrastructure/docker-compose.qa.yml`
- `infrastructure/docker-compose.pilot.yml`
- `infrastructure/srv01.env.example`
- `infrastructure/pilot.env.example`
- `.env.qa.example`
- `scripts/qa/check-google-smtp-compose-wiring.sh` (nuevo control sin secretos)
- `docs/operaciones/google-smtp-669.md`
- `docs/modelo-datos/registros/2026-10-23-chatgpt-google-smtp-compose-wiring-c3-669.md` (nuevo)
- este handoff.

Solución: variable **opcional** `PMGM_SMTP_ENCRYPTION_KEY` en el archivo de entorno local de infraestructura; expuesta **sólo** al contenedor API como `SystemMail__EncryptionKey`; no se envía a Keycloak ni web. Vacía => UI indica que no está configurada, sin generar ni inventar claves. No se modifica ningún entorno real ni `main`. Se añadirá prueba estática en CI y runbook, 3/3 antes de merge.

Siguiente paso tras integración: operador prepara clave aleatoria de 32 bytes bajo custodia, modifica archivo de entorno existente protegido, aplica despliegue conforme a políticas QA vigentes (NO automático), guarda contraseña de aplicación desde Parámetros y prueba entrega en buzón controlado. 
