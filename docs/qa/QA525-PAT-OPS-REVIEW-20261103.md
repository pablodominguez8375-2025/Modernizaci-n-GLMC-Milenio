# QA525 — PAT de lectura: contrato de revisión y separación de cortes

Estado: DRAFT; **prohibido instalar o desplegar a partir de este documento**. Issue #525 / #822.

## Situación vigente (05-11-2026)

- QA físico glmdch: API+WEB en `6eed4f00b7917da3cca36bbda70e16d16328648d`, journal `APPLIED`, un evento aplicado, WEB healthy y health/OIDC PASS. Launcher `READ_ONLY`, `may_apply=false`.
- `prepared.json` permanece retenido como evidencia ligada al corte aplicado. No borrarlo y no repetir el actualizador. `old41d9` y `cfe4 PENDING` son antecedentes históricos, no la línea base vigente.
- PAT Fine-grained validado en el host: API GitHub límite 5.000/h, raíz `/etc/pmgm/qa525-github-readonly.pat` root:root 0600; jamás incluir el secreto en código, logs, comentarios o respuestas.
- La aplicación del corte mostró dos STOP pre-launcher por cuota anónima. Esta dependencia queda eliminada del diseño vNext: la red y GitHub se usan únicamente en `SYNC/CACHE`, antes de la admisión y fuera del tramo mutante.

## Implementación vNext en este PR

`scripts/qa/qa525-vnext-cache.py` separa tres comandos sin capacidad de despliegue:

1. `sync`: usa obligatoriamente el PAT root-only y sin fallback anónimo, selecciona un SHA de `dev` completamente validado, descarga el artefacto firmado y crea caché privada ligada al recibo OPS verificado, `app_source_sha`, artifact ID, digest, bundle Sigstore, manifest, identidades de archivo y caducidad. Un lock exclusivo serializa SYNC y una caché expirada se rota sin destruir evidencia.
2. `admit`: trabaja sin red; valida módulos desde rutas fijas y hashes del recibo OPS root0600, reverifica el bundle/manifest contra la evidencia de SYNC, deriva la vigencia G6 desde el timestamp real del archivo (48 h), exige aprobación máxima de 30 minutos ligada a artefacto/scope/environment/action/host y emite `valid_until`. La creación del recibo es exclusiva (`O_EXCL`); el nonce no se consume hasta la futura transición durable `APPLYING`.
3. `postcheck`: valida journal `APPLIED`, runtime API/WEB y launcher cerrado. Acepta `prepared.json` retenido únicamente si está ligado al mismo APP SHA e imágenes, `may_apply=false` y no existe `.next`.

El componente no contiene comando `apply`, no invoca Docker ni `pmgm-qa-update` y no autoriza instalación. La siguiente entrega debe conectar un motor APPLY offline con state machine durable y recovery por transición, bajo artefacto OPS firmado y revisión independiente.

Pruebas históricas del contrato: 81/81 PASS al corte `b7658a5a`. Nuevos HEAD de revisión posteriores deben evaluar su CI exacto (sin extrapolar 81/81). La suite añadió casos sobre el acta G6 real, prepared y seguridad de módulos. Los tests desacoplan explícitamente el gate root sin debilitarlo en producción; validan recibo OPS y módulos fijos, bundle Sigstore + manifest ligados para admisión offline, cero red, no reenvío del PAT al blob, redirect no confiable, aprobación/G6 reales, carrera concurrente del mismo nonce, rotación preservando caché expirada, digest streaming de memoria acotada, symlink/hardlink/permisos, `prepared.json` retenido y drift de runtime.

## Separación obligatoria

- **Corte de aplicación ya completado:** `app_source_sha=6eed4f00b7917da3cca36bbda70e16d16328648d` está `APPLIED` en QA según el cierre físico #525. Su digest histórico de release es `sha256:9bdeaa217f09f5d0e19e50d214a4abb936a2b89246ebbcf71fb71178e5574af2`. Claude dio GO el 05-11 para integrar este código **inactivo** a `dev` (HEAD revisado `d7e3a14`), pero ello no autoriza operar QA. Si `dev` avanza, jamás inferir que el nuevo SHA fue desplegado: el APP físico permanece `6eed4` hasta evidencia posterior.
- **Corte de operación firmado (pendiente):** no heredar autorización de la firma del corte de aplicación. El bootstrap debe validar identidad Sigstore, manifest, conjunto completo de fuentes y hashes, nonce/acta root0600, cinco locks y reversión. El instalador #822 incluye `qa-agent-readiness.py` en `SOURCES`; reemplazar únicamente el launcher o editar el árbol signed-stage rompe la cadena de confianza.
- **Lectura de PAT:** únicamente root/glmdch, archivo regular 0600 root:root de un solo enlace, sin seguir symlinks y sin caída a consulta anónima. HTTPS fijo `api.github.com`, sin proxy/redirección, errores HTTP fail-closed y sin filtrar valor de token.
- **Cuota:** solo pertenece a `SYNC/CACHE`. ADMISSION y el futuro APPLY no consultan GitHub ni dependen de cuota.
- **Autorizaciones diferentes:** instalar operador ≠ archivar los cinco registros `cfe4` ≠ un intento A2 API+WEB. Cada cambio físico requiere el procedimiento y aprobación correspondientes.

