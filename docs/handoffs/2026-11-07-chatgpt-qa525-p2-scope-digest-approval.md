# QA525 P2 — alcance exclusivo pruebas, sin host — 2026-11-07

Base dev@65c0dd4cb82c62d530547229819b9913a28948ea (PR #925 integrado), Issue #933.

Claude review PR #925 #6713708160 detectó superviviente R-no-digest bajo mutación del runner. Inspección de código confirmado: `qa525-vnext-held-physical-runner.py` YA contiene `_is_digest(receipt)` en líneas 130-131; por tanto no se necesitó modificar código de ejecución.

En tests/test_qa525_vnext_held_physical_runner.py añadimos matriz de 10 retornos inválidos `require_recorded` (None, bool, int, bytes, empty, truthy, wrong sha prefix/length/case) y error externo; todos deben cerrar `PHYSRUNNER_SCOPE_NOT_RECORDED`, sin verificación de PIN ni child, bajo RootPlane/locks/lease reales del test con efectos falsos.

En tests/test_qa525_vnext_held_physical_composition.py se corrigió tautología de aprobación: callback entrega valor RAW, de forma que `CommandGuard` es quien debe aplicar `is True`. Corrección de docstring obsoleto.

Sin archivos de otros propietarios, ni Docker/SSH/sudo, ni permisos QA físicos. Requerir CI exact-head, revisión cruzada para pruebas, squash únicamente dev. Continúa Issue #822 NO-GO físico y legacy entrypoint preservado hasta autorización separada.