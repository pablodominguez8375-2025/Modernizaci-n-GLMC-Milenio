# Concurrencia de ajustes frente a cierre anual — 01-10-2026

Issue #191 / PR #250. La anulación concurrente de un recibo podía causar HTTP 500 al cierre anual, aunque PostgreSQL rechazaba correctamente la transacción obsoleta. Se reconoce ese conflicto como 409 tras rollback y se pide actualizar saldos/reintentar; no hay reintento automático ni modificación de las reglas del cierre.

Ocho carreras HTTP PostgreSQL CLP/USD fuerzan ambos órdenes de commit, con anulación y corrección a crédito. Verifican originales, cierre/caja/crédito/auditoría, idempotencia y ajuste posterior en ejercicio abierto. Reproducción: 380/382 PASS, dos 500 en anulación primero; validación del HEAD reparado pendiente. Sin UI/migraciones/tarifas nuevas. Presentación financiera institucional y operación #191 siguen pendientes; main congelado y srv01 pausado.
