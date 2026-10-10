# Test HTTP de tres Maestros — Issue #427
Fecha: 2026-10-10. Rama `test/issue427-three-interviewer-http-20261010-gpt` desde dev@b3a1f29c5e538740e9e05afb6636bb5ad6ee5f28.

PR de pruebas, sin cambios de normas, pagos, identidad operacional, infraestructura ni srv01.

Objetivo: ampliar cobertura PostgreSQL HTTP de designación formal, tres identidades institucionales reales de prueba, aceptación por cada Maestro, protección entre Talleres y validación de paquete privado. Datos sintéticos y limpieza de recursos. Para la carga real de PDF/DOCX, validar antivirus/almacenamiento y plantilla de notificaciones en entorno aislado; sin esquivar políticas productivas.

Archivos reservados: backend/tests/PMGM.Api.Tests/Integration/CandidateInterviewAssignmentPositiveHttpTests.cs, backend/tests/PMGM.Api.Tests/Integration/CandidatePublicationHttpWorkflowTests.cs (únicamente handler de sujeto de prueba), este handoff y configuración de shards solo si es indispensable (archivo caliente: solicitar reserva primero). PR draft; NO merge hasta CI/Showcase/QA exact-head SUCCESS. Handoff siguiente a START-HERE solo después de integración.
