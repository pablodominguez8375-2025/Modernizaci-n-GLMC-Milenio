# Registro de cambio de datos — Issue 275 — materialización y traslado con CRV

## Identificación y alcance

- Issue/PR: #275 / #276; fuente: GOV-003, GOV-004 y regla vigente de #116.
- Autor/responsable: ChatGPT / Sponsor Pablo; fecha: 2026-10-03.
- SHA base vivo de `dev`: `b36f0ff278853b01ce2748663db5c97dfdeac008`; rama: `feature/admissions-residual-275`.
- Registro anterior: `2026-10-02-chatgpt-admissions-residual.md`.
- Incluye: contrato de materialización idempotente de `AdmissionCase` a `Membership`, evento institucional y precondiciones; solicitud de traslado exige retiro voluntario aprobado y firmado.
- Excluye: tarifas, autoridades, firmas normativas distintas, migraciones, `main`, `srv01`, UAT institucional y filas reales.
- Estado: rama de trabajo; SHA exacto se completará en el recibo de cierre.

## Diccionario del alcance actualizado

| Entidad/tabla/contrato | Campo funcional y técnico | Significado/finalidad | Tipo lógico / SQL o JSON | Nullable / default | Valores/validación | Acceso |
|---|---|---|---|---|---|---|
| `AdmissionCase` / `admission_cases` | `status` | Cierra el expediente después de materializar | string / varchar(40) | no / existente | `resolved` sólo tras creación idempotente | Secretaría/Régimen según autorización |
| `AdmissionDecision` / `admission_decisions` | `decisionType` | Registro de materialización | string / varchar(120) | no | `membership_materialized` | auditoría institucional |
| `Membership` / `memberships` | `MembershipType` | Tipo derivado del expediente | string / existente | no | `affiliation` o `incorporation` | módulo de membresía |
| contrato POST `/api/admisiones/expedientes/{caseId}/materializar` | `effectiveDate`, `evidenceReference` | Fecha civil y resolución que autorizan el alta | JSON fecha/string | obligatorios | fecha no futura; expediente elegible; referencia no vacía | Secretaría del Taller |
| contrato POST `/api/members/{memberId}/transfers/` | `withdrawalRequestId` | Vínculo obligatorio con carta de retiro | JSON UUID nullable por compatibilidad de DTO, obligatorio en ejecución | null en DTO legado | retiro voluntario aprobado, firmado por Orador, mismo origen | autoridades de traslado |

## Estructuras y relaciones

| Schema / tabla | PK | FK y destino | Cardinalidad | Unique / índices / checks |
|---|---|---|---|---|
| `core.memberships` | `Id` | `MemberId → members`, `OrganizationId → organizations` | un hermano puede tener historial; una materialización crea una pertenencia activa por fecha/Taller | se reutiliza búsqueda idempotente por hermano/Taller/fecha; no se agrega migración |
| `core.institutional_status_events` | `Id` | `MemberId → members`, `OrganizationId → organizations` | 1 evento por materialización exitosa | evento `active`, evidencia y motivo |
| `core.member_withdrawal_requests` | `Id` | `MemberId`, `OriginOrganizationId` | traslado requiere una solicitud aprobada | `WithdrawalType=voluntary`, estado aprobado y firma Orador |
| `core.admission_decisions` | `Id` | `AdmissionCaseId → admission_cases` | 0..n decisiones por expediente | `DecisionType=membership_materialized` |

## Antes y después

| Elemento | Antes | Después | Motivo / impacto |
|---|---|---|---|
| Materialización | No había operación final de expediente | POST controlado crea pertenencia, evento y decisión; reintento devuelve la misma pertenencia | evita doble alta y conserva auditoría |
| Traslado | podía solicitarse sin referencia verificable de retiro | exige CRV voluntaria aprobada y firmada del Taller de origen | aplica regla normativa declarada por Sponsor |

## Migración, compatibilidad e impacto

- Sin migración ni backfill: sólo contratos y reglas sobre entidades existentes; no se cambia schema físico.
- La materialización es reversible mediante los procedimientos institucionales de retiro; no se borra historia.
- La operación usa evidencia sintética, auditoría antes/después y autorización existente; no cambia tarifas ni autoridades.
- CI/Showcase/QA Installable se ejecutan sobre el SHA exacto; no se instala `srv01`.

## Evidencia y control de salida

| Comprobación | SHA exacto | Resultado | Evidencia |
|---|---|---|---|
| Código ↔ entidades/configuración | pendiente de cierre | pendiente | PR #276 |
| Reglas de CRV e idempotencia | pendiente de cierre | pendiente | pruebas CI |
| CI, Showcase y QA Installable | pendiente de cierre | pendiente | Actions del SHA final |
| GitHub y Línea Base | pendiente de cierre | pendiente | recibo/handoff |

Main permanece congelada; `srv01`, QA física y UAT institucional permanecen pendientes.
