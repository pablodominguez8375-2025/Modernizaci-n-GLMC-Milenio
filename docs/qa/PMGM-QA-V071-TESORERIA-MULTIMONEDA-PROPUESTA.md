# PMGM-QA-V071 — Propuesta de aceptación multimoneda

**Estado:** Borrador de criterios; no agregar al gate de release hasta aprobar PMGM-ARCH-015.  
**Fuente:** Decreto N.º 1.759 y propuesta `docs/architecture/PMGM-ARCH-015-tesoreria-multimoneda-propuesta.md`.

## Criterios propuestos

1. Taller chileno mantiene cargos, pagos, Cuadro, apertura y reportes CLP idénticos a los existentes.
2. Taller peruano genera únicamente cuota ordinaria de USD 6, con moneda resuelta por servidor y vigencia del decreto.
3. Sin tarifa oficial para cónyuge, estudiante o tercera edad en Perú, la API bloquea el plan y generación de cargo; no crea monto cero ni monto CLP.
4. La cuota local del Taller peruano, si se configura, debe expresarse en USD y ser igual o superior a USD 6; el margen local y aporte oficial se informan separadamente en USD.
5. Cambiar territorio o tarifa futura no altera cargos históricos, pagos, comprobantes ni moneda congelada.
6. La API rechaza pagos de moneda distinta a la del cargo/Cuadro; el cliente no puede cambiar la moneda.
7. La conciliación del Cuadro compara esperados, transferencias, depósitos, abonado y diferencia dentro de una sola moneda.
8. Caja resume CLP y USD en líneas independientes. La fórmula apertura + ingresos − egresos autorizados, diferencia contra saldo contado y cierre anual se comprueban por moneda.
9. Los registros existentes se migran a CLP y sus totales anteriores mantienen el mismo valor.
10. La interfaz, comprobantes y CSV muestran código de moneda en todos los importes; escritorio, tablet y teléfono no recortan columnas ni controles.
11. Demo usa datos ficticios y presenta ambos casos sin conversión ficticia.
12. Auditoría registra moneda junto con valor, período, actor, fecha y referencia.

## Gate requerido tras aprobación

- Unit tests de resolución de tarifa y rechazo de monedas/categorías no válidas.
- PostgreSQL: migración CLP histórica, cargos USD, recibo, Cuadro y conciliación por moneda, cierres anuales por moneda.
- Frontend: tests de formateo, selección no editable, totales separados, error de mezcla y CSV.
- Build/lint, migration/privacy/data-classification/release gates.
- Showcase responsive con los mismos contratos y datos sintéticos.
- CI exact-head, Pages y paquete QA del mismo SHA.
- QA física y UAT continúan separadas en Issue #97.
