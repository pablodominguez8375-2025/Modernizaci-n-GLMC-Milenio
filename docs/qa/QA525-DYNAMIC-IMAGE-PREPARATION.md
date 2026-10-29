# QA525 — preparación dinámica de imágenes API+WEB, sin despliegue

**Estado:** código candidato en PR #790, sin instalación física, `main` intacta.

El antiguo preparador del PR #771 fija `TARGET` a SHAs históricos. Este nuevo operador toma el SHA del gate firmado integrado (#783/#785/#787) **durante cada ejecución**, nunca de un parámetro arbitrario de cliente o de una constante histórica. Se ejecuta solo en `glmdch` y como root a petición expresa `--prepare`. El operador depende de root ownership de cada archivo Python transitivo antes de importar código, de la cadena preflight de solo lectura, del cambio de política y de staging real con firma verificada. Mantiene exactamente los locks de las operaciones QA previas. Para compatibilidad con Ubuntu, `/run/lock` se admite solo como directorio real propietario root con modo 0700, 0755 o 1777 (sticky), validando siempre el archivo individual root/0600, `O_NOFOLLOW` y flock exclusivo. Ningún otro directorio escribible por terceros está permitido.

Proceso propuesto:

1. Comprobar directorio de estado root/0700 y locks, más los siete contenedores actuales (API, WEB, Keycloak, PostgreSQL, MinIO, gateway y ClamAV).
2. Hacer preflight físico y revisión de cambios: `Program.cs`, identidad, permisos, modelos/migraciones, Docker/Compose o configuración sensible producen `BLOCKED_REVIEW_REQUIRED` y no se construye nada.
3. Preparar mediante `qa525-current-host-staging.py` el ZIP de release exacto, su digest y firma Sigstore real. Corroborar nuevamente manifiesto y SHA actual del source/host.
4. Construir imagen API con etiqueta OCI del SHA, verificar ID y etiqueta, construir WEB con el helper conservado en `dev` y mantener ambos identificadores.
5. Repetir gate/manifest y snapshot físico; si algún servicio cambió, RECHAZAR. Escribir recibo privado `prepared.json` 0600 únicamente tras éxito; sin imprimir secretos.

**Limites:** prepara imágenes y recibe código firmado; por diseño no llama a `docker compose up`, no construye migraciones, no crea respaldos, no modifica cuentas, master, Keycloak ni SMTP, no ejecuta `apply` y devuelve `may_apply:false`. Las imágenes y el recibo no son UAT. El staging firmado se elimina al salir; el futuro aplicador deberá revalidar por su cuenta paquete y firma y crear backup fresco antes de cambiar servicios.

La ruta `/opt/centenario/qa525-signed-prepare` necesita instalación administrativa verificada previa con root/0700. **NO ejecutar este código en glmdch solo por estar fusionado a dev**. El primer uso depende del hilo QA operativo, revisión de identidad y versión efectiva, instalación de archivos root, restauración independiente y plan de contingencia. El corte v0.183 y posteriores con cambios a `Program.cs` y `UserAccounts` sigue BLOQUEADO para preparación automática sin revisión separada; el operador nuevo no lo elude.

Pruebas offline: `tests/test_qa525_current_image_preparation.py`. Probar fail-closed ante host incorrecto, firma/manifiesto inválido, SHA cambiante, imágenes erróneas, contexto ausente, Web receipt discordante y contenedor detenido. CI `qa525-dynamic-image-preparation.yml` sin acceso Docker ni secretos. El PR #789 describe el futuro contrato de rollback; el PR #771 no se debe fusionar en bloque.

**Próxima fase:** operador dinámico de aplicación REVERSIBLE, acompañado de verificación física de ambas bases y custodia externa. El traspaso al hilo QA operativo ocurre solo después de preparar estos componentes y el runbook de intervención supervisada.
