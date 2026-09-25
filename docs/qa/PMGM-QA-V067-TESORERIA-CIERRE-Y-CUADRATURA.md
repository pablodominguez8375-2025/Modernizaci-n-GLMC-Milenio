# PMGM-QA-V067 — Cierre anual y cuadratura de Tesorería

**Estado al 25-09-2026:** base funcional existente en `dev`; ampliación de trazabilidad de control/auditoría en preparación para PR. QA física/UAT siguen pendientes por Issue #97 y la pausa de srv01.
**Origen funcional:** mejoras compatibles del “Sistema Logial de Ejemplo - solo como referencia”.

## Entregado

- Cierre anual auditado con saldo de apertura, ingresos, egresos autorizados, saldo de cierre y cantidad de movimientos.
- Impedimento de cierre mientras existan egresos pendientes de autorización.
- Bloqueo de nuevos movimientos y autorizaciones que afecten ejercicios cerrados; configuración de apertura protegida después del primer cierre.
- Saldo de cierre trasladado como apertura del ejercicio siguiente.
- Informe mensual agrupado por período, Debe, Haber y Neto, con egresos pendientes identificados aparte.
- Filtros por clase, categoría, medio de pago y búsqueda; paginación de 20 filas y CSV del resultado filtrado.
- Extensión de auditoría en curso: cada línea expone ID de transacción, sujeto y hora UTC de registro, y sujeto/hora UTC de autorización para egresos autorizados; el CSV conserva esos campos.
- El reporte comunica de forma explícita diferencia cero, diferencia por revisar o conteo pendiente, e informa los egresos no autorizados separados del saldo.
- El CSV escapa campos que comienzan con caracteres interpretables como fórmula para evitar ejecución al abrirlo en planillas.

La regla referencial de cuota al retirarse o fallecer un miembro no se automatiza: requiere validación institucional antes de incorporarla a los cálculos.

## Verificación

- Frontend: falta ejecutar pruebas, lint y build del corte actual; este entorno no tenía `vitest` instalado al primer intento.
- Backend y migración PostgreSQL: CI exact-head requerido; el entorno local no dispone de .NET SDK.
- QA-038 de `release/PMGM-QA-SRV01-REGRESSION.template.json` cubre cierre, bloqueo, arrastre, Debe/Haber/Neto, filtros y CSV. Ampliar su evidencia para verificar ID + actor/UTC de registro y autorización, cuadratura en cero/diferencia/pendiente, CSV con prefijo de fórmula y preservación numérica de netos negativos.
- La aceptación física en srv01/UAT queda pendiente por Issue #97.
