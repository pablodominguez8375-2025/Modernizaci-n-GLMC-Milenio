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
- La cuadratura de caja/banco puede guardarse como una conciliación histórica inmutable por período. El servidor vuelve a calcular apertura, ingresos, egresos autorizados y pendientes desde los libros; conserva saldo calculado y contado, diferencia, cantidad de movimientos, referencia opcional, nota, actor y timestamp UTC. Un nuevo conteo crea otro registro, sin reemplazar el anterior ni alterar movimientos.
- El reporte muestra las 50 conciliaciones más recientes del mismo rango. QA-041 verifica saldo calculado, diferencia, atribución y repetición histórica; no constituye aceptación física.

La regla referencial de cuota al retirarse o fallecer un miembro no se automatiza: requiere validación institucional antes de incorporarla a los cálculos.

## Verificación

- Frontend local: 198/198 pruebas, lint y build TypeScript/Vite aprobados. Los gates exact-head se consultan en PR #167; no sustituyen QA física/UAT.
- Backend y migración PostgreSQL: CI exact-head requerido; el entorno local no dispone de .NET SDK.
- QA-038 de `release/PMGM-QA-SRV01-REGRESSION.template.json` cubre cierre, bloqueo, arrastre, Debe/Haber/Neto, filtros y CSV. QA-040 verifica trazabilidad y CSV segura; QA-041 cubre la evidencia persistida y su inmutabilidad.
- La aceptación física en srv01/UAT queda pendiente por Issue #97.


## Conciliación persistida — alcance autorizado en curso

La revisión del Sistema Logial de Ejemplo mantiene como referencia los reportes de flujo, Debe/Haber/Neto, saldos, respaldo y observaciones. El semáforo de Gran Tesorería ya existe en el Cuadro Mensual institucional y no se duplica en caja del Taller. Se descarta la proyección de caja de la referencia porque parte de supuestos de cobranza que el sistema no confirma.

El nuevo registro de conciliación es append-only por Taller y fechas Desde/Hasta. Guarda los importes recalculados en el backend desde pagos, ingresos, egresos autorizados y pendientes; además, saldo contado, diferencia, cantidad de movimientos, referencia/nota opcional, actor y UTC. Repetir el arqueo crea una nueva evidencia; no reemplaza una anterior y no genera ni edita movimientos. El reporte lista hasta 50 últimas conciliaciones del mismo rango. QA-041 cubrirá consistencia aritmética, aislamiento por Taller, atribución, repetición inmutable y que guardar no altere el saldo del libro.

El desarrollo está en `feature/treasury-reconciliation-audit-20260926` con base `dev@85fc5a5816910ae9c477a0aecc1121aca21429b8`; revisión automática, pruebas de integración y aceptación física permanecen pendientes. `srv01` sigue pausado; no se declara QA/UAT aceptada.
