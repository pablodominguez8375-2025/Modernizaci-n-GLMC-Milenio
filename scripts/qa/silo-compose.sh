#!/usr/bin/env bash
# Sourced by QA scripts after their trusted env file. No server is started here.
qa_silo_cleanup(){
  if [ -n "${QA_SILO_MODEL_DIR:-}" ]; then rm -rf -- "$QA_SILO_MODEL_DIR"; fi
}
qa_silo_prepare(){
  local bundle="${PMGM_QA_SILO_BUNDLE_DIR:-}"
  if [ -z "$bundle" ] && [ -d "$ROOT/storage-bundle" ]; then bundle="$ROOT/storage-bundle"; fi
  [ -n "$bundle" ] || return 0
  if [ -f "$ROOT/BUILD-INFO.txt" ]; then
    PMGM_QA_SILO_EXPECTED_SOURCE_SHA="$(awk -F= '$1=="SOURCE_SHA" {print $2}' "$ROOT/BUILD-INFO.txt")"
  else
    PMGM_QA_SILO_EXPECTED_SOURCE_SHA="$(git -C "$ROOT" rev-parse HEAD)"
  fi
  export PMGM_QA_SILO_EXPECTED_SOURCE_SHA
  PMGM_QA_SILO_BUNDLE_DIR="$(cd -- "$bundle" && pwd)"
  export PMGM_QA_SILO_BUNDLE_DIR
  QA_SILO_MODEL_DIR="$(mktemp -d)"
  trap qa_silo_cleanup EXIT
  COMPOSE_FILE="$QA_SILO_MODEL_DIR/compose.json"
  python3 "$ROOT/scripts/qa/prepare-silo-archive.py" --bundle "$PMGM_QA_SILO_BUNDLE_DIR" \
    --source-sha "$PMGM_QA_SILO_EXPECTED_SOURCE_SHA" --compose-output "$COMPOSE_FILE" >/dev/null \
    || { echo 'Archivo Silo QA bloqueado; no se inicia ni restaura el stack.' >&2; return 1; }
}
