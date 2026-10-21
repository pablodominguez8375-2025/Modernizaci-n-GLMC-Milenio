# Issue #613 — C1 validación backend del origen del candidato

21-10-2026; agente:chatgpt. Base `dev@dbd8e53f6c4b9c884e76517fa720458141fdfb52`; no tocar `main`, QA físico ni datos reales. Issue reclamado con PR draft.

Alcance C1: clase nueva CandidateOriginPolicy con validación de opción recomendado/internet/otro, exigencia de recomendante activo validado por servidor, longitud de descripción, inadmisibilidad de extra campos, bloqueo de recomendante como entrevistador y pruebas unitarias. Sin endpoints, entidad ni migración todavía: el valor no se persiste ni viaja al PDF en este incremento. La fase C2 debe integrar el almacenamiento/auditoría, la captura web y el circuito de entrevistas.

Archivos previstos: backend/src/PMGM.Api/Modules/CandidateIntake/CandidateOriginPolicy.cs; backend/tests/PMGM.Api.Tests/CandidateOriginPolicyTests.cs; registro GOV-004 y este handoff.

Gates y prueba de compilación por verificar en GitHub, no inferir QA/UAT. UI de Claude fuera de alcance.

Entregado en rama: `CandidateOriginPolicy.cs` (opciones `brother/internet/other`, `unregistered` solo legado; verificaciones de hermano activo desde fuente backend/fecha/evidencia; normalización de descripción; exclusión de recomendante en entrevistas) y `CandidateOriginPolicyTests.cs` con casos positivos/negativos. Registro de datos: `docs/modelo-datos/registros/2026-10-21-chatgpt-candidate-origin-policy-613-c1.md`. Sin migraciones, endpoints ni cambios UI. CI exact-head pendiente y no merge.
