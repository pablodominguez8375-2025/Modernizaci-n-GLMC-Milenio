# PMGM Release Candidate v1.0.0-rc1 — Piloto Operacional

**Estado:** Release Candidate técnica  
**Canal:** `release_candidate`  
**Objetivo:** validar una base reproducible y auditable antes de promover Proyecto Milenio a v1.0 estable.  
**Rama:** `release/v1.0-rc1`  
**Manifest:** `release/PMGM-RELEASE-1.0.0-rc1.json`

## 1. Qué representa esta RC

v1.0.0-rc1 consolida la primera base integral de Proyecto Milenio apta para validación operacional controlada. No equivale por sí sola a una puesta en producción definitiva ni autoriza el ingreso masivo de datos personales reales.

La RC congela el núcleo funcional, seguridad, persistencia, infraestructura del piloto, backup/restauración y bootstrap institucional para realizar una UAT institucional sobre una versión identificable y repetible.

La promoción a v1.0 estable requiere además los controles manuales descritos en este documento y en el manifest de release.

## 2. Alcance funcional congelado

La RC incluye, entre otros, los siguientes bloques ya integrados:

- identidad institucional OIDC mediante Keycloak;
- autorización por roles y alcance institucional;
- Superadmin de plataforma separado del Administrador de Gran Logia;
- personas, miembros, pertenencias, grados, cargos e historial institucional;
- transferencias entre Talleres preservando el historial de origen;
- Régimen Interior, reportes ejecutivos, control de miembros y calidad/corroboración de datos;
- Gran Secretaría, espacios, documentos oficiales y autorización de ceremonias;
- regularidad de Gran Tesorería y Gran Hospitalaria como requisito de ceremonias;
- publicación configurable de insinuados para iniciaciones;
- Gestión Logial y docencia;
- Gestor Documental con Object Storage, versionado, validación de contenido y análisis antimalware;
- Biblioteca Virtual con control por grado/permisos;
- Gran Archivero separado de Biblioteca Virtual y sin planchas de trabajo;
- notificaciones institucionales;
- calendario institucional proyectado, preservando `Ocupado` para eventos protegidos;
- controles técnicos de preparación para Ley 21.719;
- bootstrap institucional idempotente y auditable;
- backup, verificación y restauración de PostgreSQL/Object Storage/identidad según los procedimientos del piloto.

CENDOC permanece fuera del alcance de Proyecto Milenio.

## 3. Gates automatizados obligatorios

Un SHA sólo puede ser considerado candidato v1.0.0-rc1 cuando el workflow PMGM CI termina completamente verde sobre ese mismo SHA.

Los cinco jobs críticos son:

1. `Backend build and tests`.
2. `Frontend lint and build`.
3. `Infrastructure configuration`.
4. `First implementation authenticated smoke`.
5. `Pilot operational HTTPS and recovery`.

Dentro de esos jobs se validan además:

- Privacy gate — Ley 21.719;
- Data classification gate;
- Migration safety gate;
- pruebas unitarias e integración PostgreSQL;
- MinIO/S3 privado;
- ClamAV;
- frontend, Nginx y configuraciones first/pilot;
- OIDC, PKCE y separación SPA/Keycloak;
- ausencia de password grant en el cliente piloto;
- backup y recovery drill;
- versión expuesta por `/api/system/info` coherente con el assembly y el manifest;
- `DemoData__Enabled=false` y `VITE_USE_MOCKS=false` en piloto;
- bootstrap sin campos de personas, credenciales ni secretos.

`tests/release_gate.py` falla si alguno de los invariantes de release deja de cumplirse.

## 4. Bootstrap inicial del piloto

El paquete versionado inicial es:

`docs/instalacion/PMGM-PILOT-BOOTSTRAP-V033.json`

Contiene exclusivamente estructura institucional:

- Gran Logia Mixta de Chile;
- Respetable Logia Libertad Nº 23;
- referencia a los catálogos de seguridad y cargos que administra el backend.

No contiene RUT, RUN, correo, teléfono, dirección, fechas personales, usuarios nominales ni credenciales.

Procedimiento operacional:

