#!/usr/bin/env bash
# QA525 GO-H-G6-REVIEW v1 — corte 164a07ec. Finaliza solo el acta local G6.
# No repite backup/restore ni cambia servicios; exige evidencia previa y GO exclusivo review.
set -Eeuo pipefail
set +x
umask 077

SHA="164a07ecd36eb45428b6aeac4854a17e339ca525"
SHORT="164a07ec"
CMD="review"
export QA_CMD="$CMD"
FP_EXPECTED="SHA256:DN64Bjj/7SMjP7LX9Nwrcp3lPRBpT2cKPs1NmaL1Y5k"
NS="qa525-go-cut-v1@pmgm"
PY="/usr/bin/python3.14"
PY_SHA="52e0a13e60a981d8c4b6478be2ba5176f69da07948a056bf49cf6f077e30cb41"
DOCKER="/usr/bin/docker"
ARCHIVE="centenario-qa-g6-v2-CIFRADO-20261113T165400Z.tar.gz"
ARCHIVE_SHA="b0c77c1c26ba48e1c6942abab3ea5d5b1a9aaa3228cbd65753bc274a775276fe"
IMAGE="postgres@sha256:2d2b8998d31037bf721cfdf764d76ba74171b4fab3431b7f72c27c56ddbdf9e3"
STAMP="20261113T165400Z"
CUT_EPOCH="$(date -u -d '2026-11-13T16:54:00Z' +%s)"
REVIEW_DEADLINE_EPOCH="$((CUT_EPOCH + 172800))"
RESULT_EXPECTED="G6_SAME_HOST_ISOLATED_RESTORE_PASS_NOT_DISASTER_RECOVERY"
STATE_ROOT="/var/lib/pmgm-qa525-g6-v2"
DOWNLOAD="$STATE_ROOT/downloads-v2/$ARCHIVE"
WORKDIR="$STATE_ROOT/restores-v2/$STAMP"
RESTORE_REPORT="$STATE_ROOT/state-v2/isolated-restore-v2-$STAMP.report.json"
RECEIPT="$STATE_ROOT/state-v2/receipt-$STAMP.json"
REVIEW="$STATE_ROOT/state-v2/review-$STAMP.json"
DRIVE_ID="1THaJaMm0sKRi1_VGp1eqOBuyzgQP54aT"
VERIFY_CONTAINER="pmgm-g6v2-verify-20261113t165400z"
MINIO="centenario-qa-lab2-minio-1"
POSTGRES="centenario-qa-lab2-postgres-1"

stop() {
  echo "QA525_GO_H_G6_REVIEW=STOP_$1"
  echo "G6_REVIEW=NO_GO"
  exit 1
}

[ "$(hostname -s)" = "glmdch" ] && [ "$(id -un)" = "soporte" ] && [ "$(id -u)" = "1000" ] || stop HOST_USER
[ -t 0 ] || stop NEEDS_TERMINAL
[ "$(readlink -f "$PY")" = "$PY" ] && [ "$(sha256sum "$PY" | cut -d' ' -f1)" = "$PY_SHA" ] || stop INTERPRETER_PIN
[ "$(readlink -f "$DOCKER")" = "$DOCKER" ] || stop DOCKER_PATH

D="$HOME/qa525-corte-$SHORT"
T="/opt/centenario/qa525-root-source/$SHA"
V="$D/kit-g6-$CMD/values.txt"
[ -f "$D/app-inner.zip" ] && [ ! -L "$D/app-inner.zip" ] || stop NO_APP_ZIP
[ -f "$V" ] && [ ! -L "$V" ] && [ "$(stat -c '%u:%a' "$V")" = "$(id -u):600" ] || stop RUN_REVIEW_KIT_A_FIRST
val() { sed -n "s/^$1=//p" "$V" | head -1; }
export QA_PLAN="$(val PLAN)" QA_ADMISSION="$(val ADMISSION)" QA_POLICY="$(val POLICY)" QA_OPERATION="$(val OPERATION)"
[ "$(val CMD)" = "$CMD" ] || stop VALUES_COMMAND
INBOX="$(val INBOX)"; SOCK="$(val SOCK)"
[ "$INBOX" = "/var/lib/pmgm-qa525/inbox-g6-review" ] || stop VALUES_INBOX
[ "$SOCK" = "/run/pmgm-qa525/launcher.sock" ] || stop VALUES_SOCK

