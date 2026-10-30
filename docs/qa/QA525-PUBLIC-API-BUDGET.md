# QA525 — presupuesto de API pública y fallos HTTP — 2026-10-30

## Incidente confirmado

El PO obtuvo HTTP403, CUOTA0, reset2026-10-30T18:35:29Z desde glmdch. El entrypoint se detuvo en readiness antes de construir imágenes (cero candidatas). No se ejecutó apply ni se cambió la BD. La preparación repetía inventarios completos en callbacks adyacentes; la cuota pública se agotó. No es evidencia de fallo del respaldo, de EF99 ni de Docker.

## Contrato del correctivo PR807

- Cada evaluación completa usa un solo inventario de workflows para CI y publicador. Se conserva la lectura real de HEAD al final y no se cachean releases, artefactos, fallos ni datos entre invocaciones.
- Un corte conocido puede fijarse con `expected_sha` (API interna, no permiso): se seleccionan sus pruebas/artefactos, pero la lectura final REAL de dev debe coincidir. Un SHA antiguo jamás pasa por estar fijado.
- `verify_current(selection=...)` solo reutiliza la selección completa recién comprobada por el llamador. Sigue descargando, verificando criptográficamente Sigstore/manifiesto y ejecutando una evaluación completa independiente después. Una selección falsa, antigua o distinta NO puede publicar staging. CLI pública sin selección conserva doble evaluación.
- El entrypoint clasifica y revisa esquema sobre la MISMA comparación exacta del corte, sin repetir gates en funciones puras. Los dos blobs EF siguen obteniéndose y cotejándose con el contrato PR801.
- Preparación: verificación completa inicial, nueva firma, manifiesto y HEAD vivos en los seis callbacks de API/WEB, y gate completo al terminar, ANTES de escribir prepared.json. Un cambio de CI durante build no deja recibo válido, aunque existan imágenes de staging.
- Aplicación: `pre` solo prepara journal/imágenes anteriores bajo locks y usa la evidencia criptográfica de ESTA operación; revalida actas/G6/esquema. Inmediatamente antes de recrear servicios y después de salud/snapshot se exigen gates completos FRESCOS. Un fallo final, incluida revocación CI, revierte ambas imágenes. No se reemplaza la evidencia por un JSON aprobado ni se acepta una fase desconocida.
- Sin cambios en locks, nonce, expiraciones, G6, esquema99, flags, Compose/identidad, OCI, rollback o UAT. Sin tokens ni credenciales nuevas. Ningún cache persistente, TTL, aceptación offline o fallback de fuente.

## Presupuesto medido

Las pruebas herméticas ejecutan el gate y orquestador reales con HTTP/firmante/Docker simulados. Gate general5 consultas; gate con corte conocido4. Preparación21 consultas contando clasificación/blobs (18 medidas en orquestación +3 medidas en revisión de cambios). Apply19 por secuencia de fases actual: revisión7, revalidación de firma4, just-before4, post4; fases pre no repiten red. Staging independiente10 consultas. Los valores suponen respuestas exitosas y un corte estable; no garantizan cuota disponible si otros procesos comparten la IP.

No se reintenta automáticamente ante 403/429. El mensaje distingue GITHUB_API_RATE_LIMITED (remaining0), GITHUB_API_RETRY_LATER, GITHUB_API_HTTP_ERROR y fallo de red/JSON. No imprime cuerpos HTTP, tokens ni excepciones arbitrarias. Esperar reset y revalidar las actas si vencieron, nunca cambiar solo las fechas.

## Operación

PR/CI/release nueva obligatorios antes de instalar este correctivo. Conservar las evidencias EF99/G6 mientras no haya deriva/caducidad; no repetir migración o respaldo por agotamiento de cuota. Nueva fuente requiere revisión y actas auténticas ligadas a su SHA/digest. PR804/805, main, producción y srv01 excluidos. No hay nueva aplicación física ni UAT por estas pruebas.
