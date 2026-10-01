# Cierre de sincronización REQ-025 — 2026-10-01

Issue #235 y PR #236; continúa [handoff de trabajo](2026-10-01-chatgpt-req025-publicaciones.md).

## Integración
Inicio dev@34d696cce914ed5af09af9da938c8effeaa926d9; main@6dfb9546a4873baff15955cf86abfd7d47e3d111. PR #236 head 7fd72ae317b19c3ce7a52f076a073400295c9d33 integrada por squash en dev@814dd7bdd91de0735b28f7c2ccd7d2659171fd13. Gates exact-head SUCCESS: CI 36854357356 (#1728), Showcase 36854357368 (#1059), QA 36854357438 (#697). Log backend confirma 354 ejecutadas, 354 aprobadas, 0 fallos, 0 omitidas.

La sincronización recupera todas las diferencias documentales de PR #45: acceso autenticado transversal, foto/nombres/Taller, minimización, foto protegida, vigencia/búsqueda, configuración habilitada y referencia fotográfica del snapshot. Secciones 4/5/8/9 comparadas idénticas contra #45; secciones 1/2/3/6/7 del dev vivo preservadas. Condición de derecho de ceremonia, fila en matriz y criterio de saldo/comprobantes/auditoría conservados; este último renumerado 14. No se fusionó ni editó la rama histórica.

## Validación y límites
Privacidad: 12 tratamientos; clasificación: 99 campos; migraciones: 58; git diff --check PASS. Sólo REQ-025 y handoff nuevo en #236, sin código/roles/migraciones/UI ni normas institucionales nuevas. Cruce documental no caliente #45/#116 informado; reserva #235 agente:chatgpt y draft antes de editar. Línea Base registra el trabajo con readback exacto, fecha nativa y enlaces previos conservados.

Documentar no acredita implementación: CandidatePublicationSnapshot no incluye referencia fotográfica; GetPublishedPhotoAsync lee PhotoVersionId del perfil actual. Snapshot de referencia histórica y configuración versionada conservan seguimiento técnico **#237 abierto**. No se declaran cubiertos por la suite HTTP #233 ni se crean nuevas atribuciones.

## Registro final y continuidad
Este PR documental posterior agrega este handoff y exactamente una línea a START-HERE; no reescribe bloques. Los gates de este head y del dev final, Pages qa-current, digest/BUILD_RUN_ID/checksums del ZIP realmente servido y el cierre de #45 sin merge como reemplazado se registrarán en comentarios #235/PR y Línea Base después de verificarlos. No reutilizar digest del corte previo. #235 sólo trata sincronización documental; #237 conserva remanentes del producto.

Consultar recibos vivos de #235/#236 y el PR de este cierre, además de Línea Base. Demo https://pablodominguez8375-2025.github.io/Modernizaci-n-GLMC-Milenio/ . Main congelado; **srv01 pausado, despliegue QA pendiente**, sin instalación, QA física ni UAT. Gates y capturas automatizadas no son aceptación institucional. UI v0.63 conservada.
