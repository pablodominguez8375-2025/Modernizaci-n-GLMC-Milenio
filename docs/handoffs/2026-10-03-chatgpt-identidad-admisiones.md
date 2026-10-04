# Handoff — identidad mínima de Admisiones — ChatGPT

Issue #275 / PR draft #276. Base rama `5f3df076a1bdf1c4c68496f4f4b490a0867bc272`; dev `b070dbc71d25f811f0b70999dab8f378a5343cc8`; main `6dfb9546a4873baff15955cf86abfd7d47e3d111`. Alcance/reservas/estimación registrados en #276 antes de programar. Sin PR Claude abierto encontrado, #267 reserva Authorization preservada; no App/CSS/migración nuevos.

Corrección: lookup mínimo para miembros retirados en historia local o de otro Taller por número exacto; incorporación selecciona Persona sin Member de expedientes autorizados. No abrir búsqueda global de People ni fichas privadas para conocer PersonId. Alta externa nueva pendiente explícita. Selección manual, sin primer resultado automático; limpieza al cambiar contexto y guardia contra respuestas tardías; UI sin UUID internos visibles.

Archivos: AdmissionPersonLookup.cs, mapeo en AdmissionEndpoints.cs, prueba HTTP PostgreSQL AdmissionPersonLookupHttpTests.cs, AdmissionsPage.tsx/test, api/admissionLookup.ts/test y pmgmApi.ts. [Salida GOV-004](../modelo-datos/cambios/2026-10-03-issue-275-identidad-admisiones.md). Frontend local 340/340 PASS, lint/build PASS; backend pendiente CI propio (sin SDK local). Resultado final/SHA/gates en recibos GitHub/Drive.

Drive: búsqueda de novedades desde último registro sólo devuelve actualización propia Línea Base; sin fuente normativa nueva identificada. Se conserva Protocolo 2026, Constitución/Reglamento y regla CRV acordada. Gates previos 5f3df07: CI #1841, Showcase #1202 y QA #840 SUCCESS; 401 backend/333 frontend, paquete QA MANIFEST 907/907 verificado en recibo anterior. Son antecedentes, no certificación del nuevo corte.

Pendientes: validación backend/Showcase/QA nuevos; alta externa sin duplicar identidad ni crear Member previo a resolución; validación de selección en POST por ID directo; circuito completo y demo central/persistencia, comisión/autoridades, CRV/evidencia y traslado/materialización idempotentes. #116 sigue NO FUSIONAR TODAVÍA; #276 draft, no cerrar por lookup aislado.

No merge a dev ni modificación main/srv01; despliegue QA pendiente, sin QA física/UAT. START-HERE (+1 línea) sólo por PR documental posterior al merge. Paquete propio/qa-current.json/SHA256/MANIFEST/SOURCE_SHA/BUILD_RUN_ID se verifican antes de declarar publicación; no reutilizar corte previo.
