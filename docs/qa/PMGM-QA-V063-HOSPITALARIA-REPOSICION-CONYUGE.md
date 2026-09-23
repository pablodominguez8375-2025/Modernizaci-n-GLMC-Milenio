# PMGM QA v0.63 — Reposición por fallecimiento y cuota de cónyuge

## QA-034 — Hospitalaria / Gran Hospitalaria

Validar con datos sintéticos en demo y posteriormente sobre el mismo SHA instalado en srv01:

1. Configurar tarifa $1.500 por activo con fecha efectiva y referencia; verificar que los casos conservan la tarifa vigente a la fecha de defunción.
2. Sincronizar una defunción registrada y verificar idempotencia. Calcular obligaciones según membresías activas a esa fecha y por Taller, excluyendo al fallecido y personas no activas.
3. En Hospitalaria, registrar pagos parciales/completos por obligación con fecha, medio y comprobante; bloquear sobrepagos y duplicados.
4. Bloquear transferencia si falta cobro; habilitarla al completar el Taller por el total exacto y exigir referencia.
5. Gran Hospitalaria recibe cifras y transferencias agregadas, sin nombres ni datos de pagos individuales.
6. Conciliar sólo cuando transferido = exigible = recaudado; una diferencia se observa y deja la regularidad pendiente.
7. Tras una observación, permitir un nuevo envío numerado y conservar el intento anterior; Ceremonias sólo consume el visto bueno de la última transferencia conciliada.
8. En Tesorería del Taller, configurar la cuota de cónyuge de referencia $15.000 CLP/mes, con vigencia; exigir configurar por separado el aporte aplicable a Gran Tesorería.
9. Verificar auditoría de generación, pagos, transferencias, observación/conciliación y cambios de tarifa.

La prueba frontend valida los contratos simulados de la demo. La migración/API real requiere CI exact-head y pruebas integradas con PostgreSQL. La validación física QA/UAT queda bajo Issue #97.