1. iniciar el stack piloto y verificar health checks;
2. ingresar con la cuenta inicial de Superadmin provisionada fuera del repositorio;
3. abrir **Configuración inicial**;
4. ejecutar **dry-run**;
5. revisar que no existan conflictos de institución, Taller o catálogos;
6. confirmar expresamente la operación;
7. ejecutar `apply`;
8. conservar la evidencia de auditoría y el hash del paquete aplicado.

Reejecutar exactamente el mismo `packageKey/packageVersion` con el mismo contenido no duplica información. Reutilizar la misma clave/versión con contenido diferente debe ser rechazado.

## 5. UAT mínima para promoción a v1.0 estable

Antes de aprobar v1.0 estable se debe validar, como mínimo, un recorrido institucional de extremo a extremo con perfiles representativos:

- ingreso y cierre de sesión OIDC;
- segregación Superadmin / Gran Logia / Taller;
- consulta de ficha de miembro y Taller;
- preservación del grado e historial ante traslado;
- reportería de Régimen Interior;
- detección y corroboración de anomalías de datos;
- regularidad Tesorería/Hospitalaria;
- solicitud, validación y autorización de ceremonia;
- publicación de insinuado y regla de plazo;
- reserva institucional y calendario;
- Gestión Logial;
- carga/versionado/descarga documental;
- Biblioteca Virtual respetando grado/permisos;
- Gran Archivero sólo para perfiles autorizados;
- notificaciones;
- masking `Ocupado` en calendario cuando corresponde;
- bootstrap únicamente para Superadmin.

Los datos de UAT deben ser ficticios o expresamente preparados para prueba. La RC no habilita por defecto el uso de datos reales de miembros.

## 6. Condiciones manuales de promoción

No crear una versión estable `v1.0.0` mientras alguna de estas condiciones esté pendiente:

- CI completamente verde sobre el SHA exacto candidato;
- UAT institucional aprobada por Sponsor/Product Owner;
- hostname definitivo y TLS del piloto validados;
- secretos operacionales creados fuera de Git y rotados respecto de valores de instalación/prueba;
- backup y restauración verificados en la máquina/entorno objetivo;
- bootstrap institucional ejecutado después de dry-run sin conflictos;
- revisión legal/privacidad realizada antes de incorporar datos personales reales;
- responsables operacionales identificados para identidad, base de datos, documentos, backup y privacidad.

## 7. Seguridad y privacidad

La RC mantiene los siguientes principios:

- menor privilegio y separación de funciones;
- `platform_superadmin` no reemplaza los cargos institucionales;
- OIDC Authorization Code Flow + PKCE S256;
- password grant deshabilitado para `pmgm-web` en piloto;
- ningún secreto operacional versionado;
- Object Storage sin acceso anónimo;
- documentos sometidos a política de tipo, integridad y antimalware;
- auditoría de acciones críticas;
- datos especialmente sensibles no habilitados en logs;
- clasificación de datos y controles de retención como línea base de ingeniería para Ley 21.719.

La revisión jurídica final de bases de licitud, plazos de conservación, avisos, contratos con encargados y transferencias internacionales sigue siendo un requisito organizacional; los controles técnicos no sustituyen esa revisión.

## 8. Rollback y recuperación

Ante una falla durante la ventana de validación:

1. detener cambios funcionales y preservar evidencia/logs no sensibles;
2. detener el stack si existe riesgo de corrupción o inconsistencia;
3. identificar el último backup verificado anterior al cambio;
4. ejecutar el procedimiento oficial de restauración del piloto;
5. verificar PostgreSQL, Object Storage, identidad y health checks;
6. repetir smoke operacional;
7. documentar el incidente y su causa antes de reanudar la UAT.

No se debe resolver una regresión borrando historial institucional o sobrescribiendo trazabilidad de miembros.

## 9. Regla de promoción

`v1.0.0-rc1` puede fusionarse a `dev` cuando todos los gates automáticos estén verdes en el mismo head.

`v1.0.0` estable sólo puede declararse después de la aceptación operacional descrita arriba. La existencia de una RC verde demuestra reproducibilidad técnica; no constituye por sí misma aprobación institucional de producción.
