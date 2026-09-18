# 2026-09-18 — Automatización de despliegue QA srv01 v1

## Objetivo

Eliminar pasos manuales innecesarios para desplegar en `srv01` el mismo SHA publicado en GitHub Pages y generado como instalable QA.

## Cambios

- Keycloak QA pasa a persistir en PostgreSQL `pmgm_keycloak`.
- PostgreSQL QA queda inicializado con `pmgm` + `pmgm_keycloak`.
- nombre de proyecto Compose fijo: `pmgm-srv01`.
- instalador crea `pmgm_keycloak` de forma idempotente si el volumen ya existía.
- smoke valida ambas bases PostgreSQL.
- nuevos scripts:
  - `backup-srv01-qa.sh`;
  - `verify-srv01-backup.sh`;
  - `restore-srv01-qa.sh`;
  - `deploy-srv01-from-pages.sh`;
  - `rollback-srv01-qa.sh`.
- Pages publica, además de la demo:
  - `downloads/qa-current.json`;
  - ZIP QA del mismo SHA.
- el deploy de srv01:
  - exige paridad SHA Pages / dev;
  - valida SHA-256 externo;
  - valida `BUILD-INFO.txt`;
  - valida `MANIFEST.sha256`;
  - conserva releases por SHA;
  - genera respaldo previo cuando existe un despliegue anterior;
  - instala y ejecuta smoke;
  - prepara regresión QA de 21 controles.
- documentación de autodeploy y rollback.
- `PMGM-NEXT-001` actualizado: srv01 es hito bloqueante antes de abrir el siguiente incremento de insinuaciones.

## Seguridad

- no se publican secretos en Pages;
- `/etc/pmgm/srv01.env` permanece fuera de Git;
- el ZIP público sólo contiene código ya público, datos ficticios y configuración de ejemplo;
- no se habilitan datos personales reales;
- producción sigue requiriendo perfil institucional con TLS y gates separados.

## Criterio de integración

- PMGM CI verde;
- PMGM Showcase Demo verde;
- QA srv01 Installable verde;
- Pages post-merge publica demo + ZIP del mismo SHA;
- Issue #97 sigue abierto hasta ejecutar físicamente deploy/smoke/regresión en srv01.
