# Proyecto Centenario — Issue #65: transiciones seguras de ceremonias

**Fecha:** 11-10-2026 (America/Santiago). **PR:** [#449](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/pull/449), rama `fix/issue65-ceremony-state-transitions-20261011-gpt`, base original `dev@8d961b9887a133c05cffd4bc3ace6fcfd5a9df9f`. **Estable:** `main@6dfb9546a4873baff15955cf86abfd7d47e3d111`, no modificar. `srv01` pausado (#97).

**Fuente:** `docs/PMGM-REQ-001-flujos-institucionales-2026.md` puntos 001.09 (corrección en expediente), 001.17–001.20 (visto bueno y plancha), 001.47 (historial), `AGENTS.md` y `docs/PMGM-GOV-003-coordinacion-multi-ia.md`. #65 reservado para ChatGPT y PR draft antes del código.

## Brecha auditada

Antes de este incremento, `POST /api/ceremonias/solicitudes/{requestId}/autorizar` convertía en **rejected** las solicitudes con prerequisitos financieros, publicación, balotaje o visaciones faltantes, salvo observación específica de reinsinuación, imposibilitando correcciones dentro del mismo expediente. Al mismo tiempo sólo rechazaba el estado previo `authorized`: `rejected` y `completed` podían ser reautorizados si las visaciones luego cambiaban. `GET /elegibilidad`, matriz de Gran Maestría y bandeja no tomaban el estado terminal de solicitud al determinar `canAuthorize`.

## Incremento implementado (pendiente de gates finales)

1. **Política `CeremonyAuthorizationStatePolicy`**: sólo `draft`, `under_review`, `eligible`, `observed` son estados desde los cuales se puede intentar autorizar. `authorized`, `completed`, `rejected` y estados desconocidos quedan bloqueados. Un intento con requisitos insatisfechos sólo deja `observed` y **no equivale a rechazo formal**.
2. **POST /autorizar**: controla el estado terminal antes de consultar requisitos, responde 409 con código `ceremony_request_status` sin mutar historial. Ante prerequisitos incumplidos registra auditoría `ceremony.authorization.blocked` (resultado `observed`) y mantiene solicitud corregible dentro del mismo expediente, en lugar de transformarla a `rejected`.
3. **GET /elegibilidad y POST /autorizar** usan la política compartida, agregando `ceremony_request_status` en la lista de requisitos cuando el expediente está cerrado; `canAuthorize=false` en esos casos.
4. **Matriz de Gran Maestría y bandeja institucional**, incluidas acciones de autorización, siguen igual política y evitan ofrecer acciones sobre solicitudes `completed`. `CeremonyEligibilityService` acepta estado opcional, para la misma evaluación si el consumidor dispone de él.
5. Pruebas de dominio `backend/tests/PMGM.Api.Tests/Ceremonies/CeremonyAuthorizationStatePolicyTests.cs` verifican transición y los cinco tipos reglamentarios (iniciación, afiliación, incorporación, aumento y exaltación), estados terminales y no regresión de solicitudes abiertas.
6. Prueba HTTP PostgreSQL `backend/tests/PMGM.Api.Tests/Integration/CeremonyAuthorizationStateHttpTests.cs` comprueba 409 sin reautorizar `rejected/completed/authorized`; solicitud `under_review` pasa a `observed` al faltar vistos buenos; paridad en `GET /elegibilidad`, matriz Gran Maestría y bandeja; estado persistido y evento de auditoría. Registrada en shard B de `.github/backend-integration-shards.json`.

## Matriz mínima de regresión

| Estado anterior | Intento POST /autorizar sin requisitos | GET elegibilidad / matriz / bandeja |
| --- | --- | --- |
| draft / under_review / eligible / observed | HTTP 409, estado final observed, expediente corregible | Evalúa requisitos en vivo |
| rejected | HTTP 409, permanece rejected | canAuthorize=false, requisito de estado bloqueado |
| authorized | HTTP 409, permanece authorized | canAuthorize=false para segunda autorización |
| completed | HTTP 409, permanece completed | canAuthorize=false; ninguna acción nueva de autorización |
| desconocido | HTTP 409, no se modifica | canAuthorize=false |

## Límites, riesgos y próximos pasos

- No interpreta una observación financiera o documental como sentencia de rechazo; una sentencia o rechazo institucional formal debe originarse en el flujo aprobatorio específico, con fecha, decisión y auditoría propia. Este incremento **no implementa** ese expediente disciplinario ni una apelación.
- No cambia montos, permisos, tarifario, datos personales, firma digital, estados de balotaje ni emisión de planchas.
- **#65 sigue abierto**: matriz integral UAT con aceptación de Sponsor y validaciones de planchas/autorizaciones. QA física y `srv01` siguen expresamente aplazadas por #97.
- Al finalizar el PR se deben registrar SHA exacto y SUCCESS de CI, Showcase y QA, el squash a dev si procede y los gates postmerge. Documentar en issue #65 y el Estado ejecutivo de Google Drive; no declarar publicación Pages/ZIP sin prueba.

