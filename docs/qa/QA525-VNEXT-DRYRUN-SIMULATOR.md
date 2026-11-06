# QA525 vNext — Simulación contractual inactiva (alcance, 2026-11-05)

Referencia: Issue #525, tren #855 exact HEAD 429f29b8fb414e5e5eb8764616dc88610087224b. Rama APILADA sobre #855 para preservar la revisión y evitar conflictos con Claude.

Objetivo: validar por pruebas herméticas una secuencia de planificación API_SWITCHING → API_VERIFIED → WEB_SWITCHING → WEB_VERIFIED → VERIFYING y otra de rollback, usando exclusivamente **observaciones sintéticas** contra las bibliotecas existentes del tren. No es ejecutor, verificador de firma, motor físico, orquestador root, aprobador ni autoridad de despliegue. Cualquier resultado lleva NO_APPLY/NO_INSTALL.

Archivos reservados para el incremento: `scripts/qa/qa525-vnext-dryrun-simulator.py` nuevo, `tests/test_qa525_vnext_dryrun_simulator.py` nuevo, un import de prueba en `tests/test_qa_agent_readiness.py` en esta rama. No editar archivos de Claude (#848/#850/#852/#854). No tocar host glmdch, Docker, red, DB, Keycloak, main, producción ni las cinco exclusiones legacy.

**Puertas:** pruebas negativas de imagen/capas/oneshot/observaciones inconsistentes y prueba de vuelo adulterada; CI exact HEAD + revisión adversarial independiente; PR DRAFT apilado hasta integración controlada. NO MERGE del simulador antes de #855. La integración futura requiere además ADM/OPS/Sponsor/G6 firmados, journaling con fsync, 5 flock + root.lock, bloqueo legacy, medición física confiable y rollback, sin derivar autorización de este prototipo.
