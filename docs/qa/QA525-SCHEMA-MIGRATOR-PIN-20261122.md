# QA525 — pin OCI migrador y condición PRE-4 (22-11-2026)

Estado: **NO_GO físico**. Este documento registra el digest que debe fijar el Sponsor en la configuración **autenticada externa** del corte #1073; no es una autorización ni un archivo de configuración activo.

- Base de código del migrador atestada en dev: `16262d475a49517dbbc37fd7598962e07c5c21f1`.
- OCI **image ID / config digest** obligatorio para `schema_cut.migrator_image_digest` y plan firmado: `sha256:9330ae6095c5fc867d4bae09e8e019790b73e10e355403cfe933e0d4331a8296`.
- Digest de manifiesto OCI, **no intercambiable con image ID**: `sha256:a3d78cc8d4c021695d6b5b637ce1bb38b1a21ef4e8afeba68181e791764c76b3`.
- Atestación OCI: workflow run 45039211109; reproducibilidad informada por Claude. No asumir que el artefacto está cargado en glmdch.
- `schema_cut` no admite valores predeterminados del repositorio: `root-launch-config.py` deja `CONFIG=None`. El valor final debe llegar por el PRE-4 autenticado y coincidir con el plan canónico, el archivo atestado y el `docker image inspect` del host.
- Si el pin difiere, el launcher/plan y preflight deben detenerse antes de DDL. Si no hay artefacto firmado exacto o la rama diverge de dev, no levantar el GO.
- PRE-4 debe verificar fuente root 0600 y /run tmpfs real antes de gastar nonce; env-files temporales se crean únicamente en la fase schema, dentro del plano retenido, y se destruyen al salir.
- `InitialLoad:ApplyEnabled` permanece OFF en este corte. Activación para datos reales requiere autorización específica posterior y cierre del flag; el preview sintético no habilita apply.

**No** promover main, fusionar PR ni ejecutar nada físicamente en glmdch sin autorización. Registro coordinado en PR #1073, issue #525 y Drive.
