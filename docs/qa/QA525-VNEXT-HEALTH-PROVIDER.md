# QA525 vNext — proveedor de salud pública (INACTIVO)

`scripts/qa/qa525-vnext-health.py` construye los dos callables que `qa525-vnext-runtime.build_reader` ya espera: `health_check()` y `served_web_revision()`. No contiene código de red: importa solo `json` y `re`; el único acceso al host es el `fetch(url) -> (status, final_url, body)` inyectado por el motor futuro.

## Contrato (replica el de los operadores existentes)
Origen fijo `https://sistema.granlogiamixta.cl`; estado exactamente 200 y `final_url == url` (una redirección es fallo); `/` (≤256 KiB), `/health/web` = `ok`, `/health/ready` = `{"status":"ready","database":"postgresql"}`, discovery OIDC del realm `pmgm` con `issuer` exacto y `authorization_endpoint`/`token_endpoint`/`jwks_uri` bajo `/identity/realms/pmgm/` del origen fijo (`build` rechaza cualquier otro origen), y `/qa-version.json` = exactamente `{"environment":"QA_SYNTHETIC","source_sha":<40 hex minúsculas>}` (≤2 KiB). JSON estricto (sin claves duplicadas ni NaN). Cualquier excepción, tipo incorrecto o cuerpo excesivo es fallo con código fijo; no se repite texto externo.

## Espera acotada (obligatoria en apply y rollback)
`wait_until_ready(check, clock=..., sleep=..., budget=180, retry=2)` devuelve `True` solo si `check()` devuelve exactamente `True` antes del plazo (≤600 s; reloj y sleep inyectados, sin importar `time`). Motivo (#525, 18-nov-2026): una sola sonda inmediata tras recrear falló la verificación de avance y la del rollback aunque el rollback físico fue correcto, porque la API tarda ~6 s en escuchar y no tiene healthcheck Docker. **Toda verificación posterior a una recreación (avance y rollback) debe usar esta espera**, nunca una sonda única. El plazo global y el reintento de 2 s quedan en el contrato; tras el plazo no hay más intentos.

## Límites
Solo prueba el contrato contra fakes. NO es una comprobación física, no autentica el origen ni la firma, y su resultado no autoriza APPLY. Falta el `fetch` real (sin redirecciones, con tiempo máximo y TLS verificado), que debe aportar el CLI físico revisado. Interfaz 4 del mapa de brechas de #525.
