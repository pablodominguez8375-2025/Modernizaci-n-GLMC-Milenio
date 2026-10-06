# Corrección: Menú móvil → Inicio

- Issue #341; PR #342. Solicitud directa del PO: revisar y corregir el bloqueo al volver a Inicio desde Menú.
- Base dev: 86302aaba94b6e6e7ceb6df2120c252854e229c5; main: 6dfb9546a4873baff15955cf86abfd7d47e3d111.
- Causa: App cierra Menú en el efecto dependiente de view; volver a la vista ya activa no dispara ese efecto.
- MobileTabBar ahora cierra explícitamente el menú abierto antes de ejecutar Inicio, Mi ficha/Pendientes, Agenda o Avisos. Con el menú cerrado no lo abre accidentalmente.
- Archivos: frontend/src/MobileTabBar.tsx, frontend/src/MobileTabBar.test.tsx y este handoff. No cambia App, CSS, backend, permisos, modelo, contratos ni reglas de datos.
- Prueba de regresión: seis casos fallan antes de corregir; después pasan. Suite frontend: 434/434; lint y compilación TypeScript/Vite correctos.
- Revisión Drive al inicio: ningún archivo nuevo/modificado en Proyecto Centenario desde la revisión previa. Propuesta de reordenamiento de menús sigue pendiente del PO y queda fuera de este arreglo.
- Anti-conflicto: dev sin cambios; PR #116/#60/#58 sin solapamientos en MobileTabBar. Tarea UI asignada expresamente por el PO a Codex.
- Gates exact-head, SHA integrado/publicado y checksums QA: se registran en el recibo persistente del PR #342 después de su verificación.
- Pages y QA se actualizan mediante los workflows del mismo código. Despliegue QA pendiente: srv01/UAT pausados (#97); main sin promoción.
- Continuidad: PR documental posterior agregará únicamente el enlace a este handoff en START-HERE; adenda de Línea Base Drive al cierre.
