#!/usr/bin/env bash
# Verifica la PROCEDENCIA de la lista de migraciones embebida en qa-a0-diag.sh contra git (solo lectura del repositorio, sin red ni host).
# Uso (desde un clon con ambos commits): bash scripts/qa/a0/provenance-check.sh
set -eu -o pipefail
BASE=da274d651d8e9e056525413406c0aab62131504f      # corte que corre QA real (postflight MANUAL_APPLY_VERIFIED_API_WEB, #525 comentario 6966428945)
TARGET=5563d8909e63aba43abb270938842c49d9140a99    # dev tras #1045 (release candidato del corte de Talleres)
EXPECTED_BASE_SHA=50896346f08f4cf9d6613e1dd663bbb9f305fd6f863e6676093ab58e939dd744
EXPECTED_DELTA="20261118150000_AddWorkshopRegistryState 20261118170000_AddWorkshopRetiredGuard 20261119010000_AddWorkshopStatusTransitions 20261119010100_AddWorkshopStatusTransitionsAppendOnly"
ids() { git ls-tree -r --name-only "$1" | grep -E '/Migrations/[0-9]{14}_[A-Za-z0-9]+\.cs$' | grep -v Designer | sed -E 's#.*/Migrations/([0-9]{14}_[A-Za-z0-9]+)\.cs#\1#' | LC_ALL=C sort; }
B="$(ids "$BASE")"; T="$(ids "$TARGET")"
[ "$(printf '%s\n' "$B" | sha256sum | cut -d' ' -f1)" = "$EXPECTED_BASE_SHA" ] || { echo "STOP: la lista de $BASE no coincide con el hash embebido" >&2; exit 2; }
REMOVED="$(comm -23 <(printf '%s\n' "$B") <(printf '%s\n' "$T"))"
ADDED="$(comm -13 <(printf '%s\n' "$B") <(printf '%s\n' "$T") | tr '\n' ' ' | sed 's/ $//')"
[ -z "$REMOVED" ] || { echo "STOP: migraciones quitadas entre base y release: $REMOVED" >&2; exit 2; }
[ "$ADDED" = "$EXPECTED_DELTA" ] || { echo "STOP: el delta de migraciones no es el esperado: $ADDED" >&2; exit 2; }
echo "PROVENANCE=OK base=$BASE(100 ids, sha256 $EXPECTED_BASE_SHA) release=$TARGET delta=4 quitadas=0"
echo "release_ids_sha256=$(printf '%s\n' "$T" | sha256sum | cut -d' ' -f1)"
