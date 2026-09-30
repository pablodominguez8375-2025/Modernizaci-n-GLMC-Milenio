# PMGM-ARCH-012 — Navegación operativa de Tesorería por cargo

## Decisión

Tesorería del Taller y Gran Tesorería se presentan como espacios funcionales separados, cada uno con un único acceso principal y navegación interna orientada a tareas. No se exponen como entradas laterales independientes las subfunciones del mismo cargo.

## Tesorería del Taller

El menú `Tesorería` reúne:

1. **Resumen:** caja acumulada a la fecha, ingresos y egresos del mes en curso, compromisos con Gran Tesorería y estado de cobranza.
2. **Cuotas y Cobranzas:** saldos individuales por hermano, historial por período, morosidad de años anteriores, pagos adelantados, pagos acumulados y acción directa para registrar una recepción e imputarla a uno o más períodos.
3. **Ingresos y Egresos:** libro de caja para ingresos varios y egresos. Al registrar un pago desde Cuotas y Cobranzas, se crea un único ingreso contable de categoría **Ingreso por pago de cuotas**, con fecha efectiva de recepción; la imputación al período antiguo/futuro actualiza el submayor del hermano sin duplicar la caja. El egreso afecta el saldo sólo una vez autorizado por el Venerable Maestro.

El historial del hermano distingue período moroso, cuota vigente, período futuro pendiente y período futuro abonado. El año/fecha de recepción determina el ejercicio de caja; el año/período aplicado determina qué obligación queda saldada. El reporte de morosidad se consulta por hermano y período; los anticipos futuros se muestran por separado.

Una recepción se conserva como un movimiento único de caja, vinculado a un miembro, Taller, moneda, fecha efectiva, comprobante e idempotencia. Sus imputaciones son asignaciones separadas a cargos de uno o más períodos. La suma imputada no puede exceder la recepción ni el saldo de cada cargo; la diferencia queda como crédito disponible. Imputar después ese crédito reduce la deuda correspondiente, pero no genera otro movimiento de caja. La UI de Tesorería expone el registro multiperíodo y la aplicación posterior del crédito. Esto define el comportamiento operativo y no asigna una cuenta ni clasificación financiera oficial para anticipos o recuperaciones.

Las recepciones e imputaciones conservan auditoría y no reescriben ejercicios cerrados. Las reglas técnicas para rectificar/anular una recepción errónea o registrar una devolución efectiva aún requieren completar el procedimiento trazable de reversa antes de cerrar Issue #191.
4. **Cuadro mensual:** preparación, pago y envío del Cuadro Logial Mensual a Gran Tesorería, manteniendo separada la caja local.
5. **Configuraciones:** parámetros propios del Taller y planes de cuota con vigencia; el tarifario institucional continúa bajo control de Gran Tesorería. Las categorías de ingresos y egresos se especifican y filtran por registro.
6. **Reportes:** detalle de movimientos por rango, ingresos, egresos autorizados y pendientes, saldo de apertura/cierre, diferencia de cuadratura y exportación CSV. El detalle conserva ID de origen y metadatos de registro/autorización para trazabilidad; el CSV protege datos textuales ante fórmulas al abrirlo en planillas.

El Venerable Maestro accede exclusivamente a `Egresos por autorizar` dentro de Ingresos y Egresos; puede revisar y autorizar, pero no editar cuotas, cargos, pagos ni Cuadros mensuales. Los egresos pendientes no reducen el saldo de caja ni el cierre reportado.

## Gran Tesorería

El menú `Gran Tesorería` reúne:

1. **Cuadros mensuales:** consolidado por línea de cuota, total exigible, pagos, diferencias y conciliación.
2. **Estado de Talleres:** consulta y registro de regularidad financiera institucional.

Gran Tesorería no administra la caja local, los egresos ni la cobranza individual del Taller. El detalle de hermanos permanece oculto inicialmente y sólo se consulta expresamente para resolver diferencias.

## Criterios de experiencia

- un único acceso lateral por función institucional;
- nombres basados en la tarea que el usuario necesita realizar;
- explicación breve bajo cada opción;
- selector de Taller y período visible dentro del contexto operativo;
- misma navegación en producto real y demo GitHub Pages;
- adaptación móvil sin eliminar acciones autorizadas;
- backend como autoridad final de permisos.

## No regresión

La reorganización no cambia montos, reglas de conciliación, aprobaciones, privacidad, auditoría ni responsabilidades institucionales. Los documentos oficiales continúan cargándose como PDF firmado físicamente; el sistema no los genera.

## Correcciones y anulaciones de registro — Issue #191 / PR #221

Decisión confirmada por el Sponsor el 29-09-2026: se implementan corrección de imputaciones y anulación de recibos duplicados/erróneos de dinero no recibido. La devolución efectiva queda como un flujo separado y no se simula mediante anulación.

`POST /recibos/{receiptId}/ajustes` acepta `correction` o `void`, fecha efectiva en ejercicio abierto, motivo e idempotencia. El actor se obtiene de la sesión y el registro conserva UTC. Una corrección identifica una imputación positiva original y un importe disponible: crea su contrapartida y nuevas imputaciones del mismo miembro/Taller/moneda; la diferencia vuelve a crédito. No mueve caja. La anulación es total: crea contrapartidas de todas las imputaciones pendientes y un ingreso negativo enlazado por el importe del recibo. No crea un egreso. Los originales no cambian.

La migración `20260929233000_AddLodgeReceiptAdjustments` agrega ajustes, enlaces de contrapartida y fecha efectiva a las imputaciones nuevas. El índice recibo/cargo pasa a no único para conservar múltiples abonos y ajustes; una restricción impide más de una anulación por recibo. Triggers PostgreSQL rechazan UPDATE/DELETE de recibos, imputaciones y ajustes. El Down no elimina historial: rollback requiere restauración de respaldo según el runbook vigente.

Informes de caja, resumen, conciliaciones, arrastre y cierres incluyen el ingreso negativo sólo desde la fecha efectiva. Las consultas históricas de cuotas filtran las contrapartidas por esa fecha; no excluyen globalmente el recibo original. Cierres y conciliaciones guardados no se recalculan ni se reabren. El ajuste y el cierre usan transacciones serializables; una colisión de ajuste devuelve conflicto para reintentar con la misma clave. Repetir exactamente un ajuste no crea otro registro; reutilizar su clave con datos distintos produce conflicto. El crédito de un recibo anulado no puede aplicarse.

La UI muestra recibos completos, imputaciones disponibles y el historial de motivo/actor/fecha. El mock valida el mismo miembro y se corrigió la prueba que aplicaba crédito entre dos hermanos diferentes. La presentación financiera oficial de anticipos/recuperaciones permanece pendiente de fuente institucional; estos tipos operativos no crean cuentas contables oficiales. La devolución efectiva no forma parte de este incremento.
