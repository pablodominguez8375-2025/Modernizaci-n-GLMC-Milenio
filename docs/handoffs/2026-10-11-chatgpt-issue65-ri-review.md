# Proyecto Centenario — Issue #65: revisión de reinsinuación por Régimen Interior

- Corte: 11-10-2026, zona America/Santiago.
- PR [#448](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/pull/448), rama `fix/issue65-ri-representation-decision-20261011-gpt`, iniciada en `dev@f6009a057c625f048a97edff7b9546f9ca3e9438`.
- `main@6dfb9546a4873baff15955cf86abfd7d47e3d111` **no autorizada para promoción**; `srv01` sin despliegue (#97).
- Vinculado a PRs [#444](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/pull/444) (balotaje) y [#447](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/pull/447) (plazo anual y referencia de subsanación).
- Fuentes: `docs/PMGM-REQ-001-flujos-institucionales-2026.md`, `docs/PMGM-GOV-003-coordinacion-multi-ia.md`, Drive Línea Base Maestra y Estado ejecutivo. Verificar el corte de Drive al continuar.

## Defecto
Una reinsinuación registrada conforme al PR #447 genera `candidate_representation_review=observed` con acta del Taller. Pero un visto bueno genérico `internal_affairs=approved` podía permitir que una Iniciación se considerase habilitada en la matriz sin una decisión explícita sobre esa subsanación. Faltaba interfaz de Régimen Interior y auditoría de resolución.

## Alcance implementado (pendiente del cierre de gates)

1. `CandidateRePresentationReviewEndpoints.cs`: nuevas rutas privadas bajo `/api/insinuados/regimen-interior/reinsinuaciones`:
   - `GET /`: lista únicamente a perfiles con ámbito de Orden y rol `RegimenInterior`/`GranLogiaAdmin`, conserva referencia **original** de Secretaría separada de la resolución última.
   - `POST /{requestId}/resolver`: sólo Iniciación con constancia inicial observada, expediente abierto, resolución explícita `approved`/`observed`/`rejected`, referencia 1–240 y notas <=1000, no acepta `exception_approved` como bypass.
   - Separación de funciones: actor que creó el expediente, leído desde `AuditEvent`, **no puede resolver su propia solicitud**, aun si posee rol de Orden. Actor desconocido se deniega.
   - Escritura **append-only** de `CeremonyValidation`, con acción de auditoría independiente. Reenvío idéntico es idempotente. La constancia inicial no se sobrescribe.
2. `CandidateRePresentationReviewPolicy.cs` y `CeremonyEligibilityPolicy.cs`: requisito adicional **sólo cuando existe** `candidate_representation_review`; último `approved` habilita; `observed`, `rejected`, `pending` o excepción no habilitan; no afecta candidatos de primera presentación.
3. Proyecciones alineadas: `CeremonyEligibilityService`, `CeremonyEndpoints.BuildEligibilityContextAsync` (GET elegibilidad y POST autorizar), `GrandMasterCeremonyEndpoints` y `CeremonyReviewQueueEndpoints`. La autorización de ceremonia exige **ambos** vistos buenos: Régimen Interior genérico y validación específica de subsanación cuando aplica. Mantener pruebas de balotaje previas.
4. Vista existente `RegimenInteriorPage.tsx`: bandeja, estado y referencia original, acta de resolución y acciones aprobar/observar/rechazar. API real/demostración sintética en `frontend/src/api/pmgmApi.ts`; tests en `RegimenInteriorRePresentation.test.tsx`.
5. Pruebas `CandidateRePresentationReviewPolicyTests.cs`: elegibilidad fail-closed y sin requisito para candidatos iniciales; `CandidateRePresentationReviewHttpTests.cs`: roles, ámbito Orden, creador distinto, referencia requerida, rechazo de excepción, queue, append-only, replay y auditoría real con PostgreSQL. Test fixture aislado permite `X-Publication-Test-Scope=order`; configuración de shard B incluye nueva prueba.

## Límites que se deben preservar

- Ninguna aceptación de datos sensibles o fotocopias por ruta nueva: se referencia un acta, no se copia contenido a notificaciones ni a respuesta pública.
- El registro `approved` expresa resolución **administrativa de subsanación** documentada por RI; **no equivale** a aprobación global de Iniciación. Balotaje, publicación, derechos y otros vistos buenos siguen obligatorios.
- El UI demo usa datos exclusivamente ficticios; su estado es efímero y no reemplaza la revisión institucional real ni su UAT.
- No eliminar eventos de rechazo previos. La anulación legal de un rechazo requiere decisión/modelo distinto, no borrar historia.
- **#65 permanece abierto** por UAT institucional integral; #97 impide QA física. No promover main.

## Validación y handoff multi-IA

- Gates exact-head del PR: CI, Showcase y QA Installable pendientes de comprobación definitiva. No declarar éxito ni squash sin verificar los tres SUCCESS y HEAD vivo de dev.
- Después de fusionar registrar: PR HEAD, commit squash, CI/Showcase/QA postmerge y `qa-current.json` SHA, huella de ZIP si verificable. Anotar en GitHub Issue #65 y Drive.
