# QA525 — Inventario del corte `dev@3be7060e` (off-host, sin autoridad)

Estado: BORRADOR de preparación (contrato #1008 comentarios 6870797018 / 6870883320). NO es GO, NO autoriza ejecutar nada en glmdch, NO contiene firma del Sponsor. `main` intacta `6dfb9546`.

## 1. Corte
- dev = `3be7060e52aa02659d7ba502b8c5086f9a21ae5d` (squash de #1011 sobre `164a07ec`).
- QA físico hoy (registro #525): launcher nuevo `67f0c393…` (GO LAUNCHER_ONLY 12-11); API/WEB en `6eed4f00b7917da3cca36bbda70e16d16328648d`.

## 2. Delta real instalado → candidato (`6eed4f00..3be7060e`, 93 commits)
| Zona | Archivos | Efecto en runtime QA |
|---|---|---|
| `backend/` | **0** | API: sin cambio de código |
| `database/` y migraciones | **0** | **Sin migraciones; sin cambio de BD** |
| `release/`, Keycloak/realm/tema | **0** | Sin cambio de identidad |
| `infrastructure/` | 1 (`silo/Dockerfile.qa-rebuild`) | Solo CI de reconstrucción Silo; no se despliega en QA |
| `frontend/` | 12 (todo en `frontend/src/`) | **WEB**: UI v0.191 (#988 Gestor Documental) y v0.192 (#992 Sistema › Talleres) |
| `scripts/`, `tests/`, `docs/`, `.github/` | 74 / 90 / 51 / 2 | Herramientas y CI; no son runtime de la aplicación |

Conclusión técnica del delta: **el cambio de aplicación es solo WEB** (12 archivos de `frontend/src`, ninguno de autenticación/OIDC/tokens; `pmgmApi.ts` solo cambia el perfil demo con `useMocks`). No hay migraciones, ni escrituras de BD o identidad. Esto cumple la condición de la «decisión provisional de riesgo» de ChatGPT (#1008 6870797018) para una primera actualización sin migraciones. Lo debe confirmar el verificador firmado del paquete (no este documento).

## 3. Artefactos exact-head `3be7060e` (GitHub Actions)
| Workflow | Run | Resultado | Artefacto / digest |
|---|---|---|---|
| QA srv01 Installable | 43549863707 | success | `proyecto-centenario-qa-srv01-3be7060e…` id 14095489123 `sha256:753ca4eaed80465cb93e83f3babc10be2b8005a96907d2fe7d9ed78dd6701e65` |
| Pre-UAT Installable | 43549863655 | success | id 14095244520 `sha256:dedcfd047446aec8aa489c47cd1d0030a760d2a816920f7470309526ba5e46f2` |
| PMGM Showcase Demo | 43549863620 | success | github-pages id 14096286909 `sha256:4ad01ccb22240df2827df37a349253233fe78b6cea5b6efa6f28d158bd1b976d` |
| PMGM CI / HTTPS-OIDC / auto-agent readiness | 43549863718 / 43549863646 / 43549863661 | success | — |
| QA Silo supply and recovery | 43549863767 | **failure** `vendor_platform_and_license` HTTP 400 (consulta externa al vendor; archivos Silo sin cambio en el delta); un reintento solicitado | no bloquea el paquete QA, pero debe estar verde o explicado antes del GO |
| Publish signed synthetic QA envelope | 43549863680 | en curso al redactar | release `qa-signed-3be7060e…` (zip + atestación Sigstore): **pendiente** — fijar SHA del zip cuando publique |

## 4. Gates para UNA sola intervención física futura (todos deben cumplirse; si falla uno: STOP)
1. Release firmada `qa-signed-3be7060e…` publicada; SHA del zip y atestación APP verificados offline (`gh attestation verify`, `source_repository_id` numérico).
2. Corte A/B regenerado para `3be7060e`; recibo OPS v3 **firmado por el Sponsor** (clave `qa525-ops`, huella `SHA256:DN64Bjj/7SMjP7LX9Nwrcp3lPRBpT2cKPs1NmaL1Y5k`, namespace `qa525-ops-v2@pmgm`). No se fabrica ni se asume firma.
3. Admisión/aprobación v2 con fin efectivo estrictamente `< 2026-11-15T16:54Z` (checkpoint G6 = `20261113T165400Z`+48h; ventana ≤30 min; igualdad = STOP).
4. Pins de evidencia G6 v2 entregados por el bootstrap root (no por el operador): review `f793a010…4e370`, receipt `f4c98b7e…fea9`, restore `65307e7a…a4a9`, archivo `b0c77c1c26ba48e1c6942abab3ea5d5b1a9aaa3228cbd65753bc274a775276fe`, repo `b4df75c5…65ce`, epoch `75870a7a926569c94e853a2a44f0a580`; `exports-v2/<archivo>` releído por FD bajo locks (root 0600 nlink 1) — el PASS stat/sha previo NO equivale a esta lectura.
5. KIT y GO NUEVOS (namespace `qa525-go-cut-v1@pmgm`); nunca reutilizar GO de `164a07ec` ni de `5e09d7b5`. Validar PIN/host/valores antes de gastar GO.
6. Alcance declarado WEB-only (o API+WEB sin migraciones): si el paquete firmado contiene migraciones o escrituras de BD/identidad, STOP.
7. Rollback de imágenes (API/WEB `6eed4f00`) presentes; rollback de BD NO requerido por no haber migraciones (el backup de 13-11 13:55 CLT no es contemporáneo: si hay datos reales posteriores que preservar, STOP y checkpoint nuevo).

## 5. Bloqueos actuales
- Release firmada del corte aún publicándose; Silo supply en rojo por 400 externo (reintento en curso).
- Firma OPS v3 del Sponsor: pendiente.
- Lectura FD de `exports-v2` en bootstrap root: pendiente de ChatGPT/PO.
- Ensayos negativos (STOP por corte/pin/expiración) sobre la cadena nueva: pendientes de generarse con la release publicada.

Este documento no concede autoridad: `may_apply=False` en todas las bibliotecas.