## Revisión adversarial y pendientes de instalación (05-11-2026)

Dictamen Claude en PR #845 comentario [6655297963](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/pull/845#issuecomment-6655297963): **0 P0, 0 P1**, 11 workflows principales SUCCESS sobre `d7e3a14`; GO para merge a `dev` como código **inactivo**, NO-GO instalación. Tras esa revisión se añadieron mejoras en la rama: carga OPS desde los mismos bytes verificados (P2 hash/exec); validador acotado de margen G6; ruta de acta anclada a `/etc/pmgm/qa525-g6-reviewed-<timestamp>.json` con pruebas de rechazo. Toda modificación de HEAD invalida la aprobación exact-head hasta nueva revisión; no presentar un CI parcial como GO.

Pendientes que no deben confundirse con la autorización de código:
- Fijar/verificar procedencia firmada del recibo OPS y dependencias transitivas del comando externo `gh attestation verify`; impedir recibos root autoafirmados como autorización.
- Aclarar el digest Sigstore: `qa-signed-staging.py` verifica el **ZIP instalable interno firmado**, mientras `artifact_sha256` corresponde al **ZIP externo de Actions**. No comparar ambos digests como si fueran el mismo objeto; comprobar el subject contra el hash del miembro interno firmado y ligar ese miembro al artefacto externo guardado.
- Completar validación de ancestros root/no-symlink, rotación segura de cachés inválidas, referencia verificable de Sponsor y semántica de los dos planos de estado.
- G6: la ruta del último acta verificada en #525 fue `/etc/pmgm/qa525-g6-reviewed-20261104T105358Z.json` (SHA del archivo registrada en el issue); el nombre demuestra ruta, **no** demuestra sus claves internas. Las claves exactas aún requieren evidencia física sin valores secretos, si se amplía el esquema.
- State machine futura: `CACHED_VERIFIED -> ADMITTED -> APPLYING -> APPLIED | ROLLED_BACK | RECOVERY_REQUIRED`; nonce O_EXCL solo en APPLYING, journal root0600 y fsync; revalidar baseline/G6/TTL/identidad antes de aplicar; APPLY sin red y crash-injection, recovery no automático de estados ambiguos. Sin implementación APPLY en el código actual.

## Definition of Done

1. Cambios de cliente y guion en este PR DRAFT; pruebas de autenticación negativas y control de cuota.
2. Revisión independiente de seguridad; CI exact-head. PR DRAFT no equivale a merge/autorización.
3. Distribución del cambio operacional bajo firma y bootstrap existente, con mecanismo #822 revisado y verificado.
4. Motor APPLY offline/state machine durable y pruebas de crash/recovery en cada transición.
5. Distribución OPS firmada, revisión adversarial exact-head y aprobación física posterior.
6. Prueba física futura con un SHA posterior; `6eed4` ya está aplicado y no debe volver a desplegarse.

El ciclo `6eed4` está finalizado. Este PR mejora el proceso futuro y no cambia el runtime actual.

## Diseño bloqueante — transición APPLY offline y recuperación (especificación, NO implementado)

**Fuente de verdad transaccional:** la carpeta del lanzador real `/opt/centenario/qa525-signed-prepare` es el plano autoritativo de `dual-<APP_SHA>.json`, journals y marcador de nonce consumido. El recibo `admission-<nonce>.json` que genera vNext en `/var/lib/pmgm/qa525-state` es **entrada inmutable** y **nunca** prueba por sí solo que se aplicó una versión. Antes de crear el motor, decidir si un recibo verificado se copia con hash y `O_EXCL` al plano autoritativo o se fija por descriptor (dev/ino/tamaño/mtime) bajo locks; ningún proceso debe escribir estados paralelos contradictorios. Sin sincronización transaccional comprobada, STOP.

