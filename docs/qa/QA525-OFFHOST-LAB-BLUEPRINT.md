# QA525 — laboratorio offhost reproducible y **no desplegado**

El PR #799 define exclusivamente **un blueprint sin valores de imagen desplegables**, una herramienta de validación pura y pruebas. No instala Docker, no descarga imágenes, no crea volúmenes, no copia bases y no autoriza un primer apply de glmdch. Se origina de `dev@9069114d773cc704af6e74b1c69fee36dda2a7eb`; no modifica `main`.

## Alcance
`infrastructure/qa525/restore-lab.compose.json` describe **una única** base PostgreSQL 17 de laboratorio descartable: proyecto `qa525-restore-lab`, contenedor `qa525-restore-lab-postgres-1`, `network_mode:none`, PID/IPC privados, sin puertos, sin dispositivos host, sin bind mounts, no-privileged, no-new-privileges, capacidades sensibles recortadas. Usa solamente el volumen `qa525-restore-lab_pgdata` bajo `/var/lib/postgresql/data`. El operador `qa525-offhost-dual-restore-drill.py` PR #798 comprobará esos invariantes **físicamente** además de la configuración.

El valor `image` está deliberadamente sujeto a `${QA525_POSTGRES_IMAGE:?Set an independently verified postgres:17@sha256 digest}`. **No hay digest preseleccionado**: antes de cualquier operación física, un custodio debe seleccionar la imagen `postgres:17@sha256:<digest>`, verificar su procedencia independiente y fijar el digest. La mera forma `@sha256` NO valida firma o procedencia; el guard obliga la forma de referencia OCI y mantiene `image_signature_verified:false`.

El entorno contiene solo `POSTGRES_DB=postgres` y `POSTGRES_HOST_AUTH_METHOD=trust` para el **laboratorio sin red**. **Nunca** debe desplegarse ese método de autenticación en QA operativo, producción, hosts conectados o contenedores que compartan acceso a PostgreSQL externo. El laboratorio tiene acceso exclusivo de administrador autorizado y deberá contener únicamente datos sintéticos.

## Contratos de preflight sin ejecutar

Antes de preparar un host, en el repositorio se permite:

```bash
python3 scripts/qa/operators/qa525-restore-lab-blueprint-guard.py --check-template
```

Con imagen digest explícita y Compose disponible en una **máquina distinta de glmdch** podrá evaluarse solamente el JSON resultante y pasarlo al verificador; el siguiente ejemplo no invoca `up` ni cambia servicios:

```bash
QA525_POSTGRES_IMAGE='postgres:17@sha256:<digest-verificado>' \
  docker compose -p qa525-restore-lab -f infrastructure/qa525/restore-lab.compose.json config --format json \
  | python3 scripts/qa/operators/qa525-restore-lab-blueprint-guard.py --check-rendered
```

El identificador `<digest-verificado>` es un marcador documental: **no es una imagen real**. No ejecutar hasta tener una imagen oficial con digest válido.

La inspección negativa falla si se agregan servicios, redes, puertos, modos host, privilegios, montaje bind, secretos de QA, volúmenes externos o referencias de imagen sin digest. Los casos herméticos se ejecutan sin Docker mediante `tests/test_qa525_restore_lab_blueprint_guard.py`.

## Operación física posterior — sujeta a autorización

El custodio preparará un host/VM externa con permisos mínimos y acceso restringido, seleccionará un digest PostgreSQL 17 verificable, probará el Compose renderizado, y **solo entonces**, en un cambio operativo independiente y autorizado, creará el laboratorio descartable. Inicializará las bases vacías `qa525_verify_pmgm` y `qa525_verify_pmgm_keycloak`, así como el lock root/0600 `/var/lib/qa525-restore-lab/restore.lock`, copiará con custodia y hashes ambas `.age` más manifest a un directorio privado, y ejecutará el operador revisado del PR #798.

No hay pruebas de restauración real todavía. El éxito CI del blueprint solo demuestra coherencia de **estructura estática**; no demuestra ausencia de red física, autenticidad de imagen, respaldo offhost ni restauración exitosa. Queda bloqueado cualquier apply de API/WEB, migración, cambio de Keycloak/Master/SMTP o promoción main mientras los gates completos de #525 no estén acreditados.

Continuidad: Issue #525, Issue #97 y Estado Ejecutivo del Proyecto Centenario en Drive.
