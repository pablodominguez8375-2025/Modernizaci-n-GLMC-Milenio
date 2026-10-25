# Handoff — 2026-10-23 — ChatGPT — Preflight sintético #690

## Motivo
Issue #678 solicita QA operativo con datos ficticios y registros reales controlados. PR #679 genera fixtures `PMGM-QA-OPERATIVE-DATASET-v1` pero **no las importa**. Este PR crea la validación **offline** previa a cualquier importador autenticado. No corresponde a la carga física de datos.

## Propiedad/coordinación
Agente: ChatGPT. Issue #690, rama `feature/qa-synthetic-preflight-690-20261023-gpt`, PR draft con base en `dev`. Archivos exclusivos nuevos: `scripts/qa/qa_synthetic_preflight.py`, `tests/test_qa_synthetic_preflight.py`, este handoff. No modificar PR #679 ni código UI de Claude.

## Contrato
Un archivo JSON suministrado localmente, ya existente, sólo sintetico con `manifest.schema=PMGM-QA-OPERATIVE-DATASET-v1`, `classification=SYNTHETIC_ONLY`, `import_status=NOT_IMPORTED`. Validación de estructura, límites, foreign keys, dominios de email de prueba, duplicados, identidad sintética. Reglas de mapeo explícitas de Organizations, People, Members, Memberships; IDs `Member.Id` y `Membership.Id` distintos para cumplir esquema EF. Áreas contables/hospitalaria/tenidas declaradas pendientes de mapeo; nada se aplica.

Salida: sólo resumen no personal, SHA y flags, sin JSON de personas, email, IDs de registros o cadenas de conexión. No API, DB, Keycloak, correo, Silo ni escritura en archivos.

## Gates y secuencia
Tests stdlib Python + CI/Showcase/Instalable exact HEAD, comprobar comparación contra dev antes de squash. Registrar en GitHub y Drive. No promover main, no alterar glmdch. Para carga efectiva se necesitará PR separado para importador administrativo transaccional con autorización fuerte y reversión, más QA físico aprobado.

## Implementación efectiva
- PR #679 fusionada a `dev@4b338241152b9666a9b329100d490b9c995c054b` con seis gates SUCCESS (PR #679); su dataset continúa NOT_IMPORTED.
- CLI `python3 scripts/qa/qa_synthetic_preflight.py --input /ruta/privada/lote.json` imprime SHA-256 y totales sanitizados o un código fijo de rechazo. No crea archivos ni usa red.
- Entidades de primera etapa planificadas: Organization, Person, Member, Membership, con derivación determinista de ID de Membership distinta de Member. **No aplica ni almacena esas entidades**.
- Nueva cobertura `tests/test_qa_synthetic_preflight.py` y workflow `.github/workflows/qa-synthetic-preflight.yml` (offline, checkout pinneado, cero secretos). Run #40051253725 SUCCESS para commit previo; repetir checks tras edición documental.
- PR #691 permanece draft. Su rama partió de dev anterior a #679; la conciliación automática de rama resultó bloqueada por seguridad de herramientas en este turno. **No hubo force update ni bypass**. Requiere verificar dev/HEAD vivo y rebase seguro autorizado, luego 3/3 gates exact-head antes de integración.
- Main y QA físico sin cambios; no comunicar que existen personas creadas ni lotes importados.

## Endurecimiento de frontera sintética — #692
La prevalidación `preflight()` ahora exige igualdad completa entre el JSON recibido y `qa_operational_fixtures.make_dataset(members, workshops, months)` del mismo repositorio. Esta verificación posterior al control estructural anterior rechaza **cualquier clave adicional en registros o manifiesto, cualquier texto alterado y cualquier mezcla de identidad no canónica**, devolviendo únicamente el código fijo `NON_CANONICAL_SYNTHETIC_DATA`. Los UUID y relaciones del lote se verifican antes, y el reporte de éxito permanece reducido a hashes, conteos y el contrato EF de entidades (sin filas ni PII). No hay write path ni siquiera cuando todo es canónico.

Las pruebas usan el propio generador oficial e incorporan inyecciones adversariales de campos no autorizados para cada sección, manifiesto y parámetros, así como cadenas de aspecto sintético con valores extra, y comprueban que el CLI no revela valores, rutas ni nombres. Workflow offline exact-head inicial SUCCESS en corrida 40053133541; repetir gates completos en nuevo SHA tras cualquier rebase.

No se crea un importador por este PR, no se ajusta el esquema SQL, no se toca glmdch ni se cambia main. El futuro importador administrativo requiere revisión distinta y debe preservar autenticación, rol, auditoría, idempotencia y aislamiento de datos reales.
