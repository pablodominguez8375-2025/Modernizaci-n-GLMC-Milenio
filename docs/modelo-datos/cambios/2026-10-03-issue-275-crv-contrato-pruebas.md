# Registro de salida — CRV, contrato y pruebas

Issue #275 / PR #276, rama `feature/admissions-residual-275`. Corte previo [fecha CRV](2026-10-03-issue-275-fecha-carta-retiro.md) preservado. Base de rama `8dca4e5bfc0827163d2f742b4ce3ca4e34a483df`; integración de base dev `b070dbc71d25f811f0b70999dab8f378a5343cc8` mediante merge automático local. Sin integración a dev ni schema instalado. SHA entregado en recibo GitHub/Drive.

## Diccionario parcial y estructuras

| Campo/regla | Antes | Ahora | Tipo/nulos/validación |
|---|---|---|---|
| WithdrawalLetterGrantedDate persistido | Columna añadida en rama | Sin cambio | PostgreSQL date / DateOnly?, nullable sin default; fecha de otorgamiento, no carga; legado null |
| withdrawalLetterGrantedDate respuesta frontend | Omitido del DTO y demo | Declarado y devuelto por demo/API | string ISO YYYY-MM-DD o null, refleja el contrato backend existente |
| affiliationMode en demo | Admitía valor incoherente sin fecha válida | Rechaza fecha ausente/inválida/futura y modalidad contradictoria | simple/activation; hasta CRV + 3 meses calendario inclusive simple; después activation |
| Cálculo backend al crear | Inline, reloj consultado repetidamente | Política pura comprobable y fecha de Chile capturada una vez | Sin cambio normativo; nunca infiere fecha legacy ni modalidad de null; evita overflow en DateOnly.MaxValue |

Sin migración adicional: no cambia schema, PK/FK, índices, relaciones, cardinalidades, defaults ni borrados; conserva la migración nullable anterior. Sin backfill, reclasificación de casos existentes, cambio de permisos/retención/tarifas o reportes. La respuesta añade un campo ya presente en backend; consumidores restantes se conservan. Datos de pruebas sintéticos, sin documentos ni identidades reales. Fecha y modalidad son datos institucionales bajo los controles de Admisiones vigentes.

## Pruebas y límites

11 casos backend de política: límite inclusivo/día siguiente, fin de mes, año bisiesto, futuro, legacy null y extremo del tipo. Seis casos HTTP PostgreSQL: simple/activation válidos, modalidad manipulada en ambos sentidos, futuro y ausencia; sólo altas válidas persisten fecha y PersonId/MemberId originales. Migración ejecutada por la prueba sobre DB CI cuando PMGM_TEST_POSTGRES existe. Sin esa variable siguen el contrato ambiental de suites existentes y retornan sin ejecutar DB; no contarlos como evidencia PostgreSQL sin revisar logs/TRX del job con DB.

Ocho casos frontend de adaptador/contrato: fecha/modalidad válidas, contradicciones, futuro/inválida/ausente y transporte real hacia endpoint. Resultados locales y gates exactos constan en recibo. No SDK .NET local: pruebas backend deben aprobar en CI antes de atribuir validación.

Pendientes: búsqueda segura de retirados/de otros Talleres y personas externas; validación de fecha contra carta revisada; decisiones posteriores al alta; circuito completo/autoridades de comisión y materialización idempotente. La demo todavía no persiste ni resuelve un circuito completo: este corte sólo corrige el alta y su contrato de fecha. No declaración de equivalencia integral ni UAT.

Main congelado, srv01 pausado, despliegue QA pendiente. ZIP final y trazabilidad propia se verifican aparte; no reutilizar evidencias de 8dca4e5.
