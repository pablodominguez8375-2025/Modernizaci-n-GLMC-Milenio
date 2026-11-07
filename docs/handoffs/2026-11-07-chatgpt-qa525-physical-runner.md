# QA525 B7 — ejecutor físico cgroup allowlist (SIN CLI, NO-GO)

Fecha 07-11-2026. Issue #918 · PR DRAFT en apertura. Responsable ChatGPT. Revisión cruzada Claude.
HEAD base dev 91a19ea3bc53b3eb01edd8c92a800a56cb4388b6; main 6dfb9546a4873baff15955cf86abfd7d47e3d111 inalterada.

Diseño: el futuro launcher autenticado PRE-root debe construir RootPlane real, BoundJournal, RootBoundary, la lectura física 7+3 y el plan firmado; luego inyectar un runner al vuelo B7 de #917. Este nuevo runner **no puede confiar en held_runner sintético**. Construirá cuatro argv systemd-run --scope --collect exactos: api/web y target/baseline, a partir de un plan fijado; solo emitirá una acción tras comprobar el reporte actual del mismo RootBoundary y la vigencia de hash/propietario externo de systemd-run, docker, Python/child/Compose, por callback inyectado desde el futuro bootstrap firmado. No admite PATH, shell, otros comandos, banderas extras ni fuentes mutables.

No se valida aquí la procedencia APP Sigstore ni OPS Ed25519 Sponsor: **son obligación del punto de entrada externo**. No existe ejecutor por defecto. NO significa permiso de instalación ni de actualización. #822 STOP. Mantener las cinco cerraduras+lease durante todo el vuelo y rollback; G6 Restic/Drive requiere nuevo recibo/restore físico.
