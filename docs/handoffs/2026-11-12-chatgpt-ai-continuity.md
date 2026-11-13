# Handoff — 2026-11-12 — ChatGPT — coordinación IA compacta (#821)

## Propósito y decisión
Recuperar continuidad multi-IA con menor consumo de contexto **sin reescribir ni borrar** `START-HERE`, `AGENTS`, `PMGM-GOV`, adendas, historiales o actas. Se crea una puerta de entrada corta y un contrato documental de estados. No es una nueva autoridad ni permite operación física.

## Fuentes verificadas
- `dev` al inicio: `5e09d7b5718309e31f639a1eb45890546e7b29cf`; `main`: `6dfb9546a4873baff15955cf86abfd7d47e3d111`, **leídos de refs GitHub vivos al comenzar**, no de memoria.
- `AGENTS.md`, `START-HERE.md`, `docs/PMGM-GOV-003-coordinacion-multi-ia.md` y `docs/handoffs/README.md` consultados desde `dev`; Issue #821 abierto con `agente:chatgpt`. Issue #369 cerrado.
- Revisados PR abiertos y listas de archivos antes del primer commit: #1002, #1001, #1000, #843, #841, #805, #804, #771, #766, #765, #764, #737, #479 y #1. No reclamaban las tres rutas nuevas de este PR. Hay conflictos de gobierno/README/workflows en otros PR y se evitaron.
- Drive «Proyecto Centenario»: metadatos recientes desde 10-11, índice mínimo 10-11, Estado Maestro Operativo, adenda 12-11 launcher y Línea Base. Se conservan como fuentes/documentos históricos, no como HEAD vivo.
- Normativa institucional: NO aplicable, no se modifican cargos, permisos, firmas ni procedimientos.

## Entrega: EN CÓDIGO, NO INTEGRADA
- Issue #821; PR draft #1003; rama `docs/ai-continuidad-20261112-gpt` desde `dev` observado.
- Archivos nuevos únicamente:
  - `docs/coordination/AI-CONTINUITY-QUICKSTART.md`
  - `docs/coordination/AI-STATUS-EVIDENCE-CONTRACT.md`
  - `docs/handoffs/2026-11-12-chatgpt-ai-continuity.md`
- El primero reduce a seis pasos la entrada y enlaces estables; el segundo obliga a clasificar EN CÓDIGO/CI/RELEASE/QA FÍSICO/UAT/MAIN con fuente, lectura actual y autorización, un cierre de diez líneas y principios de no duplicación.
- No se tocó ningún archivo caliente, ni README/START-HERE/AGENTS/GOV/ADR/workflows, ni ramas ajenas.

## Gates y estados que se completan con evidencia
- CI/Showcase/QA Installable exact-head: **PENDIENTE de comprobar** tras el último commit de este PR; no declarar éxito por checks anteriores.
- Pages SHA/sourceSha: **UNKNOWN**; QA instalable SHA-256: **UNKNOWN**, ninguna versión nueva publicada/desplegada.
- QA FÍSICO: **NO INSPECCIONADO en esta sesión**; solo `glmdch` válido; `srv01` histórico.
- UAT: **UNKNOWN**. MAIN: **sin modificación por este trabajo**.
- GO físico: **NO AUTORIZADO por este PR documental**. No ejecutar G6, sudo ni despliegue.

## Siguiente paso y responsables
1. ChatGPT: comprobar readback de archivos/PR; consultar CI exact-head y HEAD vivo de `dev/main` antes de evaluar integración.
2. Si el PR puede fusionarse sin conflictos y gates SUCCESS, realizar squash solo a `dev` según autorización aplicable. No fusionar mientras falte evidencia. No actualizar START-HERE hasta PR documental post-merge que añada solo un enlace.
3. El siguiente agente: iniciar por `docs/coordination/AI-CONTINUITY-QUICKSTART.md` **una vez integrado en dev** y cotejar fuentes vivas. No asumir vigencia de este handoff.
4. Sponsor: sigue reservando aprobaciones de GO físico, main, producción e identidad institucional.

## URLs
- Issue: https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/821
- PR: https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/pull/1003
- Carpeta oficial Drive: https://drive.google.com/drive/folders/1P74Q8lhNPu6lHZ5zZR9ZD3AJFr_EtZyO
