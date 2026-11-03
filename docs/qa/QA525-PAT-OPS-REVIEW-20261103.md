# QA525 — PAT de lectura: contrato de revisión y separación de cortes

Estado: DRAFT; **prohibido instalar o desplegar a partir de este documento**. Issue #525 / #822.

## Situación confirmada (03-11-2026)

- QA físico glmdch: API+WEB en revisión OCI `41d9c490d138419c7286445f37e7aeddf0d1b8d5`, `prepared.json` de `cfe4e6573ece2ae81414cc252047d36cc56e5c73` con imágenes construidas, journal `PENDING`, `may_apply=false`.
- Respaldo G6 revisado, locks, EF100 y versión pública previa: comprobaciones de solo lectura PASS; admisión G6 caduca 2026-11-04T10:45Z.
- PAT Fine-grained validado en el host: API GitHub límite 5.000/h, raíz `/etc/pmgm/qa525-github-readonly.pat` root:root 0600; jamás incluir el secreto en código, logs, comentarios o respuestas.
- Dos A0 detuvieron por cuota anónima 54/49; control previo posterior detuvo con 47 de 58; **no ejecutar más A0 anónimos por reintento**.

## Separación obligatoria

- **Corte de aplicación inmóvil:** `app_source_sha=6eed4f00b7917da3cca36bbda70e16d16328648d`, `app_digest=sha256:9bdeaa217f09f5d0e19e50d214a4abb936a2b89246ebbcf71fb71178e5574af2`. El gate QA525 compara SHA con HEAD vivo de `dev`: NO fusionar esta rama a `dev` antes de completar ese corte.
- **Corte de operación firmado (pendiente):** no heredar autorización de la firma del corte de aplicación. El bootstrap debe validar identidad Sigstore, manifest, conjunto completo de fuentes y hashes, nonce/acta root0600, cinco locks y reversión. El instalador #822 incluye `qa-agent-readiness.py` en `SOURCES`; reemplazar únicamente el launcher o editar el árbol signed-stage rompe la cadena de confianza.
- **Lectura de PAT:** únicamente root/glmdch, archivo regular 0600 root:root de un solo enlace, sin seguir symlinks y sin caída a consulta anónima. HTTPS fijo `api.github.com`, sin proxy/redirección, errores HTTP fail-closed y sin filtrar valor de token.
- **Cuota:** el nuevo guion Ruta A debe chequear cuota autenticada (>=58) en A0 y antes de A2. No reducir umbral ni deshabilitar controles de firma/HEAD.
- **Autorizaciones diferentes:** instalar operador ≠ archivar los cinco registros `cfe4` ≠ un intento A2 API+WEB. Cada cambio físico requiere el procedimiento y aprobación correspondientes.

## Definition of Done

1. Cambios de cliente y guion en este PR DRAFT; pruebas de autenticación negativas y control de cuota.
2. Revisión independiente de seguridad; CI exact-head. PR DRAFT no equivale a merge/autorización.
3. Distribución del cambio operacional bajo firma y bootstrap existente, con mecanismo #822 revisado y verificado.
4. A0 autenticado PASS físico + G6 vigente + estado pendiente sin drift.
5. Autorización expresa Sponsor para A1 custodia cinco evidencias y UN A2 al SHA de aplicación.
6. Verificación del journal `APPLIED`, API/WEB OCI `6eed4`, /qa-version.json, /health/ready, OIDC, rollback y UAT sintética.

Sin el punto 6 **no afirmar actualización finalizada**.
