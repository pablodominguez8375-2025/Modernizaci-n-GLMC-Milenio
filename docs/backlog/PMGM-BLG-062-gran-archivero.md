# PMGM-BLG-062 — Gran Archivero

## Estado
Implementado para v0.29, pendiente validación CI y merge.

## Entregables
- entidad y migración `GrandArchiveRecord`;
- rol `grand_archivist` y capability de sesión;
- catálogo, filtros, candidatos elegibles, incorporación, retiro y contenido autenticado;
- vínculo a `InstitutionalDocument`/`DocumentVersion` sin duplicar S3;
- exclusión de planchas y documentos de Taller;
- UI responsive y mocks de showcase;
- prueba PostgreSQL de reglas archivísticas;
- PMGM-DPA-012 y clasificación de datos específica;
- QA v0.29.

## Criterio de cierre
CI completo verde sobre el SHA final, PR listo y squash merge a `dev`.
