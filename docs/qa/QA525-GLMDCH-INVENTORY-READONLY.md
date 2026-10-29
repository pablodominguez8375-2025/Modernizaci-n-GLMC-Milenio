# QA525 — inventario físico **solo de lectura** antes de preparar QA

**Estado:** PR #795 de desarrollo. Este script no ha sido ejecutado en `glmdch` y no instala nada. Su objetivo es desbloquear el diseño del aplicador físico sin confiar en el SHA histórico `6ff4...` ni inventar el Compose vigente.

## Por qué existe
Los gates de publicación y los preflights que dependen de la política de cambios están correctamente bloqueados por modificaciones sensibles en `Program.cs` y `Modules/UserAccounts`. Sin embargo, necesitamos conocer **qué imágenes, contenedores y capas Compose corren realmente** para revisar el riesgo y construir un rollback válido. Un inventario de solo lectura no debe requerir aprobar antes una versión que todavía no hemos examinado.

El script `scripts/qa/operators/qa525-host-inventory-readonly.py` reutiliza el adaptador `qa-lite-compose-adapter.py` de GitHub, después de validar los dos archivos Python y TODOS sus ancestros como root-owned sin symlinks y sin escritor de grupo/otros. Requiere `root`, hostname `glmdch` y `--inventory`. Solo consulta Docker `ps`/`inspect` y calcula SHA-256 de los archivos Compose ya instalados. No abre ni muestra `Config.Env`, passwords, mounts, redes, puertos, secretos ni logs; sí expone identificadores no secretos de imágenes/contenedores y rutas/hashes de configuración para comparar. Falla en cerrado si faltan servicios, si alguno no está corriendo o si hay discrepancia de directorio Compose. Los siete servicios son API, WEB, Keycloak, PostgreSQL, MinIO, gateway y ClamAV.

## Ejecución futura, exclusivamente en hilo «Montaje QA operativo»
No instalar ni ejecutar solo porque el PR esté en `dev`. Primero acreditar canal administrativo, fijar SHA del código revisado y su checksum, copiar como root con permisos seguros y comprobar que el host y directorios corresponden. En ese contexto, el único comando del componente es:

```bash
python3 /ruta-root-verificada/scripts/qa/operators/qa525-host-inventory-readonly.py --inventory
```

La ruta es ilustrativa, NO una ruta física acreditada. El comando debe ejecutarse desde una copia instalada cuyo árbol sea controlado por root y contenga el adaptador en `scripts/qa/qa-lite-compose-adapter.py`. Salida: `PHYSICAL_METADATA_CAPTURED_NO_CHANGE_AUTHORITY` con los siete IDs de contenedor e imagen, workdir Compose y SHA-256 de archivos de capas API/WEB. Nunca imprime configuraciones de entorno.

**No acredita** identidad Master/qa.admin, firma Sigstore, contenido de secretos, migraciones, respaldo, integridad de datos, Silo, permisos ni aptitud UAT; la salida contiene `may_prepare:false` y `may_apply:false`. Publicar en registros compartidos solo el mínimo de información técnica permitida, nunca el JSON raw sin revisión.

## Dependencias para actualización supervisada
Este informe físico debe revisarse junto con la lista analítica de cambios sensibles del PR #794. El primer aplicador de API+WEB solo puede habilitarse después de una revisión explícita de identidad/Program.cs y migraciones, backup fresco cifrado fuera de glmdch para pmgm y pmgm_keycloak, restore drill independiente, imágenes rollback retenidas, gates SHA/firma, y pruebas autenticadas. No reutilizar `backup-srv01-qa.sh` del otro stack ni el `apply` de PR #771.

CI `qa525-host-inventory-readonly.yml`: pruebas herméticas de salida redactada, drift de siete servicios, imagen, Compose, trusted import y rechazo de `--apply`. No requiere Docker físico.
