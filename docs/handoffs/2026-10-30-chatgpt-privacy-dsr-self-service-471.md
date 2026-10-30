# Handoff #471 — autoservicio de derechos del titular (fase A)

- Estado: desarrollo en rama, **no fusionado ni desplegado**.
- Issue: #471, etiquetado agente:chatgpt, contrato comentado en #471/6513153490.
- Base inicial: `dev@ceaca327cacb983850a66a9dbdb41a8d4a3c4316`.
- Rama: `feature/privacy-self-service-20261030-gpt`.
- Archivos previstos: `backend/src/PMGM.Api/Modules/Privacy/PrivacyEndpoints.cs`, `backend/tests/PMGM.Api.Tests/Integration/PrivacyOwnRequestPostgreSqlTests.cs` y este handoff.
- Alcance: GET/POST autenticados `/api/privacy/data-subject-requests/me`, identificación desde issuer+sub verificado en BD y PersonId institucional, solo solicitudes propias; no descargar datos, no consultar expedientes ajenos, no nueva migración ni UI.
- Plazo jurídico: únicamente regla versionada de `IPrivacyLegalRuleResolver`; sin regla no inventar fecha, dejar revisión administrativa pendiente y permitir recepción.
- IdentityVerified=false hasta verificación humana formal, sin inferir legitimación por mera autenticación.
- Pruebas y HEAD exacto: PENDIENTE, se consignarán al cierre.
- GitHub Pages y release QA: sin modificación por esta rama; no desplegar QA.
- Exclusiones: `main`, producción, `srv01`, migración #801 (ya aplicada en glmdch), intervención física #525, datos reales y exportación ARCO todavía no habilitada.
