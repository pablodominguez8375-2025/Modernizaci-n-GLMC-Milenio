# Seguridad backend — reserva #331

Base dev 5b2d131dcfd9da0104eb680e0a79b8760616fa77; main 6dfb9546a4873baff15955cf86abfd7d47e3d111.

Corte: backend/Dockerfile, backend/src/PMGM.Api/Program.cs, nueva infraestructura RequestRateLimiting, pruebas nuevas, .github/workflows/ci.yml, scripts/check-dependency-vulnerabilities.py y pruebas, handoff nuevo. Sin migración ni cambios de datos/reglas/contratos institucionales. Excluye archivos de Claude #327.

Drive revisado: instrucciones de seguridad del 05-10 y adenda PROPUESTA; continuación técnica autorizada por el PO. Se detecta nginx 12m en #327 frente a 50 MiB en configuración de documentos; sugerencia registrada en PR.

Implementado en PR #332: API final usa USER APP_UID; límites por sujeto/issuer (120 escrituras, 20 cargas, 30 descargas/ICS por minuto, configurables positivos) tras autorización; HTTP 429 en español con Retry-After/no-store y sin ejecutar operación. Lecturas/health conservadas. Contadores locales por proceso, no protección distribuida ni validación de capacidad.

CI incorpora auditoría NuGet directa/transitiva para API y tests, falla ante alta/crítica o reporte incompleto; npm producción falla ante alta/crítica. Informes NuGet como artifact. Pruebas nuevas: 7 HTTP y 4 del parser. Local parser 4/4, privacidad 12, clasificación 100 y migraciones 62 PASS. .NET/Docker no disponibles localmente; validar backend, no-root, PostgreSQL y smokes en gates exact-head. No afirmar esas pruebas ejecutadas aún.

GOV-004: [registro de transporte y configuración](../modelo-datos/2026-10-06-request-rate-limiting.md); sin migración ni cambios de modelo/reglas de datos institucionales. Pendiente gates exact-head, integración, publicación e instalable. Main y srv01/UAT pausados; despliegue QA pendiente. Instrucciones Drive sólo parcialmente cubiertas: frontend no-root/CSP, Actions SHA/Dependabot y evaluación system/info siguen pendientes; PR #327 permanece propiedad Claude. Sin intervención en su rama.
