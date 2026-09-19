# Proyecto Centenario — handoff operativo PR #116 / srv01

**Fecha de corte:** 19-09-2026  
**Estado:** candidato pre-merge validado automáticamente; despliegue físico y UAT pendientes.

## 1. Estado vivo verificado

- `dev`: `40a0fbfba30b7efd2399ab5cb6e8f8017ae90d9e`.
- `main`: `6dfb9546a4873baff15955cf86abfd7d47e3d111`.
- PR #116: draft, abierto y no fusionado.
- rama PR #116: `feature/admissions-2026-completion`.
- HEAD candidato: `813edb9af016f8d0f55c799d91b405c8e41f8fb4`.
- PR #119: documental, draft, separado y no fusionado.
- HEAD PR #119 al iniciar este registro: `4be337be1365c51da85acfcca464abf9726ad853`.

No mover `dev` ni fusionar PR #116 o PR #119 antes de cerrar la validación física del candidato, salvo decisión explícita que incluya sincronizar la rama y repetir todos los gates del HEAD resultante.

## 2. Gates y artifact del candidato

Gates exact-head confirmados para `813edb9af016f8d0f55c799d91b405c8e41f8fb4`:

- PMGM CI #1331: `SUCCESS`.
- PMGM Showcase Demo #556: `SUCCESS`.
- QA srv01 Installable #194: `SUCCESS`.

Artifact QA pre-merge:

- ID: `10589425902`.
- run: `35458495195`.
- nombre: `proyecto-centenario-qa-srv01-813edb9af016f8d0f55c799d91b405c8e41f8fb4`.
- SHA-256 del contenedor: `2857669f9d015386c2004c7064a1ea0a6628b54f6382f6791c9270b68b4bbc5f`.
- expiración registrada: `2026-10-19T17:34:18Z`.

Verificación independiente realizada el 19-09-2026:

- el contenedor descargado coincide con el SHA-256 registrado;
- contiene `Proyecto-Centenario-QA-srv01-813edb9af016.zip`;
- `BUILD-INFO.txt` declara el `SOURCE_SHA`, `SOURCE_REF` y `BUILD_RUN_ID` correctos;
- `sha256sum -c MANIFEST.sha256` valida todos los archivos;
- están presentes instalador, Compose, preflight, backup, restore, rollback, smoke y kit de regresión;
- la plantilla de regresión contiene `QA-001..QA-026`.

Evidencia registrada en Issue #97, comentario `5744663094`.

## 3. Regla especial para este despliegue

PR #116 permanece fuera de `dev`. Por ello, `qa-current.json` de GitHub Pages no representa el candidato `813edb9a` y no debe usarse `deploy-srv01-from-pages.sh` para atribuirle ese SHA.

La QA física pre-merge debe usar el artifact exacto ID `10589425902`, transferido a `srv01` por un operador autorizado. No guardar tokens o secretos en el repositorio, artifact o evidencia.

## 4. Secuencia operacional pendiente

1. Transferir el artifact exacto a `srv01`.
2. Extraer el ZIP de producto bajo `/opt/centenario/releases/813edb9af016f8d0f55c799d91b405c8e41f8fb4`.
3. Verificar `SOURCE_SHA` y ejecutar `sha256sum -c MANIFEST.sha256`.
4. Confirmar `/etc/pmgm/srv01.env` sin valores `REEMPLAZAR_*`.
5. Generar backup previo de PostgreSQL aplicación, PostgreSQL Keycloak y MinIO cuando exista instalación operativa.
6. Ejecutar `INSTALAR.sh --env /etc/pmgm/srv01.env`.
7. Ejecutar smoke autenticado y comprobar persistencia e infraestructura interna.
8. Preparar la evidencia mediante `scripts/prepare-srv01-regression.sh`.
9. Ejecutar y documentar `QA-001..QA-026`; el gate debe devolver `26/26 pass`.
10. Ejecutar UAT institucional, incluyendo `UAT-017`.
11. Registrar resultados, defectos y decisión del Product Owner en Issue #97.

## 5. Terminología obligatoria durante QA/UAT

- `Cuadro del Taller`: hermanos pertenecientes a un Taller.
- `Cuadro General de la Orden`: vista consolidada institucional.
- `Padrón de la Gran Asamblea`: sólo electores vigentes habilitados para la Gran Asamblea.

No usar “padrón” como sinónimo de membresía, Cuadro, asistencia o nómina genérica.

## 6. Criterio de continuidad

Mientras no exista evidencia física en Issue #97, el estado correcto es:

> Artifact íntegro y gates automáticos verdes; despliegue `srv01`, QA 26/26 y UAT-017 pendientes. PR #116 y PR #119 permanecen draft y sin fusionar; `dev` y `main` no cambian.

