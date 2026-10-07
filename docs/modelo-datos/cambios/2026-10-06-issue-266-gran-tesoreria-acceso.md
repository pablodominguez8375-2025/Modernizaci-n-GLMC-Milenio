# #266 / #350 — Acceso efectivo de Gran Tesorería

Base: dev89772b2d72639e09872b2b1f5bfceee65fca17cc. Rama feature/grand-treasury-access-20261006-gpt. Extiende [registro anterior de Orden/Hospitalaria](2026-10-06-issue-266-gran-hospitalaria-acceso.md); conserva el catálogo/snapshot JSONB y los grants treasury ya usados por tarifario. SHA exacto, gates y publicación en recibo de cierre de PR/Issue/Línea Base.

## Diccionario del contrato nuevo

GET /api/tesoreria/acceso autenticado, sólo sujeto de sesión con CanManageTreasuryRegularity. private/no-store. No recibe sujeto ni organizationId; no expone catálogo, sujetos, roles ni asignaciones.

| Campo | Tipo / nulabilidad | Regla / clasificación |
|---|---|---|
| version | int32, requerido | Versión del snapshot técnico vigente; metadato interno de acceso |
| managed | boolean, requerido | Derivado: cualquier asignación histórica exacta del sujeto con OrganizationId null, incluso revocada, expirada o futura; metadato privado |
| actions | array string, requerido | Subconjunto view/create/write evaluado por grants treasury de Orden, perfil/menu/vista activos, vigencia civil inclusiva America/Santiago y view obligatorio; metadato privado |

Sin historial de Orden se conserva acceso institucional legacy. Un grant de Taller nunca confiere autoridad de Orden. El grant técnico restringe: no crea cargo, firma, aprobación o autoridad. create sigue asociado al tarifario existente; write a conciliación, abonos y registro manual; view a lectura administrativa.

## Antes / después

| Operación | Antes | Después |
|---|---|---|
| Cuadros mensuales lista/detalle de Gran Tesorería | Autoridad institucional | Autoridad + treasury/view Orden; lectores locales conservan política propia |
| Conciliar cuadro | Autoridad institucional | Autoridad + treasury/write Orden, antes de mutar; conciliación/cálculo/auditoría preservados |
| Regularidad Taller y miembro: registro | Autoridad institucional | Autoridad + treasury/write Orden; snapshots append-only preservados |
| Regularidad Taller y miembro: lectura administrativa | Autoridad institucional | Autoridad + treasury/view Orden; proyección mínima status/asOfDate de otros lectores institucionales preservada |
| Derechos ceremoniales listado / abono | Autoridad institucional | view / write de Orden además de autoridad; importes, comprobantes e idempotencia preservados |
| Consulta zonas | Autoridad institucional | view de Orden para autoridad superior; lectura local de Oriente preservada |
| Pantalla Gran Tesorería | Controles por cargo | Consulta propia previa; cierre de vistas al perder view; controles de conciliación/regularidad/abono sólo con write; remonta al cambiar cliente, sujeto, versión o sección |

No se alteran DTO de pagos, cuadros ni regularidad. Proyecciones compartidas de tarifario oficial usadas por otros módulos no se amplían ni se convierten en datos administrativos. La API real y el adaptador sintético aplican el mismo corte; las fixtures de flujos autorizados identifican ahora el cargo Gran Tesorero. La demo minimiza la regularidad para lectores no administrativos.

## Estructuras e impacto

Sin migración: no cambian entidades, columnas, PK/FK, índices, cardinalidades, retención, precisión monetaria ni borrado. Se reutilizan DynamicAccessSnapshot, FinancialRegularitySnapshot, TreasuryMonthlyStatement y CeremonyRightPayment. No hay transformación de registros existentes. La evaluación deriva del snapshot vigente por solicitud, sin caché compartida por sujeto.

Frontend invalida acceso ante mutación de catálogo, foco y cada30 segundos; la comprobación inicial o invalidación explícita oculta las vistas; un error deniega. El sondeo conserva formularios si el acceso no cambia y los desmonta cuando la respuesta cambia versión/capacidades. Respuestas de regularidad se descartan al cambiar Taller/fecha y al desmontar; el contexto de acceso desmonta las subpáginas. Una solicitud ya autorizada y ejecutada no se revierte al cambiar la vista: se impide publicar su respuesta en el contexto nuevo.

## Verificación y límites

Dos tests unitarios nuevos de histórico/vigencia/ámbito; HTTP PostgreSQL de consulta sin escritura, denegaciones sin éxito de auditoría, autoridad local insuficiente, conciliación/regularidad autorizadas, revocación y proyección mínima. Pruebas demo de denegación directa/roles/revocación y UI de consulta sin edición. Frontend completo, lint/build, privacy/classification/migration y gates exact-head requeridos.

Navegación global en App.tsx pendiente por regla de archivos calientes y #116 histórica abierta; otros módulos, impresión/exportación independiente y regularidad individual Hospitalaria fuera de este corte. No modifica #190/#191, importes, reglas contables ni identidad. main congelada; srv01/UAT pausados #97, despliegue QA pendiente. CI/artefactos no acreditan instalación o aceptación institucional.
