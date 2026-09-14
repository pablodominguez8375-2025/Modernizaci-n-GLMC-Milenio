# PMGM-SEC-001 — Lineamientos de Seguridad v0.1

## Objetivo
Establecer controles mínimos de seguridad y privacidad desde el inicio del Proyecto Milenio.

## Controles base
- SSO con OpenID Connect.
- MFA para perfiles sensibles y administración.
- RBAC con principio de mínimo privilegio.
- TLS obligatorio en tránsito.
- Cifrado de datos sensibles en reposo cuando corresponda.
- Secretos fuera del código y del repositorio.
- Logging estructurado y auditoría de acciones relevantes.
- Backups automáticos y pruebas de restauración.
- Separación de ambientes desarrollo/pruebas/producción.
- Revisión de dependencias y vulnerabilidades.
- Gestión de sesiones, expiración y revocación.
- Control de acceso a documentos por clasificación y rol.

## Privacidad
- Minimización de datos.
- Finalidad definida para cada dato personal.
- Acceso restringido según rol.
- Historial y trazabilidad de modificaciones.
- Políticas de retención y eliminación.
- Exportación/corrección cuando corresponda.
- Evaluación legal de tratamientos de datos personales y biométricos antes de incorporarlos.

## Auditoría mínima
Registrar:
- usuario;
- fecha y hora;
- acción;
- entidad afectada;
- identificador de registro;
- valor anterior y nuevo cuando aplique;
- resultado;
- origen técnico relevante.

## Regla de despliegue
No se desplegará información productiva o secretos en repositorios, imágenes públicas ni archivos de configuración versionados.
