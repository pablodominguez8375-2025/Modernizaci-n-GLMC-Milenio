# URGENTE #669 — Parámetros correo SMTP Google, clave y prueba real
Fecha: 23-10-2026. Sponsor solicitó funcionalidad real en la pantalla Parámetros → Correo electrónico.
Base inicial dev@ce5f6dcba5850d86dea47ac3bfc6382f14646ddc.

## Confirmación del defecto
En `SystemConfigurationEndpoints` no existe entrada editable segura para contraseña SMTP; el formulario genérico versionado muestra otros parámetros, pero carece de credencial y prueba. `SystemOperationsPanel.testMail` responde éxito simulado, jamás envía correo. `InitialAccountMail` usa configuración ambiental independiente de Parámetros.

## Alcance reclamado y coordinación con Clau
ChatGPT implementará (a) backend seguro para guardar credencial cifrada con clave de cifrado externa al código, nunca leerla en GET/historial/auditoría; (b) endpoint de prueba real TLS Gmail port 587 que envía al correo de prueba autorizado; (c) campo contraseña de aplicación y botón de prueba en la tarjeta de Parámetros Correo electrónico; (d) pruebas negativas y sin secretos; (e) conexión del flujo real de correo inicial a configuración guardada cuando el servidor tenga clave de cifrado disponible. El desarrollo NO desplegará secretos ni enviará correos reales en el entorno de desarrollo. Los cambios de UI se acotan a `SystemSettingsCategoryPanel` / api y un nuevo panel hijo; no editar `SystemOperationsPanel` ajeno en paralelo.

Seguridad: no permitir TLS inseguro ni servidor arbitrario para el test; sólo `smtp.gmail.com:587` con STARTTLS, credencial app de Google o OAuth futuro. Clave simétrica de 32 bytes base64 por `SystemMail__EncryptionKey` en servidor de API (fuera GitHub/Drive), persistencia en BD sólo cifrada. El UI nunca revela, exporta ni conserva el secreto después del guardado. El envío efectivo sólo se ejecuta por pulsación explícita del administrador.

No tocar main ni QA físico. PR hacia dev con CI/Showcase/Instalable exact HEAD y registro Drive+GitHub.
## Implementación en rama PR #670
Se añadieron API protegida de secreto/estado/prueba real, AES-GCM con clave de servidor, integración de envío de alta de usuarios, pantalla de Parámetros y cliente frontend. Baterías unitarias del cifrado, validación Google STARTTLS y demo que nunca afirma envío real; manual técnico nuevo. Validar gates CI/Showcase/Instalable del HEAD vivo; sin cambios en main ni QA físico y sin credenciales reales.
