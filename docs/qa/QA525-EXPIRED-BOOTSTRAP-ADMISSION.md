# QA525 — segunda preparación tras admisión expirada
Estado: PR844 para revisión; NO ejecutar desde este documento.
Base dev6eed4f00; #822/#525. Alcance: solo archivo histórico de admisión bootstrap.

## Decisión
Se amplía el bootstrap existente con --archive-expired-admission y --archive-approval.
No se cambia la negativa del bootstrap normal ante admisión existente.
No se sobrescribe ni renueva la admisiónA: se conserva en STORE/admission-history-N/admission.json.
El comando no instala launcher, no prepara una admisiónB y no toca prepared/journalsAPI+WEB, DB ni contenedores.

## Cadena de confianza y revisión
El archivo bootstrap nuevo es Python privilegiado: autenticar su hash por revisión independiente ANTES del primer byte root, desde el paquete firmado del SHA revisado. Su parámetro trusted-bootstrap-sha256 NO se autentica a sí mismo.
La operación revalida ZIP/Sigstore/source-digest/manifest completo del siguiente corte antes de permitir el archivo de A.
El auxiliar installer adyacente debe ser root0600 y tener el SHA256 fijado en ARCHIVE_INSTALLER_SHA256; se usa solo para locks y lectura de historia, no para instalación.
Revisión adversarial y CI exact-HEAD antes de integrar/publicar. Ningún comando físico está autorizado por tener CIverde.

## Acta nueva
Root0600 y referencias reales verificadas por operador/revisor. Schema qa525-admission-archive-v1, action ARCHIVE_EXPIRED_BOOTSTRAP_ADMISSION_ONLY.
Campos exactos: schema,action,source_sha,next_source_sha,next_release_asset_sha256,admission_sha256,receipt_sha256,launcher_sha256,cut_nonce,nonce,issued_at,expires_at,operator,operator_reviewed,sponsor_reference,technical_reference.
source_sha es A; next_source_sha/digest es B firmado. Hashes SHA256 de bytes originales y launcher. cut_nonce corresponde al receipt launcher-only COMMITTED de A. nonce es NUEVO, no de ninguna instalación/recovery previa. TTL<=900s. Referencias de #525/#822 son atestación root, no autenticación de su texto por red.

## Operación y fallos
1. Aislamiento Python -I -S, UIDroot, hostglmdch y pin de bootstrap.
2. Lockbootstrap existente; rechaza admission-archive-pending.json.
3. Verificar firma y todos los miembros deB en temporal privado, sin ejecutar miembros.
4. Cinco locksQA existentes (apertura read-only), scan_history con verificación de recuperación.
5. Leer bytes privados acotados deA, acta, receiptCOMMITTED y launcher; comprobar expiraciónA, TTLacta, SHA/cut/digest/hash, launcher sin deriva y ausencia recovery-admission.
6. Releer bytes para guardia final. Crear archivo privado, copiar acta, consumir nonce de archivo irreversiblemente, fsync. Marcador durable ANTES de moverA.
7. RenombrarA a historia conservando bytes/fechas; fsync ambos directorios, escribir receipt durable EXPIRED_BOOTSTRAP_ADMISSION_ARCHIVED_NO_AUTHORITY y solo entonces quitar marcador.
Si falla antes de mover, A sigue original; si falla después, está en historia. El marcador bloquea toda preparación hasta revisión manual. No borrar marcador, acta o recibos a mano ni reintentar.
Si falla después del consumo pero antes del marcador, la aprobación quedó consumida y requiere aprobación nueva; nunca se reutiliza nonce.
Una vez confirmado archivo completo, bootstrap normal autenticaB NUEVAMENTE y genera admisión nueva de900s; no copia fechas/autoridadA. Después instalaciónlauncher con aprobación distinta y planrevisado. ConciliaciónAPI+WEB requiere su propia aprobación.

## Ensayo reproducible aislado
Suite existente ampliada: python3 -m unittest discover -s tests -p test_qa525_signed_root_bootstrap_prep.py -q.
37pruebas locales PASS. ZIPs/manifest reales sintéticos; SOLO transporteSigstore y host/ownership se simulan, y helperlocks/history inyectado en las pruebas de archivo. Se prueba preparaciónA ->archivoA->preparaciónB, conservaciónbytes/launcher, B emitida con fecha nueva, fallofirma, bindings/TTL/replay, fallo trasrename bloquea nueva preparación y hash del helper. 30pruebas existentes conservadas.
Un fixture inicial del ensayo recursó su propio Mock al envolver el verificador; corregido antes de publicación. No hubo ejecución física.
Límite: no sustituye revisión de locks/permisos contra host ni prueba de primera instalación/rollback en glmdch. El ensayo cubre el bloqueo de admisión, no certifica despliegueAPI+WEB ni UAT. Antes de otra ventanaSponsor se requiere completar el ensayo conjunto del recorrido de instalación y conciliación, y revisión del guion final.
