# #191 — Caja, regularidad y cuotas no recuperadas

Base dev: 4a026bdf62adb8579ad3faab08d57975383c0185. Main: 6dfb9546a4873baff15955cf86abfd7d47e3d111.

Decisión PO 05-10-2026: sin plan de cuentas. Dinero recibido por fecha efectiva; control de cuotas separado; deuda no recuperada por expulsión por no pago en informe como pérdida sin movimiento de caja.

Reserva ChatGPT: LodgeTreasuryEndpoints.cs, nuevo LodgeUnrecoveredDues.cs y entidades/migración nueva específica; PmgmDbContext.cs (cruce no caliente con #116); pruebas nuevas de Tesorería; LodgeTreasuryPanel.tsx; pmgmApi.ts (cruce no caliente con #116); prueba frontend nueva; DB-010, changelog y este handoff. Migración nueva de Tesorería, sin modificar migraciones históricas de #116. Un único agente backend/migración activo en este corte. App.tsx, estilos/identidad y archivos de #327 excluidos. #58/#60 no se modifican.

Diseño: reconocimiento explícito con retiro forzoso formal aprobado del mismo Taller, respaldo y confirmación de causa no pago. Saldo impago calculado por servidor y snapshot por cargo; idempotencia y auditoría. Sin inferir causa desde texto ni automatizar expulsiones. No borrar obligaciones ni conceder regularidad al registrar pérdida. Reporte separado de movimientos/ingresos/egresos de caja.

Drive revisado: solo Línea Base modificada desde consulta anterior, contiene decisión PO recién registrada; instrucciones de seguridad y adenda PROPUESTA quedan fuera de este corte. Estado: reservado, no implementación ni pruebas aún. Cierre estimado durante esta sesión sujeto a gates exact-head/publicación. Srv01/UAT pausados; no promover main.
