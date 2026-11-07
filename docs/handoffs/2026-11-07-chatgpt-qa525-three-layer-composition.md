# QA525 P1 — cocontrol hermético de composición del vuelo B7 (SIN GO FÍSICO)

Fecha: 2026-11-07 (Santiago). Responsable: ChatGPT, Issue #924. PR DRAFT de trabajo; no usar como aprobación para instalar nada.

## Fotografía verificada antes del cambio
- Único QA físico: glmdch, sin segunda VM. dev@bd7fa0699ca26fa5180765574cd8c8acae676731, main@6dfb9546a4873baff15955cf86abfd7d47e3d111 sin cambio.
- PR #920 integrado a dev con runner inferior de 4 argv y callbacks inyectados.
- PR #921 HEAD 0ca52ec5801a0b228ab394edcb2ee061f782fbb5: 7/7 CI SUCCESS del SHA anterior a integración #920, **requiere rebase y NUEVA CI**; no modificar sus archivos.
- PR #923 HEAD 58fed3bff040b6670f0264d7d0da0a81ff35e4a3: 7/7 CI SUCCESS exact-head, pero sin review independiente todavía; no modificar sus archivos.
- No existe prueba integrada aceptada de #923 -> #921 -> #920. Se reserva solo nuevo test `tests/test_qa525_vnext_held_physical_composition.py`; no se incorporan imports faltantes ni skips engañosos.

## Diseño para implementar cuando ambos PR dependientes estén en dev
1. Construir test con engine y RootPlane REALES sobre Port de POSIX simulado, cinco locks, journal y nonce; sin proceso ni red.
2. El factory recibe el plan REAL del bridge B7 como 4.º argumento; deriva de él `spec` del runner inferior y CommandGuard superior, con misma boundary y report, sin nueva captura de baseline.
3. El guard superior usa como `run_effect` el callable del runner inferior; callbacks de firma/pins/intent/approval/scopes/cgroup son inyectados y no autentican el host.
4. Verificar dos efectos target api+web, cuatro efectos en secuencia web-failure+rollback dual, el uso de la MISMA identidad de plane y validación del scope manifest antes de cada child.
5. Negativos: scope ausente, intent stage incorrecto, aprobación 1/yes, cgroup ACTIVE/UNKNOWN, pérdida de lock, G6 alterado, pins cambiados pre/post, plan mutado, child incierto, exit code booleano y reintento. Demostrar NO GO real y recuperación bloqueante.
6. Test/suite no debe importar subprocess ni abrir systemd/Docker/SSH. No reclamar prueba G6 off-host ni firma OPS/APP por tener callbacks sintéticos.
7. CI 7/7 exact HEAD del PR de composición, revisión cruzada 0 P0/P1 y merge squash secuencial a dev sólo después de dependencias y rebase.

## Gate físico independiente (Issue #822, OPEN/NO-GO)
OPS Ed25519 con huella pinneada desde gestor del Sponsor fuera de Git/Drive, APP Sigstore OIDC de SHA/workflow exacto, bootstrap PRE-root antes de ejecutar cualquier Python root, verificación de binarios+Compose root-owned y cgroup real, G6 A cifrado Restic+Drive con descarga/restore aislado fresco <48 h en glmdch, 10 servicios reales (7 running + 3 oneshots exited/0), recuperaciones/rollback y GO Sponsor **separado** para launcher-only y para API+WEB. Ninguna acción física, main/producción, BD ni identidad por este PR.

## Trazabilidad
- GitHub: Issue #924, #525, PR #920 / #921 / #923; fuente de verdad posterior a cualquier chat.
- Google Drive: Proyecto Centenario — Estado Maestro Operativo (continuidad IA), actualización de este checkpoint.
- Pendiente técnico inmediato: CI #921 tras rebase, dictámenes independientes #921/#923, implementación del test nuevo sin omitir fallos.
