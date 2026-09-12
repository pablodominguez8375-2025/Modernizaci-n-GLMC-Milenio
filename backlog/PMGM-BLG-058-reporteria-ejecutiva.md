# PMGM-BLG-058 — Reportería Ejecutiva comparativa

**Prioridad:** P1  
**Estado:** Implementado en rama / pendiente de CI y merge  
**Versión objetivo:** v0.25  
**Área responsable:** Régimen Interior / Administración central

## Objetivo

Entregar una vista ejecutiva de la Orden y sus Talleres a partir de las fuentes institucionales existentes, sin crear una base paralela ni duplicar datos maestros.

## Alcance v0.25

- corte configurable (`asOf`) y período (`from` → `asOf`);
- total de Talleres y miembros vigentes de la Orden;
- activos, inactivos y estados bloqueantes;
- retiros voluntarios y forzosos, reintegros y defunciones;
- traslados ejecutados y pendientes;
- distribución por grado actual;
- morosidad de miembros y estado del Taller en Gran Tesorería;
- estado mínimo de Gran Hospitalaria;
- Tenidas y sesiones de docencia realizadas en el período;
- comparativo por Taller con banderas de atención;
- búsqueda y filtro de Talleres con alertas en la UI;
- acceso limitado a `CanRunRegimenInteriorReports`;
- `Cache-Control: private, no-store`;
- sin correo, teléfono, dirección ni documentos personales en la proyección.

## Past Active — definición operativa

Mientras no exista una categoría estatutaria/código específico en el modelo, v0.25 define **Past Active** como:

> Miembro actualmente afiliado que registra al menos un cargo institucional finalizado antes de la fecha de corte.

La definición se devuelve explícitamente en la API para evitar interpretaciones silenciosas y podrá reemplazarse por una regla institucional formal sin cambiar el contrato general del reporte.

## Portabilidad entre Talleres

El grado es atributo histórico del hermano y viaja con él. El reporte usa el último evento de grado del miembro sin restringirlo al Taller donde ese evento ocurrió. Se corrige también la Ficha de Taller para aplicar la misma regla.

El historial de cargos y afiliaciones permanece asociado a las organizaciones donde ocurrió, sin reescribir el pasado.

## Ley 21.719

La reportería reutiliza los tratamientos ya registrados para Core/Membresía y Régimen Interior. No introduce un tratamiento nuevo: genera una proyección agregada y minimizada para una finalidad institucional ya definida.

Guardrails:

- RBAC de Orden para Régimen Interior/administración autorizada;
- no PII de contacto;
- no notas, evidencias ni razones individuales;
- no contenido documental;
- salida no cacheable por intermediarios;
- indicadores agregados y estados mínimos.

## Criterios de aceptación

1. Un usuario sin permiso de reportes recibe 403.
2. El grado de un miembro trasladado sigue contabilizándose en su Taller vigente.
3. El historial de cargos permite identificar Past Active sin mover el cargo al Taller nuevo.
4. La morosidad se obtiene de Gran Tesorería, no de una copia local de reportería.
5. Hospitalaria se proyecta como estado mínimo del Taller.
6. Se muestran totales Orden y comparativo por Taller.
7. El frontend usa Bearer token, `credentials: omit`, `cache: no-store` y mismo origen.
8. El showcase funciona con datos ficticios sin llamadas de red.
9. Backend, frontend, PostgreSQL, infraestructura y gates de Ley 21.719 deben quedar verdes antes del merge.

## Posterior a v0.25

- exportación PDF/XLSX con autorización y marca de clasificación;
- tendencias por mes/período;
- comparación contra período anterior;
- indicadores configurables por Régimen Interior;
- drill-down autorizado desde métricas agregadas a listados de gestión;
- definición estatutaria de Past Active si la institución entrega código/regla formal.
