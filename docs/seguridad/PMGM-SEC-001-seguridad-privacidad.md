# PMGM-SEC-001 — Baseline de seguridad y privacidad

**Estado:** Baseline inicial  
**Versión:** 0.1

## 1. Objetivo
Establecer controles mínimos de seguridad, privacidad, trazabilidad y continuidad para todas las etapas del Proyecto Milenio.

## 2. Principios
- Seguridad y privacidad desde el diseño y por defecto.
- Mínimo privilegio.
- Necesidad de saber y acceso por finalidad.
- Autenticación centralizada.
- Separación entre identidad, autorización y datos de negocio.
- Cifrado en tránsito y en reposo cuando corresponda.
- Auditoría de operaciones relevantes.
- Minimización de datos personales.
- Separación estricta de ambientes.
- No utilizar datos productivos reales en desarrollo salvo autorización y controles específicos.

## 3. Identidad y autenticación
- SSO mediante OpenID Connect/OAuth 2.0.
- MFA obligatorio para administradores y perfiles de alto privilegio; extensible a otros usuarios.
- Contraseñas administradas por el proveedor de identidad, no por módulos de negocio.
- Recuperación de cuenta centralizada.
- Revocación inmediata al perder la calidad o función que otorga acceso.
- Sesiones con expiración, renovación controlada y revocación.
- Prohibición de cuentas compartidas para tareas administrativas.

## 4. Autorización
Modelo combinado:
- RBAC para cargos y funciones institucionales.
- Contexto de organización/Taller.
- Permisos específicos para operaciones sensibles.
- Vigencia temporal de roles cuando corresponda.

Ejemplo: ser Secretario de un Taller no debe otorgar acceso de Secretaría a todos los Talleres.

## 5. Datos personales
- Registrar solo datos necesarios para fines institucionales definidos.
- Utilizar identificadores técnicos internos no significativos.
- Evitar usar RUT u otros identificadores nacionales como clave primaria o identificador público de recursos.
- Distinguir datos de contacto visibles entre miembros de los datos administrativos internos.
- Restringir exportaciones masivas.
- Registrar y auditar accesos administrativos de riesgo.
- Definir política de conservación y eliminación antes de producción.

## 6. Datos sensibles y campos restringidos
Cuando exista información de mayor sensibilidad:
- acceso explícitamente autorizado;
- cifrado adicional a nivel de aplicación o columna cuando el análisis de riesgo lo justifique;
- exclusión de logs y mensajes de error;
- enmascaramiento en interfaces no autorizadas;
- prohibición de replicarla en ambientes de prueba sin anonimización.

## 7. API y aplicaciones
- Solo HTTPS/TLS.
- Validación de entrada en servidor.
- Autorización en cada operación, no solo en la interfaz.
- Protección frente a IDOR mediante validación de contexto.
- Rate limiting para superficies expuestas.
- CORS restringido.
- Headers de seguridad apropiados.
- OpenAPI sin exponer secretos.
- Versionado de contratos.
- Manejo seguro de errores sin stack traces en producción.

## 8. Secretos
- No guardar claves, tokens, contraseñas ni certificados privados en Git.
- Utilizar secretos del entorno/secret manager.
- Rotación de credenciales.
- Diferentes secretos por ambiente.
- Las credenciales de integraciones externas deben tener permisos mínimos.

## 9. Base de datos
- PostgreSQL no expuesto directamente a Internet.
- Usuario de aplicación con permisos mínimos.
- Cuentas administrativas separadas.
- Backups cifrados cuando sea viable y requerido.
- Registro de migraciones.
- Restricciones e integridad en base de datos, además de validación de aplicación.
- Pruebas de restauración programadas.

## 10. Documentos y archivos
- Los archivos no deben confiar en el nombre o MIME informado por el cliente.
- Validar tamaño, extensión, tipo y contenido cuando corresponda.
- Nombres físicos aleatorios/no predecibles.
- Acceso mediante autorización de aplicación o URLs temporales.
- Hash de integridad.
- Versionado y trazabilidad.
- Escaneo antimalware para cargas cuando la infraestructura lo permita.

## 11. Auditoría
Eventos mínimos:
- inicio/cierre de sesión relevante;
- fallos repetidos de autenticación;
- cambios de roles y permisos;
- altas/bajas/modificaciones de miembros;
- cambios de pertenencia, grado y cargos;
- acceso o descarga de información restringida cuando aplique;
- creación/modificación/eliminación lógica de documentos;
- operaciones administrativas y exportaciones.

Los logs deben ser protegidos contra modificación y tener retención definida.

## 12. Desarrollo seguro
Pipeline mínimo:
- compilación reproducible;
- pruebas unitarias;
- análisis de dependencias;
- análisis estático cuando sea viable;
- revisión de secretos accidentales;
- pruebas de integración;
- aprobación antes de fusionar a `main`.

No se aceptará código productivo directamente en `main` sin el flujo acordado.

## 13. Ambientes
- Desarrollo: datos sintéticos.
- Staging: configuración semejante a producción, sin uso indiscriminado de datos reales.
- Producción: acceso operativo limitado, monitoreo y respaldos.

Los ambientes deben utilizar credenciales y bases separadas.

## 14. Continuidad
Antes de producción se deberá definir:
- RPO objetivo.
- RTO objetivo.
- frecuencia de backups.
- copia fuera del servidor principal.
- procedimiento de restauración.
- responsables de contingencia.
- prueba periódica documentada.

## 15. Checklist mínimo de salida a producción
- MFA administrativo habilitado.
- TLS válido.
- secretos fuera del repositorio.
- base de datos sin exposición pública.
- backups automáticos probados.
- auditoría activa.
- monitoreo y alertas básicas.
- roles revisados.
- datos de prueba eliminados.
- política de actualización y rollback documentada.
- revisión de privacidad y permisos completada.

## 16. Requisitos normativos
La arquitectura deberá permitir implementar los principios y obligaciones aplicables de protección de datos personales en Chile, incluyendo las exigencias vigentes al momento de cada puesta en producción. La validación jurídica específica se documentará separadamente y deberá mantenerse actualizada.
