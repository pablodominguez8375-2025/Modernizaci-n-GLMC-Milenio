# #694 fase J — precondiciones de apertura/cierre de ventana, sin persistir

Agente ChatGPT. 24-10-2026. Branch `feature/initial-load-window-transition-guard-694-20261024-gpt` desde dev `c3b5bfeb05e65240b6fd7c677d58146899645fea`. #708 GET privado sigue DRAFT con checks nuevos.

## Contrato
`InitialLoadWindowTransitionGuard.Plan` recibe sólo el estado persistente ya consultado (`InitialLoadWindow`), una decisión positiva producida por `InitialLoadAdmissionPolicy.Evaluate` usando actor Máster validado y evidencia obtenida de servidor, un evento de auditoría propuesto con Guid no vacío, marca temporal UTC, y resumen SHA-256 de aprobación legal verificada por el servidor para la apertura. **El caller NO debe construir la decisión desde JSON ni usar datos del cliente como prueba jurídica.**

La función verifica coherencia del estado actual con `InitialLoadWindowStatusReader.Project`, versión canónica 0→1→2, decisión de operación coincidente, marca UTC monotónica, comprobante auditado no duplicado y hash jurídico hexadecimal seguro. Sólo retorna código fijo, propuesta de estado y versión, nunca el digest ni ID de evento. `CanApply=false`, `WritesPerformed=0`, incluso cuando sea coherente.

## Límites / seguridad
No existe conexión a endpoint, escritura de DB, evento de auditoría persistido, trámite jurídico formal, transacción ni importación en este PR. La transición real futura exige combinar `core.audit_events` y `core.initial_load_windows` en **la misma transacción** y controlar concurrencia/idempotencia; usar API segura autenticada a Máster admin de token validado. **No abrir la carga ni recibir RUT/nóminas reales** sin autorización Jurídico Ley 21.719. La política no acredita cumplimiento legal.

Archivos exclusivos: nuevo `backend/src/PMGM.Api/Modules/Bootstrap/InitialLoadWindowTransitionGuard.cs`, tests nuevos `backend/tests/PMGM.Api.Tests/InitialLoadWindowTransitionGuardTests.cs` y este handoff. Evitar migraciones/Program.cs/Code UI Claude. PR draft y gates 3/3 exact-head, atrás=0 antes de squash a dev. main/glmdch no se tocan.

Pendientes finales de #694: servicio de transición auditado + pruebas con PostgreSQL, autorización servidor jurídico y máster, importación idempotente/reversible, edición de Taller por Secretaría propia/Gran Secretaría, QA físico/UAT. Issue #694 permanece abierto.
