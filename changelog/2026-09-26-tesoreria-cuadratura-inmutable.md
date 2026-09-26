# 2026-09-26 — Evidencia histórica de cuadratura de caja

## Alcance
Se amplía el reporte de control contable del Taller a partir de funciones compatibles del Sistema Logial de referencia. Cada arqueo puede guardarse como registro inmutable asociado a período, con apertura, ingresos, egresos aprobados y pendientes, cierre calculado, saldo contado, diferencia, número de movimientos, referencia/nota opcional, sujeto autenticado y hora UTC. El backend calcula los totales desde el libro; repetir la operación crea una nueva evidencia y no modifica movimientos existentes.

Se conserva separada la conciliación mensual de Gran Tesorería. No se incorpora el semáforo duplicado ni la proyección de caja de la referencia, cuya cobranza supuesta no es evidencia de ingresos reales. Permisos y aprobaciones existentes no cambian.

## Control y verificación
PR #169 quedó integrado por squash en `dev` como `2db812f37c2080d166b74692f0118f566879138c`. Frontend: 199/199 pruebas, lint y build; QA, clasificación, migraciones y diff-check locales pasan. Post-merge PMGM CI #1559 (run 36205450888), Showcase/Pages #829 (run 36205450889), QA Installable #467 (run 36205450901) y Pre-UAT #361 (run 36205450882) SUCCESS.

El paquete QA de Actions tiene digest `sha256:673d107bdb5de4cafcc265437177f22074d4c490ffe9fe5148a15dff8dc1c1cb`; `BUILD-INFO.txt` lleva `SOURCE_SHA=2db812f37c2080d166b74692f0118f566879138c` y `MANIFEST.sha256` valida. Pages `qa-current.json` lleva el mismo `sourceSha` y su archivo ZIP SHA-256 `0a8989a5aaac3bf8fc2a5ecb13dc2ca16a9b9127b30de8eccfc1774323a942be`; `BUILD-INFO` y `MANIFEST` del ZIP Pages también validan. Actions y Pages crean sus archivos en ejecuciones independientes, por eso difieren los hashes binarios aunque apunten al mismo SHA de código.

QA-041 cubre la conciliación inmutable. La plantilla tiene 41 controles pending. Instalación física, smoke autenticado, regresión institucional y UAT en srv01 siguen pendientes por decisión del Sponsor; QA/UAT no se aceptan hasta contar con esa evidencia.
