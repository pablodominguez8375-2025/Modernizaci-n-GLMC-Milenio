# QA525 vNext – `fetch` HTTPS físico del proveedor de salud (INACTIVO)

`scripts/qa/qa525-vnext-https-fetch.py` entrega `fetch(url) -> (status, final_url, body)` para `qa525-vnext-health.build`. Es biblioteca: no hace nada al importarse, sin CLI. Solo corre cuando un punto de entrada físico futuro, revisado, lo llame. No autoriza instalar ni aplicar.

## Reglas (todas con prueba)
- Solo las 5 URL exactas que pide el proveedor (origen fijo `https://sistema.granlogiamixta.cl` + ruta fija). Sin consulta, fragmento, usuario, puerto ni otro host: `HTTPSFETCH_URL_NOT_ALLOWED`.
- TLS con la raíz de confianza del sistema, verificación de certificado y de nombre, TLS 1.2 o superior. Sin certificados de cliente, cookies ni credenciales.
- Sin redirecciones: un 3xx se devuelve tal cual con `final_url` = URL pedida y el proveedor lo rechaza.
- Tope de cuerpo 262144 bytes (`HTTPSFETCH_BODY_TOO_LARGE`), cabeceras 16384 (`HTTPSFETCH_HEADER_TOO_LARGE`).
- **Plazo total duro de 20 s** (`HTTPSFETCH_DEADLINE`): socket propio (sin `http.client`), y antes de cada conexión, handshake TLS, envío y lectura se fija `settimeout(tiempo restante)`. Un servidor que gotea bytes (cabeceras, cuerpo o handshake) no puede extender el plazo. Sin hilos, señales ni subprocesos. Probado con servidores reales de loopback que gotean/callan y tiempo de reloj real. Conexión TCP 8 s como máximo.
- Cliente HTTP/1.0 estricto, `Connection: close`: se rechazan `Transfer-Encoding`, `Content-Encoding` no identidad, `Content-Length` duplicado/no numérico/inconsistente, línea de estado o cabecera mal formada (`HTTPSFETCH_RESPONSE_INVALID`).
- Errores con código fijo (`HTTPSFETCH_TLS_FAILED`, `HTTPSFETCH_FAILED`, ...), sin URL, cabeceras, cuerpo ni mensajes del sistema. La conexión siempre se cierra.
- Sin proxy por variables de entorno, sin lectura de entorno, sin subprocesos.

## No cubre
La resolución DNS (`getaddrinfo`) no admite plazo desde Python: un futuro punto de entrada debe acotarla con un límite externo al proceso (p. ej. `timeout` del supervisor). Queda como residual documentado.
Que la salida de red real de `glmdch` llegue al origen, la validez real del certificado, ni el punto de entrada físico. Se comprueba en el ensayo físico con GO del Sponsor.
