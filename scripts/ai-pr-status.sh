#!/usr/bin/env bash
# Resumen de una línea por PR para humanos o IA: head, borrador, mergeable, CI exact-head
# y último comentario. Solo lectura (REST; GraphQL está bloqueado en el arnés).
# Uso: scripts/ai-pr-status.sh 852 854 859        (repo por defecto o REPO=owner/name)
set -u
REPO="${REPO:-pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio}"
[ "$#" -gt 0 ] || { echo "uso: $0 <PR> [<PR> ...]"; exit 2; }
for n in "$@"; do
  case "$n" in *[!0-9]*|'') echo "PR inválido: $n"; continue ;; esac
  pr=$(gh api "repos/$REPO/pulls/$n" --jq '[.head.sha,.state,(.merged|tostring),(.draft|tostring),(.mergeable_state // "?"),.base.ref]|@tsv' 2>/dev/null) \
    || { echo "#$n no accesible"; continue; }
  IFS=$'\t' read -r sha state merged draft mstate base <<<"$pr"
  ci=$(gh api "repos/$REPO/commits/$sha/check-runs" --paginate \
        --jq '[.check_runs[]|.conclusion//"running"]|group_by(.)|map("\(.[0]):\(length)")|join(" ")' 2>/dev/null)
  last=$(gh api "repos/$REPO/issues/$n/comments" --jq '.[-1]|"\(.id) \(.created_at) \(.body[0:110]|gsub("\n";" "))"' 2>/dev/null)
  echo "#$n ${sha:0:8} $state merged=$merged draft=$draft base=$base mergeable=$mstate CI[${ci:-sin checks}] ultimo: ${last:-sin comentarios}"
done
