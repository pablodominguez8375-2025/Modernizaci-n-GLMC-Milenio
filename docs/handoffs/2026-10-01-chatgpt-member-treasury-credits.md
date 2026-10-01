# Reserva — crédito propio en Mi ficha — 01-10-2026

Issue #254; base dev 86945d34278f874313873f56f7f0a3f9dc417a6c, main 6dfb9546a4873baff15955cf86abfd7d47e3d111. GOV-003: etiqueta/draft y reserva antes de programar. Continúa cierre #250/#251, sin rehacerlo.

Brecha: API entrega unappliedCredits, contrato/UI no los muestran. Consulta por recibo/moneda sin compensar deuda ni convertir; pruebas de proyección tras ajustes y privacidad. Dominio Tesorería/ChatGPT; UI P3 #253 de Claude separada, sin cruce de sus archivos ni archivos calientes. Cruce no caliente #58 en MemberPortalPage.tsx/membershipApi.ts informado sin tocar su rama. Estimación 01-10-2026 sujeta a gates.

Archivos: frontend/src/MemberPortalPage.tsx, frontend/src/MemberTreasuryCredits.tsx, frontend/src/MemberTreasuryCredits.test.tsx, frontend/src/api/membershipApi.ts, frontend/src/api/membershipApi.self.test.ts, backend/tests/PMGM.Api.Tests/Integration/MemberSelfTreasuryCreditsPostgreSqlTests.cs, docs/qa/PMGM-QA-MEMBER-CREDITS.md, docs/handoffs/2026-10-01-chatgpt-member-treasury-credits.md.

Fuentes GitHub/Drive y ramas/PR/comentarios/reservas/Actions comprobados. #253 afirma P3 aprobado, pero la adenda Drive leída aún figura PROPUESTA; se registra para que su dueño concilie el estado. No se toma ni fusiona #253 ni su delegación de auditoría. #116 sigue restringido, #1 no promover. #58 conserva otras brechas y #191 presentación institucional/operación. Main congelado y srv01 pausado; despliegue QA pendiente, sin QA física/UAT. START-HERE exactamente una línea en PR documental posterior al merge. Implementación/gates/publicación todavía pendientes.

## Implementación en la rama

PR #255. Se añade contrato opcional MemberTreasuryCredit, mock de dos recibos ficticios CLP/USD y componente MemberTreasuryCredits dentro de cartola. Recibo/fecha/moneda/disponible sin compensar deuda ni mostrar IDs/referencias bancarias. No modifica backend productivo ni estilos/identidad.

Dos escenarios HTTP PostgreSQL prueban corrección que libera crédito, anulación, recibo exhausto, otro Hermano excluido aunque cambie query, recibo propio de otro Taller sin cargos de su moneda, no-cache, originales y deuda intactos. API existente conserva cálculos. Pruebas frontend renderizan el componente y verifican contrato/compatibilidad/sintéticos. Matriz docs/qa/PMGM-QA-MEMBER-CREDITS.md. Local: 11 pruebas dirigidas PASS, lint PASS, tsc/build PASS (confirmar salida final); backend requiere CI con PostgreSQL, sin SDK local.

La base 86945d34278f874313873f56f7f0a3f9dc417a6c incorpora #253/UI v0.66; no presentar P3 como pendiente técnico de implementación. Su auditoría de exportaciones en servidor sigue delegada y sin completar; adenda Drive aún PROPUESTA por conciliar, aviso al dueño en #253. No editar ramas ajenas ni reclamar #252 de Claude.

Gates exact-head y publicación de este nuevo corte aún pendientes; no reutilizar evidencia de #250/#251/#253. Handoff de cierre y START-HERE +1 línea en PR documental posterior, con recibos finales GitHub/Drive que registrarán squash/sourceSha/ZIP/SHA-256/MANIFEST/SOURCE_SHA/BUILD_RUN_ID propios.

