# PMGM-QA-V067 — Cierre anual y cuadratura de Tesorería

**Estado:** implementación en rama; requiere CI exact-head, despliegue de Pages y QA física.
**Origen funcional:** mejoras compatibles del “Sistema Logial de Ejemplo - solo como referencia”.

## Entregado

- Cierre anual auditado con saldo de apertura, ingresos, egresos autorizados, saldo de cierre y cantidad de movimientos.
- Impedimento de cierre mientras existan egresos pendientes de autorización.
- Bloqueo de nuevos movimientos y autorizaciones que afecten ejercicios cerrados; configuración de apertura protegida después del primer cierre.
- Saldo de cierre trasladado como apertura del ejercicio siguiente.
- Informe mensual agrupado por período, Debe, Haber y Neto, con egresos pendientes identificados aparte.
- Filtros por clase, categoría, medio de pago y búsqueda; paginación de 20 filas y CSV del resultado filtrado.

La regla referencial de cuota al retirarse o fallecer un miembro no se automatiza: requiere validación institucional antes de incorporarla a los cálculos.

## Verificación

- Frontend: pruebas, lint y build a ejecutar en esta rama.
- Backend y migración PostgreSQL: CI exact-head requerido; el entorno local no dispone de .NET SDK.
- QA-038 de `release/PMGM-QA-SRV01-REGRESSION.template.json` cubre cierre, bloqueo, arrastre, Debe/Haber/Neto, filtros y CSV.
- La aceptación física en srv01/UAT queda pendiente por Issue #97.
