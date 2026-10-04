# PMGM-DB-009 — Acceso efectivo a Hospitalaria del Taller

Issue #266 / PR #322; base dev `a07aab8e37a8998812850de61e0b80a5503f69cd`. Extiende [DB-005](PMGM-DB-005-control-acceso-dinamico.md), preserva [DB-008](PMGM-DB-008-acceso-efectivo-tarifario.md).

## Contrato derivado privado

GET `/api/gestion-logial/hospitalaria/talleres/{organizationId}/acceso`, autenticado y limitado por CanReadLodgeHospitalaria. Cache-Control private, no-store; sin terceros, asignaciones ni antecedentes de ayudas.

| Campo | Tipo lógico / JSON | Nulable / default | Significado, validación y clasificación |
|---|---|---|---|
| version | entero / number | no / versión actual | Derivado del snapshot vigente, técnico privado |
| organizationId | UUID / string | no / Taller solicitado | Ámbito exacto de autorización; técnico privado |
| managed | booleano / boolean | no / false | Existe asignación del sujeto exacto en ese Taller, incluso revocada/futura/vencida; privado |
| actions | lista / string[] | no / [] | Subconjunto view/create/write, intersección con atribuciones existentes; privado |

Sin persistencia nueva ni migración. `core.dynamic_access_snapshots` conserva PK Id, versión única, payload JSONB inmutable; asignación → perfil por código en snapshot y OrganizationId exacto. Se preservan relaciones, campos, decimales, PK/FK, índices, unicidad y auditoría existentes de movimientos, rendiciones, obligaciones, pagos y transferencias. Sin borrado ni transformación de filas.

## Regla derivada

Sujetos sin asignaciones en el Taller conservan el autorizador anterior. Sujetos administrados requieren asignación activa y vigente en fecha civil de Chile, perfil custom/menú/vista activos y hospitalaria/view junto a la acción requerida. Revocar/vencer/desactivar no restaura el acceso anterior. Asignaciones de Orden u otro Taller no sustituyen este ámbito.

| Operación local | Acción técnica | Autoridad institucional preservada |
|---|---|---|
| Resumen, rendiciones, reposiciones individuales | view | lectura Hospitalaria del Taller |
| Acuerdos y revisiones del Consejo | view | gestión Hospitalaria del Taller |
| Registrar movimiento | create | gestión Hospitalaria del Taller |
| Preparar/enviar rendición, registrar pago o transferencia | write | gestión Hospitalaria del Taller |
| Autorizar egreso como Venerable | write | Venerable del Taller real del movimiento |
| Vincular autorización del Consejo | write | Hospitalario + acuerdo válido del mismo Taller y monto |

Para IDs de movimiento/rendición/obligación se usa el Taller persistido, no un parámetro adicional del cliente. La proyección no concede una acción institucional nueva ni simula una autorización del Consejo.

Frontend bloquea datos/formularios mientras comprueba acceso; retira formularios sin create/write y oculta datos ante cambio de Taller, sujeto, período o versión/grants. Las respuestas locales tardías no se aplican a otro contexto. Refresco por catálogo, foco y cada 30 s; servidor comprueba cada petición. Demo reproduce restricciones técnicas; los roles visuales siguen intersectados con capacidades existentes. No certificar que el adaptador sintético sustituya las autorizaciones JWT reales.

Gran Hospitalaria, regularidad institucional, navegación global, impresión/exportación y otros módulos quedan fuera de cobertura. No cambia tarifa, firma, autoridad, retención ni aprobación institucional. Schema/despliegue físico y UAT sin certificar; srv01 pausado.
