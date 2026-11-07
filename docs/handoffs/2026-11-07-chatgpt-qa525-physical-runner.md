# QA525 B7 — ejecutor físico cgroup allowlist (SIN CLI, NO-GO)

Fecha 07-11-2026. Issue #918 · PR DRAFT en apertura. Responsable ChatGPT. Revisión cruzada Claude.
HEAD base dev 91a19ea3bc53b3eb01edd8c92a800a56cb4388b6; main 6dfb9546a4873baff15955cf86abfd7d47e3d111 inalterada.

Diseño: el futuro launcher autenticado PRE-root debe construir RootPlane real, BoundJournal, RootBoundary, la lectura física 7+3 y el plan firmado; luego inyectar un runner al vuelo B7 de #917. Este nuevo runner **no puede confiar en held_runner sintético**. Construirá cuatro argv systemd-run --scope --collect exactos: api/web y target/baseline, a partir de un plan fijado; solo emitirá una acción tras comprobar el reporte actual del mismo RootBoundary y la vigencia de hash/propietario externo de systemd-run, docker, Python/child/Compose, por callback inyectado desde el futuro bootstrap firmado. No admite PATH, shell, otros comandos, banderas extras ni fuentes mutables.

No se valida aquí la procedencia APP Sigstore ni OPS Ed25519 Sponsor: **son obligación del punto de entrada externo**. No existe ejecutor por defecto. NO significa permiso de instalación ni de actualización. #822 STOP. Mantener las cinco cerraduras+lease durante todo el vuelo y rollback; G6 Restic/Drive requiere nuevo recibo/restore físico.

## Código publicado (PR #920)

Implementado script biblioteca `qa525-vnext-held-physical-runner.py` con función `bind(root_locks,boundary_module,containment,scopes,plane,boundary,report,spec,pins,verify_pins,execute)`. Exige clase RootPlane exacta y registro B7 actual, que coincide con nonce de lease y digest de admisión. Construye por `containment.systemd_run_argv` exactamente 4 comandos permitidos (api/web baseline y target) y congela su lista. Cada comando requiere scope manifest del mismo lease a través de `scopes.require_recorded` y checks del plano de cinco locks antes/después. Un callback externo de verificación de binarios/Python/child/Compose debe retornar exactamente True ANTES y DESPUÉS de ejecutar una acción inyectada; el registro aquí NO autentica por sí solo. Resultados nunca presuponen GO.

Nueva suite `tests/test_qa525_vnext_held_physical_runner.py`: puertos lock sintéticos, journal/lease/root boundary reales, scope manifest realmente persistido en directorio privado de pruebas, callbacks sin Docker/host. Prueba las 4 formas canónicas, flags/comandos alterados, scope faltante, binarios no corroborados, revocación de pin tras invocar child, lock cambiado antes/durante child, informe copiado, mutación posterior a bind, inputs falsos, resultado no entero y ausencia de API host. CI exact-head y revisión Claude son pasos siguientes.

### Riesgos aún NO resueltos

#917 invoca `runner_factory(plane,boundary,report)` y no entrega plan autenticado como argumento; para conectar físicamente, el futuro entrypoint debe comprobar que `spec` externo está ANCLADO a las mismas capas reales de baseline y destino capturadas y que cada SHA proviene de fuente firmada. Los callbacks `verify_pins` y `execute` son fronteras de confianza NO implementadas aquí, así como autenticación APP/OPS, cgroup/proceso real, G6 fresco/restore, 10 contenedores físicos, autoridad Sponsor y GO diferenciado. Un acta en GitHub/Drive NO suple esos requisitos.
