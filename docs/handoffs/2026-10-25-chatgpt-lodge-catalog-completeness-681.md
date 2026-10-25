# #681 — Diagnóstico de columnas completas para GET de Talleres (25-10-2026)

Responsable: ChatGPT. Base `dev@c5f660bb1676d5b5dfd6229b0c73d4e75b0b9dad`. Trabajo acotado al backend, sin acceso SSH, datos personales, despliegue ni cambios `main`.

## Brecha verificable en código
El endpoint administrativo de solo lectura `GET /api/system/support/lodge-data-health` verifica una lista fija de columnas de `core.lodge_meetings` y `core.lodge_instruction_sessions`. Los GET `/api/gestion-logial/talleres/{id}/tenidas` y `.../instrucciones` materializan entidades EF completas, pero el diagnóstico omitía columnas del modelo: `LodgeMeeting.HeldAtUtc` y `LodgeInstructionSession.CreatedBySubject`. Un indicador `ready` podría ser un falso positivo ante una deriva de esquema.

## Corrección acotada
- Añadir las columnas faltantes a las listas de inspección existentes; sin SQL dinámico, consultas a filas, credenciales, cambios de permisos ni migraciones.
- Prueba de regresión estructural: comparar todas las propiedades EF mapeadas de esas dos entidades con las columnas de diagnóstico, para evitar futuras omisiones sin exponer datos.
- Conservar protección `CanConfigureSystem`, flags agregados y `Cache-Control: no-store`.

## Criterios / límites
Ejecutar CI backend y PostgreSQL, Showcase y QA Installable sobre HEAD exacto. No declarar resuelto #681: la causa real del 500 precisa comprobar endpoints/status/correlationId con cuenta sintética en glmdch y metadata real, usando la tarjeta #688 ya integrada por Claude. `main` y host físico no se modifican; respaldo/rollback previo a cualquier despliegue posterior. Issue #685 sigue abierto hasta validación QA. Registro espejo Drive al cerrar.
