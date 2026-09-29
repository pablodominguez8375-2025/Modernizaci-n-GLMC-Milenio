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
