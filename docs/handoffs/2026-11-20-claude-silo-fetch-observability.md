# Handoff 2026-11-20 — Observabilidad de fetch del Silo (HTTP 400 en `vendor_platform_and_license`)

Contexto: dev@a790cd42, `QA Silo supply and recovery` falló 3 veces con `failures.vendor_platform_and_license = HTTPError 400`; el stage agrupa token, índice, manifiesto, config, atestaciones y LICENSE, por lo que el 400 no era atribuible.

Cambio mínimo (sin reintentos, sin tocar workflows/gates/pins/releases):
- `scripts/qa/verify-silo-supply.py`: `fetch(..., stage=None)` etiqueta el error con un enum cerrado `FETCH_STAGES` (`token,index,manifest,config,attestation,attestation_blob,license`) y, en HTTPError, un booleano `redirected` (¿la respuesta de error vino de otra URL que la pedida?). Comportamiento idéntico si `stage` es None/desconocido. Los llamadores de `registry_evidence()` pasan su etapa.
- `scripts/qa/rebuild-silo-candidate.py`: `failure_details()` añade, SOLO si hay etiqueta válida, `fetch_stage`, `failure_class` (`client_error`/`rate_limited`/`server_error`/`timeout`/`network`) y `redirected`. Nunca URL, cabeceras ni texto de excepción. Forma previa `{type, http_status}` intacta para errores sin etiqueta.
- Pruebas (`tests/test_qa_silo_supply.py`, gate Silo existente): 6 pruebas nuevas con 400/403/429/500/503/timeout/URLError simulados por etapa, sin fuga de URL/cabecera, etiqueta de la etapa que falla en `registry_evidence`, ruta feliz y `oversized` sin cambios. 4 mutantes de control detectados.
- Hipótesis a contrastar con el próximo fallo (NO diagnóstico): `Authorization: Bearer` reenviado por urllib al redirect del CDN de blobs (`redirected=true` en `config`/`attestation_blob`).
- Backoff/retry: deliberadamente NO incluido; primero clasificar 4xx no transitorio vs 429/5xx/timeout con evidencia.
