# Handoff — QA operativo, datos sintéticos y reales controlados — #678

Fecha: 23-10-2026. Sponsor solicitó QA **operativo** con datos ficticios y reales para pruebas funcionales y de carga.

## Fuentes y estado
- GitHub base `dev@84ddb69fe2d9b7500fcf2e1021e75d793ee61fcd`; `main@6dfb9546a4873baff15955cf86abfd7d47e3d111` estable. No promover main.
- QA glmdch `https://sistema.granlogiamixta.cl`: última sincronización API+WEB **confirmada** `33c805ec260e7cb80413647cf094067438711a7d` según #525 (no es HEAD actual).
- Ya existen `FirstImplementationSeedService.cs` (dos Talleres demo) y `QaIdentitySeedService.cs` (21 cuentas `qa.*`, 20 miembros de prueba), con guardas staging/HTTPS/BD2.
- Almacenamiento Silo aprobado únicamente para sintético (#470/#479); sigue prohibido introducir PII real en ese volumen.
- Issues vinculados: #97 QA/UAT, #525 despliegue, #470/#471 privacidad/seguridad, #523 RBAC, #533 Asamblea y #669 SMTP.

## Decisión y límites
QA puede utilizar datos ficticios y datos reales **autorizados**, pero los registros reales personales sólo en circuito de protección aislado: base jurídica/finalidad documentada, minimización, autorización responsable, controles de acceso por Taller, cifrado, backups externos, fecha de expiración, borrado y auditoría. Los datos institucionales reales no personales deben señalar fuente y vigencia. No insertar PII en GitHub, Drive, fixtures, Actions, Pages ni logs. No usar copias completas de producción, credenciales institucionales ni correos reales no controlados.

## Entrega incremental de este PR
- `scripts/qa/qa_operational_fixtures.py`: generador determinista sintético JSON, **sin conexión ni escritura de BD**, con datos transversales y banderas explícitas `SYNTHETIC_ONLY` y `NOT_IMPORTED`. Por defecto previsualiza conteos; salida local exige `--output` y archivo nuevo protegido modo 0600.
- `tests/test_qa_operational_fixtures.py`: invariantes de identidad ficticia, idempotencia, FK entre entidades, separación de caja vs imputación de cuotas y no salida a correo.
- Este handoff + documento de QA de segregación, pruebas, y seguimiento GOV004 si se modifican esquemas/datos (este PR no altera tablas).
- **No** toca seed existente, Keycloak, migraciones, frontend ni archivos calientes de otras IA.

## Siguientes etapas aún pendientes
1. Validar generador y gates 3/3 exact HEAD, merge sólo dev si autorizado por GOV-003.
2. Desarrollar importador transaccional idempotente contra entidades y API reales, con `dry-run`, backup y rollback, y ejecución física autorizada en QA. Un JSON local generado **NO** significa datos cargados.
3. Poblar QA-SYNTH de extremo a extremo y verificar UAT, perfiles, tenidas, recaudación, Hospitalaria, candidatos y asambleas.
4. Cargar datos institucionales auténticos no personales usando fuentes oficiales verificadas en Drive.
5. Habilitar circuito separado QA-REAL-CONTROLADO para mínimos datos personales reales **sólo** tras controles #470/#471 y custodia del responsable. No mezclar volúmenes Silo sintéticos con información real.
6. Registrar cada corte de QA y resultado de importación en #678, #97 y Drive sin PII. No hay acceso SSH autónomo al host desde esta sesión.

No se han importado datos a glmdch, no se han alterado volúmenes ni permisos, ni se ha enviado correo real.
