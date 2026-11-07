# QA525 vNext – hijo acotado con plazo externo (INACTIVO, residual DNS de #900)

`scripts/qa/qa525-vnext-bounded-child.py` ejecuta **un** proceso hijo fijo con un plazo duro **fuera del proceso**. Cierra el residual de `qa525-vnext-https-fetch.py`: sus sockets tienen plazo, pero `getaddrinfo` (DNS) no. No hay ejecutable ni ruta por defecto: un punto de entrada raíz futuro, revisado y firmado, los entrega. No ejecuta Docker, no toma locks y no autoriza nada.

## Garantías (todas con prueba y procesos reales)
- argv validado (ejecutable absoluto, sin `..`, cadenas simples acotadas), `shell=False`, stdin = `/dev/null`, `cwd=/`, **entorno mínimo fijo** (no hereda proxies, tokens ni credenciales).
- El hijo corre en su propia sesión/grupo. Ante timeout, exceso de salida o error se mata **todo el grupo** (SIGKILL) y se recoge al líder; tras una salida normal también se mata el grupo antes de recoger, para no dejar procesos huérfanos. Un hijo bloqueado (como un DNS colgado), un goteo lento o una inundación de stdout/stderr terminan en `BOUNDED_TIMEOUT` / `BOUNDED_OUTPUT_TOO_LARGE`.
- stdout acotado; stderr se drena, se cuenta y **se descarta** (nunca se devuelve ni se cita). Todos los errores son códigos fijos (`BOUNDED_*`, o `HTTPSFETCH_*` validado por regex), sin argv, rutas ni texto del hijo.

## `fetch_via_child`
Adapta el contrato `fetch(url) -> (status, final_url, body)` del proveedor de salud: solo las 5 URL permitidas; lanza `[python, -I, child, url]` y exige un documento JSON exacto (`status`, `final_url`, `body` en base64, tope 262144). `qa525-vnext-https-fetch-child.py` es la entrada del hijo: no lee entorno, usa solo la biblioteca de fetch, imprime una línea JSON y sale 0, 2 (código fijo) o 3 (sin salida).

## Límites
- Matar un grupo de procesos **no es un cgroup**: un hijo que haga `setsid` escapa. El punto de entrada real debe correr esto dentro de la unidad transitoria de `qa525-vnext-containment`.
- El plazo externo debe ser mayor que el interno del fetch (20 s) para que el error propio salga primero; el externo es la red de seguridad contra DNS.
- No prueba red real ni CA/hostname desde glmdch; eso es el ensayo físico con GO del Sponsor.
