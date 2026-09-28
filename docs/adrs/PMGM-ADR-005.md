# PMGM-ADR-005 — Libros separados CLP/USD y recepción en Perú

## Estado
Alcance aprobado por Product Owner el 28-09-2026; implementación en PR hacia `dev` pendiente de CI exact-head.

## Contexto y fuentes
El Product Owner aprobó registrar por separado saldos y libros en CLP y USD, sin conversión automática, y permitir que el componente local del Taller peruano se configure en USD separado del aporte oficial. También definió que Gran Tesorería confirma la recepción del abono en la cuenta bancaria en USD y, con esa conformidad, la Logia queda al día en sus compromisos financieros.

El Decreto N.º 1.759 fue emitido el 15-12-2025, rige desde el 01-01-2026 y fija para Perú sólo la cuota ordinaria de USD 6. No define tarifas USD peruanas para cónyuge, estudiante o tercera edad ni un tipo de cambio. El Manual de Usuario — Módulo Tesorería, versión 2026, actualizado el 21-09-2026, separa la fecha real de recepción de la imputación por período de cuota.

Fuentes Drive consultadas: [Decreto N.º 1.759](https://drive.google.com/file/d/1qsXM3CPAJw9jW1EHk3YjbNinHnexRub2/view) y [Manual de Tesorería 2026](https://drive.google.com/file/d/1tOHHXW5d4hv57GckDm3yVSme8NkdEfgf/view). Decisión PO registrada en Issue #190 y PR #154; PR #154 es una propuesta documental sobre base obsoleta y no se integra.

## Decisión
- Cada monto, operación, reporte, configuración de apertura, conciliación y cierre contable se identifica con `CLP` o `USD`.
- Los saldos, cartolas, reportes, conciliaciones y cierres se consultan por libro. No se suman monedas, convierten, netean ni imputan tasas de cambio.
- El Oriente peruano registra en USD la cuota oficial ordinaria de USD 6 y el total cobrado al Hermano; el componente local se calcula por separado como diferencia entre total de Taller y aporte oficial. Las categorías peruanas sin tarifa oficial aplicable permanecen bloqueadas hasta nueva fuente aprobada.
- El Cuadro de pago a Gran Tesorería usa la moneda del Taller. Para Perú aplica USD 6 a la cuota ordinaria. Sólo Gran Tesorería puede confirmar recepción bancaria al conciliar un Cuadro cuadrado. El evento registra actor y fecha y actualiza la regularidad financiera del Taller.
- El autoservicio del Hermano presenta saldos agrupados por moneda, y nunca presenta un total combinado cuando existen varias monedas.
- Los registros históricos reciben la moneda de compatibilidad CLP en la migración; no se cambia ni recalcula su valor histórico.

## Consecuencias y límites
Se amplía el modelo de Tesorería existente; no se crea un subsistema paralelo ni permisos nuevos. El cargo del Hermano sigue siendo un único total que conserva separado el componente institucional/local en el plan y la cartola. No se decide una política de anticipos, reversos o correcciones contables distinta de la que ya está documentada. QA automatizada no equivale a instalación, UAT ni aceptación institucional. La pausa de `srv01` (Issue #97) permanece.

## Trazabilidad
- Issues #190 y #191.
- PR #154 (histórica; no integrar).
- Decreto N.º 1.759 (Drive ID `1qsXM3CPAJw9jW1EHk3YjbNinHnexRub2`).
- Manual Tesorería 2026 (Drive ID `1tOHHXW5d4hv57GckDm3yVSme8NkdEfgf`).
