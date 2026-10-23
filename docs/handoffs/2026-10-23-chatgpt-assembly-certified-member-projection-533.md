# #533 C22 — preparación de registros acreditados de asambleístas
Fecha: 23-10-2026 · responsable agente:chatgpt · base dev@837d54c2520155288841fef1db4fd6da9f507254.

## Alcance reclamado
Construir un ensamblador backend **sin API pública ni escritura automática** que transforme, de modo atómico y comprobable, evidencias históricas de V.M. ya acreditadas y snapshots de identidad/membresía obtenidos por servicio confiable en filas candidatas `AssemblyMemberStatusRecord`, con causales fail-closed, límite de volumen y protección frente a duplicados, sin activar voto. Pruebas unitarias positivas y negativas. Crear registro GOV004 por salida de datos.

Archivos: `backend/src/PMGM.Api/Modules/RegimenInterior/AssemblyAccreditedMemberMaterializationPolicy.cs` (nuevo), `backend/tests/PMGM.Api.Tests/AssemblyAccreditedMemberMaterializationPolicyTests.cs` (nuevo), este handoff y `docs/modelo-datos/registros/2026-10-23-chatgpt-assembly-member-materialization-533.md` (nuevo). Ninguna migración ni archivo de Claude.

## Límites vigentes
Este preparador no autentica actas/identidad por sí solo. **Sólo un futuro conector de infraestructura autenticado puede suministrar las evidencias.** Sin cobertura completa, nada se persiste, lista vacía no equivale a padrón cerrado, todos los derechos electorales continúan en estado pendiente hasta Gran Tesorería/Tribunal/RI. PR/CI exact HEAD antes de integrar en dev, no main ni QA físico.
