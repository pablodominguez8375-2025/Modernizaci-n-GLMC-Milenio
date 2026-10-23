# GOV-004 — Asamblea #533 C23: inserción inicial y auditoría
Fecha: 23-10-2026. Sin migración; reutiliza core.assemblies, core.assembly_member_statuses, core.assembly_eligibilities y core.audit_events ya existentes.

## Nuevo flujo interno
`AssemblyAccreditedRosterPersistenceService.PersistAsync` recibe el lote previamente preparado por C22 (sin interfaz HTTP), ID de Asamblea, corte certificado y contexto del actor RI con ámbito Orden. Comprueba identidad de Asamblea, modelo íntegro, unicidad en lote, solo estado draft, inmutabilidad contra reimportación. Bloquea fila de Asamblea y crea filas candidatas + una evaluación `pendiente` por candidato con MayAttend=false y MayVote=false, sin saldo, inhabilidad ni certificados inventados. Se emite un evento auditado en la misma transacción SERIALIZABLE sin nombres, RUT ni ID de personas en metadatos.

## Impacto de datos
Inserción append-only, no altera registros previos ni `main`. Reimportaciones se rechazan para que un proceso explícito de rectificación posterior resuelva cambios de fuente. Sin DDL y sin datos reales en tests.

## Condición pendiente esencial
Este método no acredita datos por sí mismo: se debe implementar y verificar conectores oficiales de Gran Secretaría/cargos V.M. y membresía, Gran Tesorería, Régimen Interior y Tribunal de Honor. La persistencia de candidatos NO es autorización de sufragio ni cierre ni PDF oficial; ninguna ruta HTTP o DI de orquestación se incluye hasta esa cobertura. Revisar UAT y Ley 21.719 antes de usar fuentes reales.
