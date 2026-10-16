#!/usr/bin/env bash
set -euo pipefail
umask 077
: "${RUNNER_TEMP:?}" "${QA_READONLY_SSH_KEY:?}" "${QA_SSH_HOST:?}" "${QA_SSH_PORT:?}" "${QA_SSH_USER:?}" "${QA_SSH_KNOWN_HOSTS:?}"
[[ "$QA_SSH_HOST" =~ ^[a-zA-Z0-9][a-zA-Z0-9.-]*$ ]]
[[ "$QA_SSH_USER" =~ ^[a-z_][a-z0-9_-]*$ ]]
[[ "$QA_SSH_PORT" == 22107 ]]
qa_temp=$(mktemp -d "$RUNNER_TEMP/qa-readonly.XXXXXX")
trap 'rm -rf -- "$qa_temp"' EXIT
printf '%s\n' "$QA_READONLY_SSH_KEY" > "$qa_temp/key"
printf '%s\n' "$QA_SSH_KNOWN_HOSTS" > "$qa_temp/known_hosts"
unset QA_READONLY_SSH_KEY
mkdir -p "$RUNNER_TEMP/qa-evidence"
rm -f "$RUNNER_TEMP/qa-evidence/report.json"
# Raw output and SSH diagnostics are private temporary files, never artifacts/logs.
if ! timeout 90 ssh -F /dev/null -T -n -a -x \
  -o BatchMode=yes -o IdentitiesOnly=yes -o IdentityAgent=none \
  -o PasswordAuthentication=no -o KbdInteractiveAuthentication=no \
  -o StrictHostKeyChecking=yes -o GlobalKnownHostsFile=/dev/null \
  -o UserKnownHostsFile="$qa_temp/known_hosts" -o UpdateHostKeys=no \
  -o ClearAllForwardings=yes -o RequestTTY=no -o ConnectTimeout=15 \
  -o ServerAliveInterval=15 -o ServerAliveCountMax=2 \
  -i "$qa_temp/key" -p "$QA_SSH_PORT" "$QA_SSH_USER@$QA_SSH_HOST" \
  centenario-qa-readonly-v1 > "$qa_temp/raw.json" 2> "$qa_temp/ssh-error"; then
  echo 'QA SSH failed; no remote content published' >&2
  exit 1
fi
helper_hash=$(sha256sum scripts/qa/centenario-qa-readonly | cut -d ' ' -f 1)
python3 scripts/qa/validate-report.py "$qa_temp/raw.json" "$RUNNER_TEMP/qa-evidence/report.json" "$helper_hash"
