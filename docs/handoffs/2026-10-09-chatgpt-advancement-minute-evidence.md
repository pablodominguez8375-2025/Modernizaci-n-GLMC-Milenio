# Handoff — Issue #47 / PR #418: acta completa y revisión privada de planchas

## Estado y límites
- PR draft: https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/pull/418
- Rama: `feature/advancement-minute-review-20261009-gpt`
- Base al iniciar: `dev@3a7f984759c109cdc05e895ed6e195b3f82d95e1`.
- `main@6dfb9546a4873baff15955cf86abfd7d47e3d111` no debe cambiarse.
- `srv01` y UAT físicos permanecen pausados por Issue #97.
- No concede autorización, ni valida firmas, deliberaciones, presentación ni aprobación de planchas por la Cámara del Medio.

## Requisito institucional y brecha
Para ascensos (Aumento de Salario 1.º grado y Exaltación 2.º grado), el sistema ya muestra versiones documentales y vínculos a Tenidas celebradas, además de verificar metadatos de extractos remitidos (PR #416). Hasta este incremento no podía indicar separadamente cuáles Tenidas vinculadas a una plancha disponen de una **versión revisable del acta completa**. El acta completa no es sinónimo de acta de aprobación de trabajos.

## Incremento implementado en rama
1. `AdvancementMeetingMinuteEvidencePolicy.cs`: verifica desde `DocumentVersions` y `InstitutionalDocument` que la versión de acta exista, corresponda al mismo Taller, tenga estado documental válido, esté disponible, sea PDF/Word, tenga SHA-256 válido, referencia de escaneo y ObjectKey no vacío.
2. `AdvancementWorkPaperEndpoints.cs`: GET privado `/api/ceremonias/solicitudes/{requestId}/avance/planchas` coteja también `FullMinuteDocumentVersionId` de los registros de Secretaría con fuente documental viva y adjunta el resultado sólo a vínculos a Tenidas realmente celebradas, no ceremoniales, del grado, autor, Taller, fecha y versión aplicables. Una referencia defectuosa no aparece como acta revisable.
3. `AdvancementWorkPaperReviewPolicy.cs`: agrega `ReviewableFullMinuteMeetingIds` como lista separada de `SubmittedExtractMeetingIds`; agrupa duplicados y no permite que la disponibilidad de acta incremente `ConfirmedPresented`.
4. Tests xUnit `AdvancementMeetingMinuteEvidencePolicyTests.cs` y `AdvancementWorkPaperReviewPolicyTests.cs`: límites de integridad/ámbito, formatos PDF y Word, documento retirado, ausencia de versión, extracto y acta por separado, duplicados y no certificación.
5. Este handoff nuevo. Sin migraciones, cambios UI, autorización HTTP, reglas institucionales o archivos calientes.

**Límite crucial**: los metadatos validados no prueban que el objeto S3 exista ni que el acta contenga firmas o voto de la Cámara del Medio. `PresentationVerified=false`, `ConfirmedPresented=0`, `PresentationEvidenceAvailable=false` y `authorizesCeremony=false` permanecen como antes. La validación institucional sigue pendiente.

## Evidencia de CI/QA a completar
- Triple gate HEAD exacto de PR: PMGM CI, Showcase Demo, Proyecto Centenario QA srv01 Installable: comprobar y registrar sólo resultados reales.
- QA físico `srv01`, publicación pública Pages y SHA del paquete: no asumir ni declarar verificados sin readback específico.

## Fuentes y próximos pasos
- `START-HERE.md`, `AGENTS.md`, `docs/PMGM-GOV-003-coordinacion-multi-ia.md`.
- Línea Base Maestra Drive: https://docs.google.com/document/d/1ncl0d--Bny8PzvtP38muPo5H2-JM5QUDGqH3lJ5ZtPM/edit
- Adenda ascensos/dispensas Drive: https://docs.google.com/document/d/15KCbiQVa8S4dkDUkQVUjCQBeKGGlJMPrOy4HSPBtbAg/edit
- Fuente institucional: protocolo 2026 de solicitudes de ceremonias y Reglamento General 3.2 b/c (dos trabajos diferentes), 3.4 (dispensas restringidas).
- Pendiente Issue #47: expediente persistente autenticable de presentación/aprobación con acta de Cámara del Medio, calificación, votación y documento vinculados a Hermano/Taller/grado/fecha y auditoría; antigüedad continuada certificada.
- Pendiente Issue #48: expediente de dispensa fundado, unanimidad y acta, resolución de Régimen Interior, sin dispensar los dos trabajos.
- Hacer PR documental separado para una línea nueva en `START-HERE.md` **después** de integrar la PR funcional y sus tres gates.
