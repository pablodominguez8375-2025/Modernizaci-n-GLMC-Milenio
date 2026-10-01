# Handoff ChatGPT — Evidencia histórica de publicación

Fecha: 01-10-2026. Issue #237; PR draft #239. Dominio principal: backend, evidencia privada y configuración aprobada; frontend solo paridad del adaptador y pruebas, sin identidad visual.

Inicio: dev `8fd41253f9bb25acc4b8df1c7389873f12a34279`; main congelado `6dfb9546a4873baff15955cf86abfd7d47e3d111`. Rama `feature/publication-evidence-20261001-gpt`. Reclamo `agente:chatgpt` y PR draft anteriores a la programación. Solapamiento no caliente con PR #116 informado en su discusión; no se modifica su rama.

## Cambio

REQ-025 ya aprobado: se fija la versión de foto y la política de campos al aprobar la publicación. La evidencia se guarda atómicamente en la auditoría existente. Foto pública y autorización utilizan esa evidencia; el expediente privado puede cambiar sin reemplazarla. No se exponen IDs documentales ni se amplían permisos o campos públicos. La nueva política solo admite Fotografía, Nombre completo y Taller, con vigencias hacia adelante en fecha Chile. Las versiones futuras no desplazan antes de tiempo a la actual.

Compatibilidad explícita: publicaciones históricas sin evidencia siguen con su consulta anterior, y la autorización registra `legacy_not_recorded`; no se inventa una foto histórica. Evidencia inválida bloquea foto pública. La política base tiene VersionId nulo si no hay versión persistida. No se requiere migración.

## Archivos

- backend/src/PMGM.Api/Modules/Ceremonies/CeremonyEndpoints.cs
- backend/src/PMGM.Api/Modules/Ceremonies/CandidatePublicationEndpoints.cs
- backend/src/PMGM.Api/Modules/Ceremonies/CandidatePublicationEvidenceSnapshot.cs
- backend/src/PMGM.Api/Modules/CandidateIntake/CandidateIntakeEndpoints.cs
- backend/src/PMGM.Api/Modules/SystemConfiguration/SystemConfigurationEndpoints.cs
- backend/tests/PMGM.Api.Tests/Integration/CandidatePublicationEvidenceHttpTests.cs
- backend/tests/PMGM.Api.Tests/Ceremonies/CandidatePublicationEvidenceSnapshotTests.cs
- frontend/src/api/pmgmApi.ts
- frontend/src/SystemConfigurationPage.test.ts
- docs/qa/PMGM-QA-PUBLICATION-EVIDENCE.md
- este handoff nuevo.

## Verificación antes de integración

Local: frontend completo 233/233 PASS; SystemConfigurationPage 6/6 PASS; build TypeScript/Vite PASS; lint PASS; diff --check PASS. Backend se acredita en CI con PostgreSQL, no por ausencia de entorno local. HTTP cubre aprobación, cambio de ficha, idempotencia, políticas, autorización, expiración, legado y evidencia inválida. Transporte de notificaciones sustituido únicamente en esta prueba, sin certificación de entrega.

Correcciones de fixture detectadas por CI: importación del contexto de ficha, recarga del estado autorizado antes del escenario legado y balotaje aprobado exigido por el middleware vigente. No se modifica ni omite dicho guard. Adaptador QA corregido para que la política base no aparezca como una versión persistida y conserve estado default hasta una vigencia registrada.

Estado de este commit: implementación en rama; gates exact-head y squash todavía pendientes. Pages y paquete vigentes al inicio corresponden a dev `8fd41253f9bb25acc4b8df1c7389873f12a34279`: paquete `Proyecto-Centenario-QA-srv01-8fd41253f9bb.zip`, SHA-256 `86fb95ba9a7cf64668fc2e91214eaba8c5301b01f2ab28672ae376a5d9d40976`. No atribuir este paquete al PR #239. SHA final, runs, paquete nuevo y lectura de retorno Drive se consignarán en un handoff documental posterior al squash.

## Continuidad y límites

Consultar primero dev vivo y el cierre documental posterior. START-HERE se modifica solo después del squash mediante una línea de enlace. No modificar main, identidad, migraciones, workflows ni kit de regresión reclamados; no tocar ramas de otros agentes. srv01 pausado por #97/PR #1: despliegue QA pendiente; sin UAT. #191 mantiene remanentes operacionales/contables y #190 USD Perú ya integrado no se reabre. Los otros PR históricos conservan su propio alcance y no se fusionan por antigüedad.
