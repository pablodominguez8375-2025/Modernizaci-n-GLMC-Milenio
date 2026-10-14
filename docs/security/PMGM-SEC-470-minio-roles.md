# PMGM-SEC-470 — Credencial documental MinIO de mínimo privilegio (piloto)

Estado: código propuesto en PR #479, Issue #470. **No equivale a despliegue/validación IAM real**; el piloto y `srv01` no se ejecutaron aquí.

## Alcance de las dos identidades

- `PMGM_PILOT_MINIO_USER` / `PMGM_PILOT_MINIO_PASSWORD`: cuenta administradora del servidor S3 privado, **solo** en `minio` y `minio-init`; no entregar a la API.
- `PMGM_PILOT_MINIO_APP_USER` / `PMGM_PILOT_MINIO_APP_PASSWORD`: cuenta de servicio exclusiva de la API documental. Debe ser distinta, con contraseña aleatoria única de 20 caracteres o más. La API recibe sólo estas credenciales.
- `minio-init` crea el bucket privado y aplica `infrastructure/minio/pmgm-documents-policy.json` al nuevo usuario mediante `mc admin policy create`, `mc admin user add` y `mc admin policy attach`. El proceso no abre puertos públicos.
- Política IAM: únicamente `s3:GetObject`, `s3:PutObject`, `s3:DeleteObject` sobre `arn:aws:s3:::pmgm-documents/*`. No `admin:*`, ni `s3:*`, ni permiso de objetos en otros buckets, ni `ListAllMyBuckets`. La lógica S3 del backend usa HEAD, PUT, GET, DELETE de claves conocidas; HEAD requiere `s3:GetObject`.

## Procedimiento al preparar el piloto

1. Copiar `infrastructure/pilot.env.example` a archivo privado `infrastructure/.env.pilot.local`. Generar dos credenciales de MinIO **diferentes**, con usuario de servicio nuevo. No publicar contraseñas ni salida de `docker compose config` en GitHub/Drive/tickets. Restringir permisos al archivo de entorno.
2. Ejecutar el preflight `PMGM_PILOT_ENV_FILE=/ruta/segura bash scripts/preflight-pilot.sh`; este verifica ambas cuentas distintas y unicidad/longitud de las contraseñas.
3. Sólo con autorización de despliegue, arrancar el piloto con `scripts/start-pilot.sh`. `minio-init` debe terminar correctamente antes del servicio API. **Si en una instancia existente ya existe el nombre del usuario de servicio con políticas más amplias, no reutilizarlo sin auditar/remover esas concesiones**; los permisos efectivos incluyen asociaciones de usuario y grupo.
4. Verificar en un ambiente de QA aislado con datos **sintéticos** la cuenta de servicio: escribir, consultar, leer y borrar un objeto bajo `pmgm-documents/`; intentar acceder a otro bucket de prueba, listar los buckets y ejecutar un comando administrativo con la cuenta restringida. Los tres últimos deben denegarse. El resultado debe registrarse sin copiar credenciales ni contenido personal.
5. Validar operaciones normales de `S3DocumentObjectStore` tras la transición, incluso el caso en que una clave todavía no existe (HEAD puede retornar 403 en algunas configuraciones, que exige revisión de comportamiento). Nunca eliminar objetos históricos al cambiar credenciales.
6. Si falla el acceso legítimo a documentos, detener el despliegue e investigar policy/identidad. La reversión debe preservar los objetos existentes; **no restituir indefinidamente credenciales root a la API ni ampliar policy a `s3:*`**. Escalar a operador para corregir permisos y relanzar con evidencia de mínimos privilegios.

## CI, límites y seguridad

`tests/test_minio_pilot_policy.py` verifica acciones/recursos exactos y que Compose/preflight/CI no use credenciales de administración en DocumentStorage; el workflow CI valida Compose y Smoke piloto con `adobe/s3mock` por rapidez. **S3Mock no ejerce MinIO IAM**, por lo que no es correcto afirmar que una prueba CI confirma denegaciones por usuario en MinIO real. Un ensayo de MinIO real o revisión de una política efectiva en QA es requisito de aceptación antes de cerrar el criterio de #470.

No se modifica el Compose de primera implementación, QA/srv01, Keycloak, reglas de Taller ni roles de PostgreSQL. Separar migraciones PostgreSQL, runtime de aplicación y Keycloak en distintas identidades queda para la siguiente entrega y necesita planificación para evitar pérdida de acceso.

Referencias técnicas: documentación de `mc admin user add`, `mc admin policy create` y `mc admin policy attach --user`; verificar comandos contra la versión `mc` fijada durante el despliegue. No almacenar contraseñas reales en el repositorio.
