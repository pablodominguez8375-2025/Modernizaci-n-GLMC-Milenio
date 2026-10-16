#!/usr/bin/env bash
# Narrow guards for the first-install-only public QA profile. No mutations.
qa_https_require_empty_volume(){
  [ "${PMGM_QA_HTTPS:-false}" = true ] || return 0
  local names volume="${PMGM_QA_COMPOSE_PROJECT:-pmgm-srv01}_pmgm_srv01_postgres_data"
  names="$(docker volume ls --format '{{.Name}}')" || {
    echo 'HTTPS QA bloqueado: no se pudo comprobar volúmenes.' >&2; return 1;
  }
  if printf '%s\n' "$names" | grep -Fxq -- "$volume"; then
    echo 'HTTPS QA bloqueado: volumen existente requiere migración revisada del realm/BD.' >&2
    return 1
  fi
}
qa_https_require_data_only_restore(){
  if [ "${PMGM_QA_HTTPS:-false}" = true ] && [ "${DATA_ONLY:-false}" != true ]; then
    echo 'Restore HTTPS QA exige --data-only: revisar realm restaurado antes de reabrir web/Keycloak.' >&2
    return 1
  fi
}
