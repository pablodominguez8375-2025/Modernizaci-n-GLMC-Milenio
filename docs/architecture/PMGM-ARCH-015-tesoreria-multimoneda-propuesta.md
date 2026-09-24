# PMGM-ARCH-015 — Propuesta de Tesorería multimoneda

**Estado:** Propuesta técnica; requiere decisión del Sponsor/Product Owner y validación operativa de Gran Tesorería antes de implementar.  
**Fecha:** 24-09-2026  
**Fuente normativa:** Decreto N.º 1.759, 15-12-2025, vigente desde 01-01-2026 (copia oficial en Drive: `decreto 1759 Establece valor cuotas 2026.pdf`).

## 1. Motivo

El decreto fija la cuota ordinaria de los Talleres del Perú en **USD 6**. El código vigente reconoce el dato en el catálogo, pero `GrandTreasuryFeeSchedule.Resolve` devuelve tarifa no disponible para Perú porque los modelos de cargos, pagos, Cuadro de Gran Tesorería y caja local expresan importes sin moneda. Habilitar sólo la tarifa permitiría comparar o sumar USD con CLP.

La propuesta mantiene los flujos actuales de Chile en CLP y extiende la unidad contable explícita a cada saldo, movimiento y comprobante.

## 2. Reglas de seguridad contable propuestas

1. Todo importe persistido o expuesto en una operación financiera lleva código ISO 4217 de moneda. Para este alcance se permiten `CLP` y `USD`.
2. El servidor obtiene la moneda del Oriente/tarifa vigente. El cliente no puede seleccionar ni cambiar la moneda de un aporte oficial, cargo o pago.
3. Cada cargo congela importe, moneda, tipo de cuota, aporte oficial, vigencia y referencia normativa aplicables al generarse.
4. Pagos, recibos, Cuadros mensuales y conciliaciones conservan la misma moneda del cargo/Cuadro. Se rechaza cualquier intento de mezclar monedas dentro de un mismo Cuadro o recibo.
5. CLP y USD se cuadran, reportan, exportan y cierran por separado. No se suman entre sí ni se comparan con un saldo de otra moneda.
6. No se convierte USD a CLP automáticamente. No se inventan tasas, equivalencias, tarifas ni categorías ausentes del decreto.
7. Se asigna `CLP` a los datos financieros históricos durante la migración, porque ésa es la unidad actualmente operada por el sistema.
8. Perú sólo dispone por fuente normativa de tarifa ordinaria de **USD 6**. Cónyuge, estudiante y tercera edad permanecen no disponibles hasta que exista fuente institucional aplicable.
9. El libro de caja necesita saldo de apertura por moneda y fecha de vigencia para cuadrar efectivo existente al habilitar USD. Movimientos, saldo contado, diferencia, apertura siguiente y cierre anual también se agrupan por moneda.
10. Todo CSV identifica la moneda en cada fila. Totales y subtotales nunca omiten moneda.

## 3. Perímetro funcional

### Incluido en la eventual implementación

- Plan de cuota y cargo de miembro con moneda y snapshot de tarifa institucional.
- Pago de cuota, recibo y estado de cuenta individual con la moneda del cargo.
- Cuadro mensual de Gran Tesorería: moneda fijada desde el Taller/Oriente, líneas y pagos homogéneos, conciliación en dicha moneda.
- Caja del Taller: aperturas, ingresos, egresos, resumen, cuadratura, reportes, exportación y cierre anual por moneda.
- Demo con datos sintéticos que muestre un Taller chileno en CLP y uno peruano en USD.
- Migración retrocompatible; filas existentes quedan en CLP.

### Fuera de alcance

- Conversión cambiaria, cotización de referencia, diferencias de cambio o transferencia entre saldos de monedas distintas.
- Tarifas USD para categorías distintas de la cuota ordinaria peruana.
- Automatización de cuotas de cesantía, que requiere su propio diseño y flujo.
- Cualquier alteración de las tarifas chilenas, la cuota de cónyuge vigente o derechos ceremoniales.

## 4. Consecuencias y controles

- Evita errores por agregación de magnitudes distintas y conserva el valor original pagado.
- Aumenta el trabajo para conciliaciones y reportes: cada comparación debe emparejar moneda y período.
- Se debe definir quién carga y certifica el saldo inicial USD y su respaldo antes del primer período real.
- Gran Tesorería debe confirmar el procedimiento real de recepción/conciliación de transferencias en USD; la especificación no presume banco, comisión, fecha de conversión ni cuenta contable.
- La generación de cargos de categorías no autorizadas para Perú debe fallar de forma visible y no crear un cargo cero o CLP.

## 5. Decisiones solicitadas

Antes de aceptar este ADR se requiere confirmar:

1. ¿Se autoriza llevar saldos CLP y USD en sublibros separados, sin conversión automática?
2. ¿El aporte ordinario peruano se cobra y concilia por **USD 6**, manteniendo también en USD la cuota local que configure el Taller?
3. ¿Gran Tesorería confirmará el circuito operativo de recepción, respaldo y conciliación de pagos USD antes de QA institucional?

Hasta resolver estas decisiones, Perú debe seguir mostrando tarifa no disponible en el flujo de cargos y pagos. El catálogo informativo puede mostrar USD 6 con su fuente.

## 6. Evidencia de fuente

El Decreto N.º 1.759 establece expresamente cuota ordinaria para Logias del Perú de **US$ 6,00**. Define en CLP las cuotas de cónyuge, tercera edad y estudiante para Santiago y otros Orientes; no asigna en el texto revisado equivalentes USD para esas categorías. Este documento no extrapola esos montos.
