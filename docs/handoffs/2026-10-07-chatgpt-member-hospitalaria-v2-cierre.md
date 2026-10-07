# Cierre — Mi ficha y Hospitalaria v2 — 07-10-2026

Esta adenda supersede los pendientes de conexión e integración del handoff inicial. Fuente Drive: 1nQ9GAePY0GLTpfMb62OqGqE9FYdKyFea98nPcQp8e3w (instrucciones v2). Issue #354; PR funcional #355, integrada por squash.

## Corte entregado

Base dev 50ac8019961c7e359b91613d0186c88f9bbd800d. Head exacto de PR probado 8e96a5285c499c3125740d47f06cd2c6e9a345b8. Corte funcional integrado y publicado: dev@55af02239fd90497a24af3e444f1b6795f833706. Main permanece 6dfb9546a4873baff15955cf86abfd7d47e3d111. Este PR posterior añade sólo registro documental.

Mi ficha conserva las tres pestañas de Claude #353; los historiales de cargos, reposiciones propias y asistencias consumen contratos reales con filtros/resumen. Hospitalaria registra defunciones con generación inmediata atómica e idempotente, decreto formal (número/fecha/vigencia/referencia) sin retroactividad y reposiciones por activo. Aporte mensual de CLP6000 por Taller separado de CLP1500 por hermano activo, con versiones, pago y comprobante en rendición local y consulta/revisión de Gran Hospitalaria. Formularios bajo demanda y demo sintética compatibles.

No se toman selectores de MemberId para la lectura propia; identidad institucional autenticada y no-store. Selector de defunción minimiza datos a id/nombre de miembros del Taller con permiso create Hospitalaria. Migración nueva 20261007120000: aporte inicial desde instalación; no cargos retroactivos ni decreto inventado para registros históricos. Datos/modelo documentados en docs/modelo-datos/cambios/2026-10-07-issue-354-mi-ficha-hospitalaria.md y catálogo de clasificación.

## Evidencia

- Local: backend/tests Release compilan sin warnings/errores; frontend build/lint y453 pruebas PASS.
- Exact-head: CI37620905345, Showcase37620905502, QA37620905468 SUCCESS. Backend513 aprobadas,0fallidas,0omitidas; PostgreSQL/S3/ClamAV reales de CI. Matriz responsive811 capturas; artifact11482278369 digest14bc1aace682a12a234da687a5c00d4b3725299778119ea010305fa03dd96ecb. Capturas móviles de Mi ficha y Gran Hospitalaria inspeccionadas.
- Post-merge55af022: CI37622365191, Showcase/Pages37622365338 (Deploy SUCCESS), QA37622365173 y Pre-UAT37622365218 SUCCESS.
- Pages público: https://pablodominguez8375-2025.github.io/Modernizaci-n-GLMC-Milenio/ . qa-current.json leído confirma sourceSha55af02239fd90497a24af3e444f1b6795f833706; ZIP Proyecto-Centenario-QA-srv01-55af02239fd9.zip, SHA256 2ce8b9a7f15e4527cdc5147ff788b01c2a91cb92330c55103ada694a8d82dac6.
- El ZIP público verificado proviene del run de cierre PR37622365828; SOURCE_SHA55af022 y MANIFEST1094 validados.
- Artifact Pages push11483600742 digest2c2d59ea3e41899c1c35e57487b389363f060fdc05470e0d3181682aa3bd382a. Responsive post-merge11483325900 digestc292ebed48d64039b2e6dedd8f65977b93851399c09a06e30f6135d04db4c751.
- QA Actions11482731311 digestebc8ad10c7a0056ffd62708e45674e69caab5e671fb91ce273c1560ad8440ac5. ZIP interior0ab182f84c0edffdae72feecbedbd178d375dfc90431d202f91da485f91dd426; SOURCE_SHA55af022 y MANIFEST1094 entradas verificados. Los paquetes Pages y Actions se construyeron por separado.

## Continuidad

25 archivos funcionales/datos/pruebas/documentación en #355. Handoff inicial: docs/handoffs/2026-10-07-chatgpt-member-hospitalaria-v2.md. Línea Base Drive actualizada; fuente v2 se marca EJECUTADA tras verificación de Pages. Sin novedades en documentos de raíz Proyecto Centenario en el corte revisado. Issue #97 mantiene srv01 pausado: paquetes generados, no instalados; despliegue QA pendiente y sin QA física/UAT aceptada. No promover main.
