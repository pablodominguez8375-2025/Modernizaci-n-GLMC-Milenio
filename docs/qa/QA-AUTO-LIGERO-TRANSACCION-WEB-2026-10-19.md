# QA #525 — Transacción reversible de aplicación WEB, etapa controlada

Fecha: 19-10-2026. Destino EXCLUSIVO previsto: glmdch, Compose centenario-qa-lab2, datos sintéticos. Sponsor aprobó QA-AUTO-LIGERO y mantener QA con el último corte validado de dev; main/srv01/prod excluidos.

## Evidencia nueva verificada en GitHub

Corte dev observado al comienzo de esta PR: d8f73fe74dd30a459218c939a6e690b5e8df579d. Los siete flujos de push del mismo SHA concluyeron SUCCESS, incluido `QA Silo supply and recovery`, run 39313106966. Artefacto `qa-glmdch-signed-storage-d8f73fe74dd30a459218c939a6e690b5e8df579d`, ID 12217232570, SHA256 de metadatos sha256:0f030b3224d324be5d62241a51e3e301bde5efc3fc408e3fd8ad32276e725b40, no caducado al momento de consulta. Un artefacto registrado en GitHub NO significa firma validada en host ni imagen web instalada.

Estado físico previo: web y API creadas 18-10-2026 sin label `org.opencontainers.image.revision`, y no podemos atribuirles SHA instalado. Dos BD pmgm y pmgm_keycloak y objetos Silo viven en volúmenes persistentes. Respaldo G6 cifrado 19-oct local/restic PASS y offhost Drive HASH MATCH, ID 1Sl_qrcJDZIHEMByngwAUrCrewguWVhT1, SHA256 bed4ee196074a1919234da44c6bbd66c8a7cd16bdce70fba97ed1e34df8f9cae. No reponer ese respaldo directamente sin considerar cambios posteriores.

## Entregable de esta PR — sin capacidad de modificar host

- `scripts/qa/qa-lite-web-transaction.py`: coordinador puro para WEB únicamente. Rechaza otro host, proyecto, servicios API/BD/Silo, migraciones, cambios de red, baseline sin SHA, candidato sin recibos verificados, respaldo externo ausente/viejo y rollback no fijado.
- `tests/test_qa_lite_web_transaction.py`: simulador de adaptador con pruebas adversariales: cambios parciales, health mal, dependencias alteradas, rollback fallido, primer despliegue sin baseline, divergencia, falta de backup.
- `.github/workflows/qa-lite-web-transaction.yml`: pruebas offline independientes en pull request y push; sin token de escritura, Docker, SSH, privilegios ni conexión a glmdch.

`make_plan` recibe resultados NORMALIZADOS de verificadores independientes, no valida firmas por sí mismo. `coordinate` solo recibe adaptadores inyectados; NO existe un adaptador con acceso Docker en este incremento. `WEB_HEALTHY_PENDING_AUTHENTICATED_QA` nunca equivale a aceptación funcional de Usuarios/Auditoría.

## Trabajo inmediato para completar despliegue automático

1. Diseñar/implementar adaptador HOST restringido con Compose REAL de lab2. Antes, verificar mediante diagnóstico read-only `com.docker.compose.project.config_files` del contenedor web: NO utilizar `scripts/install-srv01-qa.sh` para actualizar el lab2 existente, pues ese script arranca y prepara dependencias, migraciones y bootstrap. Nunca imprimir variables de entorno, tokens ni el contenido de config.
2. Primer corte supervisado: verificar artefacto GitHub por Sigstore y manifest en glmdch, contrastar la fuente física e imagen web resultante y establecer baseline confiable (la imagen actual no lleva revisión). Crear imagen OCI web con SHA de origen verificable y configuración OIDC actual, sin tocar servicios de BD/Silo/Keycloak. Adaptador debe permitir Compose web --no-deps --no-build con imagen previa fijada para rollback; comprobar salud, sin cambiar puertos, red, volúmenes, API ni migraciones.
3. Después de aprobar ensayo de rollback en stack sintético y bootstrap único, implementar timer pull-only, estado del SHA instalado + hashes de imágenes, logs saneados en GitHub/Drive y modo pausa. GitHub Actions NO recibe acceso SSH/root/Docker directo.
4. UAT de qa.admin en Android: Sistema > Usuarios y > Auditoría de accesos deben permanecer en pantalla, y mostrar error de permiso claro, no salto a Inicio; `Perfiles y accesos` sigue pendiente de diseñador/CRUD de vistas y Buscar según #523.

**Seguridad:** no implementar ni activar despliegue usando solo un JSON con booleanos "firma válida". Obtener evidencia criptográfica independientemente y revalidar en host. Mantener datos sintéticos, backups externos e identidad qa.admin. Nunca modificar main, srv01, producción ni secretos.
