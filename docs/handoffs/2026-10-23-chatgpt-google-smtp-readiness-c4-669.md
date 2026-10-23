# #669 C4 — Estado de preparación SMTP Google claro y sin secretos

ChatGPT, 23-10-2026. Base verificada `dev@33c805ec260e7cb80413647cf094067438711a7d` tras C1 #670, C2 #672 y C3 #674 fusionadas. NO modificar main ni QA físico ni generar secretos.

## Diagnóstico
GET `/api/system/mail/credentials` actualmente informa `configured=true` al existir ciphertext+clave raíz aun si faltan servidor/cuenta/STARTTLS o cambió la clave de cifrado y no se puede descifrar. En UI parece listo para prueba sin estarlo. Se agrega estado seguro que separe credencial guardada vs transporte listo para intentar prueba, sin tocar contraseña ni transmitir datos.

## Archivos reclamados
`backend/src/PMGM.Api/Modules/SystemConfiguration/SystemMailConfigurationEndpoints.cs`, `backend/tests/PMGM.Api.Tests/Integration/SystemMailGoogleHttpTests.cs`, `frontend/src/api/pmgmApi.ts`, `frontend/src/GoogleSmtpSettingsPanel.tsx`, `frontend/src/GoogleSmtpSettingsPanel.test.tsx`, `docs/operaciones/google-smtp-669.md`, GOV004 y este handoff. No edición de otros archivos ni de los de Clau.

## Contrato
Respuesta GET con `configured` (la clave está guardada y hay clave raíz de cifrado), `encryptionReady`, `transportReady` (resolución Gmail 587/STARTTLS y ciphertext descifrable), `reason` (código fijo no sensible: `missing_encryption_key`, `missing_password`, `incomplete_transport`, `unreadable_password`, `ready_to_test`). No prueba SMTP implícita; nunca marca `deliveryVerified` porque sólo el envío real puede hacerlo. No incluye valores de servidor, correo ni secretos. UI muestra razones útiles y mantiene separado guardar clave y verificar conexión. Tests HTTP Postgres validan vacío / listo / configuración incompleta / clave incompatible, sin red.

3/3 gate exact-HEAD + readback antes de integración a dev. Clave real sólo en servidor protegido y formulario.