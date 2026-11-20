# QA525 KIT-G6-A v2 — ARTEFACTO=GLMDCH_OPERATIVO_CORTE_164a07ec — COMANDO=backup — como soporte@glmdch, SIN sudo. Genera los archivos de UN comando
# G6 v2: g6-operation.json, plan/admission, app-subject, config privada con g6_pins y manifiesto v3. Imprime sus SHA,
# el PIN del manifiesto y la línea para GO-T-G6. No ejecuta nada como root ni firma nada.
(
set -euo pipefail
export QA_SHA="164a07ecd36eb45428b6aeac4854a17e339ca525"
export QA_FP="SHA256:DN64Bjj/7SMjP7LX9Nwrcp3lPRBpT2cKPs1NmaL1Y5k"
export QA_RELEASE="sha256:2e66bfdbb59b1512247c02ac13ab1443e804e067cf44a2b98bb0b7c5c3472c6d"
export QA_OLD="67f0c3932fd2cde5c71c13dc912c81653af41182ad078d0490f7d1d2afb6bcae"
export QA_PY="/usr/bin/python3.14"
export QA_PY_SHA="52e0a13e60a981d8c4b6478be2ba5176f69da07948a056bf49cf6f077e30cb41"
export QA_SSHKEYGEN="/usr/bin/ssh-keygen"
export QA_SSHKEYGEN_SHA="2dd834413c9d3f4c0e3535b9fcf2e96e14c11f3d36fd23b6c5fc24e50411d051"
export QA_GH="/usr/bin/gh"
export QA_GH_SHA="7469124f706944133d6a169691dd1c6c3511b12e85878d255e044e2948df4c9b"
export QA_PEER_UID="1000"
export QA_DROP_UID="65534"
export QA_DROP_GID="65534"
export QA_BASE="/var/lib/pmgm-qa525"
export QA_SOCK="/run/pmgm-qa525/launcher.sock"
export QA_REPO_ID="1356543647"
export QA_CMD="backup"
export QA_ARGV_JSON='["--restic-sha256","e0ba8fd58a7cb76707cbc064abb4ac72d5a91aa26041b9c4f763405684d00a41","--repo-id","b4df75c52a71050a4bbf493d25a7cd5dd5921d1fdb4ab8226eaf7b6b7ffb65ce","--key-epoch","75870a7a926569c94e853a2a44f0a580"]'
MODULES_LIST_SHA256="c6ee8324e81993b1b7490fe907572bdbff7827cc95a547e2310bde975d1bf417"
stop() { echo "QA525_KIT_G6_A=STOP_$1"; echo "GO_FISICO=NO_GO"; exit 1; }
[ "$(hostname)" = "glmdch" ] && [ "$(id -un)" = "soporte" ] || stop HOST_USER
[ "$(id -u)" != "0" ] || stop NO_ROOT_HERE
[ "$(id -u)" = "$QA_PEER_UID" ] || stop PEER_UID_IS_NOT_ME
umask 077
export QA_D="$HOME/qa525-corte-${QA_SHA:0:8}"
export QA_OPS_JSON="$QA_D/qa525-ops-v3-${QA_SHA:0:8}.json"
export QA_OPS_SIG="$QA_OPS_JSON.sig"
PUBFILE="$HOME/.ssh/qa525-ops.pub"
[ -f "$PUBFILE" ] && [ ! -L "$PUBFILE" ] || stop SPONSOR_PUBKEY_FILE
[ "$(cut -d' ' -f1 "$PUBFILE")" = "ssh-ed25519" ] || stop SPONSOR_PUBKEY_TYPE
export QA_PUBKEY="$(cut -d' ' -f2 "$PUBFILE")"
case "$QA_CMD" in preflight|init|backup|restore|review) ;; *) stop COMMAND ;; esac
K="$QA_D/kit-g6-$QA_CMD"
[ -d "$QA_D" ] && [ ! -L "$QA_D" ] && [ "$(stat -c '%u:%a' "$QA_D")" = "$(id -u):700" ] || stop CUT_DIR_NOT_MINE_0700
[ -d "$QA_D/modules" ] && [ ! -L "$QA_D/modules" ] || stop MODULES_DIR
for f in app-inner.zip app-bundle.json trusted-root.jsonl modules.sha256; do [ -f "$QA_D/$f" ] && [ ! -L "$QA_D/$f" ] || stop RUN_CORTE_A_FIRST; done
[ -f "$QA_OPS_JSON" ] && [ ! -L "$QA_OPS_JSON" ] && [ -f "$QA_OPS_SIG" ] && [ ! -L "$QA_OPS_SIG" ] || stop OPS_FILES
[ ! -e "$K" ] && [ ! -L "$K" ] || stop KIT_PATH_ALREADY_EXISTS_OR_IS_A_LINK
for pair in "$QA_PY:$QA_PY_SHA" "$QA_SSHKEYGEN:$QA_SSHKEYGEN_SHA" "$QA_GH:$QA_GH_SHA"; do
  p="${pair%%:*}"; h="${pair##*:}"
  [ "$(readlink -f "$p")" = "$p" ] || stop HELPER_PATH_NOT_REAL
  [ "$(sha256sum "$p" | cut -d' ' -f1)" = "$h" ] || stop HELPER_HASH_DRIFT
