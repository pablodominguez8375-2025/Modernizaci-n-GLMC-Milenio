# QA525 vNext — Comprobación G6 durante el vuelo sintético

**Alcance:** biblioteca estrictamente INACTIVA. Es coherencia de bytes y fechas, no autenticación ni autorización física. No usa Docker, SSH, host, red, claves, ni toca glmdch o main.

## Contrato

El motor ensamblado recibe g6_evidence como un diccionario de nueve campos exactos: review_bytes, receipt_bytes, restore_bytes, pinned_admission_sha256, pinned_approval_sha256, pinned_review_sha256, pinned_receipt_sha256, pinned_restore_sha256 y observed_archive_sha256. No admite omisiones, booleanos ni un resultado PASS precalculado. La entrada se copia de inmediato en un diccionario privado de bytes y strings inmutables, expuesto solo al motor mediante MappingProxyType. Se comprueba que el diccionario original no cambió antes y después de la validación: si cambia, STOP sin habilitar avance. Todos los pins siguen siendo datos de origen no autenticado hasta que un bootstrap root firmado los obtenga de descriptores protegidos.

Tras retener cinco locks sintéticos y el lease raíz, run_synthetic relee clock() y ejecuta g6_anchor.compare con los bytes crudos de la admisión/ancla y revisión G6. Exige coincidencia con nonce, digest de admisión, digest de aprobación y flags may_apply=false/may_install=false. Si falla, se niega ANTES de invocar factory, capturar baseline, escribir scopes o usar runner.

Para las transiciones API_SWITCHING y WEB_SWITCHING vuelve a ejecutar g6_anchor.compare con hora fresca DESPUÉS del external_gate y bajo locks todavía retenidos. Se comprueba también la caducidad checkpoint+48 horas hasta admission.valid_until implementada por #887. El runtime sintético devuelve APPLIED o ROLLED_BACK como resultado de fixture, nunca permiso de despliegue.

## Reversión

Cuando G6 caduca entre las dos etapas, la etapa forward se deniega. ROLLING_BACK no requiere G6 renovado, porque caducidad sobrevenida no debe impedir restaurar el baseline; el rollback SÍ requiere locks y external_gate independiente, y sigue sujeto a la verificación durable. Si éstos fallan, el journal conserva evidencia y puede exigir RECOVERY_REQUIRED. El código inactivo no garantiza recuperación física.

## Pruebas

Tests herméticos: presencia y forma de evidencias, rechazo de bytearray/memoryview/list y pins no string, mutación durante compare con bytes congelados y STOP, digest alterado antes del factory/baseline, clock() fresco tras locks, adulteración durante gate API, expiración antes de WEB y rollback verificado, mutación de G6 durante ROLLING_BACK sin impedir restauración bajo external_gate, llamadas repetidas en API y WEB. La ventana mínima entre gate e intent persiste y requiere control privilegiado del futuro bootstrap. La suite general y 7/7 workflows CI se verifican sobre el HEAD exacto del PR; se requiere revisión independiente antes de integrar dev.

## Barreras de operación

NO-GO glmdch hasta autenticación y firma OPS/APP/Sigstore antes del primer byte root, aprobación Sponsor independiente, G6 cifrado real y custodia offhost verificada, restauración aislada de dos BD y validación fuera del host cuando aplique, reloj confiable, locks/cgroup físicos, salud HTTPS/OIDC, rollback dual, UAT y acta firmada. Este PR no instala, no despliega, no habilita main ni modifica BD, identidad, Compose o producción.

Fuente de coordinación: GitHub Issue #525 y Documento Estado Maestro Operativo en Google Drive.
