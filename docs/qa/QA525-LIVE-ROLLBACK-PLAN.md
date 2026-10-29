# QA525 — contrato de plan reversible contra la topología física real

**Estado:** PR #796. Código/CI puramente local y sintético: no conecta a `glmdch`, no ejecuta Docker, backup, restauración ni despliegue.

## Evidencia física obtenida 29-10-2026

El operador en el hilo «Montaje QA operativo» aportó salida de dos consultas Docker read-only; evidencia registrada en [Issue #97, comentario 6495299122](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/97#issuecomment-6495299122) y Estado Ejecutivo de Drive.

- 7 servicios activos: API, WEB, Keycloak, PostgreSQL, MinIO, gateway y ClamAV; 3 tareas `db-grants`, `migrator` y `minio-init` `Exited(0)`.
- Imagen antigua API (image ID **físico**, no git commit): `sha256:871cf3568f45221e2b609909d8fc39ce7847d51cf5198e49d3799a2e463357d6`.
- Imagen antigua WEB: `sha256:52c2615dad07790dabf17b3b7f7d196f1cac32f0f800614e70ff2d6698a55299`.
- API con 3 capas: `compose.recovery.json`, `api-6ff4ae0d092d4214df4ea31af484efbc969fde54.json`, `qa754-identity-overlay.stage.json`.
- WEB con 2 capas: `compose.recovery.json`, `web-52c2615dad07790dabf17b3b7f7d196f1cac32f0f800614e70ff2d6698a55299.json`.
- Los IDs de imagen y las etiquetas son la línea base observada **en ese instante**. Las capas fueron reportadas con rutas omitidas; falta ejecutar el inventario #795 físico bajo fuente root verificada para registrar hashes de contenido actuales. No inferir SHA Git desde un nombre de capa.

## Qué valida el nuevo contrato

`scripts/qa/operators/qa525-live-rollback-plan.py` recibe **solo** JSON sintético o previamente sanitizado con `inventory` (forma exacta del inventario #795), `prepared` (recibo #790) y `release` (gate exact-SHA). Verifica que:
1. Hay exactamente 7 servicios diferentes activos con IDs completos (no confunde los 3 one-shots como servicios en ejecución).
2. El Compose físico tiene una base compartida y exactamente 3 capas API y 2 capas WEB, con *cuatro* archivos distintos y sha256 completo; se conserva la capa `qa754` de identidad y el override WEB corresponde al image ID anterior.
3. Los siete contenedores y sus IDs de imagen son idénticos a los del recibo de preparación. Cualquier cambio entre ambas fases bloquea.
4. Las dos nuevas imágenes API/WEB tienen IDs OCI válidos, corresponden a un mismo source SHA y asset de la release firmada, y no son imágenes ya activas.
5. El resultado informa IDs de imágenes rollback API+WEB e hashes Compose, y enumera explícitamente Keycloak, PostgreSQL, MinIO, gateway, ClamAV, más los 3 one-shots que no deben relanzarse ni tocarse.

**No genera ni ejecuta comandos.** Aunque el JSON supere todas las condiciones, produce `ROLLBACK_PLAN_STRUCTURALLY_COHERENT_HOST_ATTESTATION_REQUIRED` con `may_apply:false`, `host_runtime_rechecked_by_this_tool:false`, backups/restauración/Sigstore/migraciones/aprobación `false`. **JSON no acredita host ni permisos, ni sustituye un ensayo físico de rollback**. La imagen física observada ahora NO se fija para siempre en el código: debe releerse bajo lock al momento del eventual despliegue.

Ejemplo offline:

```bash
python3 scripts/qa/operators/qa525-live-rollback-plan.py < recibo-sintetico.json
```

El JSON no puede contener secretos; límite de 64 KiB y rechazo sin reproducir su contenido. Los tests CI usan exclusivamente IDs ficticios, no Docker real.

## Dependencias antes de activar la actualización
- Revisión supervisada de cambios sensibles de `Program.cs`, `UserAccounts`, Keycloak/SMTP, migraciones/EF y diferencias de Compose.
- Release pública firmada del **HEAD vigente**, verificada con Sigstore real en `glmdch`, y preparación de ambas imágenes bajo locks.
- Respaldo **fresco y cifrado fuera de host** de `pmgm` y `pmgm_keycloak`, con restauración real ensayada en un entorno aislado.
- Operador físico API+WEB que conserva imágenes anteriores, configura overrides solo bajo autorización y revierte ambas al fallar, con verificación de servicios y salud. No reaprovechar aplicadores de #771/#737/#766 con SHA histórico, ni respaldos del stack `pmgm-srv01`.
- Pruebas autenticadas de Master/qa.admin, ciclo de vida de cuentas y permisos de Biblioteca; producción y `main` intactas.

Mantener el hilo «Montaje QA operativo» para evidencia física y el hilo de desarrollo para implementación. Registrar avances en Issue #525/#97 y Estado Ejecutivo Drive. 
