# Concurrencia de imputaciones de recibos — 01-10-2026

Issue #191; PR #248; dominio Tesorería ChatGPT. Inicio dev `ae1e5497f50317f53d3c6612e8b15c94acec3077`; main `6dfb9546a4873baff15955cf86abfd7d47e3d111`. Reclamo y draft antes de programar; cuatro PR históricos revisados, sin cruce con los archivos de este incremento (PR #1 es comparador de promoción, fuera de alcance).

Se agregan seis casos HTTP PostgreSQL: anulación/imputación, corrección a destino/imputación e imputación/imputación, en CLP y USD sintéticos. Una barrera permite ambas lecturas serializables antes de escribir. Se exige una operación confirmada y otra HTTP 409, originales intactos, saldo/crédito/caja conciliados y auditoría exclusiva del commit. El reintento conserva la intención; una anulación posterior a la imputación sí puede confirmar y debe revertir también la nueva imputación, sin duplicar ajustes al repetir la clave.

Primer corte: solo pruebas y este handoff, para reproducir el comportamiento real antes de reparar. No existe SDK .NET local; requiere CI con PMGM_TEST_POSTGRES y evidencia de los seis casos ejecutados. No confundir retornos sin conexión con ejecución. Gates, integración, publicación y paquete pendientes.

Presentación financiera institucional, concurrencia ajuste/cierre anual y operación permanecen pendientes en #191. Sin normas contables nuevas, UI, permisos o migraciones. Main congelado; srv01 pausado, despliegue QA pendiente, sin instalación ni QA física/UAT. Se preservan los cierres anteriores y la UI de Claude.