PUBFILE="$HOME/.ssh/qa525-ops.pub"
[ -f "$PUBFILE" ] && [ ! -L "$PUBFILE" ] && [ "$(cut -d' ' -f1 "$PUBFILE")" = "ssh-ed25519" ] || stop SPONSOR_PUBKEY_FILE
PUBKEY="$(cut -d' ' -f2 "$PUBFILE")"
exec 9> "$D/.go-h-review.lock"
flock -n 9 || stop ANOTHER_REVIEW_GO_H_RUNNING

W="$(mktemp -d)"
EMITTER=""
GO_INSTALLED=""
ARCHIVED=no
NONCE="uninstalled"
absent() { sudo /usr/bin/test ! -e "$1" -a ! -L "$1"; }
rootfile() { [ "$(sudo /usr/bin/stat -c '%u:%g:%a:%F:%h' "$1" 2>/dev/null)" = "0:0:600:regular file:1" ]; }
archive_go() {
  [ "$ARCHIVED" != "si" ] || return 0
  local tag used
  tag="$(date -u +%Y%m%dT%H%M%SZ)-$$"
  used="$INBOX/.used-go-${NONCE:0:16}-$tag"
  if absent "$used.json" && absent "$used.json.sig" \
     && sudo -n /usr/bin/mv -n -T "$INBOX/go.json" "$used.json" \
     && sudo -n /usr/bin/mv -n -T "$INBOX/go.json.sig" "$used.json.sig" \
     && rootfile "$used.json" && rootfile "$used.json.sig" \
     && absent "$INBOX/go.json" && absent "$INBOX/go.json.sig"; then
    ARCHIVED=si
  fi
}
cleanup() {
  [ -z "$EMITTER" ] || kill "$EMITTER" 2>/dev/null || true
  if [ -n "$GO_INSTALLED" ]; then
    archive_go || true
    echo "GO_USADO_ARCHIVADO_AL_SALIR=$ARCHIVED"
  fi
  rm -rf -- "$W"
}
trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM
trap 'exit 129' HUP

sudo -v || stop SUDO
[ "$(sudo /usr/bin/stat -c '%u:%g:%a:%F' "$INBOX")" = "0:0:700:directory" ] || stop INBOX_NOT_ROOT_0700
for g in go.json go.json.sig .go.json.new .go.json.sig.new; do absent "$INBOX/$g" || stop INBOX_ALREADY_HAS_GO_OR_LEFTOVER; done
[ "$(sudo /usr/bin/sha256sum "$INBOX/g6-operation.json" | cut -d' ' -f1)" = "${QA_OPERATION#sha256:}" ] || stop OPERATION_FILE_MISMATCH

validate_evidence() {
  sudo "$PY" -I -S - "$RECEIPT" "$RESTORE_REPORT" "$REVIEW" "$ARCHIVE" "$ARCHIVE_SHA" "$DRIVE_ID" "$1" <<'PY'
import hashlib,json,os,stat,sys
receipt_path,restore_path,review_path,name,digest,drive,phase=sys.argv[1:]
repo="b4df75c52a71050a4bbf493d25a7cd5dd5921d1fdb4ab8226eaf7b6b7ffb65ce"
epoch="75870a7a926569c94e853a2a44f0a580"
result="G6_SAME_HOST_ISOLATED_RESTORE_PASS_NOT_DISASTER_RECOVERY"
def unique(pairs):
 d={}
 for k,v in pairs:
  if k in d: raise ValueError()
  d[k]=v
 return d
def read(path):
 before=os.lstat(path)
 fd=os.open(path,os.O_RDONLY|os.O_NOFOLLOW|os.O_CLOEXEC)
 try:
  st=os.fstat(fd)
  assert stat.S_ISREG(st.st_mode) and st.st_uid==st.st_gid==0
  assert stat.S_IMODE(st.st_mode)==0o600 and st.st_nlink==1
  assert (st.st_dev,st.st_ino)==(before.st_dev,before.st_ino)
  assert 0<st.st_size<=65536
  with os.fdopen(fd,"rb",closefd=False) as f: raw=f.read(65537)
 finally: os.close(fd)
 assert len(raw)<=65536
 o=json.loads(raw,object_pairs_hook=unique)
 assert type(o) is dict
 assert (json.dumps(o,sort_keys=True,separators=(",",":"),ensure_ascii=True,allow_nan=False)+"\n").encode("ascii")==raw
 return o
try:
 r=read(receipt_path); s=read(restore_path)
 assert r=={"schema":"qa525-g6-v2-receipt","archive_name":name,"sha256":digest,
 "status":"ENCRYPTED_LOCAL_ARCHIVE_READY_OFFHOST_PENDING","restic_repo_id":repo,"key_epoch":epoch}
 assert set(s)=={"schema","result","archive_filename","archive_sha256","restic_repo_id","key_epoch","postgres_tables_by_database","silo_objects_verified","production_touched"}
 assert s["schema"]=="qa525-g6-v2-restore" and s["result"]==result
 assert s["archive_filename"]==name and s["archive_sha256"]==digest
 assert s["restic_repo_id"]==repo and s["key_epoch"]==epoch and s["production_touched"] is False
 tables=s["postgres_tables_by_database"]
 assert type(tables) is dict and set(tables)=={"pmgm","pmgm_keycloak"}
 assert all(type(v) is int and v>0 for v in tables.values())
 assert type(s["silo_objects_verified"]) is int and s["silo_objects_verified"]>0
 if phase=="pre":
  assert not os.path.lexists(review_path)
 elif phase=="post":
  v=read(review_path)
  assert v.get("g6_offhost_sha256_verified") is True
  assert v=={"schema":"qa525-g6-v2-review","g6_archive_name":name,"g6_archive_sha256":digest,
  "g6_restore_report_name":os.path.basename(restore_path),"g6_offhost_sha256_verified":True,
  "g6_drive_file_id":drive,"restic_repo_id":repo,"key_epoch":epoch}
 else: raise ValueError()
except Exception: sys.exit(1)
PY
}

