# PMGM-BLG-061 — Cola de corroboración de Régimen Interior

**Prioridad:** P1  
**Versión objetivo:** v0.28  
**Estado:** Implementado en rama / pendiente de CI y merge

## Historia

Como Régimen Interior, necesito convertir una inconsistencia detectada por el control de calidad en un caso asignable y auditable, para poder corroborarla con respaldo sin reescribir automáticamente el historial institucional.

## Entregables v0.28

### Backend

- `RegimenInteriorDbContext` independiente sobre PostgreSQL/core.
- entidades `DataQualityCase` y `DataQualityCaseEvent`;
- migraciones de schema y cola;
- índice único parcial por fingerprint mientras el caso esté activo;
- servicio de apertura, consulta, toma y resolución;
- revalidación del hallazgo contra el motor v0.27 antes de abrir caso;
- actualización condicional para evitar doble toma concurrente;
- resolución condicionada al responsable asignado, salvo override administrativo;
- historial de workflow persistente;
- API `0.28.0`.

### API

Base: `/api/regimen-interior/data-quality/cases`

- `GET /`
- `GET /{caseId}`
- `POST /`
- `POST /{caseId}/claim`
- `POST /{caseId}/resolve`

Filtros de listado:

- estado;
- regla;
- Taller/organización;
- asignados al usuario actual;
- límite de resultados.

### Frontend QA

- acceso `Cola de corroboración` desde navegación institucional;
- apertura desde cada hallazgo vigente de `Calidad de datos`;
- KPIs de abiertos, en revisión, confirmados y descartados;
- filtros por estado/Taller/asignación;
- toma de caso;
- formulario de resolución y respaldo;
- comportamiento responsive;
- mocks ficticios para showcase.

## Privacidad / Ley 21.719

Se reutiliza `PMGM-DPA-002` y se clasifica expresamente la nueva persistencia en `PMGM-DATA-CLASSIFICATION-DATA-QUALITY-CASES.json`.

Controles:

- no se guarda una copia completa del hallazgo ni del miembro;
- referencias relacionales en lugar de duplicar identidad;
- respaldo por referencia, no por contenido;
- `ResolutionSummary` y `EvidenceReference` son restringidos;
- identificadores técnicos de actor permanecen sólo en servidor/persistencia y no se proyectan al frontend;
- no exportación por defecto;
- no datos sensibles en logs;
- `private, no-store`;
- Bearer-only y same-origin.

## Definición de terminado

- [x] modelo y migración PostgreSQL;
- [x] servicio y endpoints;
- [x] deduplicación activa;
- [x] workflow con asignación;
- [x] protección de resolución por revisor;
- [x] proyección minimizada;
- [x] frontend y showcase;
- [x] pruebas cliente;
- [x] prueba PostgreSQL de workflow;
- [x] catálogo de clasificación;
- [x] requisito PMGM-REQ-035;
- [ ] CI completo sobre el head definitivo;
- [ ] merge squash a `dev`.

## Evolución posterior

- SLA/antigüedad de casos y escalamiento;
- notificación al revisor asignado;
- relación explícita entre caso confirmado y un futuro workflow formal de rectificación;
- métricas ejecutivas de calidad sin exponer detalle de casos;
- cierre automático sólo cuando exista una regla formal que detecte desaparición del hallazgo, manteniendo revisión humana para la rectificación.
