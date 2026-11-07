# QA525 vNext — vuelo físico con una sola ventana RootPlane (sin CLI ni GO)

Fecha 07-11-2026. Issue #916, agente ChatGPT, revisión cruzada Claude.
Base dev exacta: `50fd613961af7cf678fe201791b67673c85e79cf`, main histórica estable: `6dfb9546a4873baff15955cf86abfd7d47e3d111`.

Objetivo: componer componentes vNext **existentes** bajo un mismo RootPlane real, usando las garantías B7 de PR #913 y #915, evidencia G6 raw con pines, vinculación de admission + scope manifest y rollback transaccional. Nada aquí autentica firmas OPS/APP ni autoriza ejecución de glmdch. **No hay CLI ejecutable ni runner por defecto**. Los port/runner/gate son inyectados y solo un bootstrap independiente, firmado y auditado puede confiar en ellos.

Archivos reservados exclusivamente: `scripts/qa/qa525-vnext-held-flight-bridge.py`, `tests/test_qa525_vnext_held_flight_bridge.py`, este handoff. No alterar archivos Claude o main.
Pendiente: pruebas offline, CI exact HEAD, revisión adversarial, firma PRE-root OPS Sponsor+APP Sigstore y autorización física launcher-only/API+WEB por separado. Issue #822 mantiene NO-GO.