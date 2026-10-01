# Sincronización documental REQ-025 — 2026-10-01

Issue #235, PR #236. Inicio dev@34d696cce914ed5af09af9da938c8effeaa926d9; main@6dfb9546a4873baff15955cf86abfd7d47e3d111. Rama docs/req025-publicaciones-20261001-gpt.

## Resultado propuesto
Se trasladan todas las diferencias aprobadas de PR #45 en REQ-025: portal transversal autenticado, fotografía/nombres/Taller, minimización, foto protegida, búsqueda y vigencia, configuración habilitada y referencia fotográfica en snapshot. Se preservan condición de derecho de ceremonia, fila en matriz y criterio de saldo/comprobantes/auditoría incorporados después de la rama histórica; no se sustituye el documento vivo por la versión vieja.

Requisitos y producto se distinguen: CandidatePublicationSnapshot en CeremonyEndpoints no contiene la referencia fotográfica y GetPublishedPhotoAsync usa PhotoVersionId actual del perfil. Snapshot fotográfico y configuración versionada se siguen en #237, sin declararlos cubiertos por PR #233. No se introducen atribuciones ni cambios legales; se conserva la decisión del PO de #44/#45.

## Coordinación y verificación
Reserva #235 con agente:chatgpt y draft #236 antes de editar. Cruce no caliente REQ-025 con #45/#116 informado en ambos PR; ninguna rama ajena editada. Archivos: requisito existente y este handoff nuevo. Sin código, migraciones, permisos o UI; START-HERE se tocará sólo en PR documental posterior. Se compara cada sección modificada de #45 y se preservan secciones actuales no afectadas, incluidos derechos de ceremonia. Gates CI/Showcase/QA exact-head pendientes al redactar; evidencia de cierre en comentarios de #235/#236 y Línea Base.

Base de publicación observada: 34d696cce914ed5af09af9da938c8effeaa926d9; 354/354 y paquete QA 815 checksums del corte previo. No reutilizar su digest para este incremento. Verificar Pages qa-current, SOURCE_SHA, BUILD_RUN_ID y SHA-256/MANIFEST del paquete final. Sólo después de integración/publicación se podrá cerrar #45 sin merge como reemplazado. #237 permanece seguimiento técnico abierto.

## Continuidad
Leer START-HERE, AGENTS/GOV, Línea Base y recibos finales vivos. No confundir sincronización documental con implementación de #237. No promover main ni instalar srv01: despliegue QA pendiente, sin QA física/UAT. Demo https://pablodominguez8375-2025.github.io/Modernizaci-n-GLMC-Milenio/ .
