# #533 C20 — Integridad del importe acreditado en revisión preliminar

- Fecha: 21-10-2026. Responsable: ChatGPT. Issue #533.
- Base inmutable: `dev@28779aa4853897467c14e6e8fb0097ff03d68b35`; `main@6dfb9546a4873baff15955cf86abfd7d47e3d111` no se modifica.
- Alcance previsto: evitar que la proyección provisional afirme `MayVotePreliminary` si la evaluación no contiene un importe financiero histórico verificable (ausente o negativo), incluso cuando conserve referencias de fuente. Introducir pruebas unitarias negativas y conservar comportamiento con saldo cero acreditado.
- Archivos previstos: `AssemblyPreliminaryReviewEndpoints.cs`, pruebas nuevas del módulo, registro GOV-004 y este handoff.
- Guardarraíles: solo vista preliminar; `MayVoteConfirmed=false`; no cierre, padrón oficial, PDF, migración, decisión electoral ni nuevas atribuciones institucionales. No alterar UI, `main`, QA físico ni datos reales.
- No es un conector oficial Gran Tesorería: la autenticación de saldo y fuente sigue pendiente en #533; las pruebas de este incremento no equivalen a UAT.
- Implementación en rama: condición `GrandTreasuryPendingAmount != 0m` hace fallar cerradamente la proyección `habilitado` sin cero acreditado y sin prueba específica de excepción RI. Pruebas unitarias agregadas a `AssemblyPreliminaryReviewProjectionTests`: monto null, negativo, positivo y cero.
- Registro GOV-004: `docs/modelo-datos/registros/2026-10-21-chatgpt-assembly-historical-balance-533-c20.md`.
- Estado: PR #619 DRAFT; CI exact-head, Pages e instalable por verificar. Compilación local .NET no disponible en este entorno. No QA físico, no UAT, no cierre de #533. La revisión de Drive del 21-10 identificó adendas UI y continuidad #533 sin nueva decisión que permita saldar deuda mediante un dato digitado.
