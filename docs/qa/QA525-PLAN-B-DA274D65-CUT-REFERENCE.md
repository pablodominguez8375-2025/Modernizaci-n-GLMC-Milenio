# QA525 Plan B — cut de referencia `da274d65` (solo lectura)

**NO ES UN GO, UNA ORDEN DE EJECUCIÓN NI UN MANIFIESTO FIRMADO.** Registro de continuidad y de nombres/hashes con procedencia; la autoridad real de cualquier corte proviene del launcher y sus artefactos autenticados. No usar para poblar unilateralmente OPS/GO.

Fecha de reconciliación: 2026-11-15 (America/Santiago). Fuente de verdad: comentarios de Issue #525 posteriores a la integración del PR #1028.

| Objeto | Valor confirmado públicamente | Cómo se obtuvo / limitación |
|---|---|---|
| `dev` | `da274d651d8e9e056525413406c0aab62131504f` | `fetch_commit(dev)`; integrar #1028 |
| Release QA | `qa-signed-da274d651d8e9e056525413406c0aab62131504f` | GitHub release publicada; no significa instalada |
| Outer ZIP sha256 | `f869f3e12e048d70aaecf5e4e2f5b348d6378cf02aaa9fc0e5ee3243460f2e82` | Release asset, verificación Sigstore off-host reportada en #525 |
| Inner ZIP sha256 | `5c50a675f7f495a5c0d2c5934feaff9b30b4107ef702184ab688bd5c61548baa` | Contenido del outer firmado, #6896691098 |
| Policy hash | `ec3dbd693bd54144bb18c53d347987d0bfbaa251cde3db179b82d136a50e9837` | Recomputado desde POLICY real de cache.py del corte, #6897318661 |
| Plan launcher candidato | `8ad4b1a4e5935ce37a9bdd5932b917a86f7e9aad45961567957109b187040b09` | **CANDIDATO controlado**, 613 B, 9 campos, **sin firma/GO**; #6897846362 y #6898017005 |
| Plan **inválido** | `e2240ebdafe6e0e10bc32d9b2e8a0f09291946173eef63ef12970c1d096a27cf` | Error de transcripción de un dígito en `old_launcher_sha256`; **NUNCA USAR** |
| Shim previamente observado | `90d0f5ea2dfe6363c5301b1c2b1f5cad3d7791328580c59129c56731932de7b2` | Hash físico registrado previamente, requiere nueva verificación bajo locks en corte real |
| Shim transcrito incorrectamente | `90d0f5ea0dfe6363c5301b1c2b1f5cad3d7791328580c59129c56731932de7b2` | **INVÁLIDO**: la posición 9 correcta es 2 y no 0 |
| Kit PRE-4 compartido de GO total | `pre4-pack-da274d65-v3.0.1-prefirma.zip`, sha256 `d88006abadb25dc7e61a277a45b26b8097a84f823fc5c2c41e9c0ef3087d81e3` | Reconciliado fuera de host con Drive #6898185958; prefirma, **no es kit Plan B autorizado** |
| EF historial | 100 migraciones, sha256 `f227fe9c19b91e5ef6f99c9309bcb23c98963b26fa66b837ef4307826c28f44a` | Evidencia física previa; reusar mientras no exista drift |
| Plan B PR | #1029, commit `ffcc86347c02419d374b07c0c7a2c789e2491195` | DRAFT. 7 workflows SUCCESS exact-head; no merge ni GO |

## Errores recientes que no deben repetirse

1. **Falsos objetos equivalentes**: hash de operador ≠ hash de shim. Comparar el mismo objeto, en el mismo rol y mediante fuente independiente; un carácter equivocado cambia completamente el digest.
2. **Artefacto reemplazado**: versión v3.0 prefirma fue invalidada; v3.0.1 corrigió plan+manifest. No enviar, firmar ni usar la v3.0 antigua.
3. **Release y CI confundidos con QA físico**: ninguna verificación de GitHub sustituye imagen staged, journal/lock, G6 dual o instalación física.
4. **Autorizaciones cruzadas**: ni el GO LAUNCHER_ONLY consumido ni los hashes de un corte anterior autorizan PLAN B API_WEB.
5. **Duplicación innecesaria**: reutilizar EF100, CI de #1028 y controles ya PASS si no cambió la entrada. Rehacer únicamente el dato vencible: G6, GO, live state y locks en la ventana física.

## Ruta rápida que falta para certificar

**A. Preflight read-only consolidado:** confirmar en glmdch el SHA real del operador `--successor`, estado APPLIED+LIVE+HEALTHY, prepared/locks, staging firmado de ambas imágenes, firma del corte y G6 dual vigente con restore. Rechazo ante cualquier discrepancia. El auxiliar Plan B de #1029 comprueba consistencia de un snapshot, **no autentica la evidencia**.

**B. Cambio supervisado:** el único ejecutor válido es el entrypoint existente `qa525-update.py` con `--successor` y una ruta de revisión auténtica o `--review-and-run`, dentro del procedimiento firmado y con GO API_WEB nuevo. No preparar/ejecutar con el shim antiguo por inferencia; sin aprobación, no intentar.

**C. Cierre:** verificar journal APPLIED, digest de imagen, HTTPS ready/OIDC/support00, persistencia; rollback dual si falla, STOP_RECOVERY_REQUIRED si queda incierto. Un solo acta en Issue #525 y Drive.

Estado documental: **PLAN_B_NOT_READY_FOR_HOST**, porque falta evidencia física de instalación/preflight, staging, G6 y autoridad de aplicación. Este documento no toca host.

Fuente primaria: https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/525 ; documentación canónica: https://docs.google.com/document/d/1ZL50zwm1DLTrRs0mbjw7VXFemFk3T4M05cPwKH-KoeQ/edit