# Read-only gates: evidencia coherente antes de consentimiento y GO.
[ "$(sudo /usr/bin/stat -c '%u:%g:%a:%F:%h' "$DOWNLOAD")" = "0:0:600:regular file:1" ] || stop DOWNLOAD_CUSTODY
[ "$(sudo /usr/bin/sha256sum "$DOWNLOAD" | cut -d' ' -f1)" = "$ARCHIVE_SHA" ] || stop DOWNLOAD_HASH
sudo "$DOCKER" image inspect "$IMAGE" >/dev/null 2>&1 || stop IMAGE_DIGEST_NOT_LOCAL
sudo "$DOCKER" inspect "$VERIFY_CONTAINER" >/dev/null 2>&1 && stop VERIFIER_CONTAINER_ALREADY_EXISTS
sudo /usr/bin/test ! -e "$WORKDIR" -a ! -L "$WORKDIR" || stop REVIEW_WORKDIR_ALREADY_EXISTS
validate_evidence pre || stop RECEIPT_RESTORE_OR_REVIEW_INVALID_NOTHING_SPENT
BEFORE_EVIDENCE_SHA="$(sudo /usr/bin/sha256sum "$RECEIPT" "$RESTORE_REPORT")"
identity() {
  sudo "$DOCKER" inspect -f '{{index .Config.Labels "com.docker.compose.project"}}|{{index .Config.Labels "com.docker.compose.service"}}|{{.State.Running}}|{{if .State.Health}}{{.State.Health.Status}}{{else}}no-health{{end}}' "$1"
}
MINIO_BEFORE="$(identity "$MINIO")"
POSTGRES_BEFORE="$(identity "$POSTGRES")"
[ "$MINIO_BEFORE" = "centenario-qa-lab2|minio|true|healthy" ] || stop PRODUCTION_MINIO_NOT_HEALTHY_BEFORE
[ "$POSTGRES_BEFORE" = "centenario-qa-lab2|postgres|true|healthy" ] || stop PRODUCTION_POSTGRES_NOT_HEALTHY_BEFORE
NOW_EPOCH="$(date -u +%s)"
[ "$NOW_EPOCH" -ge "$CUT_EPOCH" ] && [ "$NOW_EPOCH" -lt "$((REVIEW_DEADLINE_EPOCH - 1800))" ] || stop REVIEW_WINDOW_LESS_THAN_30_MIN_NOTHING_SPENT
echo "REVIEW_WINDOW_SECONDS_REMAINING=$((REVIEW_DEADLINE_EPOCH - NOW_EPOCH))"

