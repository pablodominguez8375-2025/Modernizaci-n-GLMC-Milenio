# Handoff técnico — PR #421 / Issue #47 — checklist documental de planchas
Fecha: 09-10-2026, Chile
Inicio: dev@c26748692e3c69cbc785ee50eb475758225ff4cc
dev verificado antes de crear handoff: c26748692e3c69cbc785ee50eb475758225ff4cc
main: 6dfb9546a4873baff15955cf86abfd7d47e3d111 (no promover)
PR: https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/pull/421
Rama: feature/advancement-review-checklist-20261009-gpt
Issue de coordinación: https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/47
Estado: PR draft, sin declaración de CI verde ni integración mientras no se compruebe.

## Motivación
La lectura privada GET /api/ceremonias/solicitudes/{requestId}/avance/planchas ya distinguía la versión revisable de la plancha, un vínculo a Tenida celebrada, el extracto remitido y el acta completa válida; faltaba exponer de forma inequívoca si las piezas forman un paquete documental COHERENTE de una misma Tenida, sin que ello implique acreditación institucional.

## Cambios
1. Nuevo AdvancementWorkPaperAccreditationChecklistPolicy.Build: intersecta IDs de la misma Tenida celebrada para plancha, extracto y acta; reporta brechas por códigos estables y requiere versión revisable. Nunca certifica presentación ni aprobación.
2. AdvancementWorkPaperReviewPolicy.Summarize agrega EvidenceChecklists al resumen privado ya restringido por perfil y Taller, sin cambiar el endpoint HTTP ni el guard de autorizar. ConfirmedPresented permanece 0; PresentationEvidenceAvailable permanece false.
3. Seis pruebas xUnit de paquete coherente, documentos de distintas tenidas, versión ausente, extracto o acta faltante, duplicados y no autorización.
4. Este handoff es nuevo; no se reescribe START-HERE hasta merge mediante PR documental separado.

## Alcance y límites
No hay migraciones, cambios de entidad, acceso nuevo, UI, reglas de mínimos, endpoints de autorización, workflows ni deploy real. Un paquete completo solo significa "listo para revisión humana". La verificación de acta, firma, voto, presentación, aprobación de dos trabajos distintos, continuidad y resolución institucional queda pendiente de #47/#48.
#97 mantiene srv01/UAT físicos pausados. main intacta.

## Validación y salida
Revisar en GitHub el diff y los tres gates exact-head del PR: PMGM CI, Showcase Demo y QA srv01 Installable.
No marcar Ready ni hacer merge si algún gate falla. Una vez integrados: comprobar HEAD postmerge dev, CI, Showcase/Pages, QA y Pre-UAT sobre dicho SHA.
No declarar Pages pública actualizada sin leer qa-current.json sobre el SHA; no declarar QA física.
Registrar en Drive adenda de continuidad de la sesión y en Issue #47 el resultado vivo. Mantener GOV-003 para coordinación con Claude.
