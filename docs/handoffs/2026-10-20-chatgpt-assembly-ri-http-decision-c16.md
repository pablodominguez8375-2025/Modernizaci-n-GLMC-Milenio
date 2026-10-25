# #533 / C16 — decisión preliminar RI por HTTP con auditoría transaccional

Fecha: 2026-10-20. Base `dev@a695eed6845d07a54aae777da11af96362a993c1`.

## Contrato backend para Clau (sujeto a CI/merge en dev)

`POST /api/regimen-interior/asambleas/{assemblyId}/revision/{personId}/decision`, cuerpo:
```json
{
  "action": "request_correction",
  "reason": "Solicitar antecedente validado",
  "evidenceReference": "RI-DOC-REFERENCIA"
}
```

La interfaz usa `personId` presente en la consulta preliminar C13 (no `memberStatusId`). **Solo** usuario autenticado con claim institucional explícito `pmgm_scope=order`, rol `pmgm_role=internal_affairs` y `sub` no vacío. Gran Secretaría, otros cargos y administradores sin rol explícito RI no pueden registrar decisiones.

Acciones admitidas: `request_correction`, `refer_validation`, `confirm_ineligible`, `attendance_only`. `confirm_eligible` NO está permitida; no se atribuye voto. Todos los cambios tienen motivación y referencia de antecedente obligatorias, validadas y acotadas por longitud, sobre Asamblea `draft` y la misma persona/asamblea. Resultado 200: `{decisionEventId, assemblyId, action, recordedAtUtc, provisional:true, mayVoteConfirmed:false, officiallyClosed:false}`. 400 campos inválidos, 403 rol insuficiente, 404 asamblea/persona ajena, 409 Asamblea no editable. Sin nombres/RUT/deudas/antecedente completo en respuesta.

Registro `core.assembly_review_decision_events` append-only C15 y `core.audit_events` en una sola transacción PostgreSQL SERIALIZABLE (insert auditoría SQL parametrizado usando fábrica canónica). Si falla auditoría la escritura no se confirma. Trigger y FK PostgreSQL siguen como segunda línea de defensa. El evento auditado no replica motivo ni número de identidad.

Prueba sintética `AssemblyReviewDecisionHttpTests` en shard b verifica 403 Gran Secretaría, rechazo `confirm_eligible`, cruces de Asamblea, 404 persona ajena, éxito persistido y auditoría del mismo evento, minimización de datos. Validar CI/Showcase/Installable exact HEAD antes de fusionar y la ejecución UAT autenticada en QA físico por hilo distinto.

## No confundir con operación electoral

**No certifica balances GT, Tribunal, cargos, ni derecho de voto.** El padrón definitivo sigue bloqueado por `trg_assembly_draft_only`. No permite cerrar, emitir papeletas, exportar padrón oficial ni usar datos reales de QA. Siguientes integraciones: fuentes completas e inmutables GT/RI/Tribunal/GS, decisiones finales, preflight real y exportación. No tocar `main`, `srv01` ni infraestructura física.
