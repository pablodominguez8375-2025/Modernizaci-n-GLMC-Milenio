# PMGM #37 — validación de UUID no vacío en capturas configurables

Corte: 2026-10-22. Agente: ChatGPT. Base inicial `dev@6fbbabc4bb6566c28777daf7b95fbcc30c827b91`, `main@b77de59e170ebfa2abec71b418f6d8b0407ba7ad`.
Coordinación: issue #37 es `agente:chatgpt`. UI de Clau y otros agentes no se modifica. PR de trabajo: [#647](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/pull/647), **DRAFT**; rama `fix/form-capture-reject-empty-reference-20261022-gpt`.

## Alcance comprometido
`FormSubmissionPolicy.Validate` ya exige una forma UUID para los campos `document`, `photo` y `reference`, pero `Guid.TryParse` acepta el valor centinela `00000000-0000-0000-0000-000000000000`. Un identificador vacío no constituye una referencia institucional válida.

Incremento propuesto (sin romper esquemas ni registros históricos):
- **Implementado:** denegar `Guid.Empty` en las nuevas capturas para `document`, `photo` y `reference`. Solo se valida forma, no autorización al recurso.
- Permitir la ausencia de respuesta en campos opcionales; conservar autorización, esquema versionado, catálogo, privacidad y endpoints actuales.
- **Implementado:** prueba xUnit parametrizada para los tres tipos: UUID no vacío válido, UUID vacío rechazado, respuesta opcional ausente permitida sin exponer identificadores privados en errores. Las pruebas HTTP/suite de CI deben aprobar en GitHub.
- No validar existencia/propiedad del objeto con este helper; esa verificación corresponde a la capa de dominio/autorización al resolver la referencia y sigue pendiente de auditoría de seguridad.

## Puertas y controles
Crear PR draft después de este primer commit, completar implementación/test y ejecutar **PMGM CI + Showcase Demo + QA Installable exact-HEAD SUCCESS**, comprobar `behind dev=0` antes de integrar solo a `dev`. No cambios en `main`, glmdch ni producción. Registrar SHA final y diffs en issue y Drive. La validación técnica no implica QA/UAT institucional completa de #37.

## Evidencia técnica del incremento
- Código: `backend/src/PMGM.Api/Modules/FormConfiguration/FormSubmissionPolicy.cs`.
- Prueba: `backend/tests/PMGM.Api.Tests/Unit/FormSubmissionPolicyTests.cs`.
- Integración: PR #647 hacia `dev`, solo tras verificación de los tres workflows exact HEAD.
- Registro espejo: issue #37 comentario #6354287016 y la adenda de Drive Proyecto Centenario.
