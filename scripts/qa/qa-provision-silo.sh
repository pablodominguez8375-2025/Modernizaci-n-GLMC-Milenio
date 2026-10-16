#!/bin/sh
# QA candidate only: isolated IAM provisioning with bounded readiness.
set -eu
fail() { printf "SILO_QA_PROVISION_FAILED:%s\n" "$1" >&2; exit 1; }
: "${MINIO_ROOT_USER:?}" "${MINIO_ROOT_PASSWORD:?}"
: "${DOCUMENT_ACCESS_KEY:?}" "${DOCUMENT_SECRET_KEY:?}"
: "${BACKUP_ACCESS_KEY:?}" "${BACKUP_SECRET_KEY:?}"
case "$MINIO_ROOT_USER:$DOCUMENT_ACCESS_KEY:$BACKUP_ACCESS_KEY" in
  *REEMPLAZAR*) fail PLACEHOLDER ;;
esac
for identity in "$MINIO_ROOT_USER" "$DOCUMENT_ACCESS_KEY" "$BACKUP_ACCESS_KEY"; do
  [ "${#identity}" -ge 8 ] || fail SHORT_IDENTITY
done
for secret in "$MINIO_ROOT_PASSWORD" "$DOCUMENT_SECRET_KEY" "$BACKUP_SECRET_KEY"; do
  [ "${#secret}" -ge 16 ] || fail SHORT_SECRET
  case "$secret" in *REEMPLAZAR*) fail PLACEHOLDER ;; esac
done
[ "$MINIO_ROOT_USER" != "$DOCUMENT_ACCESS_KEY" ] || fail ADMIN_REUSE
[ "$MINIO_ROOT_USER" != "$BACKUP_ACCESS_KEY" ] || fail ADMIN_REUSE
[ "$DOCUMENT_ACCESS_KEY" != "$BACKUP_ACCESS_KEY" ] || fail SHARED_USER
[ "$MINIO_ROOT_PASSWORD" != "$DOCUMENT_SECRET_KEY" ] || fail ADMIN_REUSE
[ "$MINIO_ROOT_PASSWORD" != "$BACKUP_SECRET_KEY" ] || fail ADMIN_REUSE
[ "$DOCUMENT_SECRET_KEY" != "$BACKUP_SECRET_KEY" ] || fail SHARED_PASSWORD
ready=0
for attempt in 1 2 3 4 5 6 7 8 9 10; do
  if mcli alias set qa http://minio:9000 "$MINIO_ROOT_USER" "$MINIO_ROOT_PASSWORD" >/dev/null 2>&1 &&
     mcli admin info qa >/dev/null 2>&1; then
    ready=1; break
  fi
  sleep 2
done
[ "$ready" = 1 ] || fail NOT_READY
mcli mb --ignore-existing qa/pmgm-documents >/dev/null 2>&1 || fail BUCKET
mcli anonymous set none qa/pmgm-documents >/dev/null 2>&1 || fail ANONYMOUS
mcli admin policy create qa pmgm-qa-documents /policies/documents.json >/dev/null 2>&1 || fail POLICY
mcli admin user add qa "$DOCUMENT_ACCESS_KEY" "$DOCUMENT_SECRET_KEY" >/dev/null 2>&1 || fail DOCUMENT_USER
mcli admin policy attach qa pmgm-qa-documents --user "$DOCUMENT_ACCESS_KEY" >/dev/null 2>&1 || fail DOCUMENT_POLICY
mcli admin policy create qa pmgm-qa-backup /policies/backup.json >/dev/null 2>&1 || fail BACKUP_POLICY
mcli admin user add qa "$BACKUP_ACCESS_KEY" "$BACKUP_SECRET_KEY" >/dev/null 2>&1 || fail BACKUP_USER
mcli admin policy attach qa pmgm-qa-backup --user "$BACKUP_ACCESS_KEY" >/dev/null 2>&1 || fail BACKUP_POLICY
printf "SILO_QA_PROVISION_READY\n"
