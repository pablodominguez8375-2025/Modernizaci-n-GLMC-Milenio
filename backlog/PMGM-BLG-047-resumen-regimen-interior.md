# PMGM-BLG-047 — Resumen operativo de Régimen Interior

**Prioridad:** P1  
**EPIC:** Secretaría y Régimen Interior  
**Estado:** EN PROGRESO

## Objetivo
Entregar la primera consulta operativa real de Régimen Interior sobre la base maestra, con fecha de corte y alcance por toda la Orden o por Taller, consumiendo la regularidad financiera desde Gran Tesorería sin duplicar la contabilidad.

## Implementado
Endpoint autenticado:

`GET /api/regimen-interior/summary`

Parámetros:
- `organizationId`: opcional; sin valor representa toda la Orden;
- `asOf`: fecha de corte opcional;
- `from`: inicio de período opcional para contar eventos.

El resultado incluye:
- miembros históricamente relacionados con el alcance;
- miembros actualmente afiliados al alcance;
- activos;
- inactivos;
- miembros con estado impeditivo vigente;
- retiros voluntarios del período;
- retiros forzosos del período;
- reintegros del período;
- defunciones del período;
- transferencias del período;
- distribución por último grado registrado;
- transferencias pendientes/aprobadas aún no ejecutadas;
- regularidad financiera proveniente de Gran Tesorería: al día, moroso, pendiente, exento y sin estado;
- total distinto de hermanos morosos en el alcance consultado.

## Seguridad
Requiere:
- autenticación válida;
- `pmgm_scope=order`;
- rol `internal_affairs` o `grand_lodge_admin`.

## Fuente financiera
- Régimen Interior no crea ni modifica saldos o movimientos contables.
- La regularidad se obtiene desde las fotografías históricas emitidas por Gran Tesorería.
- El reporte toma la validación más reciente aplicable a la fecha de corte por hermano/Taller.

## Pendiente
- reporte detallado navegable desde cada indicador;
- Hermanos Past activos según clasificación institucional;
- inconsistencias históricas;
- fechas críticas consolidadas por hermano;
- tendencias mensuales/anuales;
- exportación PDF/XLSX/CSV;
- pruebas de integración y rendimiento con volumen representativo.

## Criterio de aceptación parcial
La consulta se considera primera versión operativa cuando compila y pasa pruebas en CI, respeta el RBAC definido y obtiene los indicadores exclusivamente desde la base maestra e historiales de las áreas responsables, sin planillas paralelas.
