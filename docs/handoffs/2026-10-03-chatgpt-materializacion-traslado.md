# Handoff — materialización idempotente y traslado con Carta de Retiro Voluntario

Fecha: 2026-10-03
Issue/PR: #275 / #276, draft.

## Decisión
Se conserva #116 como histórico **NO FUSIONAR TODAVÍA**. Este bloque implementa sobre `dev` actual una operación final de materialización y endurece el traslado: sólo procede con retiro voluntario aprobado y firmado por el Orador del Taller de origen.

## Cambios
- `POST /api/admisiones/expedientes/{caseId}/materializar` valida elegibilidad vigente, fecha civil no futura y referencia institucional; crea una pertenencia activa y evento de estado; reintentos sobre la misma fecha/Taller responden idempotentemente.
- `POST /api/members/{memberId}/transfers/` requiere `withdrawalRequestId`; valida miembro/origen, retiro voluntario aprobado y firma del Orador antes de abrir el traslado.
- `AdmissionsPage` y `pmgmApi` exponen la acción de Secretaría para materializar el expediente y muestran el resultado idempotente en el panel documental; la autoridad final permanece en la API.
- Registro GOV-004: `docs/modelo-datos/cambios/2026-10-03-issue-275-materializacion-traslado.md`.

## Pendientes del PR
Ejecutar gates sobre el SHA final; completar escenarios UAT-017-06 y UAT-017-09 con datos sintéticos. El traslado se mantiene como operación institucional del módulo de membresía y no se duplica dentro de la vista de admisiones. No instalar `srv01`, no ejecutar UAT institucional, no fusionar a `main`.

## Control de salida
El SHA exacto, CI, Showcase, QA Installable, MANIFEST, SOURCE_SHA, BUILD_RUN_ID y lecturas GitHub/Drive se agregarán al recibo de cierre de este mismo bloque.
