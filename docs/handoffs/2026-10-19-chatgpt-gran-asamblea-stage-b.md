# Handoff #533 — Etapa B: trazabilidad de Asamblea y fuente de saldo

Fecha 2026-10-19. Rama `feature/gran-asamblea-persistencia-saldo-20261019-gpt`, base `dev@1e670779bf6be9be9332347c4c40d1f135ebcc6c`, responsable ChatGPT. QA se mantiene en otro hilo; NO se tocará host, entorno, credenciales ni `main`.

## Frontera de trabajo
Crear modelos y migración aditiva propiedad del contexto Régimen Interior y política conservadora de proyección de saldo desde cuadros de Gran Tesorería. No habilitar endpoints de sufragio ni modificar UI hasta completar integración con los datos reales y controles de auditoría.

## Decisiones vigentes
- Un saldo **positivo** con Gran Tesorería bloquea a todos los electores de ese Taller; ningún umbral configurable. Solo Régimen Interior puede levantar/restablecer por resolución motivada, registrada y reversible.
- Dos exportaciones: todos los asambleístas vs solo sufragantes; globalmente por apellidos y nombres es-CL, RUT para desempate, firma en blanco, sin revelar causas disciplinarias ni votos.
- Estado desconocido/ausencia de deuda comprobable jamás es «al día». Múltiples monedas no se suman sin cotización aprobada; rectificaciones se resuelven por trazabilidad y corte.

## Invariantes de seguridad
La persistencia provisional no autoriza inserción masiva ni cierre automáticamente. La futura evaluación debe verificar que la calidad de Venerable Maestro está acreditada, el estado de membresía/derechos del Tribunal y el saldo GT provienen de fuente legítima. Cierre/auditoría/CSV/PDF requieren endpoints, transacción y controles RBAC posteriores. Protección Ley 21.719 y datos personales; no recopilar votos.

## Código implementado en esta rama (19-10, Etapa B)

- Entidades `AssemblyRecord`, `AssemblyMemberStatusRecord`, `AssemblyEligibilityRecord`, `AssemblyDebtDecisionEventRecord`, `InstitutionalRestrictionRecord`. Campos de fuente y corte, RUT y apellidos para eventual snapshot de impresión, categoría VM/representante, restricciones mínimas sin exponer expediente de Tribunal. **No** hay dato de voto ni sentido de sufragio.
- Mapeo EF segregado en `RegimenInteriorDbContext`. Migración aditiva `20261019180000_AddAssemblyRosterFoundations` crea cinco tablas en `core`, con índice por Asamblea/Taller, calidad única por persona, historial de evaluaciones, FK compuesta que impide asociar evaluación a la Asamblea equivocada, y FK para actos de excepción.
- Reglas a nivel PostgreSQL: cierre de Asamblea bloqueado en esta etapa por trigger `trg_assembly_draft_only`; la migración futura de cierre auditado debe reemplazar explícitamente esa barrera. Los actos de levantamiento/revocación `assembly_debt_decision_events` son append-only, incluso frente a UPDATE, DELETE y TRUNCATE; revocación será un evento nuevo, nunca mutación.
- `AssemblyGrandTreasuryLedgerPolicy` produce `AssemblyGrandTreasuryBalance` solo si recibe meses contiguos completos, corte exacto, misma moneda y Taller, certificación de fuente y período; rechaza duplicaciones/rectificaciones irresueltas, saldos negativos no conciliados, sobrepagos, meses faltantes, evidencia antigua o moneda cruzada. Se agrega `AssemblyGrandTreasuryLedgerPolicyTests` y `AssemblyFoundationPostgreSqlTests`; este último se registra en shard b para migración y trigger.
- El adaptador que autentica y construye los períodos certificados desde Gran Tesorería real **NO EXISTE**. Los cuadros `TreasuryMonthlyStatement` con estado `reconciled` exigen diferencia cero, por lo que **NO** demuestran por sí solos que no existe deuda en otros meses/ajustes; tampoco se puede afirmar morosidad exacta leyendo `FinancialRegularitySnapshot` (no lleva monto). Por ello solo existe el proyector con contrato de entrada documentado, sin endpoint accesible a clientes ni habilitación automática.
- No hay API create/evaluate/decide/close/list/export, ni integración de cargos reales o Tribunal de Honor. La regla de deuda positiva y la excepción RI motivada permanecen preparadas por la Etapa A, pero no se consume ninguna decisión remota.
- **Datos personales**: el RUT almacenado exige RBAC por módulo, auditoría de lecturas/exportaciones, plazo de retención y minimización antes de habilitar rutas; todavía no hay exportación ni autorización externa.
- Validación: pruebas integradas en GitHub CI exact-HEAD; no declarar operación funcional ni cierre completo de #533 hasta completar etapa C/D. El QA se atiende en otro chat por orden del Sponsor.

## Próxima fase
1. Diseñar un servicio servidor Gran Tesorería capaz de cerrar/atestiguar obligaciones y pagos desde un inicio de cobertura definido, sin fingir completo si faltan cuadros, con manejo formal de rectificaciones y USD/CLP.
2. Servicio de evidencia V.M. completitud y vigencia de membresía; reflejar efectos limitados del Tribunal de Honor, no expedientes confidenciales.
3. Endpoints con autorización **exclusiva y verificada en backend** del órgano competente, resolución motivada/revocación, y eventos de auditoría; cierre transaccional solo si no hay pendientes, con digest verificable y bloqueo en DB.
4. PDF y CSV separados, cabecera en cada página, folio, firma en blanco, protección de inyección de fórmulas y acceso registrado para RUT; entregar contrato a Clau.
