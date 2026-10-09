# Handoff — Issue #47 / PR #416: integridad documental de extractos

## Identidad y alcance
- PR: https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/pull/416
- Rama: `fix/advancement-extract-integrity-20261009-gpt`.
- Base inicial: dev@77ecc6e29f0f1d7c9d78e51ef100fb8528731e83, posterior al merge funcional PR #413 y a la UI de Claude; PR #414 documental fue integrada mientras se desarrolló esta rama.
- Cambio limitado a lectura privada GET `/api/ceremonias/solicitudes/{requestId}/avance/planchas`; no modifica POST `/autorizar`, permisos, migraciones, UI ni normas institucionales.

## Brecha detectada y corrección
Antes, `SubmittedExtractMeetingIds` aceptaba un registro de Secretaría con `ExtractDocumentVersionId` no nulo y estado submitted/received sin comprobar nuevamente en el repositorio documental que la versión de PDF siguiera siendo válida.
Ahora, cada referencia se contrasta en `DocumentVersions` + `InstitutionalDocument` con:
- ID de versión real no nulo, ámbito del mismo Taller;
- estado documental válido y no retirado;
- versión disponible; tipo MIME `application/pdf`;
- SHA-256 formalmente válido, referencia de escaneo y ObjectKey no vacíos;
- estado de remisión de Secretaría submitted/received.
Sólo los IDs que superan el control vuelven a `SubmittedExtractMeetingIds`. Se mantienen los vínculos a Tenidas celebradas como candidatos aunque el extracto no pase. Una referencia de archivo no equivale a comprobación en S3 ni certifica actas, presentación, aprobación o autenticidad institucional.

## Archivos exclusivos
1. `backend/src/PMGM.Api/Modules/Ceremonies/AdvancementWorkPaperEndpoints.cs` (lectura de extractos vigentes y filtro).
2. `backend/src/PMGM.Api/Modules/Ceremonies/AdvancementExtractEvidencePolicy.cs` (política fail-closed).
3. `backend/tests/PMGM.Api.Tests/Ceremonies/AdvancementExtractEvidencePolicyTests.cs` (unitarios, estados, ámbito, MIME, integridad).
4. Este handoff nuevo.

No se cambiaron archivos calientes, workflows, START-HERE, frontend, esquema de datos o main.

## Pruebas y salidas
- Nuevas pruebas xUnit de versión válida, falta de versión, Taller incorrecto, documento retirado/estado desconocido, estado de escaneo, MIME, SHA-256, ScanReference y ObjectKey.
- CI/Showcase/QA Installable: **pendientes de verificación del HEAD exacto** del PR luego del rebase sobre dev.
- Publicación Pages y artefacto QA requieren readback por SHA; no asumir que un gate PR equivale a Pages pública.
- `srv01` y UAT físicos pausados por Issue #97; no instalar ni promover main.

## Referencias y pendiente real
- Base funcional: START-HERE.md, AGENTS.md, PMGM-GOV-003.
- Línea Base Maestra: https://docs.google.com/document/d/1ncl0d--Bny8PzvtP38muPo5H2-JM5QUDGqH3lJ5ZtPM/edit
- Adenda de ascensos: https://docs.google.com/document/d/15KCbiQVa8S4dkDUkQVUjCQBeKGGlJMPrOy4HSPBtbAg/edit
- Protocolo 2026 revisado en Drive (solicitudes y dispensas).
- Issue #47: faltan dos trabajos efectivamente presentados/aprobados por Cámara del Medio, evidencia formal persistida, acreditación y continuidad del grado.
- Issue #48: faltan expediente de dispensa, votación/acta unánime, validación de Régimen Interior; los trabajos obligatorios no son dispensables.
- Esta PR sólo endurece un indicador auxiliar de evidencia: **no cierra #47 ni #48 y no autoriza ceremonias**.
