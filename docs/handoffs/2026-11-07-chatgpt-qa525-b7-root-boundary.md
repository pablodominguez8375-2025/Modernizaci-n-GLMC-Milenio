# QA525 B7 — frontera de plano root y vínculo de inspección al vuelo (07-11-2026)

**Issue:** #914 · **PR:** #915 DRAFT en GitHub · **Agente:** ChatGPT · **Co-review:** Claude.
**HEAD al inicio:** dev 308fb097b86032ec6d51feb4afe148983a8ea572, main 6dfb9546a4873baff15955cf86abfd7d47e3d111.

## Objetivo y contrato
Para componer B7 físico, distinguir el plano real de `qa525-vnext-root-locks.RootPlane` del plano sintético que admite `qa525-vnext-host-adapters.held_runner`, y vincular cada inspección al vuelo nonce/admission. No aceptar objetos duck-typed para autoridad root; no convertir `may_apply=False` en permiso de ejecución.

## Integración pendiente

PR #913 YA integrado SOLO en dev@8855de97786851e942a13ccbb13fedc97f55b234 (squash 7/7 CI y revisión Claude 0P0/1). Añadida prueba de conexión con **B7 real** mientras el mismo RootPlane mantiene cinco locks+lease, sin efecto físico; NO usar `Boundary` como ticket para ejecutar. El motor físico sigue sin bootstrap PRE-root APP/OPS autenticado, launcher firmado y runner seguro. Requiere un nuevo gate de autoridad distinto de `may_apply=False`, G6 A fresco, exact-head y GO del Sponsor. La identidad del objeto es local al proceso; digest de nonce no reemplaza firma ni prueba custodia.

## Límites
Este PR no crea punto de entrada físico ejecutable, clave OPS, autenticación APP Sigstore, autorización del Sponsor, G6 real, acceso Docker/host ni comandos de instalación. #822 mantiene NO-GO. Ninguna acción en glmdch, main o producción.

## Entregables creados
- Nuevo módulo sin efectos físicos: `scripts/qa/qa525-vnext-b7-root-boundary.py`: clase `Boundary` exige RootPlane exacto de una misma instancia root-locks, cinco locks+lease, un `inspect_once` y un único reporte en la misma instancia, prueba de post-unlock y contexto SHA por nonce/admisión/observación. Digest no es firma, autoridad siempre falsa. Se verifica además que el nonce real `RootPlane.lease.nonce` coincida con el nonce del vuelo, incluso después de la inspección; un cambio inesperado da STOP.
- Pruebas herméticas: `tests/test_qa525_vnext_b7_root_boundary.py`: éxito mientras HELD, post-unlock, plano falso/sintético, clase de otra instancia importlib, nonce/admisión inválidos, replay/copia/mutación, reemplazo inode, lease perdido, inspector fallido y spoofing de flags/status. No prueban mundo físico ni firma.
- Este handoff + revisión adversarial Claude solicitada PR #915 comentario 6710888272; CI exact-head pendiente de completar tras código. PR #913 es prerequisito para integración física, y se mantiene en su carril propio, sin comandos de instalación.
- Auditoría de dictamen cruzado PR #913, comentario Claude #6710801478; actualización #525 y Drive oficial.