done
[ "$(sha256sum "$QA_D/modules.sha256" | cut -d' ' -f1)" = "$MODULES_LIST_SHA256" ] || stop MODULES_LIST_IS_NOT_THE_PINNED_24
[ "$(wc -l < "$QA_D/modules.sha256")" = "24" ] || stop MODULES_LIST_IS_NOT_THE_PINNED_24
[ "$(cd "$QA_D/modules" && find . -mindepth 1 -maxdepth 1 -type f ! -type l -printf '%f\n' | LC_ALL=C sort)" = "$(cut -d' ' -f3 "$QA_D/modules.sha256" | LC_ALL=C sort)" ] || stop MODULES_DIR_SET_IS_NOT_EXACT
[ "$(find "$QA_D/modules" -mindepth 1 | wc -l)" = "24" ] || stop MODULES_DIR_SET_IS_NOT_EXACT
( cd "$QA_D/modules" && sha256sum -c --status "$QA_D/modules.sha256" ) || stop MODULES_CHANGED
W="$(mktemp -d)"
trap 'rm -rf "$W"' EXIT
printf 'ssh-ed25519 %s qa525-ops\n' "$QA_PUBKEY" > "$W/key.pub"
[ "$(ssh-keygen -lf "$W/key.pub" | awk '{print $2}')" = "$QA_FP" ] || stop FINGERPRINT
printf 'qa525-ops namespaces="qa525-ops-v2@pmgm" ssh-ed25519 %s\n' "$QA_PUBKEY" > "$W/ops-signers"
ssh-keygen -Y verify -f "$W/ops-signers" -I qa525-ops -n "qa525-ops-v2@pmgm" -s "$QA_OPS_SIG" < "$QA_OPS_JSON" >/dev/null 2>&1 || stop OPS_SIGNATURE
mkdir "$K"
QA_K="$K" "$QA_PY" -I -S -c '
import hashlib, json, os, sys
E = os.environ
D, K, SHA = E["QA_D"], E["QA_K"], E["QA_SHA"]
def stop(code):
    print("QA525_KIT_G6_A=STOP_" + code); print("GO_FISICO=NO_GO"); sys.exit(1)
def rd(p):
    with open(p, "rb") as f:
        return f.read()
def h(b):
    return hashlib.sha256(b).hexdigest()
def canon(o, nl=False):
    return (json.dumps(o, sort_keys=True, separators=(",", ":"), ensure_ascii=True) + ("\n" if nl else "")).encode("ascii")
