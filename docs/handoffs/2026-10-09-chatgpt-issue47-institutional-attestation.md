# PMGM — Handoff de continuidad Issue #47 — constancias institucionales de ascenso

Fecha de trabajo: 09-10-2026, Chile. Responsable: ChatGPT (reservado en [PR #423](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/pull/423)).
Base de rama: `dev@936124e5d37099ca7e62618717478dc5102c7ecf`.
Issue funcional: https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/47. Separado de Issue #48 (dispensas).
Estado en este archivo: **PR draft sujeto a triple gate exact-head y a merge en dev.**

## Motivación y decisión funcional
Ninguna plancha ni enlace a Tenida, extracto o acta completa acreditaba antes la presentación/aprobación por Cámara del Medio. La evidencia debe ser validada por humanos competentes y no sustituida por metadatos, publicación en Biblioteca o vínculos. Dos documentos distintos de categorías `degree_symbolism` (simbolismo del grado) y `masonic_general_culture` (cultura general masónica) deben contar con constancias expresas. Cumplimientos mínimos de Tenidas/Docencia/Planchas dependen de regla institucional versionada sin hardcode. Se verifica continuidad cronológica y validación institucional.

## Nuevo circuito y componentes
- Tabla EF `core.advancement_paper_attestations` y migración `20261010021000_AddAdvancementPaperAttestations.cs`. Constancia con Taller/Hermano/solicitud, documento/versión/fecha/grado de Tenida celebrada, extracto y acta completa versionados, categoría, resoluciones, usuario y tiempos; índices por solicitud y Taller.
- `AdvancementPaperEvidenceValidator`: fuentes vivas institucional/Lodge/DocumentManagement. Coteja misma Tenida celebrada, Taller, grado saliente, Hermano, archivo PDF/Word disponible, versiones correctas, integridad SHA, escaneo, clave objeto, fechas; exige extracto recibido y acta completa verificables.
- `AdvancementCertificationEndpoints`: GET de constancias privado; Secretaría Taller registra presentación `pending`; Régimen Interior distinto del presentador resuelve `approved/rejected`, requiere fuente de acuerdo Cámara del Medio, audita. Regla de revisión inmutable. Continuidad validada separadamente por Régimen Interior usando regla institucional vigente, umbral cronológico y referencia verificable.
- `AdvancementCertifiedPaperPolicy`: deduplicación por documento y su último estado; dos categorías diferentes y documentos distintos, rechazo a evidencia no verificada, transacciones ajenas o autorrevisión.
- `AdvancementAuthorizationProjection` participa en `GET /elegibilidad` y **POST /autorizar** del endpoint real, con revalidación de evidencia y mínimos de Tenidas/Docencia. Si falta cualquier evidencia o continuidad, no habilita. Audita IDs de documentos certificados y versión de regla al autorizar; matriz Gran Maestría alineada con pago de derecho y elegibilidad.
- Bandeja de Secretaría/Gran Secretaría y frontend `CeremoniesPage` exponen únicamente capacidades por rol, candidatos documentales verificados, constancias pendientes, revisión, continuidad y resumen de cumplimiento; mock no simula acuerdos institucionales.
- Ocho pruebas unitarias de deduplicación/seguridad y HTTP PostgreSQL de no autorización sin evidencias; clase `AdvancementAttestationHttpTests` añadida a shard a en `.github/backend-integration-shards.json`.

## Regresiones detectadas y corregidas
- TS6133 variable no utilizada de selección en `CeremoniesPage.tsx`: corregido en fa84bde.
- CI shards no incluía nuevo test HTTP: añadido en 3fe74cde.
- Regresión de seguridad: bandeja permitía la acción `canAuthorize` cuando `eligibility.canAuthorize` era false; detectada en `PostgreSqlHttpWorkflowTests`, corregida en 709a18a (requiere triple gate posterior).

## Validación y criterio de salida
- Nunca declarar `CI GREEN` hasta CI, Showcase y QA Installable **SUCCESS del mismo HEAD**. Antes de merge, comprobar conflictos contra HEAD actual de dev y reserva GOV-003; no fusionar con fallos.
- Tras merge de código: comprobar CI/Showcase/QA y Pre-UAT de nuevo HEAD dev; indexar este handoff en START-HERE (PR documental independiente y testeado) si fuera necesario.
- Issue #47 solo podrá cerrarse cuando los 7 criterios originales queden acreditados con pruebas y permisos; toda aceptación **operacional/UAT** sigue separada bajo #25 y #97.
- `main` prohibida sin aprobación Sponsor. `srv01` físico/UAT permanece pausado por Issue #97. Nunca crear aprobación masónica ficticia. #48 dispensa independiente.
- Control cruzado: carpeta oficial Drive Proyecto Centenario, adenda `15KCbiQVa8S4dkDUkQVUjCQBeKGGlJMPrOy4HSPBtbAg`.
