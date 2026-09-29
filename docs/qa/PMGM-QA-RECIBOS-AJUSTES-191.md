# Recibos e imputaciones: correcciones y anulación de errores

Alcance confirmado por el Sponsor: corrección de imputación y anulación del registro erróneo; devolución real separada. PR #221 hacia dev. srv01 sigue pausado bajo #97; esta matriz no acredita ejecución física ni UAT.

| Caso | Resultado exigido |
|---|---|
| Corrección parcial a otro período del mismo hermano | Original conservado; contrapartida y nueva imputación; caja inalterada |
| Liberar a crédito | Neto aplicado disminuye; crédito disponible aumenta por la diferencia |
| Otro hermano/Taller/moneda | Rechazo completo, sin efectos parciales |
| Monto mayor al origen disponible o destino | Rechazo completo |
| Idempotencia | Repetición exacta devuelve el ajuste original; payload distinto produce conflicto |
| Anulación tras correcciones previas | Revierte remanentes positivos una vez; crédito neto cero |
| Doble anulación / reutilización de crédito anulado | Conflicto; sin otro ajuste ni movimiento |
| Recibo de ejercicio cerrado | Ajuste fechado en abierto; snapshot de cierre/conciliación anterior intacto |
| Fecha en cerrado o anterior al original/última operación | Rechazo; no se reabre período |
| Consulta anterior al ajuste | Conserva ingreso e imputación originales |
| Consulta posterior al ajuste | Refleja contrapartidas y un único ingreso negativo enlazado |
| Concurrencia ajuste/cierre o imputación | Transacciones serializables; conflicto/reintento seguro |
| UPDATE/DELETE de filas originales | PostgreSQL rechaza la operación |
| UI escritorio y móvil | Selección de recibo/origen/destino, motivo/fecha e historial; acciones sólo para Tesorería |

Pruebas automatizadas: `LodgeReceiptAdjustmentPostgreSqlTests` (requiere `PMGM_TEST_POSTGRES`), `lodgeReceiptAdjustments.test.ts`, `LodgeTreasuryPanel.test.tsx`. La prueba PostgreSQL retorna sin ejecutar el flujo si no hay variable de conexión; una suite local verde sin ella no es evidencia PostgreSQL. CI debe ejecutar el escenario contra su servicio PostgreSQL. Se conserva la matriz operacional de #97 para instalación/regresión/UAT cuando el Sponsor levante la pausa.
