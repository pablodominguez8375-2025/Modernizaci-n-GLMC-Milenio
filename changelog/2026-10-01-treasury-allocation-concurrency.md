# Imputaciones concurrentes de recibos

Issue #191 / PR #248. Seis escenarios HTTP PostgreSQL fuerzan anulación/imputación, corrección/imputación e imputación/imputación, en CLP y USD ficticios. CI inicial 36897829717 ejecutó 374 pruebas y reprodujo tres fallos 500 en lugar de 409 (371 PASS, 3 FAIL, 0 omitidas), con 40001 envuelto por Npgsql.

El endpoint de imputación transforma exclusivamente 40001/23505 y sus envolturas conocidas en conflicto HTTP 409, conservando rollback y reintento explícito desde saldos actualizados. No se cambian reglas, importes, monedas, permisos, esquema ni UI. Pruebas originales conservadas; nuevas evidencias exact-head requeridas. Carrera ajuste/cierre y presentación financiera institucional siguen pendientes, srv01 pausado y main congelado.
