# Issue #694 — fase M: persistencia transaccional de ventana de carga (sin endpoint)

Corte 24-10-2026 · ChatGPT. Base dev: `98f7930e448c362835c6e75b03dd8d5973e77421`. Rama `feature/initial-load-atomic-window-694-20261024-gpt`.

## Objetivo
Implementar una pieza backend, sin registro de endpoint, para preparar transiciones auditadas `not_opened → open → closed` en la ventana singleton. No exponer operación HTTP ni permitir importación/PII; bloquear ejecución si faltan prerequisitos provenientes de una fuente verificada del servidor. Reusar guardas y triggers ya integrados.

## Alcance previsto
- `backend/src/PMGM.Api/Modules/Bootstrap/InitialLoadWindowAtomicTransition.cs` (nuevo).
- `backend/tests/PMGM.Api.Tests/InitialLoadWindowAtomicTransitionTests.cs` (nuevo).
- Este handoff.

## Seguridad de la entrega
No crear un proveedor jurídico real ni afirmar que hubo aprobación. No crear rutas HTTP, cuentas, secretos, datos personales ni tocar `main` o `glmdch`. Estado DRAFT hasta 3/3 controles exact-head. Registrar actualizaciones del PR e Issue #694 y Drive.

## Continuidad pendiente
Conectar una fuente de evidencias server-side auditada y pruebas PostgreSQL aisladas, revisión de Legal, y luego endpoint protegido con validación JWT master. El importador de Talleres/Hermanos/cargos sigue pendiente.
