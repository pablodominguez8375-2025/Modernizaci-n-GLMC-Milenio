# PMGM-ARCH-012 — Navegación operativa de Tesorería por cargo

## Decisión

Tesorería del Taller y Gran Tesorería se presentan como espacios funcionales separados, cada uno con un único acceso principal y navegación interna orientada a tareas. No se exponen como entradas laterales independientes las subfunciones del mismo cargo.

## Tesorería del Taller

El menú `Tesorería` reúne:

1. **Resumen:** caja acumulada a la fecha, ingresos y egresos del mes en curso, compromisos con Gran Tesorería y estado de cobranza.
2. **Cuotas y Cobranzas:** saldos individuales por hermano, pagos acumulados del período, adeudos, semáforo de estado y acción directa para registrar un abono.
3. **Ingresos y Egresos:** libro de caja para ingresos varios y egresos. Los pagos de cuotas se contabilizan desde el registro de pagos para evitar duplicación; el egreso afecta el saldo sólo una vez autorizado por el Venerable Maestro.
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
