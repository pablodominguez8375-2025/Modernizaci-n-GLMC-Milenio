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
