#!/bin/sh
set -eu

endpoint="${PMGM_CI_S3_ENDPOINT:-http://minio:9000}"

to_s3_uri() {
  case "$1" in
    s3://*) printf '%s\n' "$1" ;;
    pmgm/*) printf 's3://%s\n' "${1#pmgm/}" ;;
    *) printf '%s\n' "$1" ;;
  esac
}

bucket_and_key() {
  value="$(to_s3_uri "$1")"
  value="${value#s3://}"
  printf '%s\n' "$value"
}

case "${1:-}" in
  alias)
    exit 0
    ;;
  ls)
    shift
    recursive=false
    while [ "$#" -gt 0 ]; do
      case "$1" in
        --recursive) recursive=true ;;
        --*) ;;
        *) target="$1" ;;
      esac
      shift
    done
    target="$(to_s3_uri "${target:-s3://pmgm-documents}")"
    if [ "$recursive" = true ]; then
      exec aws s3 ls "$target" --recursive --endpoint-url "$endpoint" --no-cli-pager
    fi
    exec aws s3 ls "$target" --endpoint-url "$endpoint" --no-cli-pager
    ;;
  rm)
    shift
    recursive=false
    while [ "$#" -gt 0 ]; do
      case "$1" in
        --recursive) recursive=true ;;
        --force) ;;
        --*) ;;
        *) target="$1" ;;
      esac
      shift
    done
    target="$(to_s3_uri "$target")"
    if [ "$recursive" = true ]; then
      exec aws s3 rm "$target" --recursive --endpoint-url "$endpoint" --no-cli-pager
    fi
    exec aws s3 rm "$target" --endpoint-url "$endpoint" --no-cli-pager
    ;;
  mirror)
    shift
    delete=false
    while [ "$#" -gt 0 ]; do
      case "$1" in
        --overwrite) ;;
        --remove) delete=true ;;
        --*) ;;
        *)
          if [ -z "${source:-}" ]; then source="$1"; else destination="$1"; fi
          ;;
      esac
      shift
    done
    source="$(to_s3_uri "$source")"
    destination="$(to_s3_uri "$destination")"
    set -- s3 sync "$source" "$destination" --endpoint-url "$endpoint" --no-cli-pager
    if [ "$delete" = true ]; then set -- "$@" --delete; fi
    exec aws "$@"
    ;;
  pipe)
    shift
    target="$(to_s3_uri "$1")"
    exec aws s3 cp - "$target" --endpoint-url "$endpoint" --no-cli-pager
    ;;
  cat)
    shift
    target="$(to_s3_uri "$1")"
    exec aws s3 cp "$target" - --endpoint-url "$endpoint" --no-cli-pager
    ;;
  mb)
    shift
    while [ "$#" -gt 0 ]; do
      case "$1" in --*) ;; *) target="$1" ;; esac
      shift
    done
    bucket_and_key "$target" | cut -d/ -f1 | xargs -I{} aws s3api create-bucket --bucket {} --endpoint-url "$endpoint" --no-cli-pager >/dev/null 2>&1 || true
    ;;
  anonymous)
    exit 0
    ;;
  *)
    echo "Unsupported CI mc compatibility command: $*" >&2
    exit 2
    ;;
esac
