# QA glmdch — canal de descarga read-only de artefactos completos

Fecha: 17-10-2026, fase de ingeniería sin despliegue. Issue #525.

## Motivo

El usuario tiene acceso desde Android a glmdch (`soporte` con `sudo`), pero `gh` en esa VM no posee autenticación. Las descargas ZIP de **GitHub Actions artifacts** desde su API suelen exigir autenticación; no almacenar un PAT de escritura del usuario ni exponer secretos en chat o repositorio.

El script `scripts/qa/qa-artifact-download.py` requiere:
- Selección JSON emitida por `qa-agent-readiness.py` tras validar mismo SHA de dev y gates.
- Token GitHub de **solo lectura** limitado exactamente al repositorio y permiso **Actions: Read-only** (metadatos implícitos), almacenado únicamente local, archivo regular dueño del proceso y `0600`. No crear ni pedir tokens hasta que el flujo completo esté revisado.
- Ruta de salida nueva en directorio privado. Descarga un artefacto por ID desde REST GitHub API, deniega redirección automática con `Authorization`, valida el destino CDN HTTPS y sigue **sin token** el redirect. Calcula SHA-256 en streaming y rechaza tamaño excesivo/identidad incorrecta. Archivo temporal privado `0600`, limpieza tras errores. No interpreta ZIP ni instala nada.

**Resultado** `DOWNLOADED_UNVERIFIED_NOT_INSTALLED`: bytes descargados con checksum externo igual al digest informado por GitHub; **no implica validación de firma**. Siguiente fase (PR #527) aplica `qa-signed-staging.py` para verificar atestación Sigstore, `MANIFEST.sha256`, versión y evidencia Silo antes de preparar archivos. El preflight/runtime real y recuperación siguen pendientes.

## Reglas de habilitación

1. Antes de copiar token alguno al host, revisar alcance `Actions: Read-only`, expiración/rotación, política de redirecciones CDN y pruebas de falla/revocación. Nunca usar scopes `repo` ni tokens permanentes con escritura.
2. Mantener credenciales en directorio privado de soporte o root bajo propietario del servicio. No guardarlas en variables de entorno persistentes ni revelar el token mediante terminal/chat/Drive/logs.
3. No activar una unidad systemd hasta ensayar selección por SHA exacto, descarga, firma, rollback y primer despliegue supervisado. No invocar instalador desde este script.
4. GitHub Actions en el repositorio público permanece hospedado por GitHub; jamás exponer un runner genérico en glmdch ni SSH root.
5. Mantener `main`, `srv01` y datos institucionales fuera de alcance; QA solo sintético.

Seguir registrando resultados y bloqueos en GitHub Issues #97 y #525 y Línea Base Drive.