echo "Este REVIEW crea solo el acta local G6; no repite restore ni detiene servicios."
echo "Para autorizar esta única ejecución escribe exactamente: AUTORIZO REVIEW G6 164a07ec"
IFS= read -r CONSENT
[ "$CONSENT" = "AUTORIZO REVIEW G6 164a07ec" ] || stop REVIEW_NOT_AUTHORIZED
echo "Pega la linea QA525GO1:... de Termux y pulsa Enter:"
IFS= read -r TOKEN
case "$TOKEN" in QA525GO1:*:*) ;; *) stop TOKEN_SHAPE ;; esac
REST="${TOKEN#QA525GO1:}"
printf '%s' "${REST%%:*}" | base64 -d > "$W/go.json" 2>/dev/null || stop TOKEN_B64
printf '%s' "${REST#*:}" | base64 -d > "$W/go.json.sig" 2>/dev/null || stop TOKEN_B64
printf 'ssh-ed25519 %s qa525-ops\n' "$PUBKEY" > "$W/key.pub"
[ "$(ssh-keygen -lf "$W/key.pub" | awk '{print $2}')" = "$FP_EXPECTED" ] || stop FINGERPRINT
printf 'qa525-ops namespaces="%s" ssh-ed25519 %s\n' "$NS" "$PUBKEY" > "$W/signers"
ssh-keygen -Y verify -f "$W/signers" -I qa525-ops -n "$NS" -s "$W/go.json.sig" < "$W/go.json" >/dev/null 2>&1 || stop SIGNATURE
APP="$(sha256sum "$D/app-inner.zip" | cut -d' ' -f1)"
NONCE="$(QA_GO="$W/go.json" QA_SHA="$SHA" QA_APP="$APP" "$PY" -I -S -c '
import calendar, json, os, re, sys, time
E=os.environ; raw=open(E["QA_GO"],"rb").read()
try:
 g=json.loads(raw)
 fixed={"action":"g6-v2-review-supervised","admission_sha256":E["QA_ADMISSION"],
 "app_archive_sha256":"sha256:"+E["QA_APP"],"app_source_sha":E["QA_SHA"],
 "environment":"QA_GLMDCH_ONLY","g6_command":"review","g6_operation_sha256":E["QA_OPERATION"],
 "host":"glmdch","operator":"soporte@glmdch","ops_source_sha":E["QA_SHA"],
 "plan_sha256":E["QA_PLAN"],"policy_hash":E["QA_POLICY"],"schema":"qa525-go-g6-v1",
 "scope":"QA525_G6_V2_ONLY_NO_LAUNCHER_NO_API_WEB"}
 ok=(type(g) is dict and set(g)==set(fixed)|{"nonce","issued_at","expires_at"}
 and all(g[k]==v for k,v in fixed.items())
 and json.dumps(g,sort_keys=True,separators=(",",":"),ensure_ascii=True).encode()+b"\n"==raw
 and type(g["nonce"]) is str and re.fullmatch(r"[0-9a-f]{64}",g["nonce"]))
 exp=calendar.timegm(time.strptime(g["expires_at"],"%Y-%m-%dT%H:%M:%SZ"))
 iss=calendar.timegm(time.strptime(g["issued_at"],"%Y-%m-%dT%H:%M:%SZ"))
 left=exp-int(time.time()); ok=ok and 0<exp-iss<=900 and iss<=time.time() and left>=180
except Exception: ok=False
if not ok: sys.exit(1)
sys.stderr.write("GO_SEGUNDOS_RESTANTES=%d\n"%left); print(g["nonce"])
')" || stop GO_CONTENT_OR_EXPIRED

echo "Pega el MANIFEST_PIN REVIEW del corte 164a07ec y pulsa Enter. No se muestra en pantalla:"
IFS= read -rs PIN; echo
PIN="$(printf '%s' "$PIN" | tr -d '[:space:]')"; PIN="${PIN#MANIFEST_PIN=}"
[ "${#PIN}" = "64" ] && [ -z "$(printf '%s' "$PIN" | tr -d '0-9a-f')" ] || stop PIN_SHAPE_NOTHING_SPENT
[ "$(sudo /usr/bin/sha256sum "$T/manifest-g6-review.json" | cut -d' ' -f1)" = "$PIN" ] || stop PIN_IS_NOT_REVIEW_MANIFEST_NOTHING_SPENT
echo "PIN_COINCIDE_CON_MANIFIESTO_REVIEW_ROOT=si"

