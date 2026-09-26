# PMGM-QA-V066 — Caja, cobranzas y reportes de Tesorería del Taller

**Estado:** implementado en rama; pendiente de CI exact-head, Pages y QA física.
**Referencia funcional:** `Sistema Logial de Ejemplo - solo como referencia`.
**Referencia de conciliación institucional:** `CUADRO PAGO GRAN TESORERÍA.xlsx`.

## Alcance

- El Tesorero dispone de Resumen, Cuotas y Cobranzas, Ingresos y Egresos, Cuadro mensual, Configuraciones y Reportes.
- El resumen expone saldos acumulados y del mes. Configuraciones admite saldo inicial con fecha, categorías configurables y planes de cuota con vigencia.
- El registro de pago de cuota genera el ingreso automáticamente desde la fila de cobranza; no se crea otro ingreso manual por ese mismo pago.
- Todo egreso queda pendiente hasta aprobación del Venerable Maestro. Sólo egresos aprobados reducen caja.
- Reportes presentan movimientos detallados, saldo inicial, ingresos, egresos aprobados, egresos pendientes, cierre calculado y diferencia frente al saldo contado; el control comunica si la cuadratura está pendiente, sin diferencia o requiere revisión.
- Cada movimiento reportado identifica su registro de origen y quién/cuándo lo registró; los egresos además exponen quién/cuándo los autorizó. El CSV mantiene estos campos y neutraliza entradas textuales que pudieran interpretarse como fórmulas, preservando montos negativos como números.
- Se conservan la separación de caja del Taller y Cuadro Logial Mensual a Gran Tesorería, y la autoridad de backend para los permisos.

## Verificación local

- Frontend: 191 pruebas aprobadas; build y lint aprobados.
- JSON del kit de regresión: validar; el control nuevo es QA-037.
- Backend / PostgreSQL: requiere CI exact-head; el entorno local no tiene .NET SDK. El incremento de cierres anuales y auditoría se registra en `PMGM-QA-V067`.
- Pages ejecuta el chequeo de seis pestañas en el flujo de Showcase. La publicación efectiva ocurre al integrar en `dev`.
- QA-037 en `srv01` y UAT permanecen pendientes por Issue #97.

## Criterios contables de QA-037

El control comprueba persistencia del saldo inicial y de las categorías; cargos, pagos completos y abonos; vínculo único entre pago y libro de ingresos; autorización auditada de egresos antes de afectar caja; fórmula de cierre `apertura + ingresos - egresos autorizados`; diferencia `saldo contado - cierre calculado`; detalle CSV; y acceso exclusivo del Venerable al circuito de autorización.
