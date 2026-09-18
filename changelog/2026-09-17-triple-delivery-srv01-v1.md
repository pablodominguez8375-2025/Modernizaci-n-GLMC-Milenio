# 2026-09-17 — Triple entrega automática Pages + QA srv01 v1

## Objetivo

Materializar la Definition of Done permanente del Proyecto Centenario: el mismo corte de `dev` debe producir código integrado, demo funcional GitHub Pages e instalable reproducible para `srv01`.

## Implementado

- GitHub Pages publica en cada push a `dev`, eliminando excepciones históricas por rama.
- Nuevo stack `infrastructure/docker-compose.srv01.yml` para QA operacional en el servidor real.
- Configuración segura de ejemplo `infrastructure/srv01.env.example`.
- Preflight específico compatible con el baseline de recursos de `srv01`.
- Instalador `scripts/install-srv01-qa.sh`.
- Smoke autenticado `scripts/smoke-srv01-qa.sh`.
- Generador reproducible `scripts/build-qa-installable.sh`.
- Workflow `Proyecto Centenario QA srv01 Installable` en cada push/PR a `dev`.
- ZIP con SHA exacto, `BUILD-INFO.txt`, `MANIFEST.sha256`, Git bundle, código, infraestructura, migraciones y launcher `INSTALAR.sh`.
- Realm QA ampliado con los ocho cargos reglamentarios del Taller y usuarios ficticios correspondientes.
- Callback OIDC QA parametrizable para acceso remoto a `srv01`.
- Excepción frontend HTTP exclusiva de QA interno: aplicación y OIDC deben compartir hostname y el build debe habilitar expresamente `VITE_OIDC_ALLOW_HTTP_QA=true`.
- Tests frontend para aceptar QA interno y rechazar autoridad OIDC en host distinto.
- Guía `docs/installation/QA-SRV01-INSTALL.md`.

## Seguridad

- Sin credenciales reales versionadas.
- Datos ficticios en QA.
- PostgreSQL, MinIO y ClamAV no exponen puertos del host.
- HTTP remoto se limita al perfil QA; el piloto/producción continúa exigiendo HTTPS.
- El instalable no se declara desplegado hasta ejecutar instalación y smoke en `srv01`.

## Gates requeridos

Antes de merge:
- PMGM CI verde;
- PMGM Showcase Demo verde;
- workflow QA srv01 Installable verde;
- paquete generado y manifiesto verificado sobre el HEAD exacto.

Después del merge:
- Pages debe publicar el SHA de `dev`;
- el workflow QA debe generar el ZIP del mismo SHA;
- despliegue en `srv01` queda pendiente hasta disponer de ejecución directa sobre el servidor.
