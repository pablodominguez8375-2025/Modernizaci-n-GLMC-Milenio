# QA525 B7 — continuidad de la misma ventana de cinco locks (07-11-2026)

**Issue:** #912 · **PR:** draft en curso · **Agente:** ChatGPT; revisión cruzada Claude.
**Ramas al iniciar:** `dev@8d2ddadb0c8d7deaf218e38e5d72be6a56eddbe9`, `main@6dfb9546a4873baff15955cf86abfd7d47e3d111` (no tocar).
**Estado físico:** `glmdch` NO-GO. #822 permanece abierto; motor sintético sin bootstrap físico firmado y sin permiso launcher-only/API+WEB.

## Necesidad

`held_readonly()` (PR #910 ya fusionado) toma cinco locks + lease, realiza inspección, **libera al retornar**. Un informe posterior al unlock NO conserva aislamiento para una mutación. El motor futuro debe reutilizar una función de inspección que opere dentro del *RootPlane* YA retenido durante la misma operación atómica supervisada. Esta intervención habilita reutilizar ese código sin introducir APPLY ni crear una nueva CLI.

## Alcance

- Reutilizar el validador de inspección bajo un plano activo y probado sin cambiar el wrapper público.
- Pruebas herméticas con plano retenido, reporte siempre sin autoridad, negativo post-unlock, inode/lease y fallos del inspector.
- Ningún puerto físico, autorización, red, Docker, clave, secrets, base de datos ni despliegue.

## Entregas y controles

- Archivos: `scripts/qa/qa525-vnext-b7-held-readonly.py`, `tests/test_qa525_vnext_b7_held_readonly.py`, este handoff.
- **CI exact-head anterior**: `d47e5e49bec3a900111f06c5ff94ded278b75a6a`, 7/7 SUCCESS incluidos Silo, PMGM CI, Showcase e instalable. **No transferir PASS a HEAD rebasado**; volver a comprobar 7/7. Review adversarial Claude HEAD solicitado en PR #913 comentario 6710413726, sin respuesta al último corte.
- Estado Pages / QA instalable: no atribuir a este PR hasta que CI exact HEAD lo pruebe; QA físico pendiente.
- Claude: PR #905 avanzó a HEAD `6e8fdb2019f2da50d1fc8b364bc5416fe857b18b` corrigiendo dos P2 tras revisión cruzada ChatGPT comentario 6710445732. Su CI nuevo está en curso; no fusionado por este carril.

## Siguiente trabajo distinto de esta entrega

El primer byte físico exige pre-root OPS Ed25519 con ancla externa, APP Sigstore/OIDC exact SHA, pines de bytes Compose firmados, bloqueo+lease continuo durante B7+aplicación/rollback, confinamiento DNS/cgroup, three oneshots, G6 A Restic+Drive restaurado y vigente, firma/custodia y aprobación específica del Sponsor; #822 sigue **STOP**. Alternativa G6 A QA sintético no demuestra DR de pérdida de host.