def put(name, data):
    fd = os.open(os.path.join(K, name), os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
    with os.fdopen(fd, "wb") as f:
        f.write(data)
    return "sha256:" + h(data)
app = "sha256:" + h(rd(D + "/app-inner.zip"))
ops_raw = rd(E["QA_OPS_JSON"])
try:
    ops = json.loads(ops_raw)
    ok = (type(ops) is dict and ops.get("ops_source_sha") == SHA and ops.get("app_archive_sha256") == app
          and type(ops.get("policy_hash")) is str and len(ops["policy_hash"]) == 71 and ops["policy_hash"].startswith("sha256:"))
except Exception:
    ok = False
if not ok:
    stop("OPS_RECEIPT_DOES_NOT_MATCH_THIS_CUT")
policy = ops["policy_hash"]
import zipfile
root = "Proyecto-Centenario-QA-srv01-" + SHA[:12]
inner = zipfile.ZipFile(D + "/app-inner.zip")
candidate = h(inner.read(root + "/scripts/qa/operators/qa525-update.py"))
for field, rel in (("program_sha256", "scripts/qa/qa525-vnext-cache.py"), ("readiness_sha256", "scripts/qa/qa-agent-readiness.py"),
                   ("staging_sha256", "scripts/qa/qa-signed-staging.py")):
    want = ops.get(field)
    if type(want) is not str or want.replace("sha256:", "", 1) != h(inner.read(root + "/" + rel)):
        stop("OPS_MODULE_HASH_" + field.upper())
if ops.get("schema") != "qa525-ops-v3" or ops.get("status") != "OPS_SIGNED_VERIFIED" or len(ops) != 8:
    stop("OPS_RECEIPT_SHAPE")
mods = {}
for line in rd(D + "/modules.sha256").decode("ascii").splitlines():
    digest, name = line.split("  ")
    mods[name] = digest
if len(mods) != 24:
    stop("MODULE_SET")
key = E["QA_PUBKEY"]
go_line = "qa525-ops namespaces=\"qa525-go-cut-v1@pmgm\" ssh-ed25519 %s\n" % key
ops_line = "qa525-ops namespaces=\"qa525-ops-v2@pmgm\" ssh-ed25519 %s\n" % key
scope = "QA525_G6_V2_ONLY_NO_LAUNCHER_NO_API_WEB"
cmd = E["QA_CMD"]
try:
    argv = json.loads(E["QA_ARGV_JSON"])
    ok = type(argv) is list and len(argv) <= 24 and len(argv) % 2 == 0 and all(type(v) is str and 0 < len(v) <= 256 for v in argv)
except Exception:
    ok = False
if not ok:
    stop("ARGV_JSON")
operation = canon({"schema": "qa525-g6-operation-v1", "g6_command": cmd, "argv": argv})
op_sha = put("g6-operation.json", operation)
plan = canon({
    "schema": "qa525-plan-g6-v2-v1", "scope": scope, "host": "glmdch", "environment": "QA_GLMDCH_ONLY",
    "operator": "soporte@glmdch", "source_sha": SHA, "app_archive_sha256": app, "policy_hash": policy,
    "action": "g6-v2-%s-supervised" % cmd, "g6_command": cmd, "argv": argv, "g6_operation_sha256": op_sha,
    "state_root": "/var/lib/pmgm-qa525-g6-v2",
    "excluded": ["launcher", "api", "web", "compose changes", "migrations", "identity", "main", "production"],
}, nl=True)
plan_sha = put("plan.json", plan)
bundle_sha = "sha256:" + h(rd(D + "/app-bundle.json"))
troot_sha = "sha256:" + h(rd(D + "/trusted-root.jsonl"))
ops_sha = "sha256:" + h(ops_raw)
sig_sha = "sha256:" + h(rd(E["QA_OPS_SIG"]))
helpers = {"gh": {"path": E["QA_GH"], "sha256": E["QA_GH_SHA"]},
           "ssh-keygen": {"path": E["QA_SSHKEYGEN"], "sha256": E["QA_SSHKEYGEN_SHA"]}}
interp = {"path": E["QA_PY"], "sha256": E["QA_PY_SHA"]}
admission = canon({
    "schema": "qa525-admission-g6-v2-v1", "g6_command": cmd, "g6_operation_sha256": op_sha, "scope": scope, "plan_sha256": plan_sha, "source_sha": SHA,
    "ops_v3_sha256": ops_sha, "ops_signature_sha256": sig_sha, "app_bundle_sha256": bundle_sha,
    "trusted_root_sha256": troot_sha, "sponsor_fingerprint": E["QA_FP"], "interpreter": interp, "helpers": helpers,
    "modules_list_sha256": h(rd(D + "/modules.sha256")), "peer_uid": int(E["QA_PEER_UID"]),
    "drop_uid": int(E["QA_DROP_UID"]), "drop_gid": int(E["QA_DROP_GID"]),
    "source_repository_id": E["QA_REPO_ID"],
    "status": "EVIDENCE_BOUND_NOT_A_GO_SPONSOR_SIGNS_SEPARATELY",
}, nl=True)
adm_sha = put("admission.json", admission)
subject = canon({"source_sha": SHA, "subject_sha256": app,
                 "signer_workflow": "pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/.github/workflows/qa-silo-rebuild.yml",
                 "oidc_issuer": "https://token.actions.githubusercontent.com"})
subj_sha = put("app-subject.json", subject)
base, stage = E["QA_BASE"], "/opt/centenario/qa525-root-source/" + SHA
children = {"LAUNCHER_ONLY": "qa525-vnext-root-launcher-child.py"}
for c in ("preflight", "init", "backup", "restore", "review"):
    children["G6:" + c] = "qa525-vnext-root-g6-%s.py" % c
cfg = {
    "pins": {"ops_source_sha": SHA, "app_source_sha": SHA, "app_archive_sha256": app, "plan_sha256": plan_sha,
             "admission_sha256": adm_sha, "policy_hash": policy},
    "launcher_pins": {"release_asset_sha256": E["QA_RELEASE"], "old_launcher_sha256": E["QA_OLD"],
                      "candidate_operator_sha256": candidate},
    "allowed_signers": go_line, "go_fingerprint": E["QA_FP"], "ops_v3_sha256": ops_sha, "app_subject_sha256": subj_sha,
    "inbox": base + "/inbox-g6-" + cmd, "temp_root": base + "/tmp", "socket_path": E["QA_SOCK"],
    "journal_root": base + "/journal", "lease_dir": base + "/lease", "peer_uid": int(E["QA_PEER_UID"]), "timeout": 60,
    "children": children, "g6_pins": {"g6_command": cmd, "g6_operation_sha256": op_sha},
    "authenticity": {"ops_allowed_signers": ops_line, "ops_signature_sha256": sig_sha, "app_bundle_sha256": bundle_sha,
                     "trusted_root_sha256": troot_sha, "app_archive_path": stage + "/app-inner.zip",
                     "drop_uid": int(E["QA_DROP_UID"]), "drop_gid": int(E["QA_DROP_GID"]),
                     "source_repository_id": E["QA_REPO_ID"]},
}
cfg_raw = (json.dumps(cfg, sort_keys=True, indent=1, ensure_ascii=True) + "\n").encode("ascii")
cfg_name = "sponsor-config-g6-%s.json" % cmd
cfg_sha = put(cfg_name, cfg_raw)
manifest = canon({"files": mods, "helpers": helpers, "interpreter": interp,
                  "config": {"name": cfg_name, "sha256": cfg_sha[7:]}})
man_sha = put("manifest-g6-%s.json" % cmd, manifest)
put("values.txt", ("PLAN=%s\nADMISSION=%s\nPOLICY=%s\nAPP=%s\nOPERATION=%s\nCMD=%s\nINBOX=%s\nSOCK=%s\n" % (
    plan_sha, adm_sha, policy, app, op_sha, cmd, cfg["inbox"], cfg["socket_path"])).encode())
print("PLAN_SHA256=" + plan_sha)
print("ADMISSION_SHA256=" + adm_sha)
print("POLICY_HASH=" + policy)
print("APP_SUBJECT_SHA256=" + subj_sha)
print("CONFIG_SHA256=" + cfg_sha)
print("CANDIDATE_OPERATOR_SHA256=" + candidate)
import base64
line = "|".join([SHA, app, plan_sha, adm_sha, policy, cmd, op_sha])
put("kit-line.txt", ("QA525KITG6:" + base64.b64encode(line.encode()).decode() + "\n").encode())
print("OPS_RECEIPT_BOUND_TO_CUT_ZIP_AND_3_MODULES=PASS")
print("G6_COMMAND=" + cmd)
print("G6_ARGV=" + json.dumps(argv))
print("G6_OPERATION_SHA256=" + op_sha)
print("MANIFEST_PIN=" + man_sha[7:])
print("LINEA PARA TERMUX (GO-T-G6), copiala completa:")
print("QA525KITG6:" + base64.b64encode(line.encode()).decode())
' || {
  # Fallo a mitad: NUNCA se borra. Se aparta a un directorio único nuevo y se comprueba que el traslado ocurrió.
  if F="$(mktemp -d "$QA_D/kit-g6-$QA_CMD.failed-XXXXXXXXXX")" && mv -T "$K" "$F/kit" && [ -d "$F/kit" ] && [ ! -e "$K" ] && [ ! -L "$K" ]; then
    echo "KIT_PARCIAL_CONSERVADO_EN=$F/kit"
  else
    echo "QA525_KIT_G6_A=STOP_PARTIAL_KIT_NOT_MOVED_LEFT_IN_PLACE_AT=$K"
    echo "NO_REINTENTAR_SIN_REVISION=si"
  fi
  echo "GO_FISICO=NO_GO"
  exit 1
}
echo "KIT_DIR=$K"
echo "GUARDA MANIFEST_PIN EN BITWARDEN (64 hex). Es el valor que pedira el bootstrap."
echo "QA525_KIT_G6_A=FILES_READY_NOT_INSTALLED_NO_GO"
echo "ROOT_CODE_EXECUTED=no"
echo "GO_FISICO=NO_GO"
)

