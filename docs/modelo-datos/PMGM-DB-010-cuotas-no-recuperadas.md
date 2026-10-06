# DB-010 — Cuotas no recuperadas, Issue #191 / PR #329

Decisión PO 05-10-2026: sin plan de cuentas. Caja por recepción real; control de cuotas/regularidad por período en paralelo. Expulsión formal por no pago: saldo impago informado como pérdida sin movimiento de efectivo.

Antes: recibos/imputaciones/ajustes y cierre de caja; sin evidencia de cuotas no recuperadas. Después: `core.lodge_unrecovered_dues`, independiente de ingreso/egreso/pago.

| Campo | Tipo / regla | Finalidad |
|---|---|---|
| Id | UUID PK | Evidencia única |
| OrganizationId | UUID FK restrict | Taller exacto |
| WithdrawalRequestId | UUID FK restrict | Retiro forzoso aprobado vinculado |
| ChargeId | UUID FK restrict, único | Cuota original; impedir doble pérdida |
| RecognitionDate | date, hoy / ejercicio abierto | Período del informe de pérdida |
| ChargedAmount / PaidAmount / Amount | numeric(18,2) | Snapshot cuota/abonado/impago; Amount > 0 = cuota − pagado |
| Currency | CLP/USD | Sublibros sin conversión |
| EvidenceReference | varchar(500) obligatorio | Respaldo de causa no pago confirmado |
| RecordedBySubject / RecordedAtUtc | varchar(320) / timestamptz | Trazabilidad |

UPDATE/DELETE protegidos por trigger append-only existente. Migración nueva `20261006002500_AddLodgeUnrecoveredDues`; no altera filas/tablas históricas. Rollback mediante respaldo, conserva evidencia. Requiere aplicar migraciones al instalar, no aplicada en srv01 durante la pausa.

GET `/perdidas/candidatos`: proyección mínima de retiros forzosos aprobados del Taller con deuda, sin razón privada, RUT o contacto. Un candidato no implica no pago: exige confirmación explícita y respaldo. POST `/perdidas`: importe calculado por servidor; excluye cuotas posteriores al mes de retiro, cargos pagados/pérdidas previas. Saldo a favor no aplicado bloquea reconocimiento hasta revisión. No cambia retiro/afiliación/grado, obligaciones ni estado pagado. La pérdida no concede regularidad.

GET `/reportes` agrega `unrecoveredDuesTotal` y `unrecoveredDues`, filtrados por fecha de reconocimiento, Taller y moneda. No cambia `movements`, `monthlyTotals`, ingresos, egresos, cierre, diferencia, arqueos ni cierres anuales. El snapshot expresa pérdida al reconocerla; recuperaciones futuras son recepciones reales en fecha efectiva y no sobrescriben esa evidencia histórica. Presentación separada, sin contabilidad devengada ni nuevas cuentas.

Seguridad: autoridad institucional de Tesorería intersectada con grants `view`/`write` del sujeto/Taller, se reevalúa en cada petición; candidatos `no-store`. Privacidad sensible, clasificación GL-023; sin exportación pública ni datos reales en demo. Auditoría registra ids/monto/fecha, no texto privado del retiro. La demo sólo utiliza retiro y cargos sintéticos; no demuestra expulsión institucional ni JWT real.

Pruebas nuevas: HTTP PostgreSQL CLP/USD, parcial/pagado/futuro, retiro voluntario/pendiente/otro Taller, confirmación, duplicidad, caja y obligaciones intactas, trigger inmutable. Demo CLP/USD y validación. Gates/SHAs finales en handoff; no se sustituye UAT.
