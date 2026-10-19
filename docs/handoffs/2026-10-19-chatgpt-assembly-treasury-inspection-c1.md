# Desarrollo #533 — Etapa C1: inspección read-only de cuadros GT (19-10-2026)

## Alcance
- Base dev `4d9e4ab7f768745968ea63a0bc16bede99d1621b` posterior a la PR #562 PASS 3/3. Rama `feature/gran-asamblea-inspeccion-gt-20261019-gpt`, sin tocar `main`, QA, servidores, credenciales ni la UI de Claude.
- Permitir **inspección interna y sin endpoint HTTP** de cuadros mensuales GT del mismo Taller, dentro de cobertura/corte solicitados; detectar lagunas, rectificaciones, monedas múltiples, diferencias y recepción bancaria fuera de corte.
- La inspección **nunca emite `AssemblyGrandTreasuryBalance` habilitante**, porque falta una certificación institucional completa, histórica y autorizada desde Gran Tesorería. Diferencia cero entre obligación y pago de cuadros seleccionados NO implica ausencia de cuotas atrasadas omitidas.
- Mantener datos financieros, identidad del pagador, RUT y referencia bancaria fuera de las proyecciones de salida.
- Agregar pruebas unitarias y gate exact-HEAD. Trabajo únicamente en código, servidor QA asignado a otro hilo.

## Contrato de seguridad
- `PmgmDbContext.TreasuryMonthlyStatements` es la fuente técnica autenticada por la DB, **no necesariamente completa**. El inspector solo crea diagnósticos para que Gran Tesorería certifique un libro histórico exhaustivo. 
- El histórico certificado a utilizar en voto será el de `AssemblyGrandTreasuryLedgerPolicy`, que exige `SourceAuthenticated` y `CompletenessCertified` respaldados por un nuevo procedimiento con RBAC y auditoría; no dejar que interfaz manipule estos campos.
- Pendiente: procedimiento formal de certificación por Gran Tesorería (incluye saldos antiguos, rectificaciones y USD por Oriente); persistencia/atestación del corte; conexión con régimen RI, Tribunal de Honor y cierre seguro.

## Continuidad
- Issue #533, PR #562 integrada a dev, PR nueva para C1; registro espejo Drive en Línea Base Maestra; ningún QA físico.

## Contrato publicado en PR #563
- Clase interna `AssemblyTreasuryStatementInspection` consulta `PmgmDbContext.TreasuryMonthlyStatements` con filtro por OrganizationId y meses; `AsNoTracking` + `AsSplitQuery`. Inspecciona detalles en memoria del backend, pero la respuesta diagnóstica no incorpora RUT, nombre del pagador, referencias bancarias ni detalle por hermano.
- Invariantes: cobertura mensual completa, Taller único, monedas homogéneas, estado reconciliado/cerrado, sin duplicados/rectificaciones irresueltas, fechas dentro del corte, recepción bancaria registrada, identidades conciliadas y diferencia cero en cuadros observados.
- La respuesta tiene `MayAuthorizeSuffrage=false` de forma **incondicional**. Una diferencia cero en los cuadros observados nunca produce `AssemblyGrandTreasuryBalance`, ni permite saltarse `AssemblyVoterEligibilityPolicy`. Reporta siempre `HISTORICAL_CERTIFICATION_REQUIRED`, incluso cuando `ObservedRowsConsistent=true`.
- Unit `AssemblyTreasuryStatementInspectionTests`: meses faltantes/duplicados, moneda extranjera, Taller ajeno, estado borrador, fechas bancarias tardías, pago posterior al corte y saldos incompletos; real PostgreSQL `AssemblyTreasuryStatementInspectionPostgreSqlTests`: lectura aislada entre dos Talleres sintéticos. Registrada en shard b de `.github/backend-integration-shards.json`.
- No guardar transacciones individuales ni guardar identidad del pagador en vistas de Asamblea. Revisar la futura proyección de BD para seleccionar solo campos mínimos antes de exponer un endpoint protegido.
- CI exact-head requerido y pendiente al publicar; no fusionar ni alterar host QA si no aprueba.

## Estado y bloqueo siguiente
El único mecanismo autorizado para `AssemblyGrandTreasuryLedgerPolicy.Project` será un **certificado de cobertura histórica y saldo** que Gran Tesorería emita expresamente: identidad verificada del emisor, atribución de Taller, oriente/moneda, inicio de cobertura, corte, rectificaciones y pagos de períodos anteriores. Esa certificación debe estar auditada y protegida contra modificaciones retroactivas. La fuente de cuadros actuales no acredita, por sí sola, inexistencia de deuda histórica. Quedan pendientes diseño de acto de cierre financiero y autorización RBAC institucional, además de integración de Oficina V.M. y Tribunal de Honor.
