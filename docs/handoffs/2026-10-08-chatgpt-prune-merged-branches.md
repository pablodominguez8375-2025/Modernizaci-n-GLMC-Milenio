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

Rama/PR preparados; el borrado físico debe comprobarse en GitHub Actions y no inferirse por la existencia del workflow.


## Implementación preparada — PR #381 (draft)

- Archivos añadidos en rama: `.github/scripts/prune-merged-branches.py`, `.github/workflows/prune-merged-branches.yml`, `tests/test_prune_merged_branches.py` y este handoff.
- Workflow en `pull_request` valida invariantes y ejecuta **solo dry-run** con credencial de lectura. En `push: dev` comprueba nuevamente y puede hacer DELETE con credencial `contents: write` acotada al job.
- No se añade `schedule` porque la rama predeterminada sigue siendo `main` y el Sponsor no autorizó modificarla; la ejecución automática sucede al integrar en `dev`.
- Se conservan `main`, `dev`, `release/*`, `uat/*`, `recovery/*`, `codex/*`, `claude/*`, ramas protegidas y cualquier rama con referencia operativa reconocible (`qa`, `uat`, `release`, `recovery`, `backup`, `srv01`, `rc`, `pilot`). También se excluyen ramas con PR abierto, PR no fusionado o head sin igualdad exacta.
- Mínimo 7 días desde el **último merge relacionado**, máximo 80 borrados por ejecución, checks nuevos inmediatamente antes de cada DELETE. No hay mecanismo GitHub de borrado atómico por SHA: queda un riesgo de carrera residual minimizado por la segunda comprobación.
- Primera simulación, **anterior a la ampliación de exclusiones operativas**: inventario 301 ramas, 287 PR, 183 candidatas; cero borrados. Nueve pruebas unitarias pasaron en GitHub Actions para el corte previo; repetir al HEAD definitivo antes de integrar.
- Drive: lista de carpeta oficial Proyecto Centenario leída el 08-10-2026 (98 elementos visibles); últimos modificados Línea Base Maestra y adendas Claude 08-10. No se detectaron nuevas fuentes normativas para esta automatización exclusivamente administrativa.

## Pendiente para cerrar

1. CI/Showcase/QA y validación dry-run del HEAD definitivo de PR #381 en SUCCESS.
2. Refrescar HEAD dev; pasar PR de draft a ready; fusionar por squash únicamente si el gate exact-head y gobernanza permiten.
3. Revisar ejecución posterior `push: dev`: registrar número real de ramas eliminadas y las conservadas, o el bloqueo `contents:write`, sin declarar borrado si no ocurrió.
4. Actualizar Issue #380 con recibo y mantener abierto mientras queden candidatas sin procesar. No tocar `main`, `srv01` ni UAT.
