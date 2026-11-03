#!/usr/bin/env bash
# QA525 preflight consolidado: SOLO LECTURA. Ningun resultado autoriza deployment.
# Version fijada: dev@cfe4e6573ece2ae81414cc252047d36cc56e5c73.
set -euo pipefail
export LC_ALL=C
sha='cfe4e6573ece2ae81414cc252047d36cc56e5c73'
zip_digest='fab78644c2fe95d2f747794c0f85b21cb6ed438679bb11fd629e9a9e54b5d358'
boot_digest='a5c76cfc0f94f0bf1c93fa2c83602bd1ead1ac61b65472f98f277042fa7f9769'
launcher_digest='545d7faa2af91e33e54e90d25e913e85fc20ab465ef5f6c0e90489a77537c429'
g6_digest='772a645f09823648ed25a287f837653aef4cced2ba95413422d8f20f65a9d915'
archive='centenario-qa-g6-CIFRADO-20261102T110545Z.tar.gz'
report='isolated-restore-20261102T110545Z.report.json'
tmp='/tmp/qa525-audit-EigD7wok'
out() { printf '%s=%s\n' "$1" "$2"; }
is_absent() { ! sudo test -e "$1" && ! sudo test -L "$1"; }
dir_mode() { [ "$(sudo stat -c '%u:%g %a %F' -- "$1" 2>/dev/null || :)" = "0:0 $2 directory" ]; }
file_mode() { [ "$(sudo stat -c '%u:%g %a %F %h' -- "$1" 2>/dev/null || :)" = "0:0 $2 regular file 1" ]; }
sum_eq() {
  actual="$(sudo sha256sum -- "$1" 2>/dev/null | awk '{print $1}')" || return 1
  [ "$actual" = "$2" ]
}
[ "$(id -u)" -ne 0 ] || { echo 'READONLY_NO_ROOT_SHELL'; exit 2; }
[ "$(hostname -s)" = glmdch ] && out HOST PASS || out HOST BLOCKED
if [ -d "$tmp" ] && [ -O "$tmp" ] && [ ! -L "$tmp" ]; then
  if [ "$(sha256sum "$tmp/qa-signed-$sha.zip" 2>/dev/null | awk '{print $1}')" = "$zip_digest" ]; then out RELEASE_ZIP_SHA PASS; else out RELEASE_ZIP_SHA BLOCKED; fi
  if [ "$(sha256sum "$tmp/qa525-signed-root-bootstrap-cfe4.py" 2>/dev/null | awk '{print $1}')" = "$boot_digest" ]; then out USER_BOOTSTRAP_SHA PASS; else out USER_BOOTSTRAP_SHA BLOCKED; fi
else out TEMPORARY_SOURCE TRUST_NOT_PROVEN; fi
dir_mode /opt/centenario 755 && dir_mode /opt/centenario/qa525-root-source 700 && dir_mode /etc/pmgm 700 && out ROOT_PATH_METADATA PASS || out ROOT_PATH_METADATA BLOCKED
file_mode /usr/local/sbin/pmgm-qa-update 700 && sum_eq /usr/local/sbin/pmgm-qa-update "$launcher_digest" && out HISTORICAL_LAUNCHER_SHA PASS || out HISTORICAL_LAUNCHER_SHA BLOCKED
dir_mode /opt/centenario/qa525-launcher-recovery 700 && out F1_RECOVERY_0700 PASS || out F1_RECOVERY_0700 BLOCKED
file_mode /opt/centenario/qa525-root-source/qa525-signed-root-bootstrap-prep.py 600 && sum_eq /opt/centenario/qa525-root-source/qa525-signed-root-bootstrap-prep.py "$boot_digest" && out NEW_BOOTSTRAP_ROOT_PIN PASS || out NEW_BOOTSTRAP_ROOT_PIN BLOCKED
locks_ok=true
for p in /opt/centenario/qa-api-refresh/operation.lock /opt/centenario/qa-auto-lite/web-update.lock /run/lock/pmgm-master-qa-604.lock /run/lock/pmgm-qa754-web-hotfix.lock /run/lock/pmgm-qa754-api-activation.lock /opt/centenario/qa525-root-source/bootstrap.lock; do
  file_mode "$p" 600 || locks_ok=false
done
[ "$locks_ok" = true ] && out SIX_LOCK_METADATA PASS || out SIX_LOCK_METADATA BLOCKED
if active="$(sudo lslocks -n -o PATH)"; then
  busy=false
  for p in /opt/centenario/qa-api-refresh/operation.lock /opt/centenario/qa-auto-lite/web-update.lock /run/lock/pmgm-master-qa-604.lock /run/lock/pmgm-qa754-web-hotfix.lock /run/lock/pmgm-qa754-api-activation.lock /opt/centenario/qa525-root-source/bootstrap.lock; do
    grep -Fxq -- "$p" <<< "$active" && busy=true
  done
  [ "$busy" = false ] && out LOCK_SNAPSHOT PASS || out LOCK_SNAPSHOT BLOCKED
else out LOCK_SNAPSHOT BLOCKED; fi
if dir_mode /opt/centenario/qa525-launcher-replacement 700 && [ "$(sudo find /opt/centenario/qa525-launcher-replacement -mindepth 1 -maxdepth 1 -printf x | wc -c)" -eq 0 ]; then out LAUNCHER_REPLACEMENT_JOURNAL_EMPTY PASS; else out LAUNCHER_REPLACEMENT_JOURNAL_EMPTY BLOCKED; fi
for p in /etc/pmgm/qa525-bootstrap-admission.json /etc/pmgm/qa525-recovery-bootstrap-admission.json /etc/pmgm/qa525-launcher-replace.json; do
  if is_absent "$p"; then out "$(basename "$p" .json)_ABSENT" PASS; else out "$(basename "$p" .json)_ABSENT" BLOCKED; fi
done
sum_eq "/home/soporte/$archive" "$g6_digest" && out G6_LOCAL_SHA PASS || out G6_LOCAL_SHA BLOCKED
sudo jq -e '(.result=="G6_SAME_HOST_ISOLATED_RESTORE_PASS_NOT_DISASTER_RECOVERY") and (.production_touched==false) and (.disaster_restore_other_host=="PENDING")' "/opt/centenario/qa-g6/$report" >/dev/null && out G6_SAME_HOST_RESTORE PASS_WITH_LIMITS || out G6_SAME_HOST_RESTORE BLOCKED
if sudo jq -e --arg archive "$archive" --arg sha "$g6_digest" --arg report "$report" '(.g6_archive_name==$archive) and (.g6_archive_sha256==$sha or .g6_archive_sha256==("sha256:"+$sha)) and (.g6_restore_report_name==$report) and (.g6_offhost_sha256_verified==true)' /etc/pmgm/qa525-g6-reviewed.json >/dev/null; then out G6_ACTIVE_REVIEW_BINDING PASS; else out G6_ACTIVE_REVIEW_BINDING BLOCKED; fi
if sudo jq -e --arg sha "$sha" '(.source_sha != $sha) and (.status != null)' /opt/centenario/qa525-signed-prepare/prepared.json >/dev/null; then out HISTORICAL_PREPARED PRESERVE; else out HISTORICAL_PREPARED REVIEW_REQUIRED; fi
# No hay mecanismo de aprobacion en este script. Solo lectura.
out FIRST_ROOT_EXECUTION_TRUST REQUIRES_INDEPENDENT_REVIEW
out LAUNCHER_ONLY_AUTHORIZATION NOT_GRANTED
out QA_PHYSICAL NO_GO
