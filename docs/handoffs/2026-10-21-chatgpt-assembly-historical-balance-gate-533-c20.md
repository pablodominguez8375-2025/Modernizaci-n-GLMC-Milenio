# #533 C20 — Integridad del importe acreditado en revisión preliminar

- Fecha: 21-10-2026. Responsable: ChatGPT. Issue #533.
- Base inmutable: `dev@28779aa4853897467c14e6e8fb0097ff03d68b35`; `main@6dfb9546a4873baff15955cf86abfd7d47e3d111` no se modifica.
- Alcance previsto: evitar que la proyección provisional afirme `MayVotePreliminary` si la evaluación no contiene un importe financiero histórico verificable (ausente o negativo), incluso cuando conserve referencias de fuente. Introducir pruebas unitarias negativas y conservar comportamiento con saldo cero acreditado.
- Archivos previstos: `AssemblyPreliminaryReviewEndpoints.cs`, pruebas nuevas del módulo, registro GOV-004 y este handoff.
- Guardarraíles: solo vista preliminar; `MayVoteConfirmed=false`; no cierre, padrón oficial, PDF, migración, decisión electoral ni nuevas atribuciones institucionales. No alterar UI, `main`, QA físico ni datos reales.
- No es un conector oficial Gran Tesorería: la autenticación de saldo y fuente sigue pendiente en #533; las pruebas de este incremento no equivalen a UAT.
- Estado al abrir PR: implementación, CI exact-head, Pages e instalable pendientes.
