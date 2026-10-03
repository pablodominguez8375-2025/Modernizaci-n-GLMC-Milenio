# GOV-004 — Issue #275 / PR #276 — recibo atómico y controles de procedimiento

Registro de salida del cambio de datos y contratos. Continúa [el registro anterior](2026-10-03-issue-275-materializacion-traslado.md), que documentaba una idempotencia por hermano/Taller/fecha y no prueba la atomicidad corregida en este corte.

Base dev revisada: `6083248ff8c8abade4e9fb79676439c1eca8ebf2`. Rama: `feature/admissions-residual-275`. Responsable: ChatGPT; Sponsor: Pablo. El SHA de salida es el commit que contiene este registro; los recibos externos identifican el SHA exacto y los runs asociados.

## Estructuras y relaciones del alcance

| Estructura | PK y relaciones | Propiedades afectadas / contrato | Control |
|---|---|---|---|
| `core.admission_cases` | PK `Id`; relaciones lógicas `PersonId`, `MemberId`, `OrganizationId` | `MemberId` y `Status`: asignación y `resolved` únicamente al confirmar toda la transacción | Incorporación crea un Member para la Person existente; afiliación conserva MemberId/PersonId |
| `core.members` | PK `Id`; FK `PersonId → people` | `PersonId`, `CurrentDegree`; `InstitutionalNumber` queda sin inventar | Rechaza identidad ya institucionalizada al incorporar |
| `core.memberships` | PK `Id`; FK `MemberId → members`, `OrganizationId → organizations` | `Id`, `MemberId`, `OrganizationId`, `MembershipType=regular`, `StartDate`, `Status=active`, `EvidenceReference` | Una pertenencia por recibo de expediente; no reutiliza coincidencia de fecha como evidencia |
| `core.institutional_status_events` | PK `Id`; FK Member/Organization | `EventType`, `EffectiveDate`, `EvidenceReference`, `Reason` | Un evento active por materialización y un hito de traslado por enlace ejecutado |
| `core.admission_decisions` | PK `Id`; FK `AdmissionCaseId → admission_cases`, cardinalidad 1:n | `DecisionType`, `Status`, `AsOfDate`, `SourceReference`, `Notes`, `RecordedBySubject`, `RecordedAtUtc` | `membership_materialized.Notes` contiene JSON `{MembershipId,MemberId,EffectiveDate,EvidenceReference}`. Reintento exige fecha y referencia iguales; recibo legado no verificable se observa, no se reconstruye |
| `core.ceremony_requests` | PK `Id`; vínculo lógico AdmissionCaseId | `MemberId`, `Status=completed` al confirmar | Exige autorización del mismo expediente/Taller/tipo/fecha |
| `core.secretariat_documents` | PK `Id`; vínculo `RelatedCeremonyRequestId` | Lectura de `DocumentType`, `PlanchaKind`, `Status`, `OrganizationId` | Plancha de autorización emitida; conserva compatibilidad de tipos legado explícitos |
| `core.lodge_secretariat_records` | PK `Id`; índice único RecordType/SourceRecordId | Lee `EventDate`, `CeremonyAuthorizationDocumentId`, `FullMinuteDocumentVersionId`, `ExtractDocumentVersionId` | Vincula acta, extracto y autorización a la Tenida del mismo Taller/fecha |
| `core.lodge_meetings` | PK `Id` | `CeremonyType` admite affiliation/incorporation; lectura `Status=closed` | La materialización no cierra Tenidas ni fabrica documentos |
| `core.member_transfers` | PK `Id`; FK SourceMembershipId/TargetMembershipId | `TargetMembershipId`, `Status`, `ExecutedAtUtc`, EvidenceReference | Enlaza el destino ya materializado por afiliación resuelta. No crea segunda pertenencia ni cambia EndDate/EndReason del origen |
| `core.member_withdrawal_requests` | PK `Id`; FK Member/OriginOrganization | Lectura WithdrawalType/Status/RequestedEffectiveDate/OratorSignatureSubject/OratorSignedAtUtc/EvidenceReference | CRV voluntaria aprobada y firmada, mismo origen/hermano/cierre y fecha destino posterior |

Los vínculos descritos como lógicos no afirman una FK física adicional. Este registro cubre propiedades afectadas por el incremento, no el diccionario completo del proyecto. No se incorpora migración, backfill ni datos reales.

## Antes / después

- Antes: dos guardados independientes podían dejar pertenencia sin cierre de expediente. Después: ambos contextos comparten conexión y una transacción SERIALIZABLE; pertenencia, alta externa, hito, ceremonia, decisión y auditoría confirman o revierten juntas.
- Antes: la fecha se usaba para inferir un reintento. Después: recibo por expediente con IDs y carga exacta; otra fecha/referencia devuelve conflicto.
- Antes: incorporación no materializaba una identidad externa. Después: crea Member a partir de Person/grado acreditados, sin inventar número institucional ni fecha histórica de iniciación.
- Antes: traslado rechazaba el origen ya cerrado por CRV y podía crear destino sin afiliación. Después: preserva ese cierre y exige/enlaza el destino materializado normativamente.
- Antes: revisión art. 2.3, comisión y cronología estaban almacenadas pero no condicionaban la autorización. Después: proyector común consumido por elegibilidad, habilitación, solicitud, guard de autorización y materialización. Añade lectura de primer grado previa, balotaje en fecha posterior y comisión vigente para activación/incorporación.
- Antes: CreatedAt disparaba re-presentación aun sin rechazo previo. Después: sólo rechazo previo/remediación activa esa regla.
- Antes: pantalla fabricaba decisión/actor local tras materializar. Después: relee expediente desde API; capacidades institucionales gobiernan las acciones del panel.

## Evidencia de revisión

Pruebas añadidas: re-presentación, revisión/indulto, lectura/cronología, comisión renombrada, recibo igual/diferente, políticas de retiro, HTTP PostgreSQL para cierre de Tenida, rollback tras guardar core, alta externa y replay. Frontend: transporte de recibo y discrepancias de replay sintético. Resultados exactos y límites se registran en recibo GitHub/Drive después de Actions; no reutilizar checksums de ec08044.

Main congelada; srv01, QA física y UAT institucional no ejecutadas. #116 conserva su restricción histórica.
