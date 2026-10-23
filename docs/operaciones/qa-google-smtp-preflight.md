# #669 C5 — Comprobación de clave SMTP QA sin exponer secretos
Se ejecuta **solo en el servidor QA y bajo control autorizado**, desde la raíz del instalable:
    
    python3 scripts/qa/check-google-smtp-runtime.py --env-file /etc/pmgm/srv01.env

No despliega, no ejecuta docker compose, no envía SMTP, no transmite información de contraseña. Solo lee el archivo local protegido y el YAML Compose. Comprueba regularidad y permisos 0600/0640, que no sea enlace simbólico, una sola PMGM_SMTP_ENCRYPTION_KEY válida de 32 bytes decodificados base64 y que la variable solo se mapee al proceso API. Los mensajes son códigos sin valores; no copiar archivo .env en chats/Drive/GitHub.

READY_STATIC_ONLY significa preparación estática de la configuración, **no** que el contenedor esté levantado, que TLS funcione o que Gmail acepte el envío. Si aparece BLOCKED corregir localmente bajo custodia de secretos. El guardado y prueba real siguen requiriendo una cuenta Google autorizada y un buzón de prueba controlado. No usar datos reales en QA sintético sin aprobación.

Pruebas sintéticas: python3 -m unittest discover -s tests -p test_google_smtp_runtime_preflight.py.