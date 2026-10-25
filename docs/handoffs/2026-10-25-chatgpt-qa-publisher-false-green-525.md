# Handoff — #525 publicador QA no debe informar éxito del SHA ausente

Corte de origen (2026-10-25): dev `28dd00aba206af6b3f5f95124709c954fa4bf844`; main `6dfb9546a4873baff15955cf86abfd7d47e3d111` inalterada.

## Hallazgo vivo
Showcase PUSH #40210191632 fue cancelado por ejecución manual #40210202550 de la misma ref (concurrency compartida). El selector exige exitosos PUSH, no acepta el manual; es correcto mantener el gate. El publicador PUSH #40210191615 terminó SUCCESS después de 50 iteraciones, log `QA_SIGNED_RELEASE_ALREADY_PUBLISHED` (corte viejo 25fe9fc) y `QA_VALIDATED_CUT_PUBLISHED_NEWER_PUSH_NOT_YET_GREEN`, mientras `qa-signed-28dd00a` seguía HTTP404.

## Alcance
- `scripts/qa/qa-signed-publish.py`: fallar de forma explícita cuando al agotar el plazo solo se mantuvo release ancestral y no se publicó el SHA de ese push.
- `tests/test_qa_signed_publish.py`: regresiones offline sin red ni secretos sobre cortes viejos, corte actual y push supersedido.
- Mantener selección `event=push`, digest, firma, staging, pinning y validación de ancestro sin cambios.
- La colisión de concurrencia de `.github/workflows/showcase-pages.yml` se notificó a PR #628, propietario existente del archivo caliente; no se toca aquí.

No se modifica glmdch, Docker, base de datos, `main`, credenciales ni aceptación UAT. PR DRAFT contra `dev`, merge solo si gates exact HEAD CI/Showcase/QA Installable PASS y base fresca. Registrar resultado y backup/runbook en Drive QA.
