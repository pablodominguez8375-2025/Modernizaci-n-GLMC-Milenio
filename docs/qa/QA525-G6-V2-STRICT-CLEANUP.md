# QA525 / Issue #956 — Limpieza fail-closed G6 v2 (INACTIVO)

## Riesgo confirmado (dev@7de8d724419b6c364d2781c524d62ae898a02d51)

El productor `qa525-vnext-g6-v2-producer.py` descarta errores en `shutil.rmtree(..., ignore_errors=True)`. En RESTORE escribe el reporte de éxito ANTES de eliminar los bytes descifrados de `restores-v2`. Con I/O fallida puede quedar plaintext persistente y un reporte de éxito incluso si el verificador de PostgreSQL terminó correctamente.

## Cambio mínimo exigido

1. `_remove_stage` borra con `shutil.rmtree` sin ignorar errores, exige ausencia comprobada con `lstat`, STOP de código fijo `G6P_CLEANUP_FAILED` ante error/ambigüedad. El resto del contrato permanece.
2. `restore` limpia su working directory creado antes de persistir `isolated-restore-v2-*.report.json` o devolver PASS. La limpieza es condición de éxito; reportes de fallo no deben simular GO.
3. Tests con sabotaje del borrado (PermissionError y no-op) para BACKUP y RESTORE, que exijan STOP y ausencia de report de éxito, sin modificar servicios.

## Alcance y límites

Solo biblioteca INACTIVA; **no** ejecuta QA real, Restic físico, MinIO, Docker, servidor, GitHub main, claves ni producción. No añade autorización ni GO. Verificador desechable PR #955 independiente; coordinación con Claude vía PR #956. Gates GitHub 7/7 exact-head y revisión externa antes de merge solo dev. Este documento actúa como plan y handoff en el PR.

