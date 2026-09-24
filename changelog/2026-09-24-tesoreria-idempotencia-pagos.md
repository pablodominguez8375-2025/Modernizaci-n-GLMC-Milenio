# 2026-09-24 — Idempotencia en pagos de Tesorería del Taller

## Cambio
- Se persiste una clave de idempotencia por cargo, con índice único para evitar pagos duplicados ante reintentos.
- Repetir la misma solicitud devuelve el comprobante y saldo del pago ya registrado; reutilizar la clave con datos distintos produce conflicto.
- La interfaz conserva la clave durante errores de red y genera una nueva al cambiar los datos del pago o seleccionar otro cargo.
- El gate de integración S3 usa S3Mock versionado y un bucket inicial; evita depender de imágenes Quay que el runner rechaza. La prueba valida las operaciones usadas por el almacén (subir, leer, consultar y borrar); MinIO de srv01 continúa pendiente de validación física.

## Control contable
Evita duplicar ingresos y emitir más de un comprobante cuando el servidor procesa un pago, pero el cliente no alcanza a recibir la respuesta.

## Verificación
Prueba de API de demostración incluida. CI exact-head pendiente; no representa despliegue en srv01 ni UAT.
