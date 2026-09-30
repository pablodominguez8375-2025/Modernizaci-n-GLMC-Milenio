# Cierre documental de auditoría de históricos — 2026-09-30

Issue #230; continuación del [informe](2026-09-30-chatgpt-auditoria-antiguos.md).

## Integración y decisiones verificadas

- Base de la auditoría: dev@972f03d251f17bdcac1546cec27ef6cee02c770b; main@6dfb9546a4873baff15955cf86abfd7d47e3d111.
- PR #231 integrada por squash en dev@`7497bba32feab3fc562e66908d872ff896b0766b`, desde head exacto `f30f08c72f096a8f03d3991a4fd2d5096cd9ab71`.
- Gates del head: PMGM CI 36782727735 (#1717, 351/351 backend); Showcase 36782727767 (#1044); QA installable 36782727729 (#682), todos SUCCESS.
- Se compararon **8 PR y 24 issues históricos**. Comentario individual en cada caso; cierres comprobados por lectura de retorno. **PR #43 y #67 cerrados sin merge como reemplazados; issue #46 cerrado como completado.** Se preservaron las ramas históricas.
- Se mantienen **6 PR y 23 issues históricos abiertos**. El detalle de cada pendiente está en el informe, incluyendo la diferencia entre funcionalidad existente y pruebas/aceptación incompletas de #44. El conteo excluye los registros de esta auditoría.
- Precisión #190: **Perú en USD y cuota ordinaria oficial USD 6 ya fueron aprobados e integrados en #213.** No se reabre esa decisión. El remanente comprende el tarifario completo y categorías/fuentes sin definición oficial o aceptación completa. La referencia genérica a tratamiento USD del informe no significa ausencia de lo ya integrado; precisión añadida al issue.
- Pruebas frontend dirigidas: 3 archivos, **17/17** aprobadas. Backend local no ejecutado; la evidencia backend corresponde a CI con PostgreSQL.
- Sin funcionalidades, migraciones, permisos ni reglas institucionales nuevas. Sólo registro documental y metadata de GitHub.

## Registro y publicación

Este PR documental agrega este handoff nuevo y **una sola línea al final de START-HERE.md**, enlazando el informe y el cierre. No reescribe ningún bloque histórico. La Línea Base Maestra ya recibió la auditoría y fue leída de vuelta, con fecha nativa y enlaces previos conservados; recibirá el SHA/gates/publicación final después de este merge.

Los gates post-merge de #231 y de este PR se comprobarán en sus SHA respectivos y se registrarán en #230, comentarios del PR y la Línea Base. Antes de fusionar este documento se exige nuevamente CI, Showcase y QA installable exact-head SUCCESS y dev vivo sin movimiento. No se presenta una ejecución pendiente como exitosa.

La demo y el paquete del corte inicial `972f03d` estaban verificados (809 checksums); ese digest histórico no se reutiliza como digest del corte documental nuevo. La verificación final leerá `downloads/qa-current.json.sourceSha`, SHA-256 del ZIP publicado, BUILD-INFO y checksums internos del **paquete servido realmente**. Enlace: https://pablodominguez8375-2025.github.io/Modernizaci-n-GLMC-Milenio/

## Pendientes y límites

Prioridad de recuperación: #131 y #45 (matriz/REQ-025), #44 (escenarios HTTP), #116/#66 (admisiones), #60/#59 (estados), #58/#36 coordinados con #190/#191 (cuenta personal). #3 conserva entrega efectiva de correo, #4 vista semanal, #2 preservación maestro/derivados, #5 dominio archivístico y #37 formularios configurables.

**Srv01 pausado por #97: despliegue QA pendiente; sin instalación, QA física ni UAT.** Permanecen las olas UAT #25/#27–#32 y la promoción #1; main no se modifica. Los gates Pre-UAT/CI y las capturas automatizadas no representan aceptación institucional.

Al retomar: leer ambos handoffs, Línea Base y comentarios individuales, consultar HEAD vivos y rebasar los PR históricos conservando el contrato móvil/SHA/UI v0.63. Ninguna rama antigua debe fusionarse por su antigüedad o por el cierre de una implementación alternativa.
