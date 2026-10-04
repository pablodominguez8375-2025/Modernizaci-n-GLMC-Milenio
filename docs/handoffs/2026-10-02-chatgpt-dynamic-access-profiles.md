# Handoff — perfiles dinámicos, menús, vistas y permisos

Fecha: 2026-10-02  
Issue: #266  
PR: #267 (draft)

## Reserva GOV-003

La rama parte del HEAD vivo de dev `5ed67978a86918f10e8cb9b764e26cecdaafeacf`. Se reservan Authorization, componentes de perfiles/asignaciones/revisión y documentación de datos/seguridad. No se tocan App, action-kit, tema, showcase, workflows ni las reservas de Claude.

## Requisito

Habilitar perfiles custom con menú/vista dinámicos y acciones explícitas: ver, crear/hacer, escribir/registrar, editar, borrar lógico e imprimir. Deny-by-default, backend como autoridad final, auditoría completa, sin atribuciones institucionales nuevas.

## Avance técnico

Se añadió `/api/system/access` sobre el almacén versionado existente: catálogo, CRUD de perfiles, grants, asignación/revocación y evaluación deny-by-default; se agregaron auditorías, la acción `print` y un botón de impresión controlada en revisión de accesos. No hay migración SQL: el diccionario/antes-después/impacto está en `docs/modelo-datos/PMGM-DB-005-control-acceso-dinamico.md` y la política en `docs/seguridad/PMGM-SEC-004-control-acceso-dinamico.md`.

## Pendiente técnico

Agregar pruebas backend/frontend de contrato y ejecutar CI/Showcase/QA Installable sobre el SHA exacto. Mantener srv01/UAT física pausados y main congelada.

