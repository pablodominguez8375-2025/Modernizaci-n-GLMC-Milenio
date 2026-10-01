# Concurrencia ajuste/cierre anual — 01-10-2026

Issue #191; dominio Tesorería, agente ChatGPT. Base viva dev `5f5d2f41b6e8af7900a49e69606bad086093d8f2`; main `6dfb9546a4873baff15955cf86abfd7d47e3d111` congelado.

Reserva previa a programación: prueba HTTP PostgreSQL nueva `backend/tests/PMGM.Api.Tests/Integration/LodgeReceiptYearClosureConcurrencyPostgreSqlTests.cs`, posible reparación mínima de `backend/src/PMGM.Api/Modules/Treasury/LodgeTreasuryEndpoints.cs`, matriz `docs/qa/PMGM-QA-RECIBOS-AJUSTES-191.md`, changelog nuevo y este handoff. Sin cruces con #116/#60/#58 en estos archivos; #1 es comparador de promoción y no se toca. PR draft inmediato y etiqueta #191 antes de programar. Estimación: 01-10-2026, sujeta a gates.

Alcance: reproducir anulación/corrección versus cierre anual con lecturas PostgreSQL serializables sincronizadas en CLP y USD; exigir caja, crédito, cierre inmutable y auditoría coherentes, rollback/conflicto/reintento seguro. No inventar cuentas ni tarifas, ni alterar política de anticipos o devoluciones. Nuevos gates, integración y publicación pendientes.

#131 ya cerrada/sustituida; #116 NO FUSIONAR TODAVÍA, #60/#58 remanentes preservados. UI v0.65 de Claude intacta/P3 sin aprobación. Main congelado; srv01 pausado, despliegue QA pendiente, sin instalación ni QA física/UAT. START-HERE sólo una línea en PR documental posterior al merge.

PR #250 reclamada antes de programar. Primer corte de reproducción: ocho escenarios (anulación/corrección, CLP/USD, ambos órdenes de commit), reteniendo la primera lectura de cierre dentro de su transacción serializable. Una corrección sin efecto en caja puede confirmar junto al cierre si existe un orden serializable válido; la anulación obliga al competidor con snapshot obsoleto a abortar. Se comprueban originales, caja/crédito, cierre/auditoría, reintento e idempotencia y ajuste posterior fechado en ejercicio abierto.

Sin SDK .NET local; CI PostgreSQL del nuevo HEAD debe aportar ejecución real. No se afirma reparación, gates, integración ni publicación antes de resultados. #191 mantiene presentación contable institucional y operación pendientes.

Reproducción CI `36904459172`: 382 casos ejecutados, 380 PASS, 2 FAIL, 0 omitidas. Anulación primero CLP/USD provoca 500 en cierre rechazado por PostgreSQL 40001. TRX artifact `11182524459`, digest externo `d98bad11ceb19406db0e6ad39d77b09ee6f1e7b9f1597048886d8952a415e622`, descargado/verificado. La corrección mínima añade un wrapper de CloseAnnualPeriodAsync y conserva íntegro su core: sólo 40001/23505 y envolturas Npgsql conocidas pasan a 409 después de disponer/rollback de la transacción. No modifica cálculos ni agrega reintento automático. Ocho pruebas sin relajar, matriz QA y changelog actualizados. Gates del HEAD reparado y publicación pendientes.
