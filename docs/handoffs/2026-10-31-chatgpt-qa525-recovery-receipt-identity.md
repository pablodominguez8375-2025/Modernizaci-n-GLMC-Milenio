# Handoff — 2026-10-31 — QA525 #822: bind recovery receipts to signed cut

**Actor:** ChatGPT IA. **Branch:** `security/qa525-recovery-receipt-bind-20261031-gpt`. **Base exact:** `dev@ceeb70d5eee4fae2cd4dedb7ff02848aaca771bc`. **Main:** `6dfb9546a4873baff15955cf86abfd7d47e3d111`. **QA physical:** not accessed; no new deployment or root action.

## PRE — audit and improvement rationale

Read live GitHub open/merged PRs, Issue #822/#827, AGENTS §12, GOV-003 §16, existing `qa525-launcher-replacement-contract.py` and its tests, `qa525-launcher-atomic-sandbox.py`, signed-preflight and current QA operator. Drive delta: latest adenda 13 v0.190 and continuity document, no operative authorization. No other open PR changes the two target files. PR #823 was already merged, but does **not** provide the signed physical launcher installer.

**Gap:** `assess_interrupted_swap` validates the old and new launcher hashes in a durable receipt, but not the signed `source_sha`, `release_asset_sha256`, and single-use `nonce` that the sandbox already validates. A copied receipt for an otherwise identical binary could be accepted for a different signed cut. The function also only accepts a boolean `pending` without verifying the intent record identity. The existing results remain diagnostic, never installer authorization. Severity P1 for reusable policy correctness; P0 gate for any future root installer.

**Bounded change:** tighten the pure, no-I/O recovery contract to require exact signed release identity, nonce and matching intent/receipt when present; add adversarial regressions and document explicit migration of callers. Do not build, install, apply, archive or rollback on glmdch; no Docker, database, Compose, Keycloak, SMTP, EF100, API/WEB, credentials, main, srv01 or second VM. No physical install without complete independent Sigstore chain + host locks/journal/backup + separate approval.

## DURANTE / POST — to complete

Record exact changed files and commits, any failed test and correction, CI exact PR HEAD, final dev SHA if merged, publication proof status separate from CI, Drive adenda and readback. No local syntax test is claimed until executed or CI verifies it.

## DURANTE — cambios aplicados en rama, pendientes CI

1. PR DRAFT #829 creada desde `dev@ceeb70d5eee4fae2cd4dedb7ff02848aaca771bc`. Claim/alcance limitado a contrato puro + pruebas de QA525, sin conflictos de rutas detectados con los PR abiertos.
2. `scripts/qa/operators/qa525-launcher-replacement-contract.py`: `assess_interrupted_swap` exige argumentos obligatorios `source_sha`, `release_asset_sha256`, `nonce`, y acepta evidencia `intent` solo junto a `pending=true`; la evidencia de intent/receipt debe coincidir en los cinco identificadores (`source_sha`, `release_asset_sha256`, `nonce`, `old_launcher_sha256`, `new_launcher_sha256`). Rechazos específicos para identidad inválida, ausencia o suplantación de marcador, recibo falso y marcador incompatible. Sin llamadas de IO ni facultades de instalar.
3. `tests/test_qa525_launcher_replacement_contract.py`: los escenarios históricos ahora envían identidad de release completa; se añaden pruebas negativas de manipulación del source SHA, digest, nonce, hashes, marcador y recibo. Ninguna prueba atribuye firma real a diccionarios simulados.

**Commits provisionales:** `f32ed1819e205868d1f54c8883ea25d617cc5624` (claim), `2127c64366372dd4e511c7b6cb78e6db9a4c6d71` (policy), `1d5afa146eabb3f357c1add00e67a5d38658b248` (adversarial tests). Gates exact-head **PENDING**; no afirmar que los tests pasaron sin un run verificable. No hay nueva comprobación física de glmdch, release firmada o G6. El operador antiguo permanece fuera del alcance.

**Próximo paso:** consultar CI exact HEAD de PR #829; corregir fallos si existen, después integrar exclusivamente a `dev` conforme AGENTS §12 si los gates y permisos lo permiten. Cerrar registro GitHub ↔ Drive y mantener #822 OPEN: el instalador físico firmado transaccional y la aprobación real NO se sustituyen con esta mejora.
