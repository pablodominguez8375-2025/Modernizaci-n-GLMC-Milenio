# Handoff backend #480 — Correo de contacto privacidad (19-10-2026)

PR #569, rama feature/privacy-contact-email-480-20261019-gpt. QA físico en otro hilo; main/srv01 fuera de alcance.

Contrato: `GET /api/privacy/contact` requiere autenticación pero no permiso de administrar Sistema, retorna solo `{ "email": "juridico@granlogiamixta.cl" }` o el valor de versión vigente `system.privacy.contact_email`. No expone lista de parámetros ni información personal. Resuelve fecha de vigencia de Chile, descarta versiones futuras y usa valor predeterminado si no hay valor vigente válido.

Gestión: `POST /api/system/settings/system.privacy.contact_email` usa permisos existentes de configuración, versión con fundamento, auditoría y validación estricta de una dirección única de máximo 254 caracteres. La definición aparece en categoría Privacidad de Parámetros, lista para frontend de Clau. Las versiones futuras se activan a su fecha de vigencia para lectura, sin cron externo.

Archivos: PrivacyContactPolicy.cs, PrivacyEndpoints.cs, SystemConfigurationEndpoints.cs, PrivacyContactPolicyTests.cs, PrivacyContactEmailHttpTests.cs y shard b. Verificación de pruebas por GitHub Actions exact-head antes de fusionar.

Pendiente: UAT con administrador y miembro sintéticos desde el hilo QA; el admin modifica Parámetros → Privacidad y cualquier hermano ve el correo en «Privacidad y mis datos». No cerrar el issue por CI solamente si falta aceptación física. No tocar credenciales ni datos reales.
