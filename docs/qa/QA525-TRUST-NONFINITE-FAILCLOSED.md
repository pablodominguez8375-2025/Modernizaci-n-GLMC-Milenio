# QA525 — Normalización de números JSON no finitos en admisión (2026-11-05)

Fuente: revisión independiente Claude PR #849 comentario 6675273509, P2. El JSON `1e999` es aceptado por `json.loads` de Python como `inf` aun con `parse_constant` que rechaza `Infinity`, y `json.dumps(..., allow_nan=False)` lanza `ValueError` fuera del `try` de `_parse`. El resultado actual es fallo cerrado con excepción no normalizada, no éxito. La corrección preserva la semántica *NO APPLY*, con código estático `BINDING_ADMISSION_NONCANONICAL`, nunca texto externo.

Alcance del incremento: únicamente módulo puro `scripts/qa/qa525-vnext-trust-binding.py`, tests `tests/test_qa525_vnext_trust_binding.py` y esta nota. Pruebas de exponentes enormes positivos y negativos con admisión canónica de esquema correcto. Sin modificación de firmas, ancla externa, política de ventanas, rutas de host, Docker, red, credenciales, BD, Keycloak, main o producción.

Aprobación/merge: PR DRAFT, CI exact-head, revisión adversarial apropiada; cualquier green solo autoriza código inactivo en dev, nunca despliegue QA físico. Refs #525 y #855.
