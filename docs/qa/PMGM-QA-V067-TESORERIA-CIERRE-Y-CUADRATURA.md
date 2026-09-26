# PMGM-QA-V067 — Cierre anual y cuadratura de Tesorería

**Estado:** base funcional existente en `dev`; ampliación de trazabilidad de control/auditoría registrada en PR #167 hacia `dev`. QA física/UAT siguen pendientes por Issue #97 y la pausa de srv01.
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

- Frontend local: 198/198 pruebas, lint y build TypeScript/Vite aprobados. Los gates exact-head se consultan en PR #167; no sustituyen QA física/UAT.
- Backend y migración PostgreSQL: CI exact-head requerido; el entorno local no dispone de .NET SDK.
- QA-038 de `release/PMGM-QA-SRV01-REGRESSION.template.json` cubre cierre, bloqueo, arrastre, Debe/Haber/Neto, filtros y CSV. Ampliar su evidencia para verificar ID + actor/UTC de registro y autorización, cuadratura en cero/diferencia/pendiente, CSV con prefijo de fórmula y preservación numérica de netos negativos.
- La aceptación física en srv01/UAT queda pendiente por Issue #97.


## Seguimiento del corte integrado PR #167

En `dev@5d9e5b90c5a303a07ed21658e02902d8c93a719a` quedó QA-040 para probar trazabilidad, estado de cuadratura, egresos pendientes excluidos y seguridad de CSV. PMGM CI #1551 y QA Installable #459 SUCCESS; paquete verificado por digest Actions/ZIP `sha256:9e1283bd44513f5cb11ee11410b437e6a97b5c6a3b10e6ddc4c752acd7688333`, SOURCE_SHA integrado y MANIFEST. Pages post-merge #821 está en cola; el chequeo de Pages/qa-current.json no está cerrado. La ejecución física de QA-040 y regresión institucional está pendiente porque srv01 no se monta hasta nuevo aviso. Sin aceptación de QA/UAT.
