# PMGM-BLG-044 — Transferencias entre Talleres

**Prioridad:** P1  
**EPIC:** Base maestra institucional / Régimen Interior

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