# Cierre técnico Gran Tesorería — #266 / #350

Base dev89772b2d72639e09872b2b1f5bfceee65fca17cc; main6dfb9546a4873baff15955cf86abfd7d47e3d111 congelada. HEAD funcional probado aa88e7c2d1ace6667d4562347769d5d47e9f44c5; squash integrado dev275d719720f23d0b3cb8078462f61ef2360b83e9, árbol51245566a4fa2615bb1509b37dbec48e21540c83 idéntico al probado. Este PR documental sólo agrega este handoff y una línea START-HERE; código funcional preservado.

## Resultado

Permisos treasury de Orden intersectan autoridad institucional de Gran Tesorería: view para cuadros mensuales/lista/detalle, regularidad administrativa de Taller/persona, derechos ceremoniales y zonas; write para conciliación, regularidad manual y abonos. create del tarifario anterior permanece. Historial revocado/expirado/futuro no vuelve a legacy; grants de Taller no conceden atribuciones de Orden. Proyecciones mínimas de regularidad para otros lectores institucionales preservadas. GET /api/tesoreria/acceso sólo versión/managed/actions de sesión, private/no-store. Pantalla y demo concordantes, sin montar datos protegidos al denegar; contexto de cliente/sujeto/versión/capacidades/sección remonta las vistas; sondeo estable conserva formularios. Regularidad descarta respuesta tardía de otro Taller/fecha. No migración, cargos/firmas/importes/cálculo/idempotencia/retención preservados. [Diccionario/GOV-004](../modelo-datos/cambios/2026-10-06-issue-266-gran-tesoreria-acceso.md).

## Evidencia exact-head

- CI37551413842 SUCCESS, incluida integración PostgreSQL/S3/ClamAV, ingreso autenticado y recuperación. TRX511 ejecutadas/PASS, cero fallos u omisiones; dos unitarias y un HTTP nuevos verifican histórico/vigencia/ámbito/autoridad, consulta sin mutación, denegación sin auditoría de éxito, conciliación/regularidad válida y revocación, manteniendo proyección mínima. Artifact11452938198 SHA25692bc3021b331e5a712eb35212f22fd581912fd0c14bedc8ca8d34994327a9c17 comprobado.
- Frontend447 PASS; lint/build, privacy/classification/migration PASS.
- Showcase37551413891 SUCCESS,21 perfiles/580 estados móviles; artifact11452919221 SHA256ab5b57675e40b0e1671f428500da30d3d4c9566d773e8e3f99d722a08ede0b84,CRC y748PNG verificados. Gran Tesorería1440×900 y tarifario360×800 inspeccionados. Gate anterior37551035351 detectó wrapper que afectó ancho del tarifario; corregido mediante Fragment con clave manteniendo DOM/CSS, gates frescos de cabeza actual PASS. No afirmar que automatización o capturas son UAT ni validación de roles JWT real en dispositivos físicos.
- QA37551413899 SUCCESS, artifact11452523755 SHA2560f349e65cb1648328671d37591d243694e557aa17570322589e9895ee6d6f9f5; ZIP interno6172051a9cbe12b608ea3afac7b4997ddea2b545c3ccd526aeb6d8de7873c13a. CRC/1079MANIFEST, SOURCE_SHAaa88e7c2/BUILD_RUN_ID37551413899 y19 archivos del corte exactos PASS. Artefacto de PR: no confundir con ZIP público postmerge.

## Continuidad

Drive raíz y subcarpetas del proyecto revisados desde cierre anterior: sólo Línea Base actualizada con recibo previo, sin instrucciones nuevas aplicables. PR/Issue/Drive con seguimiento y lectura de retorno. Reservas funcionales liberadas; PR posterior sólo estos dos archivos documentales. Solapes no calientes declarados con #60 (TreasuryEndpoints.cs) y #116 (CeremonyEndpoints.cs/pmgmApi.ts). No fusionar ni modificar PR históricas.

Snapshot de integración: publicación/gates de este PR documental todavía en verificación. Consultar recibos posteriores de PR/Issue #266/Línea Base para SHA final y checksum publicado. Verificar dev vivo, CI/Showcase/QA/Pre-UAT, qa-current.json, BUILD-INFO/checksum/MANIFEST/fuentes críticas y JS público antes de afirmar entrega final.

#266 sigue abierta: navegación global App.tsx excluido por archivo caliente con #116 abierta; otros módulos, impresión/exportación independiente y regularidad individual Hospitalaria pendientes. Mantener #191 aceptación operacional y #66/PR#116 NO FUSIONAR TODAVÍA; #97 mantiene srv01/UAT pausados, despliegue QA pendiente. Main intacta. Instalables/CI no acreditan schema instalado, QA física o UAT institucional.

Demo: https://pablodominguez8375-2025.github.io/Modernizaci-n-GLMC-Milenio/
PR funcional: https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/pull/350
