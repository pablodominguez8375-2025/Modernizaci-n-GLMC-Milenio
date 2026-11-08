# QA525 vNext — Hoja de firma del Sponsor (biblioteca INACTIVA)

`scripts/qa/qa525-vnext-sponsor-signing.py` construye los **bytes canónicos exactos** que el
Sponsor firmará con su propia llave OPS desde Termux. **No firma, no autoriza, no es un GO,
no toca archivos, red ni procesos.** Todo resultado dice `may_apply=False`.

## Qué produce
- **Recibo OPS v3** (`qa525-ops-v3`, 8 campos incl. `app_archive_sha256`): lo consume sin cambios
  `verify_ops_receipt(..., expected_app_archive_sha256=...)`. Namespace `qa525-ops-v2@pmgm`.
- **Acta GO** (`qa525-go-cut-v1`, ventana ≤ 1800 s): la consume sin cambios `check_cut`.
  Namespace `qa525-go-cut-v1@pmgm` — **PROPUESTO**; el verificador GO de ChatGPT debe confirmarlo.
- `review_card(bytes)`: líneas de campos en orden fijo + código corto (16 hex en 4 grupos) para
  comparar en dos dispositivos antes de firmar. `signed_here=False`, `approval_here=False`.
- `allowed_signers_line(pub, namespace, huella_pin)`: se niega si la llave no es la huella fijada
  (Bitwarden). `new_nonce(32 bytes aportados por el llamador)`: la biblioteca no genera azar.

## Uso en Termux (un comando a la vez; solo el Sponsor)
1. Generar el documento y su tarjeta; comparar el código corto en dos dispositivos.
2. `ssh-keygen -Y sign -f ~/.ssh/qa525-ops -n qa525-ops-v2@pmgm <archivo>` (frase en Bitwarden).
3. Verificar localmente con `allowed_signers` y `ssh-keygen -Y verify`.
4. Mover el `.sig` por SFTP/Drive. Nunca pegar llaves ni frases en chats, Git o Drive.

## Límites
- Firmar el OPS v3 o el acta GO es decisión exclusiva del Sponsor; esta biblioteca no la sustituye.
- Procedencia de la huella fijada: responsabilidad fuera de banda del Sponsor.
- Las pruebas usan una llave sintética desechable con `ssh-keygen` real y los verificadores
  existentes (`verify_ops_receipt` v3, `check_cut`).
