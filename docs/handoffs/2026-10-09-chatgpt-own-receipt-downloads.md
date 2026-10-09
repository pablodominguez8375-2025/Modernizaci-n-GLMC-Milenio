# Continuidad — comprobantes personales

Residual de #36. Base dev 45ae06a89be697bc7e91bdb6bca263bfc426eea6; main 6dfb9546a4873baff15955cf86abfd7d47e3d111. Sin PR funcional de otro agente abierto. Drive revisado por diferencias: sólo el recibo propio de #383, sin instrucciones nuevas.

Reserva: MemberOwnHistoryEndpoints.cs, nuevo MemberOwnReceiptEndpoints.cs, MemberSelfTreasuryCreditsPostgreSqlTests.cs; membershipApi.ts, MemberPayments.tsx, MemberPortalPage.tsx y componentes/tests nuevos de comprobantes. Implementar descarga imprimible desde recibos originales, con identidad propia, sin datos de terceros ni URLs públicas. Recibos ajenos/inexistentes 404; identidad anónima 401; auditoría y private/no-store.

Pendiente implementar, probar y publicar. No cerrar #36 globalmente: conserva foto personal y aceptación institucional. No tocar main ni srv01/UAT. El PR contendrá el recibo final y reemplazará este estado provisional.

## Actualización de la implementación

API privada de comprobantes de Tesorería y Hospitalaria, frontend de descarga HTML, datos de demostración y pruebas agregadas en PR #385. La prueba nueva del documento pasó en la primera ejecución; Showcase falló por una aserción de firma anterior en mis-pagos-claros.test.ts y ya se corrigió la aserción en la rama. CI exact-head y publicación siguen por verificar. No fusionado en dev. Registro de Drive: https://docs.google.com/document/d/105IxQBsSg8YlzLPNwBcn30d5Yh-LkuyODrO-OXCg204/edit
