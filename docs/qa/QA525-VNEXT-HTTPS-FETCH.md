# QA525 vNext – `fetch` HTTPS físico del proveedor de salud (INACTIVO)

`scripts/qa/qa525-vnext-https-fetch.py` entrega `fetch(url) -> (status, final_url, body)` para `qa525-vnext-health.build`. Es biblioteca: no hace nada al importarse, sin CLI. Solo corre cuando un punto de entrada físico futuro, revisado, lo llame. No autoriza instalar ni aplicar.

## Reglas (todas con prueba)
- Solo las 5 URL exactas que pide el proveedor (origen fijo `https://sistema.granlogiamixta.cl` + ruta fija). Sin consulta, fragmento, usuario, puerto ni otro host: `HTTPSFETCH_URL_NOT_ALLOWED`.
- TLS con la raíz de confianza del sistema, verificación de certificado y de nombre, TLS 1.2 o superior. Sin certificados de cliente, cookies ni credenciales.
- Sin redirecciones: un 3xx se devuelve tal cual con `final_url` = URL pedida y el proveedor lo rechaza.
- Tope de cuerpo 262144 bytes (`HTTPSFETCH_BODY_TOO_LARGE`) y plazo total 20 s también ante respuestas lentas (`HTTPSFETCH_DEADLINE`); conexión 8 s.
- Errores con código fijo (`HTTPSFETCH_TLS_FAILED`, `HTTPSFETCH_FAILED`, ...), sin URL, cabeceras, cuerpo ni mensajes del sistema. La conexión siempre se cierra.
- Sin proxy por variables de entorno, sin lectura de entorno, sin subprocesos.

## No cubre
Que la salida de red real de `glmdch` llegue al origen, la validez real del certificado, ni el punto de entrada físico. Se comprueba en el ensayo físico con GO del Sponsor.
