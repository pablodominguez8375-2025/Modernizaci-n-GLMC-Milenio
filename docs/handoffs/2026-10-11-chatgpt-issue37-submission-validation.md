# Issue #37 — Validación de captura contra esquema versionado (fase 1b) — 11-10-2026

## Estado y coordinación
- PR borrador: https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/pull/452
- Rama: `feature/issue37-submission-validation-20261011-gpt`.
- Base al iniciar: `dev@bbdb278541091cca1ff99c2507ed04fca6ee853a`; `main@6dfb9546a4873baff15955cf86abfd7d47e3d111` intacta.
- Issue #37 asignado a `agente:chatgpt`; revisión de PRs mostró solo #1 abierto (promoción a main por UAT). No se modifican archivos calientes ni los de Claude.
- Reserva exclusiva: `backend/src/PMGM.Api/Modules/FormConfiguration/FormSubmissionPolicy.cs`, `backend/tests/PMGM.Api.Tests/Unit/FormSubmissionPolicyTests.cs` y este handoff.

## Implementado en rama, todavía NO integrado
- Validador puro para datos JSON contra versión exacta de esquema confiable del servidor.
- Rechaza claves ajenas al esquema, intentos de escritura sobre campos ocultos/deshabilitados/solo lectura, y valores de tipo incorrecto.
- Verifica obligatoriedad de campos capturables (distingue `false` y `0` de vacío), tipos texto, número, moneda, porcentaje, fecha ISO, hora, booleano, email, teléfono, selección simple/múltiple y referencias UUID.
- Valida catálogo de listas, sin duplicados en selección múltiple; condicionales simples sólo cuando su dependencia está activa (booleano falso no activa).
- Documentos/fotos/referencias sólo admiten identificadores opacos UUID; no URLs, paths ni base64. La autorización del objeto y la persistencia siguen siendo tarea de la futura API, no de esta política.
- Mensajes de error sólo contienen claves de campo, nunca valores recibidos; no hay datos reales ni nuevas tablas.
- Pruebas unitarias cubren versión, tipos, dependencias, privacidad, validación condicional, campos obligatorios, opciones, referencias y teléfonos.

## Exclusiones y advertencias
- Este validador **no permite todavía configurar ni guardar formularios en el sistema**.
- La definición de `DependsOnKey` aún no contiene condiciones comparativas, opciones específicas ni controles de visibilidad por rol/ámbito. El API futuro debe resolver esquema **de servidor**, identidad, ámbito/rol, autorización de referencias, límites HTTP y auditoría, antes de persistir capturas.
- No se cambiaron `dev`, `main`, EF/migraciones, frontend, demo/Pages ni `srv01`.
- CI/Showcase/QA Installable exact-head: **pendiente de verificación final** sobre HEAD definitivo de esta PR. No afirmar funcionamiento integrado ni publicado.
- Issue #37 permanece abierto; próximas fases: modelos/persistencia, API administrativa/RBAC/auditoría, snapshots históricos, renderer responsivo, paridad demo y exportaciones/UAT.
- `srv01`/QA física/UAT pausadas por Issue #97.
- No editar `START-HERE.md` desde PR funcional: según GOV-003 se indexa con PR documental posterior al merge; actualizar Línea Base Maestra Drive con la evidencia final verificada.
