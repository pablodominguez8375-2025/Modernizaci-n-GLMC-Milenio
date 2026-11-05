# QA525 — PAT de lectura: contrato de revisión y separación de cortes

Estado: DRAFT; **prohibido instalar o desplegar a partir de este documento**. Issue #525 / #822.

## Situación vigente (05-11-2026)

- QA físico glmdch: API+WEB en `6eed4f00b7917da3cca36bbda70e16d16328648d`, journal `APPLIED`, un evento aplicado, WEB healthy y health/OIDC PASS. Launcher `READ_ONLY`, `may_apply=false`.
- `prepared.json` permanece retenido como evidencia ligada al corte aplicado. No borrarlo y no repetir el actualizador. `old41d9` y `cfe4 PENDING` son antecedentes históricos, no la línea base vigente.
- PAT Fine-grained validado en el host: API GitHub límite 5.000/h, raíz `/etc/pmgm/qa525-github-readonly.pat` root:root 0600; jamás incluir el secreto en código, logs, comentarios o respuestas.
- La aplicación del corte mostró dos STOP pre-launcher por cuota anónima. Esta dependencia queda eliminada del diseño vNext: la red y GitHub se usan únicamente en `SYNC/CACHE`, antes de la admisión y fuera del tramo mutante.

## Implementación vNext en este PR

`scripts/qa/qa525-vnext-cache.py` separa tres comandos sin capacidad de despliegue:

1. `sync`: usa obligatoriamente el PAT root-only y sin fallback anónimo, selecciona un SHA de `dev` completamente validado, descarga el artefacto firmado y crea caché privada ligada a `ops_source_sha`, `app_source_sha`, digest y caducidad.
2. `admit`: trabaja sin red; valida caché, hashes, aprobación de corta duración, identidad OPS/APP, margen G6 antes de escribir estado y nonce de un solo uso.
3. `postcheck`: valida journal `APPLIED`, runtime API/WEB y launcher cerrado. Acepta `prepared.json` retenido únicamente si está ligado al mismo APP SHA, `may_apply=false` y no existe `.next`.

El componente no contiene comando `apply`, no invoca Docker ni `pmgm-qa-update` y no autoriza instalación. La siguiente entrega debe conectar un motor APPLY offline con state machine durable y recovery por transición, bajo artefacto OPS firmado y revisión independiente.

Pruebas locales: 73 pruebas del contrato `QA auto-agent readiness` PASS, incluidas 20 nuevas de SYNC/admisión/postcheck vNext, atestación Sigstore durante SYNC, reverificación offline del manifest, cero red en admisión, no reenvío del PAT al blob, rechazo de redirect no confiable, rechazo de caché por symlink/permisos débiles, G6, replay de nonce, TOCTOU básico de archivos, prepared retenido y drift de runtime.

## Separación obligatoria

- **Corte de aplicación inmóvil:** `app_source_sha=6eed4f00b7917da3cca36bbda70e16d16328648d`, `app_digest=sha256:9bdeaa217f09f5d0e19e50d214a4abb936a2b89246ebbcf71fb71178e5574af2`. El gate QA525 compara SHA con HEAD vivo de `dev`: NO fusionar esta rama a `dev` antes de completar ese corte.
- **Corte de operación firmado (pendiente):** no heredar autorización de la firma del corte de aplicación. El bootstrap debe validar identidad Sigstore, manifest, conjunto completo de fuentes y hashes, nonce/acta root0600, cinco locks y reversión. El instalador #822 incluye `qa-agent-readiness.py` en `SOURCES`; reemplazar únicamente el launcher o editar el árbol signed-stage rompe la cadena de confianza.
- **Lectura de PAT:** únicamente root/glmdch, archivo regular 0600 root:root de un solo enlace, sin seguir symlinks y sin caída a consulta anónima. HTTPS fijo `api.github.com`, sin proxy/redirección, errores HTTP fail-closed y sin filtrar valor de token.
- **Cuota:** solo pertenece a `SYNC/CACHE`. ADMISSION y el futuro APPLY no consultan GitHub ni dependen de cuota.
- **Autorizaciones diferentes:** instalar operador ≠ archivar los cinco registros `cfe4` ≠ un intento A2 API+WEB. Cada cambio físico requiere el procedimiento y aprobación correspondientes.

## Definition of Done

1. Cambios de cliente y guion en este PR DRAFT; pruebas de autenticación negativas y control de cuota.
2. Revisión independiente de seguridad; CI exact-head. PR DRAFT no equivale a merge/autorización.
3. Distribución del cambio operacional bajo firma y bootstrap existente, con mecanismo #822 revisado y verificado.
4. Motor APPLY offline/state machine durable y pruebas de crash/recovery en cada transición.
5. Distribución OPS firmada, revisión adversarial exact-head y aprobación física posterior.
6. Prueba física futura con un SHA posterior; `6eed4` ya está aplicado y no debe volver a desplegarse.

El ciclo `6eed4` está finalizado. Este PR mejora el proceso futuro y no cambia el runtime actual.
