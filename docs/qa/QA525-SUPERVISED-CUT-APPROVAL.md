# QA525 — concordancia de autorización local para primer QA supervisado

## Propósito
El guard `qa525-supervised-cut-approval.py` es una **condición necesaria, nunca suficiente** para el futuro aplicador físico. Impide confundir una aprobación local para un SHA/digest con otro corte o el entorno producción. Requiere una aprobación física de duración máxima 30 minutos, solo para `QA_SYNTHETIC`. Se lee desde `/etc/pmgm/qa525-supervised-cut.json` (root:root, 0600, sin symlinks; cada directorio ancestro controlado por root).

Un documento local **no es una firma criptográfica del Sponsor**, y la mera presencia de ese documento no confirma una revisión realizada, no consume la aprobación como single-use y no ejecuta despliegue. Ningún dato sensible o JSON privado debe publicarse en GitHub/Drive.

El contrato `validate(approval, source, prepared, now)` recibe datos del operador físico futuro, obtenidos de manera **independiente** bajo lock exclusivo y desde el verificador de release exact SHA. Verifica:
- `source_sha` igual en release/payload `prepared`/aprobación local; digest `sha256` exacto.
- Acción exacta `first-qa-api-web-supervised`, entorno `QA_SYNTHETIC` y scope `LOCAL_SUPERVISION_MATCH_ONLY_NO_APPLY`.
- Una marca local `operator_reviewed:true` y nonce de 64 caracteres hex; no se debe tomar como autorización suficiente ni identidad verificada.
- `issued_at`, `expires_at` UNIX int y ventana máxima 30 minutos sin uso si está vencida.
- El payload `prepared` mantiene `may_apply:false`, imágenes construidas y snapshot estable.

El único CLI actual es `--check`, que solo confirma que hay archivo local legible y protegido; **no coteja SHA** y publica `LOCAL_APPROVAL_FILE_PRESENT_NOT_APPLY_AUTHORITY`. No existe `--apply`.

## Ejemplo de estructura de aprobación (valores sintéticos, NO copiar al servidor)
```json
{
  "target_sha": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
  "release_asset_sha256": "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
  "scope": "LOCAL_SUPERVISION_MATCH_ONLY_NO_APPLY",
  "environment": "QA_SYNTHETIC",
  "action": "first-qa-api-web-supervised",
  "nonce": "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc",
  "issued_at": 1793267400,
  "expires_at": 1793268000,
  "operator_reviewed": true
}
```

## Falta para el primer despliegue
Esta comprobación NO revalida Sigstore, SHA real del runtime, respaldo fresco de `pmgm` y `pmgm_keycloak`, custodia fuera de host, restore drill, rollback conjunto ni UAT. El futuro aplicador deberá hacer todo eso bajo locks, además de consumir una autorización de un solo uso con auditoría y revisar cambios sensibles `Program.cs`/Identity/Schema. El PR histórico #771 contiene operadores con SHA viejos y no debe fusionarse ni ejecutarse. Ningún merge a `dev` concede autorización para tocar `glmdch` o `main`.

Pruebas herméticas `tests/test_qa525_supervised_cut_approval.py`, workflow `qa525-supervised-cut-approval.yml`. Handoff GH #525/#97 y bitácora Drive.
