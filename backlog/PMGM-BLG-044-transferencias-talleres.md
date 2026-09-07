# PMGM-BLG-044 — Transferencias entre Talleres

**Prioridad:** P1  
**EPIC:** Base maestra institucional / Régimen Interior  
**Estado:** EN PROGRESO

## Alcance
- mantener identidad única del miembro;
- solicitar transferencia entre Talleres;
- validar Taller de origen y Taller receptor;
- conservar pertenencia e historia del Taller de origen;
- crear nueva pertenencia en Taller receptor;
- vincular pertenencia anterior y nueva;
- registrar fecha efectiva, motivo, resolución y documento;
- ejecutar la transferencia de forma transaccional;
- auditar actor y cambios;
- reportar entradas/salidas y trayectoria de Talleres;
- aplicar permisos diferenciados sobre datos históricos locales.

## Implementado en v0.3
- entidad `MemberTransfer` persistente;
- solicitud autenticada de transferencia;
- autorización por alcance de Taller o Gran Logia;
- aprobación por Régimen Interior / administración Gran Logia;
- ejecución transaccional;
- cierre de pertenencia de origen sin eliminarla;
- creación de nueva pertenencia en Taller receptor para el mismo `MemberId`;
- vínculo entre transferencia y pertenencia nueva;
- rechazo de doble pertenencia vigente en el destino para la fecha efectiva;
- registro de hito `workshop_transfer`;
- historial consolidado protegido por alcance institucional.

## Pendiente
- auditoría transversal con actor/correlation ID en tabla dedicada;
- rechazo/cancelación formal de transferencias;
- documentos oficiales asociados a la resolución;
- notificaciones a Taller de origen y receptor;
- pruebas de integración PostgreSQL para rollback y concurrencia;
- reportes específicos de entradas/salidas por Taller.

## Criterios de aceptación
1. cambiar de Taller no crea un segundo Miembro;
2. el Taller de origen conserva todos sus registros históricos;
3. se crea una nueva pertenencia para el mismo Miembro en el Taller receptor;
4. los hitos institucionales y grados siguen siendo únicos y continuos;
5. los datos locales nuevos quedan asociados al Taller receptor;
6. una falla durante la ejecución no deja pertenencias parcialmente actualizadas;
7. puede reconstruirse la trayectoria de Talleres por fecha;
8. el Taller receptor no obtiene acceso automático a documentos internos restringidos del Taller anterior.

## Dependencias
- PMGM-BLG-009 Personas.
- PMGM-BLG-010 Miembros.
- PMGM-BLG-011 Talleres/Logias.
- PMGM-BLG-012 Pertenencias.
- PMGM-BLG-008 Auditoría.
- PMGM-BLG-006 RBAC y contexto organizacional.
