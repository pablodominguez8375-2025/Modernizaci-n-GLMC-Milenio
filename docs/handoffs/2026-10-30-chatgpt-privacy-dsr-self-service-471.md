# Handoff #471 — autoservicio de derechos del titular (fase A)

## Referencias y estado

- Issue: #471, `agente:chatgpt`; contrato de API publicado en issue [comentario 6513153490](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/471#issuecomment-6513153490) **antes de implementar**.
- PR: [#804](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/pull/804) (DRAFT, NO MERGED).
- Rama: `feature/privacy-self-service-20261030-gpt`, creada sobre `dev@ceaca327cacb983850a66a9dbdb41a8d4a3c4316`.
- `main` histórico observado `6dfb9546a4873baff15955cf86abfd7d47e3d111`; no tocado.
- Revisados GitHub/Drive vigentes: #525 QA operativo tiene migración #801 EF99, G6 pre/post restaurados; PR #803 correctivo catalog PostgreSQL17 ya integrado en dev pero nueva release todavía en publicación. **No combinar el nuevo cambio de privacidad con el corte QA en curso.**

## Cambios de código reales

1. `backend/src/PMGM.Api/Modules/Privacy/PrivacyEndpoints.cs`: crea `GET /api/privacy/data-subject-requests/me` y `POST /api/privacy/data-subject-requests/me` bajo autenticación preexistente. `MemberId` se resuelve en servidor por `IInstitutionalMemberContextResolver`, con vínculo issuer/sub activo; `PersonId` se obtiene de `core.members`. Si no hay vínculo institucional/grado o PersonId, 404.
2. `GET`: filtro SQL por PersonId exclusivo, 100 últimas, orden por fecha e id, devuelve solo id, requestType, status, receivedAtUtc, dueDate, closedAtUtc. Nunca nombres, RUT, responsable, fundamento, evidencia, resolución o expediente ajeno; sin cache privada.
3. `POST`: DTO `CreateOwnDataSubjectRequest` acepta solamente requestType existente en `PrivacyCodes.DataSubjectRight`; usuario no puede enviar identidad, verificación legal, fecha, estado ni término. `receivedAtUtc` del servidor, `status=under_review`, `identityVerified=false` hasta comprobación formal, `channel=authenticated_portal`, `dueDate` desde regla jurídica versionada. Si no existe regla, el expediente se registra sin inventar vencimiento y queda pendiente de gestión jurídica (dueDate nullable).
4. Auditoría institucional de lectura y creación: sin PersonId, correo, RUT, notas ni evidencia; contador y tipo de derecho, regla de plazo (si está disponible) exclusivamente.
5. `backend/tests/PMGM.Api.Tests/Integration/PrivacyRequestReadAuditPostgreSqlTests.cs`: amplía la clase existente que ya está asignada a **shard b** en `.github/backend-integration-shards.json`; prueba PostgreSQL de identidad issuer/sub, acceso titular vs terceros, campos suplantados por cliente, ausencia de datos sensibles, autorización tras revocar vínculo y error 400 para derecho desconocido. Sin editar workflow/manifest calientes.

## Gates / límites

- Test backend: sujeto a CI exact-HEAD de PR #804; este handoff no afirma pruebas ejecutadas localmente (no hay SDK .NET en este entorno).
- CI, Showcase, instalable: pendientes de lectura final exact-head.
- Migraciones: **ninguna**. UI de Claude: no modificada. GitHub Pages y release QA: no modificados por PR DRAFT.
- Limitaciones expresas: no exportación/descarga ARCO de datos personales ni generación automática de respuestas, retención, decisiones jurídicas o validación humana de identidad. Es una **fase A**, no certificación Ley 21.719.
- No fusionar mientras la release y el QA físico del corte PR #803 estén pendientes de verificación; hacerlo solo después de gates exact-head y condiciones de coordinación GOV-003. Excluidos `main`, producción, `srv01`, SQL de QA, PR #771/#737/#766, datos reales.

## Siguiente IA / chat

Verificar HEAD real de PR #804 y `dev`, consultar las tres acciones CI/Showcase/QA. Reparar únicamente fallos observados. Si exitoso, dejar PR listo y **coordinar integración fuera de la ventana de corte físico QA #525**. La UI `Mis derechos` y exportación ARCO tienen entrega/decisión posteriores separadas. Registrar resultado exact-head en issue #471 y adenda Drive al cerrar la etapa.
