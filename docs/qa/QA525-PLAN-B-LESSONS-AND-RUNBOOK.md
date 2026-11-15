# QA525 — Plan B manual: errores, soluciones y ruta corta (15-11-2026)

**Rama exclusiva de contingencia — NO_GO_APPLY**. No ejecutar ni modificar glmdch, dev, main, producción, migraciones, volúmenes ni identidad. Fuentes: Issue #525, PR #1028 y documento Drive Plan B.

## Registro de errores y mecanismos anti-repetición

| Síntoma | Causa comprobada | Corrección que funciona / acción preventiva |
|---|---|---|
| `BND_FRAME_MISMATCH` | Sender emitió `QA525-FD-V1:`, receptor real esperaba `QA525-BND-V1:`; se ensayó contra receptor equivocado | Sender corregido v2.9.2, ensayo E2E contra `root-entry+boundary-recv` reales; probar ambos marcos y nonce erróneo |
| Prepare bloqueado o diff >=300 | Baseline fija `6ff4ae0d` ya obsoleta; último APPLIED+live era `6eed4f00` | PR #1028 sucesor integrado `dev@da274d65`; baseline=último APPLIED+live+healthy; también preflight físico y stage_current deben respetarla; STOP si diff truncado |
| `release not found` | Se consultó mientras publicador exact-head aún IN_PROGRESS | Esperar conclusión del run y comprobar release una sola vez; no reejecutar CI ni volver a contar EF |
| ZIP rechazado como symlink | `O_NOFOLLOW` correctamente bloqueó enlace simbólico | Conservar protección; copiar archivo regular sin sustituciones y cotejar hash+Sigstore |
| Hash de shim no coincide con operador | Se compararon objetos distintos | Comprobar rol y manifest propio de cada archivo; nunca inferir necesidad de reinstalar |
| GO vencido o consumido | Intento fallido dejó GO caducado o el launcher lo consumió correctamente | GO nuevo, por fase y solo tras preflight; reconciliar journal/nonce/locks por mecanismo firmado, no borrar ni reintentar |
| G6 histórico fuera de edad | Evidencia anterior caducó | Verificar edad/restore/offhost en la ventana actual y renovar solo componente temporal autorizado |
| Plan B de solo lectura confundido con instalador | Kit v1.2 no incluye APPLY | No afirmar que el Plan B despliega hasta que exista entrada manual auténtica, staging BUILD_ONLY y prueba física |

## Qué funcionó y se reutiliza

- PR #1028 ya está **fusionado** en `dev@da274d651d8e9e056525413406c0aab62131504f` y los gates exact-head se reportaron SUCCESS. No repetir implementación del sucesor ni 44 pruebas ya aceptadas sin cambios en el código.
- Release exacta `qa-signed-da274d651d8e9e056525413406c0aab62131504f`, outer SHA256 `f869f3e12e048d70aaecf5e4e2f5b348d6378cf02aaa9fc0e5ee3243460f2e82`, Sigstore comprobada fuera del host por Claude. **Firma en host y GO físico aún pendientes**.
- EF100 canónico en servidor, sha256 `f227fe9c19b91e5ef6f99c9309bcb23c98963b26fa66b837ef4307826c28f44a`. No repetir inventario EF salvo drift.
- Sender y receptor real compatibles bajo `QA525-BND-V1:`; rechazo `O_NOFOLLOW`, locks, journal y rollback dual son controles que deben mantenerse.
- No repetir descubrimiento de rutas y archivos registrados en #525: verificar vigencia/identidad solo cuando la operación concreta lo exija.

## Activación mínima: 3 fases y un intento

1. **ADMITIR (una hoja):** congelar target SHA+release digest; validar autoridad OPS/APP, Signature/Sigstore, preflight físico y baseline último APPLIED=API=WEB=healthy, prepared/locks, G6 restore+vigencia, EF pin sin recalcular, staging confirmado y protección sender↔receptor **REAL**. No emitir GO si hay discrepancia.
2. **CAMBIAR:** BUILD_ONLY autenticado y digests/layers exactos; usar **exclusivamente** entrada manual soportada y verificada del ejecutor sucesor ya integrado. Emitir GO `API_WEB_QA_ONLY` nuevo, único y vigente al final. Un solo cambio; nunca `docker compose up` ad hoc, `git pull` ambiguo, scripts de biblioteca directamente o `--apply-prepared` heredado por inferencia.
3. **CERRAR:** journal APPLIED, SHA/digests exactos, health/OIDC/support00, persistentes intactos. Ante fallo rollback dual autorizado a baseline física; si resultado incierto `STOP_RECOVERY_REQUIRED`. Una única acta GitHub+Drive sin secretos.

