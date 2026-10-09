# Handoff técnico — Issue #47: gate de avance en autorización real

## Estado de trabajo
- Responsable: ChatGPT, PR #394: https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/pull/394
- Base inicial: `dev@f138abe456b94f64b3c995a65322f493963edb0e`; `main` permanece sin aprobación de promoción.
- **Alcance de este incremento:** garantizar fail-closed en `CeremonyEligibilityPolicy.Evaluate`, política invocada tanto por `CeremonyEndpoints.GetEligibilityAsync` como por `CeremonyEndpoints.AuthorizeAsync`.
- Aumento de Salario requiere evaluación del grado 1; Exaltación del grado 2. Sin decisión, versión de regla, tres requisitos o identidad de grado consistente, se agrega un requisito `advancement_eligibility` observado y `CanAuthorize=false`. Si existe decisión completa válida, se permite estado aprobado ordinario o dispensa favorable documentada; un resultado sin conformidad queda rechazado.
- `CeremonyEligibilityService` usa la misma decisión de autorización, en vez de permitir avances si omite umbrales o evidencia. Iniciaciones no se bloquean por requisitos de avance.

## Código y pruebas
- `backend/src/PMGM.Api/Modules/Ceremonies/CeremonyCodes.cs`: código de requisito para matriz.
- `backend/src/PMGM.Api/Modules/Ceremonies/CeremonyEligibilityPolicy.cs`: compuerta compartida con validación completa de grado/regla.
- `backend/src/PMGM.Api/Modules/Ceremonies/CeremonyEligibilityService.cs`: reutilización de guard y bloqueo por omisión de evidencia.
- `backend/tests/PMGM.Api.Tests/Ceremonies/CeremonyEligibilityServiceTests.cs`: casos de avance sin datos, sin evidencias y con conformidad válida.
- `backend/tests/PMGM.Api.Tests/Ceremonies/CeremonyEligibilityPolicyAdvancementTests.cs`: casos de aprobación y bloqueo con versión, grado, dispensa y excepción.

## Límites y deuda obligatoria
- **Este incremento es un bloqueo preventivo, no el circuito final.** El endpoint real de autorización aún NO crea por sí solo `AdvancementEligibilityDecision` desde datos institucionales. En consecuencia, hasta completar el proyecto, Aumento/Exaltación se marcarán **observados/no autorizables**; es intencional para no violar las reglas funcionales del PO.
- Próximo incremento: configurar mínimos/versiones/vigencias a nivel de Gran Logia; construir proyección de asistencia desde `LodgeMeetings` y `LodgeAttendanceRecords`; docencia desde `LodgeInstructionSessions` y `LodgeInstructionAttendanceRecords`; planchas desde `InstitutionalDocuments` / `DocumentVersions`. Usar periodo del grado y última anotación de asistencia. **No contar un borrador documental sin evidencia de realización.** Conectar al contexto del endpoint real, agregar snapshot/auditoría de conteos y regla, y pruebas HTTP integradas.
- Issue #47 permanece abierto; Issue #48 expediente de Consejo de Maestros/votaciones/documentos también. No declarar UAT ni despliegue físico srv01 (Issue #97), ni modificar `main`.
- Los mínimos sintéticos de tests NO son norma institucional vigente.
- Gates de PR #394 deben ser CI, Showcase Demo y QA Installable **SUCCESS** del HEAD exacto antes de integrar a `dev`. Después de fusionar, documentar corte e instalación QA pendiente.
