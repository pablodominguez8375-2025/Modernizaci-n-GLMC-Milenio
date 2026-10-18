# Handoff inicial — recibos de derecho de ceremonia #540 — 18-10-2026

- Fuente: Issue #540 de Claude; coordinación PMGM-GOV-003. Dueño: ChatGPT (backend).
- Punto de partida `dev@29bf6688e53b9cd45bce3adb14ea620ac1937517`, `main@6dfb9546a4873baff15955cf86abfd7d47e3d111`, QA LAB2 instalado en SHA anterior: no tocar.
- Objetivo: ampliar `MemberOwnReceiptEndpoints` con descarga exclusivamente propia y listado por `MemberId` obtenido de identidad servidor. No incluir referencias de pago ni el registrador en DTO. Auditar la descarga.
- Inicio: los expedientes de iniciación carecen de `MemberId` hasta completarse; nunca divulgar recibos a un hermano por coincidencia de candidato/identidad sin vínculo institucional inequívoco. Indicar esta limitación al PO.
- Pruebas: autenticado titular, tercero autenticado, sin vínculo institucional; detalles y listados; no cachear.
- Revisión, CI exact-head, demo/UI posterior Claude, instalable por SHA y despliegue físico pendientes. No fusionar sin triple gate y revisión dev vivo.

## Entrega backend realizada, pendiente gates y UI
- PR DRAFT #550: https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/pull/550
- `MemberOwnReceiptEndpoints.cs`: GET `/api/membership/me/comprobantes/ceremonia` (listado de pagos propios), GET `/api/membership/me/comprobantes/ceremonia/{id}` (mismo `OwnReceiptView` del PDF vigente). Ambos usan `IInstitutionalMemberContextResolver` y filtran `CeremonyRequest.MemberId == own.MemberId` en PostgreSQL; 404 si ajeno o miembro inexistente. Cache-Control privado no-store. DTO no incluye referencia de transferencia, RUT, datos de terceros ni RecordedBySubject. Se audita la descarga de comprobante.
- Iniciación: el candidato sin `CeremonyRequest.MemberId` no puede acceder al comprobante desde Mi ficha, incluso si comparte `CandidatePersonId`; solo visible tras vinculación explícita del expediente al miembro.
- Nueva prueba `MemberOwnCeremonyReceiptPostgreSqlTests`: objeto del propio hermano, Taller ajeno 404, candidato todavía no vinculado 404, pérdida de identidad activa 404, auditoría y ausencia de datos internos.
- CI `.github/backend-integration-shards.json`: prueba incorporada en shard b. ARCHIVO COMPARTIDO con PR #512 (aviso Agenda): resolver diferencia antes de fusionar, no sobreescribir su caso `CalendarNotificationRecipientPostgreSqlTests`.
- No se tocó UI de Claude, `main`, QA LAB2 ni `srv01`; integración y QA físico/UAT permanecen pendientes. `dotnet` no está disponible en la sesión actual: solo resultados GitHub Actions sirven como pruebas efectivas.
