# Cierre técnico Gran Hospitalaria — #266 / #348

Base dev 8c092e2e37aaa5b8532b3f127c0e26a25ba2fa70; main 6dfb9546a4873baff15955cf86abfd7d47e3d111. Implementación PR #348 validada en HEAD355dbbf2b2c9d5c1faa678ccd6c46b3d63a15d71; integrada por squash en dev6d8751b11c93e7c91a1c69daafe484e14407c1a7, árbol10be747e85d702d4001f8124f27d760540e28f5b idéntico al probado. Este PR posterior sólo agrega este handoff y una línea START-HERE; código funcional preservado.

## Resultado

Gran Hospitalaria usa grants hospitalaria de ámbito Orden encima de autoridad institucional: view consulta tarifas/casos/rendiciones/detalle administrativo; create tarifa y sincronización explícita; write revisiones y regularidad manual. Perfil de Taller no concede atribuciones de Orden; una asignación histórica de Orden permanece managed tras revocación/expiración y no vuelve a legacy. Lectores institucionales mínimos de regularidad preservados. Proyección GET /api/hospitalaria/acceso sólo devuelve versión/managed/actions, private/no-store, sin sujetos/asignaciones. API real, demo y pantalla concordantes; respuestas tardías no se publican en otro sujeto/cliente/Taller/período/versión. Regularidad editable sólo se monta con write, por contexto. Sin migración, sin cambios a importes/cálculos/cargos/firmas. [Diccionario/registro GOV-004](../modelo-datos/cambios/2026-10-06-issue-266-gran-hospitalaria-acceso.md).

## Evidencia exact-head funcional

- CI37546187127 SUCCESS. TRX508 ejecutadas/PASS, cero fallos/omisiones: incluye dos unitarias nuevas de revocación/vigencia/ámbito y HTTP PostgreSQL de consulta sin mutación, denegaciones, autoridad, revisión válida/auditoría y revocación. Artifact11451117057 SHA2566f3af32fb11552f65afbd20216882dd4400d1b58127587155009407f856011c8 verificado.
- Frontend442 PASS; lint/build y gates privacidad/clasificación/migraciones PASS.
- Showcase37546187083 SUCCESS:21 perfiles/580 estados móviles,748 PNG. Artifact11450573492 SHA256f5432f5bb68a3d9c5651b4808cbeaf262284eb5f1ac219f12e7812b7f5a513ec, CRC y recuento verificados. Capturas Gran Hospitalaria móvil/escritorio inspeccionadas; auditoría automatizada no es UAT ni prueba de dispositivos institucionales reales.
- QA37546187115 SUCCESS. Artifact11449888935 SHA25666c1ce87a828c0380f69d8f6961253e56876c57a97cbeedc7dcfb031eefb296f; ZIP interno aca01f0b2e36453911cce3ff945f50b47065de40b9e557be2a0623675401b803. CRC/1070MANIFEST, SOURCE_SHA355dbbf2/BUILD_RUN_ID37546187115 y trece archivos críticos exactos PASS. Paquete de rama, no confundir con ZIP publicado postmerge.

## Estado y continuidad

Postmerge funcional/publicación y gates de este PR documental se verifican en sus recibos GitHub/Drive posteriores. No usar este snapshot para afirmar publicación final todavía. Verificar HEAD vivo, CI/Showcase/QA/Pre-UAT, qa-current.json, SOURCE_SHA/checksum/MANIFEST/archivos críticos del paquete público; registrar recibo final con SHA en PR/Issue/Línea Base. GitHub y Drive iniciales con lectura de retorno; reserva funcional liberada, documental limitada a dos archivos nuevos/cambio enlazado.

#266 permanece abierta: navegación global, demás módulos/operaciones, impresión/exportación y regularidad individual fuera de esta cobertura. Conservar revisión de scopes y autores institucionales al extender. No duplicar Tesorería/tarifario/Hospitalaria local ya integrados. #191 aceptación operacional; #66/PR#116 histórica NO FUSIONAR TODAVÍA; #97 mantiene srv01 y UAT pausados. Despliegue QA pendiente. Main congelada; ningún instalable acredita instalación/schema físico/QA institucional.

Demo: https://pablodominguez8375-2025.github.io/Modernizaci-n-GLMC-Milenio/
PR funcional: https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/pull/348
