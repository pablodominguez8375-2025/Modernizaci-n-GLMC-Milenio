# #191 — Caja, regularidad y cuotas no recuperadas

Base dev: 4a026bdf62adb8579ad3faab08d57975383c0185. Main: 6dfb9546a4873baff15955cf86abfd7d47e3d111.

Decisión PO 05-10-2026: sin plan de cuentas. Dinero recibido por fecha efectiva; control de cuotas separado; deuda no recuperada por expulsión por no pago en informe como pérdida sin movimiento de caja.

Reserva ChatGPT: LodgeTreasuryEndpoints.cs, nuevo LodgeUnrecoveredDues.cs y entidades/migración nueva específica; PmgmDbContext.cs (cruce no caliente con #116); pruebas nuevas de Tesorería; LodgeTreasuryPanel.tsx; pmgmApi.ts (cruce no caliente con #116); prueba frontend nueva; DB-010, changelog y este handoff. Migración nueva de Tesorería, sin modificar migraciones históricas de #116. Un único agente backend/migración activo en este corte. App.tsx, estilos/identidad y archivos de #327 excluidos. #58/#60 no se modifican.

Diseño: reconocimiento explícito con retiro forzoso formal aprobado del mismo Taller, respaldo y confirmación de causa no pago. Saldo impago calculado por servidor y snapshot por cargo; idempotencia y auditoría. Sin inferir causa desde texto ni automatizar expulsiones. No borrar obligaciones ni conceder regularidad al registrar pérdida. Reporte separado de movimientos/ingresos/egresos de caja.

Drive revisado: solo Línea Base modificada desde consulta anterior, contiene decisión PO recién registrada; instrucciones de seguridad y adenda PROPUESTA quedan fuera de este corte. Estado actualizado: implementado en PR draft #329; falta validar CI exact-head e integrar/publicar. Cierre estimado durante esta sesión sujeto a gates exact-head/publicación. Srv01/UAT pausados; no promover main.

## Implementación y verificación local

Código funcional publicado inicialmente en fc6586a0173f28e211737a76b9ef77f226f8842b. Añade tabla independiente append-only, migración 20261006002500, endpoints GET candidatos/POST reconocimiento, informe separado por fecha de reconocimiento y moneda, UI y adaptador sintético. Índice único ChargeId; no modifica obligaciones, regularidad ni movimientos/arqueos/cierres de caja. El monto es snapshot histórico al reconocer la pérdida; recuperaciones futuras se reciben en caja con fecha efectiva. No inferir no pago de textos privados: confirmación expresa y respaldo sobre retiro formal aprobado.

Frontend inicial 421/421; incremento adicional dirigido 4/4 para consulta sin escribir y revocación, total esperado 422. Lint/build PASS. Privacidad 12, clasificación 100, migraciones 62 PASS. No SDK .NET/PostgreSQL local; CI ejecuta dos casos nuevos CLP/USD con parcial, pagado, futuro, retiro ajeno/voluntario/pendiente, saldo a favor recibido bloqueante, idempotencia, caja/obligaciones intactas y DELETE inmutable. No declarar esos resultados backend hasta verificar CI/TRX.

[DB-010](../modelo-datos/PMGM-DB-010-cuotas-no-recuperadas.md) y [registro GOV-004](../modelo-datos/cambios/2026-10-05-issue-191-cuotas-no-recuperadas.md) integran antes/después, contrato, acceso/privacidad y migración. Clasificación GL-023. No modifica archivos calientes, ramas ajenas o migraciones históricas de #116; cruces no calientes PmgmDbContext/pmgmApi identificados. Fuente PO registrada en Issue #191 y Línea Base, reemplaza pendiente de cuentas contables. Seguridad nueva de Claude queda fuera de este corte.

Pendiente de cierre: CI, Showcase, QA exact-head; squash autorizado si pasan y dev no se mueve; publicar Pages y comprobar SOURCE_SHA/paquetes; handoff documental + una línea START-HERE; sincronizar Drive. Despliegue QA físico y UAT continúan pendientes por #97. Mantener #191 abierto hasta verificación de alcance y aceptación operacional, no pedir nuevas cuentas contables.
