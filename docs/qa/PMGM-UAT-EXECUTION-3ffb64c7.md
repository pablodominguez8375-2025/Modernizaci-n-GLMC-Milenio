# Proyecto Centenario — Ejecución UAT v1.0.0-rc1 / 3ffb64c7

## Candidato congelado

Esta ejecución sustituye, para fines de aceptación, al candidato histórico `739ba0b3...` y mantiene sin cambios los 20 casos UAT definidos para `v1.0.0-rc1`.

- SHA bajo prueba: `3ffb64c7a188c2bd4130f616928b2a657844a8aa`
- Rama congelada: `uat/v1.0.0-rc1-3ffb64c7`
- Fuente de integración: `dev`
- Entorno UAT objetivo: `pilot-operational`
- Evidencia procesable: `release/PMGM-UAT-1.0.0-rc1-3ffb64c7-evidence.json`
- CI base: run `34665624090`, resultado `success`
- Pre-UAT instalable: run `34665624111`, resultado `success`
- Showcase GitHub Pages: run `34665624081`, resultado `success`

## Regla de aceptación

La automatización verde es pre-evidencia técnica. No convierte por sí sola un caso UAT en `pass`. Cada uno de los 20 casos conserva estado `pending` hasta ser ejecutado/observado en el entorno de aceptación y registrar una referencia de evidencia suficiente. La promoción a `main` permanece bloqueada hasta 20/20 `pass` y decisión expresa del Sponsor/Product Owner.

## Pre-evidencia automática disponible

El run CI `34665624090` corresponde exactamente al SHA congelado y terminó con los cinco jobs principales en `success`:

1. `Infrastructure configuration`.
2. `First implementation authenticated smoke`, incluyendo smoke autenticado y drill de backup/recovery.
3. `Backend build and tests`, incluyendo privacidad Ley 21.719, clasificación, migraciones, PostgreSQL, S3/MinIO y ClamAV.
4. `Frontend lint and build`, incluyendo prueba de sesión OIDC y frontera API.
5. `Pilot operational HTTPS and recovery`, incluyendo preflight, HTTPS/OIDC y drill de backup/recovery.

Además, el fix previo de ceremonias dejó regresión HTTP para comprobar que la autorización permanece bloqueada antes del visto bueno de Gran Maestría y se habilita al completar los requisitos.

## Matriz UAT

| Caso | Área | Estado UAT | Pre-evidencia técnica | Acción de aceptación |
| --- | --- | --- | --- | --- |
| UAT-001 | Identidad | pending | HTTPS/OIDC smoke + sesión frontend verdes | Login y logout institucional controlado |
| UAT-002 | Autorización | pending | Frontera API/autorización cubierta por CI | Recorrer Superadmin / Gran Logia / Taller |
| UAT-003 | Bootstrap | pending | Scripts/stack reproducibles | Dry-run, apply y segundo apply sin duplicación |
| UAT-004 | Membresía | pending | Backend green | Validar ficha y grado con usuario ficticio |
| UAT-005 | Membresía | pending | Backend green | Ejecutar traslado y revisar ambos historiales |
| UAT-006 | Régimen Interior | pending | Backend green | Revisar reportes Orden/Taller |
| UAT-007 | Calidad de datos | pending | Backend green | Crear/asignar/resolver caso deduplicado |
| UAT-008 | Regularidad | pending | Backend green | Confirmar Tesorería/Hospitalaria en elegibilidad |
| UAT-009 | Ceremonias | pending | Regresión de bloqueo verde | Confirmar bloqueo por requisito faltante |
| UAT-010 | Ceremonias | pending | Regresión de autorización verde | Completar requisitos y autorizar |
| UAT-011 | Insinuados | pending | Backend green | Verificar plazo y minimización del portal |
| UAT-012 | Gran Secretaría | pending | Backend green | Reserva válida + conflicto rechazado |
| UAT-013 | Calendario | pending | Frontend/backend green | Confirmar proyección y masking `Ocupado` |
| UAT-014 | Gestión Logial | pending | Backend green | Operación en Taller propio + denegación externa |
| UAT-015 | Documentos | pending | MinIO + ClamAV integration green | Carga válida, hash y nueva versión |
| UAT-016 | Documentos | pending | ClamAV integration green | Rechazo de archivo inválido/riesgoso |
| UAT-017 | Biblioteca | pending | Backend/frontend green | Comparar catálogos por grado/perfil |
| UAT-018 | Gran Archivero | pending | Backend green | Acceso autorizado/denegado y exclusión de planchas |
| UAT-019 | Notificaciones | pending | Backend/frontend green | Recibir, leer y verificar aviso obligatorio |
| UAT-020 | Recuperación | pending | Dos drills automáticos verdes | Backup/restore/smoke en entorno UAT objetivo |

## Orden de ejecución

- Ola A: UAT-001 a UAT-005.
- Ola B: UAT-006 a UAT-010.
- Ola C: UAT-011 a UAT-014.
- Ola D: UAT-015 a UAT-019.
- Ola E: UAT-020, gate de evidencia y decisión formal.

## Guardrails

- Sólo datos ficticios o expresamente autorizados.
- No subir contraseñas, tokens, secretos, llaves ni datos personales reales a Git.
- Capturas/documentos sensibles se referencian por identificador controlado, no se almacenan en este repositorio.
- CENDOC permanece fuera del alcance.
- Biblioteca Virtual y Gran Archivero siguen siendo dominios separados.
- Las planchas de trabajo autorizadas pertenecen a Biblioteca Virtual y no a Gran Archivero.

## Gate final

Ejecutar, inicialmente permitiendo pendientes:

```bash
python3 tests/uat_evidence_gate.py release/PMGM-UAT-1.0.0-rc1-3ffb64c7-evidence.json --allow-pending
```

La validación final sin `--allow-pending` sólo debe ejecutarse cuando existan 20/20 `pass`, URL HTTPS del entorno, timestamp de ejecución, evidencia por caso y aprobación formal del Sponsor/Product Owner.
