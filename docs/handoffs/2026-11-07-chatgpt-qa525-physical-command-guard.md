# QA525 — guard estricto de comandos físicos para systemd scope

- Corte inicial dev: `91a19ea3bc53b3eb01edd8c92a800a56cb4388b6` (verificar HEAD en cada merge); main permanece en `6dfb9546a4873baff15955cf86abfd7d47e3d111`.
- Fecha: 2026-11-07; issue #919; dueño ChatGPT; PR draft pendiente.
- Instrucción Sponsor: aprovechar esperas de CI y revisión de Clau para cerrar desarrollo restante antes de pruebas físicas, no ejecutar `glmdch`.
- Respetar #822 NO-GO; usar alcance `QA_SYNTHETIC` y exclusividad de tres archivos nuevos.
- Objetivo: allowlist exacta de los cuatro argv canónicos `systemd-run --scope ... docker compose ... api/web` derivados de plan y nonce, sin confiar los datos de host ni la firma del package. Host y GO verificados por entrada **externa**.
- Entrega: módulo sin IO/proceso por defecto, pruebas negativas anti-argv/flags/retry/lock/identidad, CI exact-head y review Claude.

## Implementación y control de solapamiento

- Issue #919, PR **#921**, branch `feature/qa525-physical-command-guard-20261107-gpt`, base dev `91a19ea3bc53b3eb01edd8c92a800a56cb4388b6`; no editar #917 ni otros archivos calientes.
- Biblioteca creada `scripts/qa/qa525-vnext-physical-command-guard.py`: valida plan completo con `baseline-plan.validate`, nonce/admission y registro B7 del lease real; calcula exactamente 4 argv `systemd-run --scope` API/WEB target+baseline; comprueba (por **callbacks inyectados, NO autenticados aquí**) autoridad por etapa, pin de código, intención duradera, manifest del scope, cgroup ABSENT/EMPTY antes/después; chequea B7 locks+lease pre/post efecto; consume el comando incluso si el resultado es ambiguo. No invoca procesos directamente ni permite retry por código.
- Tests `tests/test_qa525_vnext_physical_command_guard.py`: cuatro comandos exactos, ejecución en memoria, prohibición de flags extras/arbitrariedad, pérdida de lock y cgroup, pin/autoridad/intent falso o truthy no-True, repetición, respuesta inválida, baseline invalidado, report copiado, ninguna superficie subprocess/os/network.
- **Se detectó en paralelo PR #920 / Issue #918** (otro carril agent:chatgpt) que implementa el runner inferior `qa525-vnext-held-physical-runner.py` de cuatro argv con verificación externa de digests y scopeStore real. Para evitar dos actualizadores, #921 se considera **capa de política complementaria**: `run_effect` de #921 debe recibir el runner #920, no ejecutar directamente. Los controles de allowlist pueden redundar como defensa en profundidad, pero NO crean otra autoridad. Coordinación en los PR #920 comentario 6711434517 y #921 comentario 6711434939. Antes de integrar comprobar composición/compatibilidad con la HEAD viva de #920.
- CI exact-head del #921 en curso tras commit `e41630d4cee63576278cf892e40ebfd30a85080b`. Solicitar review adversarial a Claude. Ni CI verde ni `APPLIED` sintético implican GO físico.

## Bloqueos reales pendientes

Firma OPS Ed25519 del Sponsor y APP Sigstore independentemente autenticadas **pre-root**, procedencia externa y custodia de claves, permisos/source SHA firmados de Python/child/Compose, G6 A Restic nuevo y restauración aislada, baseline 7 running +3 oneshots medidos, cgroup real, validación de crash/rollback, autorización Sponsor separada launcher-only/API+WEB. Único host `glmdch`, no main, producción, DB ni identidad. #822 NO-GO físico.
