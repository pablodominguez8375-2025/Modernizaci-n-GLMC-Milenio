# PMGM-SEC-470 — prueba IAM S3 real con Silo corregido (15-10-2026)

**Estado: prueba automática experimental en CI, sin ejecución en servidores institucionales y sin autorización operativa.**

## Alcance y suministro

La versión Silo `RELEASE.2026-10-15T00-00-00Z` corrige SN-2026-015/016 según fuentes primarias del fabricante: https://silo.pigsty.io/blog/release/silo-20261015/ y https://github.com/pgsty/silo/releases/tag/RELEASE.2026-10-15T00-00-00Z . Imagen OCI clásica multiarch publicada: `docker.io/pgsty/silo@sha256:aaac6bce13eb99316b3c88bd20143fc3c38ab607585256a411a095c44ee57f62`. **No usar** release 20260916 ni `:latest`. El test falla si la imagen no está disponible; no sustituye una auditoría independiente de la firma y SBOM ni la revisión de licencia AGPL-3.0-or-later.

## Contrato de prueba

`tests/test_silo_real_iam.py` integra un test en la suite de infraestructura CI existente mediante el import desde `tests/test_pilot_keycloak_db_isolation.py`, sin editar el workflow ni los archivos reservados a PR #479. Durante CI, en un Docker network efímero, se realiza:

1. `docker pull` de la imagen exacta por digest, creación de servidor Silo sin puertos publicados y sin volumen persistente; root y usuario documental con secretos aleatorios **generados solo en la ejecución**.
2. Crear buckets `pmgm-documents` y `pmgm-iam-forbidden`; enviar al contenedor una política de dos bloques sintética que replica el alcance requerido en PR #479 (lista solo del bucket documental; Get/Put/Delete objetos dentro de ese bucket). Admin solo para provisioning.
3. Acceder por API HTTP S3 real desde la red privada con **firmas AWS SigV4** calculadas en Python estándar, sin boto3 ni claves de empresa: PUT/HEAD/GET/DELETE permitidos, contenido idéntico; HEAD inexistente HTTP 404; listado del bucket permitido usando mcli.
4. Demostrar rechazo **HTTP 403** a PUT/GET/DELETE en bucket distinto, denegación a listado de buckets global y comandos `mcli admin info` con la cuenta documental. El bucket ajeno debe existir y ser listado por root para garantizar que las negativas no sean falso positivo.
5. Eliminar contenedor y network en `finally`, sin publicar secretos, stdout/stderr ni la inspección Docker. El runner Github Actions ya dispone de Docker para pruebas PostgreSQL reales. Si no descarga o no se comporta correctamente, **CI rojo y PR en borrador**; nunca convertir errores en saltos silenciosos.

**Límites:** la prueba IAM no ejercita HTTPS/TLS externo, endpoints de la aplicación ASP.NET, copias persistentes, ClamAV, OIDC ni retención Object Lock bajo un cluster de producción. Tampoco confirma que cualquier infraestructura futura tenga esos permisos: las credenciales y la política efectivos se deben verificar con una instancia QA autorizada. No usar datos personales ni copiar credenciales/volúmenes operacionales. No cambiar el servidor actual ni cerrar Issue #470 o PR #479 por este test aislado.

## Puerta a futuras decisiones

Exigir triple gate exact-head de este PR (CI, Showcase, QA) con prueba Docker real en **Infraestructura configuration** y constancia de resultados. Luego —solo después de decisión del Sponsor respecto del servidor S3 y revisión legal/seguridad— actualizar PR #479 sobre dev vivo, adaptar provisioning/SDK a Silo si procede, hacer backup/restore y repetir en QA autorizado. `main` y `srv01` permanecen intocables.
