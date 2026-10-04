# PMGM-DB-006 — Acceso efectivo a Tesorería del Taller

Issue #266 / PR #316. Base dev `e5697003dcd7749269a586518ccb35ad1ca50d75`. Continúa DB-005; sin migración: reutiliza snapshots JSONB y auditoría existentes. Modelo implementado en rama; instalación QA pendiente.

## Contrato derivado y privacidad

`GET /api/session/treasury-access?organizationId=<UUID>` autenticado, `private, no-store`; sólo en ámbito institucional autorizado. No acepta un sujeto ajeno, no devuelve catálogo, nombres de perfiles ni asignaciones.

| Campo | Tipo / nulabilidad | Regla |
|---|---|---|
| version | entero, no nulo | Versión del snapshot evaluado; 0 si nunca hubo snapshot |
| organizationId | UUID, no nulo | Taller solicitado y permitido institucionalmente |
| managed | booleano, no nulo | Existe una asignación histórica del sujeto exacto para ese Taller, incluso revocada |
| actions | array de códigos, no nulo | Subconjunto view/create/write/edit/delete/print; requiere view, vigencia chilena y autorización institucional |

Clasificación: permisos privados del usuario autenticado, sin datos personales de terceros. Datos calculados, no tabla nueva ni PK/FK nueva; relaciones de DB-005 se mantienen. Subject = sub o NameIdentifier, sin inferirlo del correo. OrganizationId de operación por ID de recibo/cargo/egreso proviene de la entidad persistida, no de un query alternativo.

## Cambio antes/después

Antes: gestión del catálogo y evaluación aislada; las operaciones de Tesorería sólo comprobaban el rol institucional. Ahora: se conserva ese control y se añade una restricción técnica por sujeto/Taller/acción. Sin asignación histórica en el Taller se conserva el acceso institucional actual, para no retirar cargos existentes por instalar la actualización. Desde la primera asignación, falta de grant/menú/view, vencimiento, programación futura, revocación o perfil inactivo deniegan; no vuelven automáticamente al acceso institucional anterior. Recuperación: administrador autorizado crea/reasigna un perfil técnico con las acciones correspondientes; se conserva el histórico.

Los perfiles de Orden con OrganizationId nulo no son un wildcard de Taller. Las asignaciones de otro Taller/sujeto no participan. Gran Tesorería mantiene su lectura/conciliación institucional separada del acceso local; ningún grant concede cargo, aprobación, firma o acceso a un Taller ajeno.

| Acción | Operaciones locales protegidas |
|---|---|
| view | Planes, cargos, resumen, caja, reportes, configuración, cierres, recibos, egresos, cartola; Cuadro mensual local |
| create | Plan de cuota, generación de cargos, creación/generación de Cuadro |
| write | Pagos/recibos/imputaciones, ingresos/egresos, cuadratura, cierre anual, líneas/pagos/envío del Cuadro; autorización de egreso sólo si ya tiene atribución de Venerable |
| edit | Parámetros contables, corrección append-only de recibo |
| delete | Anulación append-only de recibo erróneo; jamás borrado físico |
| print | POST autorizado/auditado antes de imprimir la vista; no es exportación CSV ni firma |

UI y demo: consulta por Taller, formularios ausentes para acciones denegadas, mismo contrato y evaluación en datos ficticios. Demo identifica sujetos como `demo:lodgeTreasurer`, etc. Refresco al cambiar sujeto/Taller, al modificar catálogo local, al recuperar foco y cada 30 segundos. Backend reevalúa cada solicitud; no depende del caché de interfaz. El navegador puede imprimir por sus controles propios: este permiso regula la acción de impresión de la aplicación.

## Validación y alcance

Pruebas: política de acceso legado/enrolado, vigencia/revocación, contrato minimizado, API PostgreSQL con rechazo de escritura/cambio/creación e impresión auditada, IDs de recibos y aislamiento de Taller, segregación de aprobación; adaptador demo y formularios de consulta. Los resultados exactos se registran en recibos de #316 y Línea Base.

Sin cambio de tarifas, contabilidad, firmas, cargos, retención, esquema ni registros históricos. No implementa la proyección global de otros módulos de #266; no declarar esa Issue cerrada. Main congelada, srv01/UAT física pausados.
