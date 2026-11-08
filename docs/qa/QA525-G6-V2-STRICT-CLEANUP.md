# QA525 / Issue #956 — Limpieza fail-closed G6 v2 (INACTIVO)

## Riesgo confirmado (dev@7de8d724419b6c364d2781c524d62ae898a02d51)

El productor `qa525-vnext-g6-v2-producer.py` descarta errores en `shutil.rmtree(..., ignore_errors=True)`. En RESTORE escribe el reporte de éxito ANTES de eliminar los bytes descifrados de `restores-v2`. Con I/O fallida puede quedar plaintext persistente y un reporte de éxito incluso si el verificador de PostgreSQL terminó correctamente.

## Cambio mínimo exigido

1. `_remove_stage` borra con `shutil.rmtree` sin ignorar errores, exige ausencia comprobada con `lstat`, STOP de código fijo `G6P_CLEANUP_FAILED` ante error/ambigüedad. El resto del contrato permanece.
2. `restore` limpia su working directory creado antes de persistir `isolated-restore-v2-*.report.json` o devolver PASS. La limpieza es condición de éxito; reportes de fallo no deben simular GO.
3. Tests con sabotaje del borrado (PermissionError y no-op) para BACKUP y RESTORE, que exijan STOP y ausencia de report de éxito, sin modificar servicios.

## Implementación documentada

- `_remove_stage(path)` ahora exige borrado exitoso de la etapa y confirma ausencia por `os.lstat` (FileNotFoundError); si rmtree falla o retorna sin borrar, STOP `G6P_CLEANUP_FAILED`.
- `restore` primero cierra y elimina su área temporal; **solo después** crea el informe de restauración exitosa.
- Nuevas pruebas con `unittest.mock` cubren PermissionError y borrado falsamente exitoso en backup y restore, y verifican orden de reporte respecto de limpieza.
- **Gate CI real**: el workflow QA Silo ejecuta solo `python3 -m unittest discover -s tests -p test_qa_silo_supply.py -v`; `tests/test_qa_silo_supply.py::load_tests` ahora importa las cinco regresiones `G6V2ProducerStrictCleanupTests` y las obliga a ejecutarse en ese control. No se altera ningún workflow compartido caliente.
- Rama del PR #957 reconciliada sin conflicto con dev@da99bb25a8242691fbdeb24061614c540dc3a8e7, que incluye PR #955. CI exact HEAD y review de Claude son gates pendientes, no aprobados anticipadamente. El PR modifica **4 archivos**: productor G6, sus tests, el puente QA Silo de pruebas y este documento.

## Alcance y límites

Solo biblioteca INACTIVA; **no** ejecuta QA real, Restic físico, MinIO, Docker, servidor, GitHub main, claves ni producción. No añade autorización ni GO. Verificador desechable PR #955 independiente; coordinación con Claude vía Issue #956 y PR #957. Gates GitHub 7/7 exact-head y revisión externa antes de merge solo dev. Este documento actúa como plan y handoff en el PR.

