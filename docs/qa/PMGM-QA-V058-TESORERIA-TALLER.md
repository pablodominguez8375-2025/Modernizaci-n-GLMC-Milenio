# PMGM QA v0.58 — Tesorería transaccional del Taller

## Alcance

El Tesorero del Taller dispone de un ámbito propio, separado de Secretaría y de Gran Tesorería. Puede parametrizar cuotas por vigencia, generar cargos mensuales para los hermanos activos, registrar abonos parciales y consultar cartola, saldos y proyección mensual.

## Reglas incorporadas

- Tipos iniciales parametrizables: cuota normal, estudiante y tercera edad.
- Cada plan conserva por separado el monto cobrado al hermano y el monto que corresponde a Gran Tesorería.
- Se impiden vigencias superpuestas para el mismo tipo de cuota.
- El cierre mensual genera una sola obligación por hermano y período.
- Los cargos conservan los montos vigentes al momento de su generación, aunque después cambie el plan.
- Los pagos pueden ser parciales, no pueden superar el saldo y generan un comprobante correlativo.
- La cartola muestra cargos, abonos, comprobantes y saldo acumulado.
- El resumen mensual informa por cobrar, recaudado, cuenta por cobrar, por pagar a Gran Tesorería, margen proyectado y semáforo.
- Todas las operaciones de escritura generan auditoría institucional.

## Validación QA

1. Ingresar a Gestión Logial y seleccionar un Taller.
2. Revisar los tres planes demostrativos: normal `$26.000/$21.000`, estudiante `$13.000/$11.000` y tercera edad `$16.000/$13.000` (Taller/Gran Tesorería).
3. Elegir el período y generar el cierre mensual.
4. Comprobar que el total esperado, la obligación a Gran Tesorería y el margen del Taller sean distintos y consistentes.
5. Verificar semáforo y contadores de pagados, parciales y pendientes.

Los montos indicados son datos ficticios de QA y no se consideran decreto institucional vigente.
