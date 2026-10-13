# PMGM-SEC-470 — IP real de auditoría con proxies de confianza (fase 2)

Estado: implementado en PR draft #478, **no habilitado automáticamente en instalaciones existentes**. Ámbito: piloto HTTPS de Keycloak (cliente → Caddy/gateway → nginx/web → API) y middleware .NET 10. Relacionado Issue #470, controles contra suplantación y separación posterior de credenciales.

## Modelo de confianza

- Caddy es el único punto de entrada externo HTTPS. Su proxy inverso no debe confiar en `X-Forwarded-For` recibido de clientes no autenticados como proxy; mantener su tratamiento seguro predeterminado y verificarlo en cada actualización. Nginx en la red privada **agrega** su peer (`$proxy_add_x_forwarded_for`) al valor recibido de Caddy.
- La API usa `UseForwardedHeaders` **antes** de autenticación y `UseRateLimiter`; `AuditEventFactory` y el limitador usan `Connection.RemoteIpAddress`.
- Se interpreta **exclusivamente** `X-Forwarded-For`; no aceptar `X-Forwarded-Host` ni `X-Forwarded-Proto` para sobrescribir hostname/esquema.
- La API solo procesa los últimos 1–2 saltos si el socket remoto coincide con una de las **IPs exactas autorizadas**. Cada IPv4 explícita se admite tanto en forma IPv4 como en la representación IPv6 mapeada `::ffff:x.y.z.w` que puede utilizar Kestrel; ambas representan **la misma dirección individual**, no una red. Se limpian confianzas implícitas (incluido loopback). El procesamiento se detiene ante un proxy intermedio no registrado. No se aceptan redes CIDR arbitrarias ni el rango de Docker completo.
- `ForwardedHeaders__KnownProxies` ausente/vacío = middleware inerte, se registra la IP del socket y el `X-Forwarded-For` del usuario no altera auditoría. Valores malformados fallan el arranque deliberadamente, no habilitan confianza amplia.

## Configurar el piloto de manera controlada

La configuración opcional en `infrastructure/docker-compose.pilot.yml` expone `ForwardedHeaders__KnownProxies: ${PMGM_PILOT_TRUSTED_PROXY_IPS:-}`. El valor debe tener el formato `IP_WEB,IP_GATEWAY` (exactamente esas dos direcciones para el piloto). No copiar el ejemplo a un entorno real sin comprobar las IPs efectivas.

1. Con el stack piloto iniciado y sin configurar confianza todavía, obtener los contenedores con `docker compose --env-file "$PMGM_PILOT_ENV_FILE" -f infrastructure/docker-compose.pilot.yml ps -q web gateway`. Desde el host Docker inspeccionar los contenedores correspondientes con `docker inspect -f '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}' CONTAINER_ID`. Verificar que ambos IP pertenecen a la red **privada** del proyecto y que el orden es **web primero, gateway después**.
2. Añadir `PMGM_PILOT_TRUSTED_PROXY_IPS=IP_WEB,IP_GATEWAY` al **archivo de entorno seguro local**, nunca a Git. Reiniciar **solo** la API mediante Compose, tras comprobar la red real y bajo procedimiento de despliegue autorizado. Repetir la inspección al recrear containers/red; IPs Docker dinámicas pueden cambiar.
3. Verificar con tráfico HTTPS real a través de Caddy que una acción autenticada auditable registra la IP del cliente en `core.audit_events."IpAddress"` y que la partición de rate limit sigue utilizando la IP normalizada para solicitudes anónimas. No consultar, imprimir ni copiar metadatos personales ajenos. Usar solo identidades y datos sintéticos de QA.
4. Intentar la misma operación con `X-Forwarded-For` fabricado desde un cliente **no confiable**; verificar que **no altera** la IP registrada. También comprobar que un acceso directo a la API desde peer no incluido en `KnownProxies` (si el entorno de pruebas aislado permite ese acceso) ignore el header falsificado.
5. Si falla cualquiera de las verificaciones, retirar `PMGM_PILOT_TRUSTED_PROXY_IPS` y reiniciar la API para volver a registrar la IP del socket sin confiar en el encabezado. No registrar un origen como IP real hasta evidencia de la prueba.

## Verificación automatizada e incompatibilidades conscientes

La prueba `backend/tests/PMGM.Api.Tests/Audit/TrustedProxyForwardingTests.cs` ejecuta el middleware real de ASP.NET Core y el factory de auditoría: cadena de dos proxies, eliminación de prefijos falsos más allá de `ForwardLimit`, cliente externo con XFF malicioso, ausencia de confianza, proxy intermedio desconocido, rechazo de configuración amplia/duplicada/loopback, y preservación de Host/esquema.

Esta fase **no prueba** IP real en QA física ni configura IPs de contenedores de manera automática; CI no conoce la red del operador. No habilita privilegios DB, no crea cuentas MinIO y no actualiza `main` ni `srv01`. La aceptación del criterio de Issue #470 exige observación operativa después de la autorización de despliegue; continúa abierta junto con segregación PostgreSQL/MinIO. Los tests que usan un `ForwardedHeadersOptions` sintético no sustituyen la comprobación del borde real Caddy–nginx.
