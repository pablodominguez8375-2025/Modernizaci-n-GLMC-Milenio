# PMGM-QA-V070 — Derechos ceremoniales vinculados al expediente

## Regla funcional

Los derechos únicos del Decreto N.º 1.759 se registran en CLP y se concilian por solicitud: Iniciación $41.000, Aumento de Salario $31.000, Exaltación $41.000, Afiliación $26.000 e Incorporación $31.000. La cuota mensual y la regularidad del Taller permanecen como controles independientes.

## Criterios de aceptación

1. Cada expediente muestra el derecho vigente, pagado, saldo y referencia normativa.
2. Sólo Gran Tesorería puede registrar abonos desde su vista operativa propia; el Tesorero del Taller no obtiene acceso a la bandeja de ceremonias de la Orden.
3. Se admiten pagos parciales que no excedan el saldo; se rechazan monto cero, fecha futura, medio inválido y sobrepago.
4. Cada movimiento emite un comprobante único y conserva actor, fecha efectiva, medio y referencia.
5. Reintentar con la misma clave y el mismo contenido devuelve el comprobante original sin duplicar el libro; reutilizar la clave con contenido distinto se rechaza.
6. Gran Secretaría no puede autorizar mientras exista saldo del derecho, aunque el Taller esté regular en sus cuotas.
7. Con saldo cero, el requisito financiero del derecho pasa a aprobado; las demás validaciones siguen aplicándose.
8. La demo reproduce pagos parciales e idempotencia con datos ficticios. En móvil, saldo y controles permanecen visibles sin overflow global.

## Alcance de ejecución

La prueba física corresponde a QA-039 sobre el SHA instalado. CI y GitHub Pages validan código y demo, pero no sustituyen la instalación, conciliación real ni UAT de srv01.
