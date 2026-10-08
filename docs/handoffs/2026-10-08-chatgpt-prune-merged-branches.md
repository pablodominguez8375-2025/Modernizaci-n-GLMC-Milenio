# Handoff — limpieza automática conservadora de ramas históricas (08-10-2026)

## Autorización y reserva

- Sponsor autorizó el 08-10-2026 eliminar automáticamente las ramas históricas *verificadas* como fusionadas y sin trabajo pendiente.
- Issue #380, agente:chatgpt; rama `fix/cleanup-merged-branches-20261008-gpt`; PR draft hacia `dev`.
- Corte inicial: `dev@02dba0f9fec857aa811449f987b157f40e84a351`, `main@6dfb9546a4873baff15955cf86abfd7d47e3d111`.
- Comprobación inicial: PR #1 dev → main está abierto; no borrarlo ni afectar sus refs.
- Auto-delete nativo `delete_branch_on_merge=true` ya indicado en Issue #380 (no se asume que sane ramas históricas).
- `srv01` / UAT se mantienen pausados (Issue #97). No promover `main`.

## Contrato de seguridad previsto

1. Solo PRs realmente fusionadas contra `dev`; coincidencia exacta entre el commit actual de la rama y el head SHA del PR fusionado.
2. Excluir ramas `main`, `dev`, `release/*`, `uat/*`, `recovery/*`, `codex/*`, `claude/*`, referencias operativas y ramas protegidas.
3. No borrar ramas con PR abiertos ni ramas con trabajo agregado después de su merge.
4. Período de gracia de 7 días después del merge; inventario y validación justo antes del DELETE; tolerar desaparición concurrente sin error.
5. La ejecución de escritura ocurrirá exclusivamente en GitHub Actions tras `push` a `dev`; el conector de ChatGPT no tiene `delete-ref`.
6. Registrar resultados, candidatos y motivos de exclusión. Sin tocar archivos de aplicación, entornos QA, Pages ni `main`.

## Estado al iniciar

Registro inicial; implementación, pruebas, PR y ejecución de Actions pendientes de validación. Los SHA de dev deben refrescarse al cierre y fusionar únicamente con gates SUCCESS exact-head.
