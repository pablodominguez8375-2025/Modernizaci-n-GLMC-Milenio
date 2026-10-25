# GOV-004 — #533 C22 Preparación de asambleístas institucionalmente acreditados
Fecha 23-10-2026. Sin migración, DDL, escrituras de base de datos ni rutas HTTP.

## Entrada de datos interna
`AssemblyVerifiedMemberIdentity`: PersonId, MemberId, OrganizationId del Taller de afiliación al corte, fecha del corte, fechas de certificación de identidad y afiliación, indicadores de verificación emitidos por fuente servidor, referencias de evidencia, datos de identidad/nombre/RUT o identificación oficial y nombre del Taller. Incluye `AssemblyMasterTermEvidenceResult` ya validado por adaptador de constancia V.M. **No es DTO público y no puede poblarse desde HTTP**.

## Salida
`AssemblyAccreditedMemberMaterializationResult`: Prepared, Members (`AssemblyMemberStatusRecord`) o BlockingReasons técnicos sin PII. Si una sola fuente es inválida o falta cobertura institucional, Members=[], sin salida parcial. Se excluye solamente a inactivos o no calificantes con fuentes verificadas. Duplicación por persona, miembro o cargo acreditado se rechaza. No existe autorización de voto/elecciones en esta etapa. Taller de afiliación actual distinto del Taller histórico de V.M. está expresamente soportado.

## Pendiente técnico para poder persistir
1. Fuente oficial autenticada completa de membresía/identidad y actas V.M. a corte (no aceptar booleanos del HTTP).
2. Servicio transaccional auditable de guardar candidatos previa autorización de Régimen Interior y comprobación de duplicados en DB/ausencia de cambios concurrentes.
3. Evaluación histórica Gran Tesorería, restricciones RI y Tribunal; después cierre y exportación oficial bajo preflight. La política pura NO demuestra completitud legal ni crea un padrón oficial.
