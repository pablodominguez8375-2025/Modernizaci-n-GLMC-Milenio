# Registro GOV-004 — #191 / PR #329

Base dev 4a026bdf62adb8579ad3faab08d57975383c0185; main 6dfb9546a4873baff15955cf86abfd7d47e3d111. Feature treasury-unrecovered-dues-20261005-gpt.

Cambio aprobado por PO: sin plan de cuentas; dinero real y regularidad paralelos; pérdida informativa de deuda no recuperada por expulsión por no pago. Añade tabla append-only, dos endpoints y proyección de pérdidas separada del reporte de caja. [DB-010](../PMGM-DB-010-cuotas-no-recuperadas.md) contiene estructura/contratos/acceso/retención. Migración nueva, no modifica migraciones históricas reclamadas en #116.

Afecta backend Tesorería/PmgmDbContext, reportes/UI/adaptador demo, clasificación GL-023. Cruces no calientes PmgmDbContext/pmgmApi con candidato histórico #116 registrados; no se cambia su rama. No toca App.tsx, workflows, identidad, #327 de Claude o reglas institucionales de retiro. Se usa la formalización existente y una confirmación financiera explícita, sin interpretar causa libre ni expulsar automáticamente.

Local: frontend 421/421 y lint/build PASS; gates de privacidad/clasificación/migraciones se verifican antes de publicar. Backend no se ejecutó localmente por falta de SDK .NET/PostgreSQL; depende de CI exact-head. Gates y publicación pendientes hasta recibo final. Main/srv01/UAT pausados.
