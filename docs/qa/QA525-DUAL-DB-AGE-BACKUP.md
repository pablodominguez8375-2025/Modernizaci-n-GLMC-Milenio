# QA525 — respaldo PostgreSQL doble cifrado en origen, **sin aplicar** QA

**Estado:** PR #797, fuente desde `dev@9d55f06dfe968cddfc8ed0420463ba0b0c6b478a`. Este operador está desarrollado y probado con dobles; **no está instalado ni ha sido ejecutado en el servidor `glmdch`**. No implica aprobación para una intervención física.

## Alcance y seguridad

El ejecutor `scripts/qa/operators/qa525-dual-db-age-backup.py` recibe una única opción `--backup`; solo para root en `glmdch`, bajo los locks previstos por QA #525/#604/#754. Captura dos bases existentes del contenedor `centenario-qa-lab2-postgres-1`, en orden fijo, usando `docker exec --user postgres ... pg_dump --format=custom --no-owner --no-acl --dbname pmgm` y `pmgm_keycloak`. El stdout binario del dump se transmite **directamente** a `age -R` para cifrado con llave pública del custodio externo; jamás se escribe un dump en claro al disco del host ni se solicita/expone contraseña PostgreSQL.

Se verifica que el contenedor sea parte del proyecto exacto mediante etiquetas Docker y que los **7 servicios estén corriendo**, con **3 one-shots en Exited(0)**. Un snapshot de ID/imagen/estado de esos diez contenedores debe coincidir antes y después del dump; el operador no detiene servicios, no recrea contenedores ni ejecuta migraciones.

Requisitos físicos bloqueantes al primer uso manual:
- `/usr/bin/docker` y `/usr/bin/age` deben existir y ser root-owned y no escribibles por terceros, con rutas ancestro confiables.
- `/etc/pmgm/qa525-offhost-age-recipient.txt` debe contener **una sola** clave pública age de custodio externo, archivo root/0600 sin symlinks ni hardlinks. **No se genera ni almacena la clave privada en glmdch.**
- `/opt/centenario/qa525-dual-db-backups` debe existir de antemano y pertenecer a root/0700, sin symlinks en el árbol. No solicitar al operador que cree la carpeta sin revisión de seguridad del path.
- Más de 2 GiB libres; 5 locks QA libres; acceso `postgres` interno a ambas bases (no credenciales interactivas). El script falla en cerrado si falta cualquiera de estas condiciones.

Genera un directorio nuevo `backup-<fechaUTC>-<token>` con `pmgm.pgdump.age`, `pmgm_keycloak.pgdump.age` y `manifest.json`, todos root/0600 bajo directorio root/0700. Incluye tamaño y SHA-256 de cada ciphertext, y SHA-256 de la clave pública; registra IDs de contenedores, no contenidos ni secretos. Si falla cualquiera de las dos exportaciones, no publica un respaldo parcial y limpia el directorio temporal. El script no elimina respaldos anteriores ni accede a Keycloak, usuarios, SMTP o credenciales.

## Límites decisivos

**Un éxito de este operador significa únicamente DOS DUMPS CIFRADOS LOCALES.** Ni el cifrado en origen ni un hash de archivo certifican la restauración, la integridad lógica ni la custodia externa. No debe usarse como evidencia para la autorización de actualización hasta que el operador externo:

1. Copie ambos archivos `.age` y manifest de forma segura a almacenamiento **fuera de glmdch**, confirme checksums y custodia con control de acceso/cifrado de transporte.
2. Descifre ambas copias fuera del servidor con la clave privada custodiada de forma independiente, valide `pg_restore --list` sobre los dos archivos descifrados y, especialmente, ejecute una **restauración real de ambas bases en un PostgreSQL aislado y temporal**, comparando esquema y permisos sin datos reales expuestos.
3. Aporte evidencia auditable de restore drill y vigencia (tiempos, checksums, IDs de contenedores, resultado). **No incluir claves privadas, contraseñas ni datos personales en GitHub/Drive.**

El recibo local y la salida nunca reclaman `offhost_custody_verified:true`, `restoration_drill_verified:true` ni `may_apply:true`. El operador tampoco verifica las banderas de migración API ni crea/autoriza el aplicador. La operación puede ser costosa en I/O: ejecutar únicamente en ventana QA aprobada. Debe supervisarse su uso, no programarlo automáticamente.

## Pendientes para el primero despliegue

Consolidar revisión sensible de Program.cs, UserAccounts/Keycloak/SMTP y EF; release exact-SHA firmada y revalidada en glmdch; imágenes API/WEB preparadas, capa identidad preservada; offhost/restore probados; apply y rollback dual con UAT autenticada. El PR #771, #766 y #737 son operadores de SHA fijo y no deben reutilizarse en bloque. Los scripts históricos backup-srv01 no corresponden al stack físico actual.

**Pruebas CI** `tests/test_qa525_dual_db_age_backup.py`: 17 casos herméticos, procesos falsos Docker/age, rechazo de fallo en cualquiera de ambos, de recipient, estado de contenedores, permiso de locks y CLI mutativa; nada contacta glmdch. Ningún merge a `main`. La ejecución física será únicamente en hilo «Montaje QA operativo» después de revisar y autorizar expresamente su instalación.
