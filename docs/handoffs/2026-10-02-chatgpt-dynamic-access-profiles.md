# Handoff — perfiles dinámicos, menús, vistas y permisos

Fecha: 2026-10-02
Issue: #266
PR: draft pendiente de crear después de este commit

## Reserva GOV-003
La rama parte del HEAD vivo de dev `5ed67978a86918f10e8cb9b764e26cecdaafeacf`. Se reservan Authorization, migración DynamicAccess, componentes de perfiles/asignaciones/revisión y documentación de datos/seguridad. No se tocan App, action-kit, tema, showcase, workflows ni las reservas de Claude.

## Requisito
Habilitar perfiles custom con menú/vista dinámicos y acciones explícitas: ver, crear/hacer, escribir/registrar, editar, borrar lógico e imprimir. Deny-by-default, backend como autoridad final, auditoría completa, sin atribuciones institucionales nuevas.

## Pendiente técnico
Implementar contrato persistente, migración, endpoints, pruebas y UI/demo sobre los componentes existentes; documentar diccionario antes/después, impacto y ausencia/presencia de migración según GOV-004. Mantener srv01/UAT física pausados y main congelada.
