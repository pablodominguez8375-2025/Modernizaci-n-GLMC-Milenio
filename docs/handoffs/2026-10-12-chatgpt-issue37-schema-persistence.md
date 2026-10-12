# Handoff — Issue #37 — persistencia inmutable de formularios (fase 2a)

**Corte verificado:** 12-10-2026, Chile. PR funcional [#454](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/pull/454) integrada por squash a `dev@9165990c76ae8388346cb99db68b4a34fe9b5d35`. `main@6dfb9546a4873baff15955cf86abfd7d47e3d111` sin promoción. Issue [#37](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/37) **ABIERTA**; agente:chatgpt. `srv01`/UAT física pausada por [#97](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/97).

## Alcance entregado en código real

1. Nueva entidad `FormSchemaVersion` y tabla `core.form_schema_versions` con versión exacta, código, ámbito, vigencia, contenido serializado, hash SHA-256, actor y tiempo de publicación. Índice único `(ScopeKey, FormCode, Version)`.
2. Ámbitos `order` y `lodge:<uuid>` calculados por servidor, con comprobación de que el Taller existe.
3. `FormSchemaVersionStore.PublishAsync`: publicación transaccional con advisory lock por ámbito/código, revisión del esquema previo y validación `FormSchemaPolicy.ValidateNext` con claves núcleo **confiables del servidor**; inserción sin reemplazar versiones previas y evento `AuditEvent` en la misma transacción.
4. Trigger PostgreSQL que bloquea tanto UPDATE como DELETE de una versión publicada. `ReadAsync` busca versión histórica **exacta**; no sustituye por la última.
5. `SchemaJson` es texto con JSON serializado deliberadamente, para que PostgreSQL no reformatee el contenido al consultar. La lectura comprueba integridad SHA-256 y coincidencia de metadatos; ante corrupción falla cerrada.
6. Fechas efectivas se normalizan a UTC/microsegundos **antes** de publicar y de calcular SHA-256, evitando falsos errores por diferencia entre precisión .NET y PostgreSQL.
7. Pruebas unitarias para ámbitos, checksum y metadatos; pruebas PostgreSQL para versiones consecutivas, aislamiento entre Talleres, rollback de versiones inválidas, auditoría y prohibición de borrado. Nueva clase registrada en `.github/backend-integration-shards.json`.

## Evidencia y límites

- PR #454 HEAD funcional: `4d6be53ce7866b10ea5c19ef123d0315857dd651`.
- Gates exact-HEAD **SUCCESS**: PMGM CI [#38273392590](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/actions/runs/38273392590), Showcase [#38273392583](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/actions/runs/38273392583), QA Installable [#38273392589](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/actions/runs/38273392589).
- Postmerge `dev@9165990c`: CI #38274157286 y Showcase #38274157252 **en ejecución** al corte; QA #38274157236 y Pre-UAT #38274157225 SUCCESS. Leer resultados finales antes de acreditar publicación.
- **NO** se ha verificado `downloads/qa-current.json.sourceSha` del nuevo corte. No atribuir a Pages un SHA no leído; paquete QA generado, **no instalado**.
- Esta PR NO incorpora endpoints, permisos administrativos nuevos, editor, renderizador, persistencia de capturas personales, exportación ni UAT. `FormSchemaVersionStore` es un servicio backend interno: una futura API debe resolver OIDC/actor/ámbito y catálogo núcleo del servidor antes de utilizarlo.
- No modifica `FormSchemaPolicy.cs` ni `FormSubmissionPolicy.cs` de fases 1/1b; conserva reglas históricas y privacidad.

## Próximos incrementos concretos (NO realizados)

1. **Fase 2b — API de administración autenticada:** verificar permisos institucionales `IInstitutionalAccessService` (incluido `CanConfigureSystem`) y el ámbito, permitir consulta de definición autorizada, publicación versionada auditada y control explícito de errores de concurrencia. El cliente jamás suministra como verdad el catálogo de campos núcleo ni las reglas de acceso. Revisar los permisos del administrador por Taller **sin crear atribuciones masónicas nuevas**.
2. **Fase 2c — capturas históricas:** enlazar registros reales a `(ámbito,código,versión)`, limitar tamaño/archivos, auditoría y privacidad. Validar siempre contra el esquema exacto proveniente del servidor y no contra JSON del cliente.
3. **Fase 3 — UI/editor responsive + paridad demo:** configuración de campos no núcleo, estados y roles, reordenamiento, condicionales por rol y etapa, 360/768/1440 px; pruebas de regresión y accesibilidad.
4. **Fase 4 — exportación/reportes y UAT institucional:** normativa Ley 21.719, versionado e interpretación histórica, pruebas de identidad/cargos y aceptación real. `srv01` continúa pausado hasta autorización Sponsor.

**Coordinación:** seguir PMGM-GOV-003 §16; nueva rama desde HEAD vivo de `dev`, etiqueta y PR draft, evitar migraciones/código de otro agente y cerrar con GitHub + Drive. PR #1 de promoción a main sigue bloqueada por UAT. Fuente viva y control final: esta ruta, `START-HERE.md`, Issue #37 y Línea Base Maestra de Drive.
