# QA525 vNext — vuelo físico con una sola ventana RootPlane (sin CLI ni GO)

Fecha 07-11-2026. Issue #916 / PR DRAFT #917, agente ChatGPT, revisión cruzada Claude solicitada.
Base dev exacta: `50fd613961af7cf678fe201791b67673c85e79cf`, main histórica estable: `6dfb9546a4873baff15955cf86abfd7d47e3d111`.

Objetivo: componer componentes vNext **existentes** bajo un mismo RootPlane real, usando las garantías B7 de PR #913 y #915, evidencia G6 raw con pines, vinculación de admission + scope manifest y rollback transaccional. Nada aquí autentica firmas OPS/APP ni autoriza ejecución de glmdch. **No hay CLI ejecutable ni runner por defecto**. Los port/runner/gate son inyectados y solo un bootstrap independiente, firmado y auditado puede confiar en ellos.

Archivos reservados exclusivamente: `scripts/qa/qa525-vnext-held-flight-bridge.py`, `tests/test_qa525_vnext_held_flight_bridge.py`, este handoff. No alterar archivos Claude o main.
Desarrollo entregado (aún en revisión): `run_held` con módulos externos inyectados, BoundJournal con reloj fresco tras cinco locks, RootBoundary B7 real y nonce de lease, G6 bytes crudos inmutables comprobados antes de B7, después de B7 y antes de API/WEB; read-only baseline revalidada en plan, manifest scopes duradero, runner externo exclusivamente a través de gate de reporte B7/RootPlane, ServiceSwitcher API/WEB y rollback transaccional por `flight.run_apply`. Nada instala, abre red o ejecuta procesos por defecto. Los resultados no acreditan firma, Sponsor, origen real del inspector, cgroup ni DR.

Pruebas `tests/test_qa525_vnext_held_flight_bridge.py` usan motor+journal+G6+flight reales con puerto RootPlane fake y acciones de Docker simuladas. Cubren APPLIED / ROLLED_BACK, G6 corrupto pre-effect, baseline cambiado, gate negado, mutación G6 entre etapas, pérdida de lock, nuevo intento de ejecución, ausencia de runner y ninguna importación ejecutora. **CI exact-head y review pendientes**.

Pendiente para despliegue: autenticación PRE-root OPS Ed25519 Sponsor y APP Sigstore exacta, validación externa Python y child/cgroup/Compose, autoridad del operador y permisos separados, resguardo G6 A nuevo y GO físico. #822 se mantiene STOP. Esta biblioteca acepta un gate/runner inyectados pero NO puede validar sus firmas u origen: ningún tercero debe usarla como comando root sin un entrypoint autenticado y revisado. Issue #822 mantiene NO-GO.