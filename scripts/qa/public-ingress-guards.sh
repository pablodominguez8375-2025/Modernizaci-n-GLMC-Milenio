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
# DB2 must never be silently applied to the legacy persistent PostgreSQL volume.
qa_db2_require_empty_volume(){
  [ "${PMGM_QA_DB2:-false}" = true ] || return 0
  local volume="${PMGM_QA_COMPOSE_PROJECT:-pmgm-srv01}_pmgm_srv01_postgres_data" names
  [ "${PMGM_QA_DB2_NEW_VOLUME_ACK:-}" = CONFIRMO_VOLUMEN_NUEVO ] || {
    echo 'BD2 QA exige confirmación explícita de volumen nuevo.' >&2; return 1;
  }
  names="$(docker volume ls --format '{{.Name}}')" || {
    echo 'BD2 QA bloqueado: no se pudieron verificar volúmenes.' >&2; return 1;
  }
  if printf '%s\n' "$names" | grep -Fxq -- "$volume"; then
    echo 'BD2 QA bloqueado: volumen PostgreSQL preexistente, migración DBA requerida.' >&2
    return 1
  fi
}
qa_https_require_data_only_restore(){
  if [ "${PMGM_QA_HTTPS:-false}" = true ] && [ "${DATA_ONLY:-false}" != true ]; then
    echo 'Restore HTTPS QA exige --data-only: revisar realm restaurado antes de reabrir web/Keycloak.' >&2
    return 1
  fi
}