## Desarrollo paralelo reutilizable

El auxiliar `scripts/qa/qa525-planb-evidence-gate.py` compara **un único snapshot offline** recibido de un colector seguro previamente autenticado. No lee datos de host, no construye ni verifica firmas y **jamás concede GO**. Su salida es `CONSISTENT_READ_ONLY_NO_GO` o `BLOCKED_READ_ONLY_NO_GO`, y siempre `may_apply=false`. Si el JSON no es auténtico, una salida consistente no acredita nada. Los tests son puramente unitarios; no equivalen a QA físico.

**Pendientes:** modo manual real y CLI revisada del sucesor, integración del preflight físico y baseline dinámica, montaje BUILD_ONLY en host, pins OPS/APP/GO exactos y policy_hash independiente, G6 vigente/rollback, smoke físico. No se permite que el auxiliar sea la autoridad del launcher.

**Regla de continuidad:** toda falla nueva debe quedar registrada con síntoma, causa, cambios mínimos, test adversarial que la reproduce, test que la evita, evidencia SHA y estado físico; referenciar lo ya PASS en lugar de repetir. No usar memoria del chat como fuente de verdad.

Referencias: https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/525 ; https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/pull/1028 ; https://docs.google.com/document/d/1ZL50zwm1DLTrRs0mbjw7VXFemFk3T4M05cPwKH-KoeQ/edit

## Revisión 15-11-2026 — anti-replay, custodia y CI del Plan B

**Fallo encontrado en nuestra primera implementación:** el auxiliar offline aceptaba `g6_valid_now=true` sin registrar cuándo se capturó la evidencia ni hasta cuándo duraba el G6. Un snapshot viejo podía parecer válido. **Corrección:** schema ahora exige `collected_at_unix` (captura de máximo 300 segundos, tolerancia de reloj 30 segundos) y `g6_expires_at_unix` (más de 300 segundos restantes). Se rechazan tipos no enteros, relojes futuros y evidencia caducada. **Importante:** estos valores no autentican un JSON: el colector físico autorizado debe producirlos y autenticarlos; no usar este auxiliar como autoridad de GO.

**Fallo encontrado:** `json.loads` acepta claves duplicadas y la última puede sustituir una anterior. **Corrección:** `object_pairs_hook=no_duplicate_keys`; cualquier campo repetido ⇒ STOP. Cobertura adversarial agregada.

**Fallo encontrado:** la lectura original por `Path.open` seguía enlaces simbólicos. **Corrección:** `os.open(... O_NOFOLLOW | O_CLOEXEC)` + `fstat` archivo regular, un solo hardlink y máximo 32 KiB; cerrar FD incluso con fallo. Pruebas de archivo regular y symlink. Nunca ejecutar este chequeo con root.

**Mecanismo reutilizable:** workflow enfocado `.github/workflows/qa525-planb-offline-tests.yml` corre `py_compile` y unittest de este auxiliar, sin dependencias, host ni secretos. CI de ese workflow en commit exacto `009b0e53d3fb6e54be7bc4e79806597cb408af22`: **SUCCESS**, run `43735945535`. El workflow genérico `QA readonly contract tests` no ejecutaba nuestro test y NO debe confundirse con esta evidencia.

**Punto de entrada de código REAL:** `scripts/qa/operators/qa525-update.py` en dev@da274d65 acepta `--successor` únicamente con `--run-reviewed /ruta/absoluta` o `--review-and-run`; inyecta el sucesor dentro del lock y luego reutiliza `--prepare-reviewed` y `--apply-supervised` del entrypoint preexistente. La existencia de la CLI en GitHub **no certifica** que el binario/operador instalado en glmdch sea idéntico o que ya exista un GO. Nunca ejecutar el comando con opciones ficticias ni sin GO y recovery aprobados.

**Decisión:** conservar PR #1029 DRAFT, solo control auxiliar read-only y documentación de errores. El siguiente trabajo Plan B es verificar interfaz sucesora físicamente con el manifiesto firmado del SHA objetivo, staging BUILD_ONLY y G6 dual vigente en una ventana supervisada, sin rehacer CI/EF/release ya PASS. No crear automatizador alternativo ni alterar el GO total.