**Contrato de estados:** `CACHED_VERIFIED → ADMITTED → APPLYING → APPLIED | ROLLED_BACK | RECOVERY_REQUIRED`. `APPLYING` empieza únicamente tras locks exclusivos y revalidación offline del recibo de admisión (`valid_until`, nonce, OPS SHA, APP SHA, `artifact_sha256`, `signed_installable_sha256`, identidad de fichero y `policy_hash`), acta G6 física con margen, baseline exacto, firma/manifest y aprobación del Sponsor para *ese* corte. Prohibido iniciar Docker o mutación antes de que el journal durable registre estado `APPLYING`. G6 expirado o permiso vencido = STOP sin consumir autorización.

**Punto irreversible y crash:** bajo lock, reservar `nonce-consumed-<nonce>` con `O_CREAT|O_EXCL|O_NOFOLLOW` y escribir/fsync su contenido (hash del recibo, alcance y SHA). Persistir y fsync el journal `APPLYING` **antes** de cualquier cambio API/WEB. Un crash entre el marcador y el journal no da permiso para reintentar: se clasifica `RECOVERY_REQUIRED`. Las transiciones posteriores requieren escritura temporal `.next`, fsync fichero, `os.replace`, fsync directorio, validación de monotonía y prohibición de sobrescribir `APPLIED`/rollback final. Fallar en cada paso no puede producir dos eventos `DUAL_API_WEB_APPLIED`.

**Ejecución sin red:** el proceso `APPLYING` no importa el cliente GitHub ni accede a PAT, `curl`, `gh`, URLs ni DNS. Ejecutarlo en modo de red denegada cuando sea compatible con Docker local (la restricción debe impedir red del proceso controlador sin impedir el acceso del Docker daemon local ya autorizado). Prueba hermética que intercepta sockets y prueba de sandbox con red denegada; una marca JSON `network_allowed=false` por sí sola no prueba aislamiento. La fase SYNC/CACHE es la única que consulta GitHub y verifica en línea Sigstore.

**Reversión y recuperación:** registrar identidad OCI previa y nueva, dependencias, oneshots, etiquetas, versión pública, health y OIDC; invocar únicamente la transacción dual ya revisada del lanzador. Ante caída parcial: cortar, clasificar `RECOVERY_REQUIRED`, preservar artefactos y journals, y solicitar procedimiento separado con nueva aprobación. `RECOVERY_REQUIRED` jamás dispara un `--review-and-run` automático. Una reversión solo es `ROLLED_BACK` cuando API/WEB, imágenes, health/OIDC y baseline anterior están comprobados; no se infiere de exit code. La UAT de roles/soporte00/Crear usuario/415/descargas es un carril posterior, no un gate ficticio en medio del atomic apply.

**Matriz de pruebas bloqueantes antes de cualquier root/instalación:** crash-injection antes/después de reservar nonce, fsync del marcador, journal `APPLYING`, cambio de API, cambio de WEB, health y escritura `APPLIED`; expiración G6 y aprobación antes de entrar `APPLYING`; doble nonce concurrente; recibo alterado; zip interno distinto conservando ZIP externo; `.next` abandonado; lock ocupado; cache expirada/huérfana; fallo real de rollback; red habilitada por error. Verificar que los reportes se publican sin secretos y que una transición terminal no se aplica dos veces.

### P2 identidad de ZIP interno vs externo (incremento de código)

`qa525-vnext-cache.py` ahora calcula `signed_installable_sha256` leyendo en *streaming* el miembro interno de nombre fijo `Proyecto-Centenario-QA-srv01-<SHA12>.zip` del ZIP externo de Actions. La fase SYNC lo ancla tras la verificación Sigstore realizada por `qa-signed-staging.py`; ADMISSION lo vuelve a calcular **offline** contra los bytes del ZIP externo y exige igualdad con el recibo. Rechaza miembros extras/ausentes, tipo o tamaño inválido, modificación de identidad de fichero y alteración del hash interno aunque se rehaga el hash externo. **Límite explícito:** el cotejo adicional NO verifica por sí solo, durante ADMISSION, el `subject digest` codificado dentro de la DSSE de Sigstore. Esa parte y la procedencia del recibo OPS firmado siguen pendientes de revisión/implementación; no presentar este incremento como cierre total del P2 ni como permiso para instalar.

