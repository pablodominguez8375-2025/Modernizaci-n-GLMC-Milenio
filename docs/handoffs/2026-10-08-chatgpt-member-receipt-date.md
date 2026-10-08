# Continuidad — fecha real del recibo en Mi ficha

Issue #382, residual de #36/#191. Base dev: 28213a6e0efed8b9ce16fcf0ebfc491472192379; main: 6dfb9546a4873baff15955cf86abfd7d47e3d111.

Alcance reservado: MemberSelfEndpoints.cs y MemberSelfTreasuryCreditsPostgreSqlTests.cs. La proyección de pagos personales debe mostrar Receipt.PaymentDate, no la fecha posterior de imputación. Se preservan las fechas de imputación, el período de cuota, los importes y las monedas.

Drive por diferencias revisado tras cierre #380: no hay instrucciones nuevas que cambien este alcance. La limpieza quedó cerrada (171 DELETE efectivos); no repetirla. Única PR abierta al inicio: #1 draft.

Pendiente: implementar, verificar prueba HTTP PostgreSQL en CLP/USD y gates exact-head; integrar y verificar Pages/QA del mismo SHA. Sin cambios de UI/migraciones. srv01/QA física/UAT pausados por #97; no promover main. El PR contendrá el recibo final de esta entrega.
