# QA525 — E2E sintético por API (Sistema›Talleres + Carga inicial)

Biblioteca inerte `scripts/qa/qa525-synthetic-e2e.py` (sin red ni archivos; el HTTP autenticado lo inyecta quien la use).
Datos exclusivamente sintéticos (RUT 30.000.000+, `@example.org`, `QA-SINT-Tnnn`); nunca datos reales.

- `smoke(http, expected_sha, unauth=None)`: solo lectura / preview (writes=0): salud, `qa-version.json` == SHA, contrato `INITIAL-LOAD-CSV-v1`, plantilla ZIP, lista de Talleres, preview válido sin escrituras, bloqueos `MEMBER_BATCH_LIMIT_30` / `ONE_WORKSHOP_PER_BATCH_REQUIRED` / `CROSS_WORKSHOP_BATCH`, y rechazo sin credenciales.
- `uat_checks(http, expected_sha, unauth=None)`: tupla de callables para el paso UAT del corte de esquema (usa `smoke`, una sola vez).
- `rehearsal(http, lots, clock)`: ESCRIBE lotes sintéticos (la ventana debe abrirla el Sponsor): primer apply, reintento `alreadyApplied`, rechazos, volumen (74 lotes / 70 Talleres / 1.008 Hermanos de referencia), 2.º lote del mismo Taller reutiliza el Taller, conflicto de persona existente sin aplicar.
- `generate(seed=525)`: generador determinista; `mutate(lot, kind)`: variantes inválidas.

Pruebas: `tests/test_qa525_synthetic_e2e.py` contra un fake en memoria con las reglas de `InitialLoadApplicabilityPolicy`; cada bug inyectado (límite 31, sin regla de 1 Taller, sin cross-workshop, preview que escribe, sin conflicto) es detectado.
Sin autoridad: no abre ventana, no usa credenciales, no interviene `glmdch`. QA_DEPLOY=NO_GO.
