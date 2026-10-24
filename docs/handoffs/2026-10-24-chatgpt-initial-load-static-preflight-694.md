# PMGM #694 — contrato de prevalidación offline de carga inicial (fase B)
Fecha: 2026-10-24 · agente ChatGPT · base dev `e470712268fe27e8bc3207e884cfe0e4010cddcb`.

## Propósito y frontera jurídica
Extender la fase A integrada por PR #698 sin introducir un endpoint de carga ni enviar datos reales al servidor. Implementar sólo una **política pura de prevalidación** de tres hojas/catálogos de filas ya parseadas, con resultados de código y ubicación (nombre canónico de hoja, fila, columna y código de error) y **sin valores personales**. El contenido de entrada puede contener PII en memoria del consumidor futuro, pero en esta fase **no se invoca por una ruta HTTP, no se registra ni se persiste**. Se prueba exclusivamente con registros sintéticos.

## Alcance
- Nuevo `backend/src/PMGM.Api/Modules/Bootstrap/InitialLoadPreflightPolicy.cs`, sin acceso a DbContext, red, disco, correo, Keycloak o colas.
- `backend/tests/PMGM.Api.Tests/InitialLoadPreflightPolicyTests.cs` con casos de referencias, duplicados, RUT inválido, campos desconocidos, fechas, código de cargo, cortes por límites de filas y privacidad del resultado.
- Convención de columna `personRef` es referencia interna temporal, no identificador institucional ni credencial.
- `Valid` equivale solamente a **prevalidación estática**, nunca a autorización de carga: `CanApply=false` y `WritesPerformed=0` siempre.
- Las reglas institucionales definitivas para grado/estado/cargos pueden requerir validación adicional con PO; no inferir reglas normativas desde ejemplos.

## Fuera de alcance
No editar `Program.cs`, endpoints, CI, migraciones, UI Claude ni `main`. No activar `apply`, no recibir ZIP/CSV real por HTTP ni abrir QA a datos personales hasta resolución jurídica. No cerrar Issue #694. PR draft y CI/Showcase/QA Installable **3/3 SUCCESS exact-head** antes de fusionar a dev. Inspeccionar y reconciliar dev vivo antes de merge.

## Continuidad
Issue #694 y PR de esta rama; controlar solapes con Claude. Los resultados por fila solo contienen metadatos sanitizados; la existencia/propiedad de cada Hermano, Taller y fecha institucional debe verificarse mediante servicios de dominio autorizados en fase posterior, auditada y transaccional.
