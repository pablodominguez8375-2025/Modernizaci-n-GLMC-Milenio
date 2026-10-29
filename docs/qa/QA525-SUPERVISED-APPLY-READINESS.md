# QA525 — comprobación supervisada de preparación, NO aplica servicios

**Estado:** PR #791, candidato de desarrollo desde `dev@bb549ee4ae5a5120d9d040b0db0b0545134f695d`. Complementa #787, #788, #789 y #790 sin modificar sus guardias de seguridad.

## Qué valida

- Host esperado `glmdch` y UID 0; fuentes Python y cada directorio ancestro root-owned, sin symlinks ni escritor de grupo/otros, **antes de importar** cualquiera de esos módulos.
- Existencia del recibo `/opt/centenario/qa525-signed-prepare/prepared.json` solo root/0600, protegido con `O_NOFOLLOW`, máximo 64KiB, directorio root/0700.
- Repetición de preflight físico de solo lectura y validación de HEAD+gates+release pública exacta; si cambia Program.cs, Keycloak, cuentas, migraciones, Docker o Compose bloquea `SENSITIVE_CHANGES_REQUIRE_REVIEW`.
- Concordancia entre `source_sha` vigente, digest de asset y los recibos producidos por #790; vigencia e identidad OCI reales de **ambas** imágenes.
- Snapshot de API, WEB, Keycloak, PostgreSQL, MinIO, gateway y ClamAV **antes y después** de las lecturas; si cualquier contenedor o imagen cambió o el recibo no coincide, bloquea.

La única salida positiva posible es `PREPARED_IMAGES_MATCH_LIVE_QA_BACKUP_AND_ROLLBACK_PENDING`, con `may_apply:false`, `signature_reverified_by_this_probe:false`, `backup_offhost_verified_by_this_probe:false` y `rollback_drill_verified_by_this_probe:false`. Esta comprobación NO vuelve a descargar el ZIP ni verifica su firma Sigstore; el operador físico futuro deberá hacerlo de nuevo.

## Uso administrativo futuro, no ejecutar todavía

```bash
python3 /opt/centenario/qa-tools/qa525-current-apply-readiness.py --inspect
```

Instalación de código root y lectura física requieren canal administrativo verificado y gobernanza #97. GitHub CI con pruebas sintéticas NO acredita ese host. No copiar comandos a glmdch por un merge o comentario.

## Bloqueos todavía abiertos

El aplicador físico del PR #771 contiene `TARGET` histórico y NO SE DEBE USAR. El nuevo aplicador debe preservar claves Master/SMTP/Keycloak, garantizar que no migra ni destruye esquema, retener imágenes anteriores, generar y verificar respaldos FRESCOS de `pmgm` y `pmgm_keycloak` con custodia fuera del host y restauración independiente, comprobar doble preflight, firma original, rollback conjunto, smoke autenticado y UAT. El corte actual desde API histórico implica cambios sensibles de identidad/Program.cs y no es apto para desatendido.

En el hilo «Montaje QA operativo» se ejecutará el primer preflight, respaldo y despliegue **supervisado** después de que el aplicador final y runbook hayan aprobado las pruebas; `main` sin promoción. GitHub #525/#97 y bitácora ejecutiva Drive son las fuentes de continuidad.

## Pruebas

`tests/test_qa525_current_apply_readiness.py` contiene casos herméticos positivos de **estructura únicamente** y rechazos de drift, SHA, digest, fuente, imagen OCI, servicio faltante, flags que intenten autorizar apply, cambios de identidad, errores sensibles y rutas de imports. El workflow `qa525-supervised-apply-readiness.yml` no tiene Docker ni credenciales, ni escribe en QA.
