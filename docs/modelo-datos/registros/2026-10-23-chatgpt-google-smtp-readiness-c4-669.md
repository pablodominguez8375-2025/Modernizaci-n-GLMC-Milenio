# GOV004 — #669 C4 Estado seguro de preparación SMTP (23-10-2026)

No hay migración, DDL ni campo persistente nuevo. **Contrato de lectura** GET /api/system/mail/credentials, sólo administradores con CanConfigureSystem:
- `configured: bool`: credencial cifrada almacenada y cifrado disponible, no confirma operatividad.
- `encryptionReady: bool`: existe clave raíz con formato AES-256.
- `transportReady: bool`: reglas de transporte smtp.gmail.com:587 STARTTLS, cuenta remitente y clave cifrada descifrable; **no hace SMTP ni confirma entrega**.
- `reason: string`: enum fijo `missing_encryption_key` / `missing_password` / `incomplete_transport` / `unreadable_password` / `ready_to_test`, nunca incluye detalles sensibles.

Auditoría: lectura sin valores secretos. El test de entrega real separado permanece en POST /api/system/mail/test con limitador antiabuso 5/min por actor. El frontend sólo habilita botón de prueba con transportReady=true y destinatario valido; modo mock siempre informa demostración, no envío.
