# QA525 GO-T-G6-RESTORE v1 — ARTEFACTO=GLMDCH_OPERATIVO_CORTE_164a07ec — en Termux (teléfono del Sponsor). Crea el acta GO de UN comando G6 v2, la firma y emite UNA línea para pegar en glmdch.
# El acta vale 14 minutos desde este instante. No imprime ni transmite la clave ni la frase de paso.
(
set -eu
KEY="${QA525_KEY:-$HOME/.ssh/qa525-ops}"
FP_EXPECTED="SHA256:DN64Bjj/7SMjP7LX9Nwrcp3lPRBpT2cKPs1NmaL1Y5k"
SHA_EXPECTED="164a07ecd36eb45428b6aeac4854a17e339ca525"
NS="qa525-go-cut-v1@pmgm"
stop() { echo "QA525_GO_T_G6_RESTORE=STOP_$1"; echo "GO_FISICO=NO_GO"; exit 1; }
umask 077
[ -f "$KEY" ] && [ -f "$KEY.pub" ] || stop KEY_FILES
FP="$(ssh-keygen -lf "$KEY.pub" | awk '{print $2}')"
[ "$FP" = "$FP_EXPECTED" ] || stop FINGERPRINT
W="$(mktemp -d)"
trap 'rm -rf "$W"' EXIT
echo "Pega la linea QA525KITG6:... que imprimio KIT-G6-A en glmdch y pulsa Enter:"
IFS= read -r KITLINE
case "$KITLINE" in QA525KITG6:*) ;; *) stop KIT_LINE_SHAPE ;; esac
VALUES="$(printf '%s' "${KITLINE#QA525KITG6:}" | base64 -d 2>/dev/null)" || stop KIT_LINE_B64
OLDIFS="$IFS"; IFS='|'; set -- $VALUES; IFS="$OLDIFS"
[ "$#" = "7" ] || stop KIT_LINE_FIELDS
OPS_SOURCE_SHA="$1"; APP_SOURCE_SHA="$1"; APP_ARCHIVE="$2"; PLAN="$3"; ADMISSION="$4"; POLICY="$5"; CMD="$6"; OPERATION="$7"
case "$CMD" in preflight|init|backup|restore|review) ;; *) stop KIT_LINE_COMMAND ;; esac
[ "$CMD" = "restore" ] || stop KIT_LINE_IS_NOT_RESTORE
[ "$OPS_SOURCE_SHA" = "$SHA_EXPECTED" ] || stop KIT_LINE_IS_NOT_THIS_CUT
for v in "$APP_ARCHIVE" "$PLAN" "$ADMISSION" "$POLICY" "$OPERATION"; do
  case "$v" in sha256:*) ;; *) stop KIT_LINE_DIGEST ;; esac
  [ "${#v}" = "71" ] && [ -z "$(printf '%s' "${v#sha256:}" | tr -d '0-9a-f')" ] || stop KIT_LINE_DIGEST
done
NONCE="$(head -c 32 /dev/urandom | od -An -v -tx1 | tr -d ' \n')"
[ "${#NONCE}" = "64" ] || stop NONCE
NOW="$(date -u +%s)"
ISSUED="$(date -u -d "@$((NOW - 20))" +%Y-%m-%dT%H:%M:%SZ)"
EXPIRES="$(date -u -d "@$((NOW + 840))" +%Y-%m-%dT%H:%M:%SZ)"
printf '{"action":"g6-v2-%s-supervised","admission_sha256":"%s","app_archive_sha256":"%s","app_source_sha":"%s","environment":"QA_GLMDCH_ONLY","expires_at":"%s","g6_command":"%s","g6_operation_sha256":"%s","host":"glmdch","issued_at":"%s","nonce":"%s","operator":"soporte@glmdch","ops_source_sha":"%s","plan_sha256":"%s","policy_hash":"%s","schema":"qa525-go-g6-v1","scope":"QA525_G6_V2_ONLY_NO_LAUNCHER_NO_API_WEB"}\n' \
  "$CMD" "$ADMISSION" "$APP_ARCHIVE" "$APP_SOURCE_SHA" "$EXPIRES" "$CMD" "$OPERATION" "$ISSUED" "$NONCE" "$OPS_SOURCE_SHA" "$PLAN" "$POLICY" > "$W/go.json"
echo "VAS A FIRMAR UN GO REAL: restore aislado G6 v2, COMANDO=$CMD, en glmdch. No toca el launcher ni API/WEB."
echo "OPERACION=$OPERATION"
echo "CORTE=$OPS_SOURCE_SHA"
echo "ZIP=$APP_ARCHIVE"
echo "PLAN=$PLAN"
echo "ADMISION=$ADMISSION"
echo "NONCE=${NONCE%????????????????????????????????????????????????????????}…"
echo "VENCE_UTC=$EXPIRES"
ssh-keygen -Y sign -f "$KEY" -n "$NS" "$W/go.json" >/dev/null 2>&1 || stop SIGN
printf 'qa525-ops namespaces="%s" %s\n' "$NS" "$(cut -d' ' -f1,2 "$KEY.pub")" > "$W/signers"
ssh-keygen -Y verify -f "$W/signers" -I qa525-ops -n "$NS" -s "$W/go.json.sig" < "$W/go.json" >/dev/null 2>&1 || stop SELF_VERIFY
echo "GO_SHA256=$(sha256sum "$W/go.json" | cut -d' ' -f1)"
echo "COPIA LA LINEA SIGUIENTE COMPLETA Y PEGALA EN GLMDCH:"
echo "QA525GO1:$(base64 -w0 "$W/go.json"):$(base64 -w0 "$W/go.json.sig")"
echo "QA525_GO_T_G6_RESTORE=SIGNED_VALID_14_MIN"
)

