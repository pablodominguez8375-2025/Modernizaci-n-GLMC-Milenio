# #427 — Tres informes y paquete HTTP / PR #438

Inicio dev@2207494e53e35bf0dc41b4ba106ca41fb12aaecc; main@6dfb9546a4873baff15955cf86abfd7d47e3d111 intacta.
Rama feature/issue427-three-report-package-20261010-gpt; PR https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/pull/438.

## Implementación de prueba
Extiende CandidateInterviewAssignmentPositiveHttpTests.cs (ya en shard b). Conserva designación, avisos, aceptación y reprogramación; añade ficha privada sintética y factoría derivada con scanner limpio de test. Dos PDF con página de texto y DOCX OOXML con relaciones, subidos por identidades independientes vía endpoints reales. Rechaza subir antes de aceptar, autor ajeno, carga ajena, entrega ajena, entrega duplicada, descarga de otro Maestro o Secretaría de otro Taller. Secretaría correcta descarga bytes exactos. El tercer archivo cargado sin entrega personal deja paquete bloqueado; cuestionario o autobiografía faltantes bloquean revisión de tercer grado. Tres entregas personales y antecedentes completos permiten registrar paquete aprobado y revisión de tercer grado sintética. Verifica integridad, clasificación sensible, acceso management_only y tres eventos de entrega.

## Límites y validación
Solo pruebas; sin cambios de producción, permisos, migraciones ni normas. Scanner y object store de test: no certifica ClamAV/MinIO real ni despliegue. Diff local sin errores; .NET no disponible localmente, CI PostgreSQL obligatorio. Gates exact-head pendientes; no integrar hasta CI/Showcase/QA SUCCESS y dev/base sincronizados. #427 OPEN por aceptación institucional visual; srv01/UAT física pausados #97. Al iniciar, CI #38095653517 y QA #38095653512 de dev@2207494 SUCCESS; Showcase #38095653513 en proceso y qa-current.json aún 5da7a3d. Revalidar publicación al cierre. Mantener un único registro de sesión en Línea Base Maestra Drive.