retire() {
  local tag g
  tag="$(date -u +%Y%m%dT%H%M%SZ)-$$"
  for g in go.json go.json.sig .go.json.new .go.json.sig.new; do
    absent "$INBOX/$g" || sudo /usr/bin/mv -n -T "$INBOX/$g" "$INBOX/.failed-$tag-${g#.}" || true
  done
  stop "$1_PARTIAL_GO_RETIRED"
}
sudo /usr/bin/install -m 0600 -o root -g root -T "$W/go.json" "$INBOX/.go.json.new" || stop INSTALL
sudo /usr/bin/install -m 0600 -o root -g root -T "$W/go.json.sig" "$INBOX/.go.json.sig.new" || retire INSTALL
rootfile "$INBOX/.go.json.new" && rootfile "$INBOX/.go.json.sig.new" || retire INSTALL
sudo /usr/bin/mv -n -T "$INBOX/.go.json.new" "$INBOX/go.json" || retire PUBLISH
sudo /usr/bin/mv -n -T "$INBOX/.go.json.sig.new" "$INBOX/go.json.sig" || retire PUBLISH
rootfile "$INBOX/go.json" && rootfile "$INBOX/go.json.sig" || retire PUBLISH
[ "$(sudo /usr/bin/sha256sum "$INBOX/go.json" | cut -d' ' -f1)" = "$(sha256sum "$W/go.json" | cut -d' ' -f1)" ] || retire READBACK
[ "$(sudo /usr/bin/sha256sum "$INBOX/go.json.sig" | cut -d' ' -f1)" = "$(sha256sum "$W/go.json.sig" | cut -d' ' -f1)" ] || retire READBACK
GO_INSTALLED=1
echo "GO_SHA256=$(sha256sum "$W/go.json" | cut -d' ' -f1)"
echo "GO_INSTALADO=si NONCE=${NONCE:0:8}…"

QA_ZIP="$D/app-inner.zip" QA_NONCE="$NONCE" QA_APP="$APP" QA_SOCK="$SOCK" QA_PARENT="$$" "$PY" -I -S -c '
import array,fcntl,os,socket,sys,time
E=os.environ; fd=os.memfd_create("qa525-app",os.MFD_CLOEXEC|os.MFD_ALLOW_SEALING)
with open(E["QA_ZIP"],"rb") as src:
 while True:
  b=src.read(1<<20)
  if not b: break
  v=memoryview(b)
  while v: v=v[os.write(fd,v):]
fcntl.fcntl(fd,fcntl.F_ADD_SEALS,fcntl.F_SEAL_WRITE|fcntl.F_SEAL_SHRINK|fcntl.F_SEAL_GROW|fcntl.F_SEAL_SEAL)
frame=b"QA525-BND-V1:"+E["QA_NONCE"].encode()+b":"+E["QA_APP"].encode(); end=time.time()+600
while time.time()<end and os.getppid()==int(E["QA_PARENT"]):
 s=socket.socket(socket.AF_UNIX,socket.SOCK_SEQPACKET)
 try:
  s.connect(E["QA_SOCK"]); s.sendmsg([frame],[(socket.SOL_SOCKET,socket.SCM_RIGHTS,array.array("i",[fd]).tobytes())]); open(E["QA_ZIP"]+".sent","w").close(); time.sleep(120); sys.exit(0)
 except OSError: s.close(); time.sleep(.2)
sys.exit(1)
' </dev/null >/dev/null 2>&1 9>&- &
EMITTER=$!
rm -f "$D/app-inner.zip.sent"

OUT="$W/bootstrap.out"; : > "$OUT"
set +e
printf '%s\n' "$PIN" | sudo /bin/sh -c 'umask 022; exec "$@"' sh "$PY" -I -S -u "$T/qa525-vnext-root-bootstrap.py" run "$T" "$T/manifest-g6-review.json" | tee "$OUT"
RC=${PIPESTATUS[1]}
set -e
PIN=""
kill "$EMITTER" 2>/dev/null || true; EMITTER=""
echo "BOOTSTRAP_EXIT=$RC"
[ -e "$D/app-inner.zip.sent" ] && FD_SENT=si || FD_SENT=no
rm -f "$D/app-inner.zip.sent"
archive_go
echo "FD_ENVIADO=$FD_SENT"
echo "GO_USADO_ARCHIVADO=$ARCHIVED"

one() { [ "$(grep -c "^$1=" "$OUT" 2>/dev/null || true)" = "1" ] && sed -n "s/^$1=//p" "$OUT"; }
NAME="$(one G6_ARCHIVE_NAME || true)"; HASH="$(one G6_ARCHIVE_SHA256 || true)"
OFFHOST="$(one G6_OFFHOST_SHA256_VERIFIED || true)"
MARKERS=no
if [ "$NAME" = "$ARCHIVE" ] && [ "$HASH" = "$ARCHIVE_SHA" ] && [ "$OFFHOST" = "true" ] \
   && ! grep -q '^G6RUN_STOP=' "$OUT"; then MARKERS=si; fi

