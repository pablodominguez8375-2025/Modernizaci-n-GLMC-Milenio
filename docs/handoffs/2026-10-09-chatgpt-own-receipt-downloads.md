# Continuidad — comprobantes personales

Residual de #36. Base dev 45ae06a89be697bc7e91bdb6bca263bfc426eea6; main 6dfb9546a4873baff15955cf86abfd7d47e3d111. Sin PR funcional de otro agente abierto. Drive revisado por diferencias: sólo el recibo propio de #383, sin instrucciones nuevas.

Reserva: MemberOwnHistoryEndpoints.cs, nuevo MemberOwnReceiptEndpoints.cs, MemberSelfTreasuryCreditsPostgreSqlTests.cs; membershipApi.ts, MemberPayments.tsx, MemberPortalPage.tsx y componentes/tests nuevos de comprobantes. Implementar descarga imprimible desde recibos originales, con identidad propia, sin datos de terceros ni URLs públicas. Recibos ajenos/inexistentes 404; identidad anónima 401; auditoría y private/no-store.

Pendiente implementar, probar y publicar. No cerrar #36 globalmente: conserva foto personal y aceptación institucional. No tocar main ni srv01/UAT. El PR contendrá el recibo final y reemplazará este estado provisional.

## Actualización de la implementación

API privada de comprobantes de Tesorería y Hospitalaria, frontend de descarga HTML, datos de demostración y pruebas agregadas en PR #385. La prueba nueva del documento pasó en la primera ejecución; Showcase falló por una aserción de firma anterior en mis-pagos-claros.test.ts y ya se corrigió la aserción en la rama. CI exact-head y publicación siguen por verificar. No fusionado en dev. Registro de Drive: https://docs.google.com/document/d/105IxQBsSg8YlzLPNwBcn30d5Yh-LkuyODrO-OXCg204/edit


## CI seguimiento (noche de Chile, 8 de octubre)

- PR #385 @48abfc0: Showcase #37864721001 **SUCCESS**, QA Installable #37864721203 **SUCCESS**; PMGM CI #37864720992 falló **una** prueba de 54 en shard b: `MemberHospitalariaV2HttpTests.Contribution_is_per_workshop_and_reconciled_separately` (`Assert.Single` lista vacía, línea 119).
- Diagnóstico confirmado: `HospitalariaContributionGenerator.GenerateAsync` filtraba alta del Taller con `CreatedAtUtc.UtcDateTime` y comparaba con fecha de `Today()` (día civil Chile). Desde el cambio de día UTC, un Taller creado de noche en Chile figura como UTC mañana y omite su aporte. Conclusión: bug de calendario local, no de las nuevas rutas de comprobante. 
- Fix mínimo incorporado: conversión de `CreatedAtUtc` al huso `America/Santiago` previo a comparar con `cutoff`, sin modificar tarifario, montos ni periodos. Se agregó regresión con `00:30Z` del día UTC siguiente, todavía día actual en Chile.
- Commits: `9e3cbfa47cb5d973dc8da4119b0d13c2612367ca` (fix), `872cecbd79d58b76052c195e5c86f2acb5ff8888` (test).
- No integrar hasta SUCCESS de **PMGM CI + Showcase + QA Installable** del nuevo HEAD exacto, merge permitido y comprobación de PR concurrente #387 (Claude). Issue #97 mantiene srv01 detenido. Mantener Drive y PR actualizados al integrar.
