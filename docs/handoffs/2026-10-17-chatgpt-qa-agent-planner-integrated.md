# Handoff — 17-10-2026 — QA glmdch, planificador automático (#525 / PR #532)

## Estado verificable
- Rama activa al inicio: `dev@c649339ef8cd937608c7b49ead8e88e3205be0d7`.
- Rama estable al inicio/cierre: `main@6dfb9546a4873baff15955cf86abfd7d47e3d111` (sin cambios).
- PR [#532](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/pull/532) HEAD exacto `ce6dea1d67d0ccbcb25b1f7663af62e100ac4b6b`, base `dev@c649339e`, mergeable. Integrada por squash exclusivamente a `dev@4c565605ed4134b4ec1b2855437bc21005acf0ef`.
- Issue [#525](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/525): registro de integración y dependencias en comentario 6228821978; issue [#97](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/97) continúa OPEN por montaje físico.

## Alcance integrado
- `scripts/qa/qa-agent-plan.py`: consulta únicamente elegibilidad del candidato firmado, exige baseline de despliegue QA físico supervisado, selector SHA exacto y decisión antirollback/fast-forward. No instala ni descarga ni maneja Docker.
- `infrastructure/systemd/pmgm-qa-agent-plan.service` y `.timer`: plantillas inactivas; servicio sin privilegios, lectura únicamente, timer horario **NO INSTALADO NI HABILITADO**.
- `tests/test_qa_agent_plan.py`, `tests/test_qa_agent_plan_units.py`: pruebas de rechazo de candidato falso, faltante baseline, downgrade/divergencia y confinamiento de unidades.
- `.github/workflows/qa-agent-planner.yml`: comprobación aislada, sin tocar el servidor.
- `docs/qa/QA-AGENT-PLANNER-SYSTEMD.md`: contrato y límites.

## Gates de la PR sobre HEAD exacto
- Offline QA agent planner: [38995840402](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/actions/runs/38995840402) — SUCCESS.
- HTTPS/OIDC: [38995840350](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/actions/runs/38995840350) — SUCCESS de runner, **no TLS físico glmdch**.
- QA Installable: [38995840344](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/actions/runs/38995840344) — SUCCESS de generación; no despliegue.
- Showcase: [38995840240](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/actions/runs/38995840240) — SUCCESS del workflow, no validación de Pages por SHA postmerge.
- PMGM CI: [38995840300](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/actions/runs/38995840300) — SUCCESS.
- QA Silo supply/recovery: [38995840340](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/actions/runs/38995840340) — SUCCESS en laboratorio; **no recuperación física glmdch**.
- Postmerge del nuevo `dev@4c565605`: controles independientes deben verificarse nuevamente; no extrapolar el resultado del HEAD de PR.
- No hubo migraciones, alteraciones de modelo ni interfaz en esta integración.

## Situación operacional y bloqueos
- `glmdch` es QA designado; `srv01` permanece pausado. El estado más reciente registrado en #525 describe Docker instalado pero sin contenedores/volúmenes, un paquete anterior y HTTP 502 en `https://sistema.granlogiamixta.cl/`. Estado físico **NO RELEÍDO** desde esta intervención; no inferir salud del host.
- Herramientas de GitHub/Drive no proveen sesión SSH o consola del servidor en esta intervención. Ninguna operación remota física, instalación, credencial, DNS, TLS, firewall, UAT ni publicación se ejecutó.
- PR #518 hacia main sigue reservada: no autoriza tocar `main`. #470/#479 y decisiones Silo/AGPL deben permanecer bloqueantes conforme a las evidencias y alcance QA sintético.
- El agente efectivo sigue faltando: no hay token local Actions:read, bootstrap, primer deployment aprobado/registrado, respaldo externo cifrado, prueba de restauración, health/smoke autenticados ni compuertas G1–G10 físicas aprobadas.
- Cada aplicación del instalable debe validar procedencia firmada, digest, manifest, imagen y binario Silo; no usar ZIP Pages ordinario ni descargar un run ambiguo.
- No copiar secretos a GitHub, Drive o chats. Evitar un self-hosted runner genérico con Docker/root en repositorio público.

## Siguientes pasos seguros
1. Verificar gates postmerge por SHA `4c565605`, nuevo SHA de `dev` y `qa-current.json` de Pages sin confundir artefacto y QA instalado.
2. Completar canal inicial de administración segura mediante consola autorizada del host o mecanismo ya aprobado; ninguna cuenta o token real en mensajes.
3. Inspección read-only y reconciliación 502 TLS, ingress/Keycloak, roles PostgreSQL, G8, acceso y firewall con evidencia sanitizada.
4. Diseñar/validar implementación de rollout supervisado con respaldo externo cifrado, restauración probada, control de migraciones, rollback y pausa; requerir aceptación de riesgo.
5. Solo tras primer QA sintético físico aceptado y baseline confiable, contemplar habilitar el temporizador de planificación. Automatizar despliegue efectivo requiere otro cambio explícito.
6. Registrar resultados en #97, #525 y Línea Base/Adenda QA de Drive, manteniendo `main` intacta.
