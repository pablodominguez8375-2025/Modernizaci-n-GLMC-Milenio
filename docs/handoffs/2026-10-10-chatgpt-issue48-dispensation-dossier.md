# Issue #48 — expediente persistido de dispensa

PR #440 parte de `dev@57f79249e2ca891036c141e640ff2bcf4d6809dc`.
Implementa el expediente que faltaba para usar una dispensa en el autorizador
real de Aumento/Exaltación.

La Secretaría del Taller registra el caso fundado con Cámara del Medio celebrada,
acta aprobada, escrutinio vinculado y nómina completa de Maestros presentes y
habilitados. La regla versionada exige unanimidad. Las reducciones se validan por
requisito y no pueden superar 50 %; Tesorería y los dos trabajos no son
dispensables. Régimen Interior resuelve con identidad distinta y referencia
formal. El autorizador vuelve a comprobar regla, acta, votación, pertenencia,
grado, continuidad y mínimos reducidos. Gran Secretaría sólo consume el resultado.

Datos/migración: `AdvancementDispensationCase`,
`20261011023000_AddAdvancementDispensationCases` y
[registro GOV-004](../modelo-datos/cambios/2026-10-10-issue-48-expediente-dispensa.md).
No modifica `main`, identidad visual ni despliega `srv01`.

Pendiente antes de integrar: compilación y pruebas .NET por CI, prueba HTTP
PostgreSQL del circuito positivo/denegaciones, y revisión de demo si se agrega UI.
