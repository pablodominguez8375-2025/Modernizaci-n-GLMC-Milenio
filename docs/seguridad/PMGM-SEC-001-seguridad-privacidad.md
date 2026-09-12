# PMGM-SEC-001 — Baseline de seguridad y privacidad

**Estado:** Baseline inicial  
**Versión:** 0.2

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

La pertenencia y trayectoria masónica deberán clasificarse conservadoramente como información sensible/alto impacto cuando permitan revelar convicciones filosóficas o ideológicas.

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
- DTOs diferenciados por finalidad para evitar sobreexposición de datos.

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
- políticas de retención y anonimización ejecutables por categoría de dato.

## 10. Documentos y archivos
- Los archivos no deben confiar en el nombre o MIME informado por el cliente.
- Validar tamaño, extensión, tipo y contenido cuando corresponda.
- Nombres físicos aleatorios/no predecibles.
- Acceso mediante autorización de aplicación o URLs temporales.
- Hash de integridad.
- Versionado y trazabilidad.
- Escaneo antimalware para cargas cuando la infraestructura lo permita.
- clasificación de documentos por sensibilidad, finalidad y conservación.

## 11. Auditoría
Eventos mínimos:
- inicio/cierre de sesión relevante;
- fallos repetidos de autenticación;
- cambios de roles y permisos;
- altas/bajas/modificaciones de miembros;
- cambios de pertenencia, grado y cargos;
- acceso o descarga de información restringida cuando aplique;
- creación/modificación/eliminación lógica de documentos;
- operaciones administrativas y exportaciones;
- cambios de política de tratamiento o retención;
- solicitudes de derechos de titulares;
- incidentes de privacidad/seguridad;
- decisiones y excepciones que utilicen datos sensibles.

Los logs deben ser protegidos contra modificación y tener retención definida.

## 12. Desarrollo seguro
Pipeline mínimo:
- compilación reproducible;
- pruebas unitarias;
- análisis de dependencias;
- análisis estático cuando sea viable;
- revisión de secretos accidentales;
- pruebas de integración;
- aprobación antes de fusionar a `main`;
- gate de privacidad para tratamientos de datos personales relevantes.

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
- finalidad/base jurídica registradas para tratamientos relevantes.
- política de conservación definida.
- proveedores/encargados identificados.
- transferencias internacionales revisadas.
- EIPD/DPIA realizada cuando corresponda.
- procedimiento de ejercicio de derechos disponible.
- procedimiento de incidentes disponible.

## 16. Ley 21.719 — requisito normativo explícito
Proyecto Milenio adopta como baseline de diseño la preparación para la Ley N° 21.719, publicada el 13-12-2024 y con vigencia general diferida al 01-12-2026.

El cumplimiento se desarrolla en:
- `PMGM-REQ-026 — Cumplimiento de Ley 21.719`;
- `PMGM-SEC-003 — Matriz técnica de cumplimiento Ley 21.719`;
- `PMGM-BLG-048 — Cumplimiento Ley 21.719`.

Son requisitos transversales:
- licitud y finalidad;
- minimización;
- transparencia;
- privacidad desde el diseño y por defecto;
- acceso, rectificación, supresión, oposición, portabilidad y bloqueo;
- confidencialidad;
- seguridad proporcional al riesgo;
- conservación/anonimización/supresión;
- tratamiento reforzado de datos sensibles;
- EIPD/DPIA cuando corresponda;
- gestión de encargados;
- transferencias internacionales;
- respuesta a vulneraciones;
- trazabilidad y accountability.

La ley y las instrucciones de la futura Agencia prevalecerán frente a supuestos de diseño incompatibles. Las interpretaciones jurídicas que afecten bases de licitud, excepciones, conservación o cesiones deberán validarse formalmente antes de producción.
