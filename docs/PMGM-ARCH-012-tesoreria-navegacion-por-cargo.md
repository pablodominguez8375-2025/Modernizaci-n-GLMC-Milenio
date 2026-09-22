# PMGM-ARCH-012 — Navegación operativa de Tesorería por cargo

## Decisión

Tesorería del Taller y Gran Tesorería se presentan como espacios funcionales separados, cada uno con un único acceso principal y navegación interna orientada a tareas. No se exponen como entradas laterales independientes las subfunciones del mismo cargo.

## Tesorería del Taller

El menú `Tesorería` reúne:

1. **Resumen:** estado mensual, planes de cuota, recaudación, saldo y monto correspondiente a Gran Tesorería.
2. **Cuotas y cobranza:** cargos, abonos, pagos, comprobantes y saldos individuales del Taller.
3. **Egresos:** registro de egresos y seguimiento de la autorización del Venerable Maestro.
4. **Cuadro mensual:** preparación, pago y envío del Cuadro Logial Mensual a Gran Tesorería.

El Venerable Maestro accede exclusivamente a `Egresos por autorizar`; puede revisar y autorizar, pero no editar cuotas, cargos, pagos ni Cuadros mensuales.

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