MINIO_AFTER=""; POSTGRES_AFTER=""
for _ in $(seq 1 24); do
  MINIO_AFTER="$(identity "$MINIO" 2>/dev/null || true)"
  POSTGRES_AFTER="$(identity "$POSTGRES" 2>/dev/null || true)"
  [ "$MINIO_AFTER" = "$MINIO_BEFORE" ] && [ "$POSTGRES_AFTER" = "$POSTGRES_BEFORE" ] && break
  sleep 5
done
PRODUCTION_OK=no
[ "$MINIO_AFTER" = "$MINIO_BEFORE" ] && [ "$POSTGRES_AFTER" = "$POSTGRES_BEFORE" ] && PRODUCTION_OK=si
DOWNLOAD_FINAL_OK=no
if [ "$(sudo /usr/bin/stat -c '%u:%g:%a:%F:%h' "$DOWNLOAD" 2>/dev/null || true)" = "0:0:600:regular file:1" ] \
   && [ "$(sudo /usr/bin/sha256sum "$DOWNLOAD" 2>/dev/null | cut -d' ' -f1)" = "$ARCHIVE_SHA" ]; then DOWNLOAD_FINAL_OK=si; fi
VERIFIER_ABSENT=no; sudo "$DOCKER" inspect "$VERIFY_CONTAINER" >/dev/null 2>&1 || VERIFIER_ABSENT=si
WORKDIR_ABSENT=no; sudo /usr/bin/test ! -e "$WORKDIR" -a ! -L "$WORKDIR" && WORKDIR_ABSENT=si
EVIDENCE_UNCHANGED=no
[ "$(sudo /usr/bin/sha256sum "$RECEIPT" "$RESTORE_REPORT" 2>/dev/null || true)" = "$BEFORE_EVIDENCE_SHA" ] && EVIDENCE_UNCHANGED=si
REVIEW_OK=no
if [ "$RC" = "0" ] && validate_evidence post; then REVIEW_OK=si; fi

echo "REVIEW_MARKERS_COMPLETE=$MARKERS"
echo "PRODUCTION_CONTAINERS_UNCHANGED_HEALTHY=$PRODUCTION_OK"
echo "DOWNLOAD_FINAL_ROOT_0600_HASH_OK=$DOWNLOAD_FINAL_OK"
echo "VERIFIER_CONTAINER_FINAL_ABSENT=$VERIFIER_ABSENT"
echo "PLAINTEXT_WORKDIR_FINAL_ABSENT=$WORKDIR_ABSENT"
echo "RECEIPT_RESTORE_EVIDENCE_UNCHANGED=$EVIDENCE_UNCHANGED"
echo "REVIEW_ROOT_0600_CANONICAL_VALID=$REVIEW_OK"

if [ "$RC" != "0" ] || [ "$FD_SENT" != "si" ]; then echo "QA525_GO_H_G6_REVIEW=COMMAND_FAILED_DO_NOT_RETRY_WITHOUT_REVIEW"; echo "GO_H_G6_REVIEW_EXIT=4"; exit 4; fi
if [ "$ARCHIVED" != "si" ]; then echo "QA525_GO_H_G6_REVIEW=EXIT_0_BUT_GO_NOT_ARCHIVED"; echo "GO_H_G6_REVIEW_EXIT=3"; exit 3; fi
if [ "$MARKERS" != "si" ] || [ "$PRODUCTION_OK" != "si" ] || [ "$DOWNLOAD_FINAL_OK" != "si" ] || [ "$VERIFIER_ABSENT" != "si" ] || [ "$WORKDIR_ABSENT" != "si" ] || [ "$REVIEW_OK" != "si" ] || [ "$EVIDENCE_UNCHANGED" != "si" ]; then
  echo "QA525_GO_H_G6_REVIEW=EXIT_0_BUT_FINAL_READBACK_FAILED_DO_NOT_RETRY_WITHOUT_REVIEW"
  echo "GO_H_G6_REVIEW_EXIT=7"
  exit 7
fi
sudo /usr/bin/sha256sum "$REVIEW" "$RECEIPT" "$RESTORE_REPORT"
echo "QA525_GO_H_G6_REVIEW=COMPLETE_EVIDENCE_FINALIZED_PRODUCTION_UNTOUCHED"
echo "GO_H_G6_REVIEW_EXIT=0"
echo "G6_REVIEW=PASS_PENDING_GLOBAL_G6_VALIDATION"

