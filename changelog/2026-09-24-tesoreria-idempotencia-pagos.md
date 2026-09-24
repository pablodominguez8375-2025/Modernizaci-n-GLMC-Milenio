# 2026-09-24 — Idempotencia en pagos de Tesorería del Taller

## Cambio
- Se persiste una clave de idempotencia por cargo, con índice único para evitar pagos duplicados ante reintentos.
- Repetir la misma solicitud devuelve el comprobante y saldo del pago ya registrado; reutilizar la clave con datos distintos produce conflicto.
- La interfaz conserva la clave durante errores de red y genera una nueva al cambiar los datos del pago o seleccionar otro cargo.
- Los jobs CI de S3 usan S3Mock 5.1.0 con bucket inicial, evitando pulls de Quay denegados en los runners. La suite verifica subida, lectura, consulta y borrado; los smoke funcionales usan el mismo servicio de prueba. El compose normal de despliegue conserva MinIO; su validación física en srv01 sigue pendiente.

## Control contable
Evita duplicar ingresos y emitir más de un comprobante cuando el servidor procesa un pago, pero el cliente no alcanza a recibir la respuesta.

## Verificación
Prueba de API de demostración incluida. CI exact-head pendiente; no representa despliegue en srv01 ni UAT.
