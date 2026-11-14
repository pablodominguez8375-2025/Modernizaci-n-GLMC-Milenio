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
| QA Silo supply and recovery | 43549863767 | intento 1 failure (`vendor_platform_and_license` HTTP 400 externo; archivos Silo sin cambio); **intento 2 success** | verde |
| Publish signed synthetic QA envelope | 43549863680 | success (intento 2; el intento 1 esperó 29 min y paró con `QA_VALIDATED_CUT_PUBLISHED_NEWER_PUSH_NOT_YET_GREEN` porque Silo seguía rojo) | release `qa-signed-3be7060e52aa02659d7ba502b8c5086f9a21ae5d` (prerelease): zip wrapper `sha256:c444d6041a8e53f4784e31a46d2d30dbb79917b3a425c7cdbe68809c52ad536e` (112401052 B) |

## 3b. Contenido de la release firmada (verificado off-host, solo lectura)
| Archivo | SHA256 |
|---|---|
| `Proyecto-Centenario-QA-srv01-3be7060e52aa.zip` (sujeto atestado = `app_archive_sha256`) | `b9ed44338af55b85df5f889cbb2cc297a7a959af0647fab02cf190c0341e500f` |
| `installable-provenance.sigstore.json` | `5e35b92d022404fd1e05a67c09a3fd533108f107c236d2206b014a49fc25fd32` |
| `…zip.verification.json` | `55304a233f9ff2bc9dfabd007812d7fc406eedf4945a5c156eaba61d45b30918` |

`gh attestation verify <zip interno> -R <repo> --bundle installable-provenance.sigstore.json` = exit 0: `sourceRepositoryDigest=3be7060e52aa02659d7ba502b8c5086f9a21ae5d`, `sourceRepositoryIdentifier=1356543647` (pin numérico), firmante `…/.github/workflows/qa-silo-rebuild.yml@refs/heads/dev` (idéntico al del corte 164a07ec). El zip wrapper NO lleva atestación propia (404 esperado): el sujeto atestado es el zip interno. Esta verificación es de consistencia off-host; la autoridad la fija el bootstrap root en glmdch.

## 4. Gates para UNA sola intervención física futura (todos deben cumplirse; si falla uno: STOP)
1. Release firmada `qa-signed-3be7060e…` PUBLICADA (ver 3b); re-verificar SHA del zip y atestación APP en el host (`gh attestation verify`, `source_repository_id` numérico).
2. Corte A/B regenerado para `3be7060e`; recibo OPS v3 **firmado por el Sponsor** (clave `qa525-ops`, huella `SHA256:DN64Bjj/7SMjP7LX9Nwrcp3lPRBpT2cKPs1NmaL1Y5k`, namespace `qa525-ops-v2@pmgm`). No se fabrica ni se asume firma.
3. Admisión/aprobación v2 con fin efectivo estrictamente `< 2026-11-15T16:54Z` (checkpoint G6 = `20261113T165400Z`+48h; ventana ≤30 min; igualdad = STOP).
4. Pins de evidencia G6 v2 entregados por el bootstrap root (no por el operador): review `f793a010…4e370`, receipt `f4c98b7e…fea9`, restore `65307e7a…a4a9`, archivo `b0c77c1c26ba48e1c6942abab3ea5d5b1a9aaa3228cbd65753bc274a775276fe`, repo `b4df75c5…65ce`, epoch `75870a7a926569c94e853a2a44f0a580`; `exports-v2/<archivo>` releído por FD bajo locks (root 0600 nlink 1) — el PASS stat/sha previo NO equivale a esta lectura.
5. KIT y GO NUEVOS (namespace `qa525-go-cut-v1@pmgm`); nunca reutilizar GO de `164a07ec` ni de `5e09d7b5`. Validar PIN/host/valores antes de gastar GO.
6. Alcance declarado WEB-only (o API+WEB sin migraciones): si el paquete firmado contiene migraciones o escrituras de BD/identidad, STOP.
7. Rollback de imágenes (API/WEB `6eed4f00`) presentes; rollback de BD NO requerido por no haber migraciones (el backup de 13-11 13:55 CLT no es contemporáneo: si hay datos reales posteriores que preservar, STOP y checkpoint nuevo).

## 5. Bloqueos actuales
- Firma OPS v3 del Sponsor: pendiente.
- Lectura FD de `exports-v2` en bootstrap root: pendiente de ChatGPT/PO.
- Ensayos negativos (STOP por corte/pin/expiración) sobre la cadena nueva: pendientes de generarse con la release publicada.

Este documento no concede autoridad: `may_apply=False` en todas las bibliotecas.
