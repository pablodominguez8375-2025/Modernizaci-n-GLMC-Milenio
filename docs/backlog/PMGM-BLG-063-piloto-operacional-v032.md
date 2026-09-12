# PMGM-BLG-063 — Piloto operacional v0.32

Estado: **En desarrollo**

## Alcance
- `docker-compose.pilot.yml` separado de QA/first-implementation.
- Caddy como único punto público HTTP/HTTPS.
- Keycloak 26.7.3 en server mode + PostgreSQL `pmgm_keycloak`.
- Realm piloto sin usuarios QA; bootstrap institucional parametrizado y contraseña temporal.
- Authorization Code + PKCE; password grant deshabilitado.
- `DemoData=false`.
- Preflight Bash/PowerShell y launchers de piloto.
- Smoke HTTPS/OIDC.
- Backup/verify/restore de `pmgm`, `pmgm_keycloak` y `pmgm-documents`.
- Recovery drill CI-only con sondas temporales y smoke posterior.
- CI debe demostrar que sólo gateway publica puertos.

## Criterio de terminado
PR con head exacto completamente verde en backend, frontend, infraestructura, primera implementación existente y job `Pilot operational HTTPS and recovery`; squash merge a `dev` sólo después de ese resultado.
