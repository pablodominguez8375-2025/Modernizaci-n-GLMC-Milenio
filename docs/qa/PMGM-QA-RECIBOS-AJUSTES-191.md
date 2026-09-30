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
| Doble anulación concurrente | Un ajuste confirmado, otro conflicto; un solo ingreso negativo y auditoría; reintento seguro |
| Dos ajustes con la misma clave idempotente | Una creación y un conflicto serializable; ambos reintentos recuperan el mismo ajuste sin duplicar caja/auditoría |
| Correcciones concurrentes que excederían el origen | Sólo una confirma; importe original intacto, neto y crédito consistentes; segundo reintento en conflicto |
| Concurrencia ajuste/cierre o imputación | Criterio adicional pendiente: transacciones serializables; conflicto/reintento seguro |
| UPDATE/DELETE de filas originales | PostgreSQL rechaza la operación |
| UI escritorio y móvil | Selección de recibo/origen/destino, motivo/fecha e historial; acciones sólo para Tesorería |

Pruebas automatizadas: `LodgeReceiptAdjustmentPostgreSqlTests` y `LodgeReceiptConcurrencyPostgreSqlTests` (ambas requieren `PMGM_TEST_POSTGRES`), `lodgeReceiptAdjustments.test.ts`, `LodgeTreasuryPanel.test.tsx`. La barrera sincroniza las lecturas PostgreSQL de dos ajustes dentro de transacciones serializables, sin cambiar código del producto ni usar sleeps para provocar la carrera. Comprueba originales, saldos, auditoría única y reintentos. La prueba de historia agrega DELETE directo de recibo, imputación y ajuste rechazado por PostgreSQL. Las carreras ajuste/cierre o imputación permanecen como criterios adicionales; esta prueba fuerza carreras entre ajustes. Las pruebas PostgreSQL retornan sin ejecutar el flujo si no hay variable de conexión; una suite local verde sin ella no es evidencia PostgreSQL. CI debe ejecutar los escenarios contra su servicio PostgreSQL; confirmar evidencia de ejecución, no sólo compilación. Se conserva la matriz operacional de #97 para instalación/regresión/UAT cuando el Sponsor levante la pausa.
