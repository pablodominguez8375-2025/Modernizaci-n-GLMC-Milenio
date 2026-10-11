# Proyecto Centenario — #65, nueva presentación después del rechazo

**Fecha:** 11-10-2026. **PR:** [#447](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/pull/447), aún en revisión al preparar este handoff.
**Base:** `dev@b1707e0a4f4421d8e9298e14d5bee9d7562d7c95`; `main@6dfb9546a4873baff15955cf86abfd7d47e3d111` (no modificar).
**Fuente:** `docs/PMGM-REQ-001-flujos-institucionales-2026.md`, requisito 001.14; `CandidateIntakeWorkflowPolicy.EvaluateRePresentation`; Línea Base Maestra vigente de Drive.
**Coordinación:** Issue #65 con etiqueta `agente:chatgpt`, PR draft abierto antes de tocar código. PR documental #446 de #37 activo; no se tocan sus archivos ni `START-HERE.md`.

## Brecha detectada en la implementación previa
- La creación de Insinuados desde Secretaría sólo bloqueaba el rechazo producido en el último año (calculando con UTC), pero transcurrido el año no exigía constancia de subsanación.
- El endpoint genérico `POST /api/ceremonias/solicitudes` de Iniciación omitía la compuerta y admitía múltiples expedientes activos de la misma identidad.

## Corrección aplicada en PR #447 (sujeta a gates)
1. `CandidateRePresentationGate.cs`: consulta el **último antecedente rechazado** de balotaje o Cámara del Medio a nivel de persona/Orden (no de Taller); aplica la regla existente de un año calendario con fecha civil `America/Santiago`, declaración de subsanación y referencia de acta de 1 a 240 caracteres.
2. En Secretaría del Taller y la creación genérica de Iniciación se ejecuta la misma compuerta. No puede eludirse por el endpoint alternativo ni por tener el rechazo en otra Logia.
3. Sin antecedentes el alta normal no cambia. Los intentos de adjuntar antecedentes inventados se rechazan.
4. Si se permite iniciar nueva presentación se reutiliza `PersonId`, se abre **nuevo expediente** y se preservan `CeremonyValidation` de rechazo anteriores. Se agrega `candidate_representation_review` en estado **observed** con referencia documental y vínculo al rechazo. Es constancia de Secretaría, **no** aprobación institucional definitiva; la revisión de Régimen Interior queda pendiente. `AuditEvent` registra antecedentes vinculados sin copiar razones íntimas a la carga.
5. La demo sintética y la pantalla de nuevo postulante aceptan referencia y declaración, y usan identificadores ficticios `RECHAZO2025` / `RECHAZO2026` para mostrar casos límite. No se expone un portal público de rechazos.
6. Pruebas: `CandidateRePresentationGateTests.cs` (fecha límite exacta, año bisiesto, ausencia de constancia, constancia falsa y longitud); `CandidateRePresentationHttpTests.cs` (PostgreSQL real, ambas rutas, persistencia, historial/auditoría y bloqueo de rechazo reciente); `candidateIntakeApi.test.ts` (paridad demo).

## Limites / riesgos
- Este incremento **no constituye resolución de Régimen Interior** ni acredita causas efectivamente subsanadas; sólo exige declaración y referencia documental y deja el nuevo expediente observado para examen posterior. Validación institucional no se simula.
- `main` intacta, `srv01` no se activa, no usar datos reales.
- No cerrar #65: queda UAT institucional, reconocimiento del documento de subsanación, variantes adicionales de ceremonia y regresión integral.
- Evaluar ruta de corrección cuando el rechazo fue anulado por resolución institucional; **no** borrar el rechazo histórico. Requiere regla formal distinta del simple transcurso del año.

## Gates y cierre
Pendientes de confirmar para el HEAD exacto del PR: PMGM CI, Showcase Demo y QA Installable; adicionalmente publicación Pages y artefacto QA postmerge. Registrar SHA y resultados tras su ejecución; no anunciar integración prematura.
