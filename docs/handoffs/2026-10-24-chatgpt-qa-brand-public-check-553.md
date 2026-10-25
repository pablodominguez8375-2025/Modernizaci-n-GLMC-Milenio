# QA P0 #553 — comprobación pública de recursos de marca (24-10-2026)

Agente ChatGPT, rama `fix/qa-brand-readonly-regression-553-20261024-gpt`, base `dev@e3880db57973459b3572896d7d6c8ee541095cf0`.

El código del Dockerfile ya aplica chmod 755/644 a /brand; el corte físico bf9f333 ya contenía dicho arreglo, pero el informe de instalación solo acredita health y no estados HTTP de los recursos de marca. NO afirmar que un 403 está reparado sin probarlo en QA.

Este incremento refuerza el preflight público de solo lectura `scripts/qa/check-public-readiness.py`: cuatro GET TLS de rutas estáticas permitidas, no sigue redirects, nunca usa cookies, usuarios, contraseñas ni API privada. Debe marcar bloqueo de assets incluso si root/OIDC están sanos. No realiza deploy, SSH, cambios de host ni PII. Tests aislados con request simulado.

Archivos reservados: `scripts/qa/check-public-readiness.py`, `tests/test_qa_public_readiness.py`, este handoff. Coordinación: evitar todos los archivos de PR #722 de Claude (App.tsx, SystemConfigurationPage, Program, permisos). Issue #681 diagnóstico físico aparte; #558 nav fix ya presente en corte físico pero falta revalidación autenticada. Cierre solo con gates 3/3 exact HEAD, dev vivo behind0; main intacta.

24-10-2026 continuidad: Claude PR #722 integrada a dev `3c599bd22f35dc47de55490245be51331e408064`; se reaplica exclusivamente este trío de archivos sobre tree de dev vivo, sin tocar permisos, frontend ni backend de Claude. Gates antiguas sobre db7b9bdf4fb1f6784f46f2d4fa7b24c7ea47cadb SUCCESS, pero gates nuevas exact SHA requeridas tras conciliación.
