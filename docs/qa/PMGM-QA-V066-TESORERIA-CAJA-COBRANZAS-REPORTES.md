# PMGM-QA-V066 — Caja, cobranzas y reportes de Tesorería del Taller

**Estado:** implementado en rama; pendiente de CI exact-head, Pages y QA física.
**Referencia funcional:** `Sistema Logial de Ejemplo - solo como referencia`.
**Referencia de conciliación institucional:** `CUADRO PAGO GRAN TESORERÍA.xlsx`.

## Alcance

- El Tesorero dispone de Resumen, Cuotas y Cobranzas, Ingresos y Egresos, Cuadro mensual, Configuraciones y Reportes.
- El resumen expone saldos acumulados y del mes. Configuraciones admite saldo inicial con fecha, categorías configurables y planes de cuota con vigencia.
- El registro de pago de cuota genera el ingreso automáticamente desde la fila de cobranza; no se crea otro ingreso manual por ese mismo pago.
- El historial por hermano diferencia cuotas vencidas de años anteriores, período vigente y cuotas futuras abonadas. La cartola muestra el período de obligación y conserva la fecha real del pago.
- El Tesorero puede escoger el cargo/período concreto al imputar cada pago, incluyendo una cuota morosa o futura previamente generada; el formulario muestra saldo y tipo de período.
- La caja registra un único movimiento de categoría **Ingreso por pago de cuotas** en la fecha efectiva de recepción. La reasignación del pago entre obligaciones no vuelve a modificar la caja.
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

## Casos incrementales: períodos anteriores y futuros

1. La cartola del hermano incluye todos los períodos registrados, incluidos los antiguos fuera de los 24 meses recientes.
2. Un saldo pendiente de un año anterior aparece como morosidad y puede seleccionarse para pago sin alterar su período ni la fecha real de recepción.
3. Un período futuro ya cargado puede seleccionarse como destino del pago adelantado, incluso cuando existe una deuda anterior de ese mismo hermano.
4. Un pago recibido hoy por período antiguo/futuro se refleja como movimiento contable en el año de recepción y reduce el saldo solamente del período seleccionado.
5. Cada período muestra cargo, total abonado, saldo y estado: moroso, vigente, parcial, pagado, futuro pendiente, adelanto parcial o adelantado pagado.
6. Los totales de caja/ingreso y el saldo del submayor se reconcilian sin duplicar efectivo al aplicar un recibo a otro período.

Base referencial: `Manual_Modulo_Tesoreria.md` de Drive, que establece ingreso automático desde Cuotas hermanos, distinción entre año contable y año de cuota, historial/cartola y exportación de morosidad por año. La visualización de anticipos como condición explícita de cartola es un requisito adicional confirmado por el Sponsor.
