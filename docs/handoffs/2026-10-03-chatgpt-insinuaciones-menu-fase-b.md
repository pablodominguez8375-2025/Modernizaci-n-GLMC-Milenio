# Handoff — Fase B: Insinuaciones e Iniciación

Fecha: 2026-10-03 America/Santiago. Issue #310.
Base dev 8b393114b38a6e9cc7bbd82ab249edd810936e5f. Main 6dfb9546a4873baff15955cf86abfd7d47e3d111 sin cambios.
Delegación aprobada: Drive 1bKnlHTbwyIV1TMLMerkjeYL_uQtGBXTy8mzuPey5Rp0.
Revisión inicial: postmerge #309 CI/Showcase/QA/Pre-UAT SUCCESS; no nuevas fuentes Drive salvo registro de cierre. Fuentes frontend/scripts locales 248/248 blobs coinciden con árbol vivo. No PR activo de Claude; #267 reservado y no tocado.
Alcance: una entrada Insinuaciones e Iniciación activa en candidates/candidateProfile/initiationCircuit; pestañas por permisos existentes; Hermano sin pestañas. Búsqueda/NAV_HINTS/auditoría y pruebas de regresión. No cambia reglas/estados/API/permisos, identidad ni ActionDrawer. Despliegue QA y UAT institucional pendientes. Main/srv01/#116/#267 intactos.
Archivos reclamados: frontend/src/App.tsx; frontend/src/menus-sin-repetir.test.ts y pruebas afectadas; .github/scripts/capture-showcase-views.mjs; este handoff.
Gates/resultados/SHA/Pages/QA se registrarán al cerrar en el recibo de PR y Línea Base. START-HERE sólo en PR documental posterior. Próximo: implementar y probar; no fusionar sin CI/Showcase/QA exact-head SUCCESS y base dev vigente.

Implementado: menú/búsqueda únicos, pestañas por capacidades y marcador activo único incluso al entrar desde Secretaría. Se preserva acceso directo/ActionDrawer. 394 frontend PASS, lint/tsc/build PASS locales; sintaxis del script PASS. Auditoría amplía cinco perfiles (Hermano, Venerable, Secretaría Taller, Gran Secretaría, Régimen Interior) a ocho tamaños, recorre todas las pestañas autorizadas y verifica 11 entradas para Venerable. Gates/publicación aún pendientes antes de merge.
