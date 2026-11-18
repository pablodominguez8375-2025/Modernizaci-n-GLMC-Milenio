# Lecciones del GO da274d65 → requisitos del actualizador automático API/web (QA525)

Fecha: 2026-11-17 (CLT), actualizado 23:15 tras cerrar el corte. Fuente: #525/#1028 y evidencia física del Sponsor en glmdch. Estado vivo: memoria `areas/qa525-go-corte.md`. Procedimiento manual completo: `claude/qa525-runbook-actualizacion-manual.md`.

## Qué falló (hechos)
- GO API_WEB_QA_ONLY da274d65 (18-nov 00:13Z): `DUAL_ROLLBACK_FAILED_MANUAL_RECOVERY_REQUIRED`. QA quedó sano en 6eed4f00 (rollback físico correcto).
- El operador recrea API y WEB y verifica la salud pública de inmediato (un intento, sin espera). La API tarda ~6 s en escuchar y no tiene healthcheck Docker, así que `compose --wait` vuelve antes de que la app esté lista.
- El rollback termina recreando la API y verifica al instante: la verificación casi siempre falla en el host real aunque el rollback físico sea correcto. Los tests con Docker simulado no lo detectaron.
- El avance tenía ~1 s de margen. Mismo patrón que 6eed4f00 (1.er intento falló, 2.º pasó).
- Un fallo de verificación deja el journal en RECOVERY_MANUALLY_REQUIRED y bloquea el reintento del mismo SHA aunque el estado físico esté bien.

## Qué funcionó (probado en glmdch el 17/18-nov)
- Sondeo HTTP directo a la IP del contenedor nuevo (API :8080 `/health/ready`, WEB :80 `/health/web` + `/qa-version.json`), exigiendo ID distinto del previo + imagen esperada + running + sin reinicios, con plazo global y reintentos de 2 s: el apply manual pasó `PASS_VERIFIED` al primer intento.
- Conciliar el journal cambiando SOLO `status` a `APPLIED` (mecanismo atómico del operador) con respaldo y recibo; el operador acepta la cadena de capas solo con journal APPLIED.
- Gates A0 de solo lectura con el mismo hash antes de cada ejecución: detectaron dos errores sin mutar (baseline objeto vs lista; falso positivo por `route-a-recovery-*` histórico).
- Pruebas herméticas que cargan el código REAL del operador (`reviewed_layers`, `expected_compose_chain`) para probar compatibilidad.
- Flujo de control: pruebas → A0 → dictamen exact-hash ChatGPT → autorización literal del Sponsor con hash → una ejecución → postflight independiente → registro.

## Requisitos para el actualizador automático
1. Esperar salud REAL ligada a la instancia nueva (ver «Qué funcionó»); plazo global explícito (avance y rollback) y reintentos acotados.
2. Healthcheck Docker en la API (o sonda directa) para que `--wait` signifique «lista».
3. Journal que distinga fallo de VERIFICACIÓN (estado físico restaurado y confirmado) de fallo FÍSICO; recuperación determinista; reintento seguro tras rollback verificado, y camino documentado para cerrar el journal a `APPLIED` sin intervención manual si el estado físico está verificado.
4. Ensayo con tiempos reales de arranque de la API: «viejo sano / nuevo caído», «sin recreación», «API lenta».
5. Sin red en la ventana mutante (diseño #845: SYNC/CACHE → admisión offline → apply), recibos con anclajes (hash del journal, IDs de contenedor antes/después, versión pública).
6. Datos (migraciones/G6) como fase separada con aprobación propia.
7. Código inactivo hacia dev con CI exact-head y 0 P0/P1 (autorización del PO 14-nov); activación y GO en glmdch solo con autorización explícita.
8. Gates que distingan artefactos históricos legítimos de pendientes peligrosos; mensajes STOP siempre a stderr.

## Lecciones de proceso
- Antes de proponer una ruta crítica, buscar el precedente del mismo estado en #525.
- Nunca derivar un esquema de una salida transformada: pedir claves+tipos crudos antes de escribir un gate.
- Antes de decidir cómo cerrar un journal, leer el código del operador que lo consume y probar con ese código; archivar el journal habría roto el próximo corte.
- Los comentarios publicados con herramientas de Claude llevan pie automático: las reseñas JSON las publica el Sponsor a mano.
- Control independiente: el dictamen exact-hash de ChatGPT encontró P0/P1 reales (3 NO-GO a la ruta B, 1 NO-GO al reconciliador); cada NO-GO cambió el hash y se corrigió antes de ejecutar.
- Vigilantes de comentarios: buscar por hash sin filtro de fecha.
- `soporte` necesita `sudo -n` para docker y para entrar a `/opt/centenario/qa525-signed-prepare`; dar siempre comandos que ya lo incluyan.

## Alcance real del actualizador (brecha frente a «API/web y datos»)
- `auto-web` (#1010) cubre SOLO cambios de imagen WEB elegibles. F4-A (assets estáticos) habría automatizado 0 de los 11 cambios WEB recientes; F4-B (pantallas TS/TSX/CSS acotadas, en sombra) 3 de 11. Todo lo que toque API, migraciones, Compose, identidad o datos sigue siendo MANUAL por diseño.
- Hoy, un corte API+WEB usa el operador firmado (`--review-and-run`). Con #1032 (espera de salud acotada) el próximo corte debería pasar al primer intento; sigue necesitando reseña firmada, aprobación/nonce y GO del Sponsor.
- Brecha a decidir con el Sponsor y ChatGPT: (1) `auto-apiweb` (API sin migraciones) con el mismo patrón política firmada + sobre + rollback único; (2) datos/migraciones como fase separada con aprobación propia y G6; (3) mientras tanto, reducir la ceremonia manual (un script genérico revisado una vez y parametrizado por `prepared.json`, en vez de un script por corte).
- Ruta de trabajo propuesta: #1032 → integrar #1021 → orquestador `auto-web` (policy + auto) → F2 sombra → F3 supervisada → recién entonces diseñar `auto-apiweb`.
