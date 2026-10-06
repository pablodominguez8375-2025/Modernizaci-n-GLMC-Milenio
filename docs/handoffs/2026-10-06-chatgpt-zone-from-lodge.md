# Zona de cuotas desde la Ficha del Taller — ChatGPT — 06-10

Issue #337 · base dev a0df0378fe6af4d2bc32401f01372d6303598e31.

Fuente: instrucciones PO del 06-10, documento 1UMBWl9tgX1NZknVMeFQR3E1bDMPVkq5vi7r6F7wChNQ; reglas PO 04-10 v2 ya ejecutadas. Adenda Claude del 06-10 revisada: #327/#334 integrados; preservar paneles de Ficha.

Reserva: TreasuryEndpoints/WorkshopOriente, consulta de zonas, Ficha y contratos/pruebas, verificación de consistencia, registro GOV004. Sin hot files compartidos, main ni srv01.

Hallazgo: la instrucción describe una asignación activa, pero en esta base el POST ya responde 409 sin modificar datos. Se retirará ese contrato legado; la consulta y el cálculo ya derivan de ciudad/país. La nota de Ficha contradice la regla vigente.

Estado: preparación; no afirmar filas institucionales verificadas. Cierre y SHA final mediante recibo persistente del PR.

## Resultado implementado

Retirado POST legado (antes 409, ahora 405) y setter API/demo. Zona desde Oriente/ciudad/país; Fichas inconsistentes sin zona. Ficha consulta zona derivada y decreto vigente, nota corregida, paneles #334 preservados. Consulta con tarjetas horizontales en móvil/columna en PC y enlace a la Ficha seleccionada. Gran Tesorería puede consultar la Ficha; edición conserva permisos de Secretaría/Régimen Interior.

[Registro GOV004](../modelo-datos/cambios/2026-10-06-issue-337-zona-cuotas.md) y [verificador SQL](../modelo-datos/sql/2026-10-06-verificar-zonas-cuotas.sql). Sin migración nueva, tarifas ni pagos históricos modificados. Verificación de todos los Talleres mediante SQL de solo lectura: ID, zona almacenada, zona derivada y corrección auditada desde Ficha.

Discrepancias sintéticas: Taller QA zona fuente, Ficha Santiago/cache Perú; detecta Perú→Santiago. Código Perú/ubicación Santiago y other_chile/Santiago quedan sin zona. No se consultaron Talleres institucionales: listado real pendiente al reanudar srv01 (#97); no atribuir resultados sintéticos a base instalada.

Build/lint y 428 pruebas frontend locales verdes; privacidad/clasificación/migraciones verdes. Prueba HTTP PostgreSQL cubre verificador SQL, POST 405 tres zonas, lectura derivada sin reescribir cache, tres cambios de Ficha y auditoría. Ocho casos de derivación y paridad demo Ficha/consulta/tarifario CLP/USD. .NET no disponible local: gates exact-head y resultados finales mediante [recibo persistente](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/pull/338#issuecomment-6016312724). Main congelado, srv01 y QA física/UAT pausados.
