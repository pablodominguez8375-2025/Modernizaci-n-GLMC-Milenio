#!/usr/bin/env bash
# CI-only MinIO IAM acceptance: never use operational credentials or real documents.
set -euo pipefail

ROOT="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"
NETWORK=pmgm-minio-iam-ci
SERVER=pmgm-minio-iam-server

cleanup() {
  docker rm -f "$SERVER" >/dev/null 2>&1 || true
  docker network rm "$NETWORK" >/dev/null 2>&1 || true
}
trap cleanup EXIT

docker network create "$NETWORK" >/dev/null
docker run -d --name "$SERVER" --network "$NETWORK" \
  -e MINIO_ROOT_USER=ci-minio-iam-root \
  -e MINIO_ROOT_PASSWORD=CiMinioIamRoot2026SecureOnly \
  quay.io/minio/minio:latest server /data >/dev/null

docker run --rm --network "$NETWORK" \
  -v "$ROOT/infrastructure/minio/pmgm-documents-policy.json:/policy.json:ro" \
  -e CI_MINIO_ROOT_USER=ci-minio-iam-root \
  -e CI_MINIO_ROOT_PASSWORD=CiMinioIamRoot2026SecureOnly \
  -e CI_MINIO_APP_USER=ci-minio-iam-documents \
  -e CI_MINIO_APP_PASSWORD=CiMinioIamDocs2026SecureOnly \
  --entrypoint /bin/sh quay.io/minio/mc:latest -c '
set -eu

ready=0
for attempt in $(seq 1 60); do
  if mc alias set admin http://pmgm-minio-iam-server:9000 "$CI_MINIO_ROOT_USER" "$CI_MINIO_ROOT_PASSWORD" >/dev/null 2>&1; then
    ready=1
    break
  fi
  sleep 2
done
[ "$ready" -eq 1 ] || { echo "MinIO sintético no inicia para prueba IAM." >&2; exit 1; }

mc mb --ignore-existing admin/pmgm-documents
mc mb --ignore-existing admin/pmgm-inaccessible
mc anonymous set none admin/pmgm-documents
mc anonymous set none admin/pmgm-inaccessible

mc admin policy create admin pmgm-documents-app /policy.json
mc admin user add admin "$CI_MINIO_APP_USER" "$CI_MINIO_APP_PASSWORD"
mc admin policy attach admin pmgm-documents-app --user "$CI_MINIO_APP_USER"

mc alias set docs http://pmgm-minio-iam-server:9000 "$CI_MINIO_APP_USER" "$CI_MINIO_APP_PASSWORD" >/dev/null
printf "pmgm-iam-synthetic-test\n" >/tmp/iam-test.txt
mc cp /tmp/iam-test.txt docs/pmgm-documents/iam-test.txt
mc stat docs/pmgm-documents/iam-test.txt >/dev/null
mc cat docs/pmgm-documents/iam-test.txt | grep -q "pmgm-iam-synthetic-test"
mc ls docs/pmgm-documents >/dev/null
mc rm docs/pmgm-documents/iam-test.txt

# La cuenta documental no puede enumerar otros recursos o administrar MinIO.
if mc ls docs/pmgm-inaccessible >/dev/null 2>&1; then
  echo "Usuario de documentos accede a bucket no autorizado." >&2
  exit 1
fi
if mc ls docs >/dev/null 2>&1; then
  echo "Usuario de documentos puede enumerar todos los buckets." >&2
  exit 1
fi
if mc admin info docs >/dev/null 2>&1; then
  echo "Usuario documental dispone de privilegios de administración." >&2
  exit 1
fi

echo "IAM MinIO OK: bucket documental permitido; administración/otros buckets denegados."
'
