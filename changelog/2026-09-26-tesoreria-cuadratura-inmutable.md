# 2026-09-26 — Evidencia histórica de cuadratura de caja

## Alcance
Se amplía el reporte de control contable del Taller a partir de funciones compatibles del Sistema Logial de referencia. Cada arqueo puede guardarse como registro inmutable asociado a período, con apertura, ingresos, egresos aprobados y pendientes, cierre calculado, saldo contado, diferencia, número de movimientos, referencia/nota opcional, sujeto autenticado y hora UTC. El backend calcula los totales desde el libro; repetir la operación crea una nueva evidencia y no modifica movimientos existentes.

Se conserva separada la conciliación mensual de Gran Tesorería. No se incorpora el semáforo duplicado ni la proyección de caja de la referencia, cuya cobranza supuesta no es evidencia de ingresos reales. Permisos y aprobaciones existentes no cambian.

## Control y verificación
QA-041 cubre la conciliación inmutable. La ejecución exact-head automatizada se actualizará al completar el PR; instalación física, smoke autenticado, regresión institucional y UAT en srv01 siguen pendientes por decisión del Sponsor.
