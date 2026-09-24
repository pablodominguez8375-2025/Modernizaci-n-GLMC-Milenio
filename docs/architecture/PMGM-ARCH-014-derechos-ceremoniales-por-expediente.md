# PMGM-ARCH-014 — Derechos ceremoniales vinculados a la solicitud

## Decisión

El pago del derecho único se registra una vez en un libro transaccional asociado directamente a `CeremonyRequest`. El catálogo del Decreto N.º 1.759 es la autoridad del monto y vigencia. La regularidad mensual del Taller continúa siendo una condición separada.

## Montos oficiales vigentes desde 01-01-2026

| Tipo de trámite | Derecho |
|---|---:|
| Iniciación | $41.000 CLP |
| Aumento de salario | $31.000 CLP |
| Exaltación | $41.000 CLP |
| Afiliación | $26.000 CLP |
| Incorporación | $31.000 CLP |

## Modelo y control

- `CeremonyRightPayment` conserva monto, moneda, medio, fecha efectiva, comprobante, referencia, clave idempotente, actor y fecha de registro.
- Se permiten abonos hasta completar el monto; sobrepagos se rechazan. Los montos CLP deben ser enteros.
- Una repetición con la misma clave/contenido devuelve el comprobante original; reutilizar la clave con contenido distinto se rechaza.
- La autorización consulta el saldo derivado del libro; el frontend nunca es autoridad del monto ni del estado.
- Sólo Gran Tesorería registra y concilia el pago del derecho institucional. El Tesorero del Taller conserva su ámbito financiero local separado. Gran Secretaría consulta el saldo y autoriza cuando todos los requisitos estén habilitados.
- La respuesta de la cola muestra el monto, pagado, saldo y referencia al decreto, con acceso sujeto al rol/contexto ya permitido para la cola.
- La demo refleja el comportamiento con datos sintéticos y su propio libro efímero; no simula una confirmación bancaria.

## Auditabilidad

Cada pago genera evento de auditoría. La entidad de pago es append-only en el flujo de aplicación. Una reversa o corrección requiere un procedimiento contable separado; no se edita ni elimina silenciosamente el asiento.

## Límites

Los derechos de Afiliación/Incorporación están presentes en el tarifario aunque algunas solicitudes se tramiten mediante el dominio `AdmissionCase`. La integración completa de esos dos tipos debe validar su vínculo funcional vigente antes de reutilizar el endpoint de `CeremonyRequest`. El presente incremento aplica donde existe expediente ceremonial enlazado.
