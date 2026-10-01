# Cierre de pruebas HTTP del portal de insinuados — 2026-10-01 UTC

Issue #44; PR #233. Continúa el [handoff de implementación](2026-10-01-chatgpt-candidate-publication-http.md).

## Resultado integrado

- Base viva: dev@6b82802d1fe2588dda6788ab8f54d9593cf8029c; main@6dfb9546a4873baff15955cf86abfd7d47e3d111.
- PR #233 integrada por squash en dev@`3f852516be9acbc8cfbdb4c61b48993b5d8732d7`, head exacto `dc435be06a0653ce6cf4ae5b7b189850b50db3c6`.
- Gates exact-head SUCCESS: PMGM CI 36801495518 (#1723), Showcase 36801495489 (#1052), QA installable 36801495582 (#690).
- TRX descargado del artifact 11135521936: SHA-256 `db40f255a06928e6943456d23bc461f3a56e3204fe3dfd5ae9b950233c479ac7` verificado, **354 ejecutadas y aprobadas, cero fallos y cero notExecuted**.
- Las tres CandidatePublicationHttpWorkflowTests y el fixture corregido MemberSelfServicePostgreSqlTests constan Passed, con duración individual 1,4–2,1s.
- Cubre acceso de otro Taller, proyección mínima, vigencia/foto ausente, anónimo 401, foto segura 200 y cese 404 al vencer, expediente/foto privada/objeto documental 403 incluso para secretario de otro Taller y acceso legítimo del secretario propietario.
- Auth sintética con claims por petición; autorización del producto y PostgreSQL reales. Objeto PNG sintético con almacenamiento en memoria: no valida transporte S3, OIDC real, servidor ni UAT.
- El primer CI 36801111893 detectó dos fallos y no fue integrado: orden textual de Cache-Control y fixture de Mi ficha que usaba mes UTC. Se comparan ahora las directivas Private/NoStore y el fixture usa America/Santiago, igual que MemberSelfEndpoints. **No se modificó el comportamiento de producción ni las expectativas monetarias**.

## Archivos y coordinación

PR #233: nueva suite de pruebas, nueva QA específica y handoff nuevo; corrección puntual del fixture previo de Mi ficha. Reserva inicial y ampliación registradas antes de editar. Sin cruces con #131/#116/#60/#58/#45, sin archivos calientes ni migraciones. Sin cambios funcionales, roles, permisos, UI ni normas institucionales.

Este PR posterior incorpora sólo este handoff nuevo y **START-HERE +1/-0**, con enlace a ambos recibos. La Línea Base Maestra ya registra la intervención, primer fallo y correcciones con lectura de retorno (fecha nativa, enlaces conservados); recibirá SHA/gates/ZIP final después del merge.

## Publicación y cierre

Se verificarán los gates postmerge de #233 y de este PR, el `sourceSha` de `downloads/qa-current.json`, el SHA-256 externo, BUILD-INFO y todos los checksums internos del ZIP realmente servido. Los IDs/gates/digest finales se registrarán en #44, comentarios de los PR y Drive, sin reutilizar el digest del corte inicial.

El issue #44 podrá cerrarse **técnicamente** tras confirmar esa publicación y dejar registro completo. PR #45 sigue abierto por su alcance normativo/documental propio en REQ-025; no se edita ni se fusiona su rama histórica. Su propuesta incluye condiciones documentales de snapshot/configuración que no se declaran completadas por estas pruebas.

Demo: https://pablodominguez8375-2025.github.io/Modernizaci-n-GLMC-Milenio/
**Srv01 pausado #97: despliegue QA pendiente, sin instalación, QA física ni UAT.** Main permanece intacta; los workflows Pre-UAT/QA y capturas automatizadas no representan aceptación operacional o institucional. UI QA v0.63 conservada.
