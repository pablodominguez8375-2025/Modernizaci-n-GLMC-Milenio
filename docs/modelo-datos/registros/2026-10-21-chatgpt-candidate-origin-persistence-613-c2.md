# GOV-004 — Issue #613, C2: persistencia de origen del candidato

**Fecha:** 21-10-2026. **PR:** #622. **Base:** dev@8dd95641c960d8197bfd74a0e783142d9de13eca.

## Cambios estructurales aditivos
- Tabla existente: `core.ceremony_requests`, contexto `PmgmDbContext`.
- `CandidateOriginKind`: `varchar(24) NULL`. Códigos válidos `brother`, `internet`, `other`, y `NULL` como histórico «Sin registrar».
- `CandidateRecommenderMemberId`: `uuid NULL`, relación FK restrictiva con `core.members.Id`, índice de consulta. No asociar recomendante de otro Taller ni de membresía no activa.
- `CandidateOriginOtherDescription`: `varchar(200) NULL`; descripción breve exclusiva de tipo `other`. Sin nombres de recomendante en listados públicos.
- Restricción SQL `CK_ceremony_candidate_origin_consistency` impide combinaciones cruzadas o falta de información obligatoria por origen. Los datos históricos quedan con NULL en los tres campos.
- Migración reversible `20261021150000_AddCandidateOriginToCeremonyRequests` sin actualización masiva ni borrados de historial.

## Escritura y lectura
- POST `/api/insinuados/taller/solicitudes`: DTO opcional por compatibilidad antigua `originKind`, `recommenderMemberId`, `originOtherDescription`. Cuando algún campo se informa, `CandidateOriginPolicy.ValidateNew` exige uno de los tres orígenes válidos. Para `brother` consulta en servidor membresía activa del mismo Taller y estado institucional vigente a fecha Chile; fuente `core.memberships.Id`, nunca acepta `IsActive` externo.
- El evento de creación y auditoría se almacenan con los datos del expediente en `PmgmDbContext`; la auditoría registra tipo y booleano, no RUT, descripción reservada ni nombre del recomendante.
- GET/PUT autorizado de `/api/insinuados/solicitudes/{id}/ficha` incluye origen únicamente en respuesta privada. El endpoint de bandeja general no expone recomendante ni descripción.
- GET `/api/insinuados/solicitudes/{id}/maestros-entrevistadores` excluye del campo `items` al recomendante persistido e informa su incompatibilidad en `excluded` (únicamente Venerable autenticado). POST de asignación verifica el mismo vínculo del expediente y rechaza con 409 y código `CANDIDATE_RECOMMENDER_CANNOT_INTERVIEW`. No se aceptan ids declarados por el navegador para imponer la exclusión.

## Impacto y límites
- Datos personales: vínculo entre candidato y recomendante en expediente reservado, por lo que aplicar Ley 21.719 y el RBAC existente. No incluir origen en publicaciones ni listas accesibles al resto de miembros.
- Integración progresiva: se preserva ausencia de campos desde formularios UI anteriores (NULL); para cumplir obligatoriedad universal a nuevas solicitudes hay que conectar UI real y planificar activación sin romper clientes existentes. No marcar #613 finalizado.
- Corrección de origen todavía requiere endpoint específico con motivo obligatorio, evento append-only y auditoría de valores anteriores/posteriores; aquí origen solo se guarda en alta y no existe endpoint de corrección.
- PDF privado, QA física/UAT y autorización institucional de despliegue siguen pendientes. Los 3 gates CI/Showcase/Instalable deben aprobar en el HEAD exacto del PR.
- Pruebas nuevas: HTTP/PostgreSQL alta con recomendante activo y persistencia; maestro excluido de GET y POST 409; ID sin membresía acreditada 400 sin expediente.
