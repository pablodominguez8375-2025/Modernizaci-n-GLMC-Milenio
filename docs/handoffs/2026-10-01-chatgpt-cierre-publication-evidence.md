# Cierre de evidencia histórica de publicación — 2026-10-01

Issue #237; PR funcional #239 y documental #240. Continúa [handoff técnico](2026-10-01-chatgpt-publication-evidence.md).

## Integración técnica verificada

Inicio dev `8fd41253f9bb25acc4b8df1c7389873f12a34279`; main `6dfb9546a4873baff15955cf86abfd7d47e3d111`. PR #239 head `352ab7e5dbe525bd61e9ed9a8e72f64877b74583` integrada por squash en dev `2b0a0786e05b0cafb40cd50734394f9c23747427`. Gates exact-head SUCCESS: CI 36862436827, Showcase 36862436813 y QA 36862436878. Base viva, PR head, seis PR abiertos y archivos comprobados antes de squash; cruces no calientes con #116 informados, sin editar ramas ajenas.

TRX del HEAD final: artefacto 11162321160, SHA-256 `91bca0086e74da1a911cce76a95861dac648ffb180b54e135745f7d7086da4c1`; 368 ejecutadas, 368 PASS, 0 FAIL/omitidas, 14 pruebas nuevas PASS incluida HTTP PostgreSQL. Frontend 233/233 PASS; lint, TypeScript/Vite, diff --check y gates locales de privacidad (12), clasificación (99) y migraciones (58) PASS. No se crean migraciones.

La aprobación guarda atómicamente en su auditoría la versión documental de foto y la política con ID/vigencia/campos/fundamento. La foto pública no cambia al cambiar la referencia de la ficha; autorización conserva la evidencia original. La política versionada solo admite los tres campos aprobados y fechas hacia adelante en America/Santiago. El adaptador QA mantiene el historial vacío de la política base no persistida y respeta intervalos de versiones futuras.

Legado explícito: si falta evidencia original, se conserva la consulta previa y la autorización registra `legacy_not_recorded`; no se inventa una foto histórica. Si hay evidencia reconocida pero inválida, se bloquea la foto pública. No se amplía el DTO público ni permisos, pagos, elegibilidad o normas institucionales. No eliminar auditorías/versiones documentales u objetos requeridos por las referencias conservadas.

La prueba HTTP usa API y autorización productivas con PostgreSQL; fotos/fechas del fixture se cambian directamente para comprobar independencia histórica. Transporte de notificaciones sustituido, objetos en memoria y autenticación sintética: no acredita SMTP/OIDC/S3 reales. Se verifica el rechazo sin balotaje y se completa el antecedente aprobado; el guard no se elimina. Correcciones de fixture y paridad están registradas, sin aceptar gates de heads anteriores.

## Registro documental y publicación

#240 agrega exactamente una línea a START-HERE, este handoff nuevo y sustituye solo la frase pendiente del seguimiento técnico REQ-025. Secciones y requisitos previos, incluidos derechos de ceremonia, permanecen. Registro inicial de revisión en Línea Base verificado, con fecha nativa y estructura/chips conservados.

Este archivo no puede anticipar su propio SHA de squash ni el ZIP servido por Pages. Tras gates y squash de #240, el recibo persistente de cierre en [Issue #237](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/237) y PR #240 registra: dev final, gates del corte, sourceSha de qa-current.json, nombre y SHA-256 del ZIP descargado, checksums internos, SOURCE_SHA y BUILD_RUN_ID real, y lectura de retorno GitHub/Drive. Consultar ese recibo como parte de este handoff. No reutilizar el digest del paquete previo ni confundir un artefacto PR con el paquete servido.

Demo: https://pablodominguez8375-2025.github.io/Modernizaci-n-GLMC-Milenio/ . Hasta comprobar el recibo final, publicación y cierre documental permanecen pendientes. La integración funcional no acredita instalación física.

## Continuidad

Main permanece congelado. **srv01 pausado, despliegue QA pendiente**, sin instalación, QA física ni UAT. #191 conserva remanentes contables/operacionales; #190 USD Perú ya integrado no se reabre. PR históricos #131/#116/#60/#58/#1 no se fusionan por antigüedad y deben consultar/rebasar sobre dev vivo antes de continuar. No tocar identidad, archivos calientes o ramas reclamados por otros agentes.
