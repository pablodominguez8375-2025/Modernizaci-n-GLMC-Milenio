# PMGM-SEC-470 — Aceptación de IP real en auditoría (laboratorio QA)

**15-10-2026 · Issue #470 · Preparación exclusivamente de solo lectura · NO certifica ejecución en srv01.**

## Estado del código encontrado (evitar duplicación)

- `backend/src/PMGM.Api/Infrastructure/TrustedProxyForwarding.cs` **ya configura** `X-Forwarded-For` con **1 o 2 IPs exactas**, sin redes CIDR de confianza, y rechaza loopback, wildcard e IPs repetidas.
- `backend/src/PMGM.Api/Program.cs` usa `app.UseForwardedHeaders()` **antes** de `UseAuthentication()` y `UseRateLimiter()`. `AuditEventFactory` escribe `HttpContext.Connection.RemoteIpAddress` en `audit_events` después de la normalización.
- `frontend/nginx.conf` pasa `proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for`; el gateway Caddy reenvía hacia el servicio `web`. Ruta pública: **cliente → Caddy (gateway) → nginx (web) → API**.
- La configuración opt-in `ForwardedHeaders__KnownProxies: ${PMGM_PILOT_TRUSTED_PROXY_IPS:-}` existe en `infrastructure/docker-compose.pilot.yml`. Si está vacía **la API NO confía en XFF**; auditaría la IP del socket (web/nginx), por seguridad. No inferir IP de cliente sin activación de confianza específica.
- Tests existentes `backend/tests/PMGM.Api.Tests/Audit/TrustedProxyForwardingTests.cs` ejercitan uno/dos saltos, IPs mapeadas, falsificación de cabeceras, límite de saltos y fallos de configuración.

## Chequeo read-only propuesto (nuevo)

`scripts/check-pilot-proxy-chain.py` inspecciona SOLO `docker compose config --format json`, contenedores **ya encendidos** `api`, `web`, `gateway`, y `docker inspect`. Funciona igualmente con overlay BD2 opt-in. Nunca crea/reinicia recursos, no guarda/expone variables de credenciales ni inspección JSON completa.

Ejemplo local para operador del **laboratorio autorizado** (no ejecutar en srv01 en pausa):

```sh
python3 scripts/check-pilot-proxy-chain.py \
  --env-file /ruta/privada/.env.pilot.local
# Si los proxies cambiaron, el operador puede SOLAMENTE visualizar propuesta local:
python3 scripts/check-pilot-proxy-chain.py \
  --env-file /ruta/privada/.env.pilot.local --show-required-local
# Para el overlay BD2 de NUEVO volumen agregar --bd2
```

Verifica: los tres contenedores están ejecutándose en **una sola red privada común**; IP IPv4 privada/unicast y distinta en los tres; `PMGM_PILOT_TRUSTED_PROXY_IPS` comprende **exactamente** las IPs actuales de `web/nginx` y `gateway/Caddy`; y la propia **API en ejecución** contiene esas IPs en `ForwardedHeaders__KnownProxies`. Esto detecta cambios de IP por recreación de Docker e impide certificar un archivo Compose cuya configuración **todavía no** está aplicada en el runtime.

**Estados del control:** `BLOCKED` si faltan servicios o hay incoherencia; `READY_FOR_AUDIT_ACCEPTANCE` si la topología y la configuración exacta están listas. Ambos tienen `scope=NO_QA_CERTIFICATION`. En fallo solo `--show-required-local` puede mostrar una sugerencia interna de dos IPs en la terminal autorizada; **no copiarla en GitHub/Drive ni pegar variables de entorno con secretos**.

## Aceptación final que queda pendiente

1. Con autorización institucional para un **QA aislado**, medir y validar la ruta cliente externa → Caddy → nginx → API; DNS/TLS, VPN/red autorizada y roles funcionales. **No desplegar en srv01** mientras #97 esté pausado.
2. Si la verificación de topología retorna `BLOCKED`, **el operador** revisa sus IPs exactas y actualiza `PMGM_PILOT_TRUSTED_PROXY_IPS` en archivo privado. La aplicación solo toma esta variable al ser recreada por un operador autorizado; este script **no** cambia/reinicia nada. Ejecutar nuevamente el chequeo.
3. Realizar una acción de prueba autenticada con un usuario **sintético** que ya emita un evento auditado (seleccionar acción y permisos según los endpoints existentes; no crear datos personales); consultar privadamente el campo `audit_events.ip_address` para esa acción/correlación y compararlo con la IP de salida conocida y autorizada del equipo de prueba, sin incluir IP completa, RUT, nombres ni tokens en tickets/Drive. Usar evidencias anonimizadas, fecha/ambiente/commit, acción y resultado PASS/FAIL. **Solo aquí** puede afirmarse IP real verificada.
4. Repetir prueba negativa desde un peer de red no registrado: una cabecera `X-Forwarded-For` falsa NO debe convertirse en IP de auditoría. La prueba funcional y de autenticación debe mantenerse, sin exponer a la red pública la API directamente.
5. Si Caddy/nginx cambia de IP al recrearse, el control queda INVALIDADO y se repite; no ampliar confianza a `0.0.0.0/0`, toda la subnet Docker ni confiar indiscriminadamente en cabeceras públicas.

**Puertas del proyecto:** no cerrar Issue #470 por este script, no dar aprobación de Ley 21.719 de forma automática, no tocar la PR #479 bloqueada de MinIO, ni `main` o `srv01`. `READY_FOR_AUDIT_ACCEPTANCE` no es evidencia de auditoría IP real. Registrar resultados reales únicamente después de ejecución institucional autorizada.

**Continuidad:** GitHub Issue #470, GOV-003 y Línea Base Drive.
