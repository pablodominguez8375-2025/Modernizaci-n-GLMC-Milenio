# Ajustes de recibos: evidencia de concurrencia e inmutabilidad

Continuación técnica del alcance aprobado de #191, sobre dev `e097298641cdd959b2e47c9354fffc748ab97e4d`. Adapta pruebas preparadas en commit local `430ee44`, conservado aparte.

- Barrera de lecturas dentro de dos transacciones PostgreSQL serializables: doble anulación, clave idempotente compartida y correcciones que excederían el origen si ambas confirmaran.
- Una transacción y auditoría confirmadas, conflicto del competidor, reintento idempotente y originales/saldos conservados.
- DELETE directo de recibo, imputación y ajuste rechazado por PostgreSQL; complementa la prueba existente de UPDATE/historia cerrada.
- Matriz QA identifica cobertura automática y criterios adicionales de ajuste/cierre o imputación. Handoff nuevo conforme GOV-003; START-HERE sólo una línea en PR documental posterior.

Requiere `PMGM_TEST_POSTGRES`; una suite sin esa variable no ejecuta estos escenarios. CI proporciona PostgreSQL y publica TRX para verificar ejecución. CI detectó que Npgsql envuelve un conflicto 40001 de SaveChanges en InvalidOperationException; el endpoint reconoce esa envoltura y retorna 409 en lugar de 500. No cambia reglas contables, permisos, migraciones ni frontend. #191 sigue abierto por presentación institucional y validación operacional. Main intacta; srv01 pausado por #97, despliegue QA pendiente, sin QA física/UAT.
