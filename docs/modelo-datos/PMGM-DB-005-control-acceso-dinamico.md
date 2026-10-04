# PMGM-DB-005 — Control de acceso dinámico

Issue #266 / PR #267. Base de continuidad: dev `3addd763d8bddf58eaa4328ec392ec1828ac9246`.

## Antes / después

La implementación inicial pretendía guardar el catálogo en `institutional_rule_settings.Value` (varchar(1000)); el catálogo completo no cabe y el reemplazo del snapshot diario permite perder cambios concurrentes. Se sustituye esa propuesta de rama, aún no integrada, por `core.dynamic_access_snapshots`, JSONB y versiones inmutables desde la aplicación. No se reinterpretan ni migran settings históricos de perfiles, cargos, cuotas o publicaciones.

## Diccionario físico

| Campo | Tipo / nulabilidad | Default y significado | Restricción / clasificación |
|---|---|---|---|
| Id | uuid no nulo | Guid nuevo; PK del snapshot | Metadato interno |
| Version | integer no nulo | Anterior + 1; catálogo inicial virtual 0 | Índice único; metadato interno |
| Payload | jsonb no nulo | Catálogo completo de la versión | Restringido: sujetos OIDC y permisos técnicos |
| RecordedAtUtc | timestamptz no nulo | UTC generado en servidor | Metadato de auditoría |

No hay FKs nuevas ni cambios en tablas de miembros, cargos o Talleres. El JSON referencia Organizations.Id: la API exige un Taller existente al asignar un perfil de alcance lodge. AuditEvents.EntityId identifica la versión; conserva sujeto del administrador/correlación y número de versión anterior, sin volcar el catálogo en auditoría.

## Diccionario lógico / contrato JSON

| Estructura | Campos y tipos | Validación |
|---|---|---|
| Catalog | version:int, actions:string[], menus:Menu[], profiles:Profile[], assignments:Assignment[] | Estado leído de snapshot más reciente; error de deserialización nunca reinicia permisos |
| Menu | code/name:string, isActive:bool, views:View[] | Catálogo de vistas conocidas del producto; no rutas arbitrarias |
| View | code/name:string, isActive:bool | Código estable canónico minúsculo; pertenece a un menú |
| Profile | id:uuid, code/name/scope:string, isSystem/isActive:bool, menuCodes:string[], grants:Grant[] | Código `[a-z][a-z0-9-]{0,79}`, nombre hasta 240, order/lodge; sistema protegido; baja lógica |
| Grant | viewCode:string, actions:string[] | Vista activa de un menú seleccionado; sin duplicados; view/create/write/edit/delete/print; mutación o impresión requieren view |
| Assignment | id:uuid, subject/profileCode:string, organizationId:uuid?, effectiveFrom:date, effectiveTo:date?, isActive:bool | Sujeto OIDC exacto hasta 320; Taller obligatorio para lodge, nulo para order; fin >= inicio; no superposición vigente/programada |
| Mutation request | expectedVersion:int más campos de la operación | Bloqueo transaccional común y compare-and-swap; conflicto 409 antes de mutar o auditar si la versión cambió |

## Migración / transacción

`20261004031137_AddDynamicAccessSnapshots` crea exclusivamente la tabla JSONB y su índice de versión único. Sigue el patrón de migraciones explícitas del proyecto; no incorpora snapshots EF globales ni recrea tablas existentes. Up no borra datos; Down elimina sólo esta tabla y requiere respaldo de las versiones si se usa en operación. Despliegue QA pendiente.

Cada mutación adquiere `pg_advisory_xact_lock(26620261004)` bajo transacción PostgreSQL antes de leer versión; valida expectedVersion y persiste nuevo snapshot + audit en un único commit. El reemplazo de grants es atómico. Metadatos del perfil y grants se guardan en dos peticiones explícitas de la UI; una creación interrumpida queda sin grants y, por tanto, denegada. No se afirma atomicidad conjunta de esas dos peticiones.

## Evaluación / límites

La evaluación exige sujeto exacto, organización exacta, vigencia inclusiva según fecha civil de Chile, perfil/menú/vista activos y grant de view más la acción solicitada. Requiere también el ámbito institucional vigente del solicitante. Los perfiles técnicos no otorgan cargos ni sustituyen la autorización de los endpoints de dominio. La ausencia de grant devuelve false en el evaluador. No se atribuye al catálogo una promoción automática de roles OIDC o capacidades institucionales.

La UI administrativa deja de guardar listas independientes en settings y consume `/api/system/access`; conserva separación entre cargos base y perfiles técnicos. API y adaptador demo comparten tipos/acciones/validaciones. La impresión de revisión usa POST protegido y registra solicitud en servidor antes de imprimir; exportación CSV administrativa continúa separada.

Pruebas: scope/fecha/sujeto/acciones, baja y revocación, perfiles protegidos, stale writes concurrentes, persistencia y auditoría PostgreSQL, Tesorero/anon sin administración, transporte con token y demo. Evidencia exact-head y postmerge se registrará en recibos del PR y Línea Base. Main/srv01/UAT no incluidos.
