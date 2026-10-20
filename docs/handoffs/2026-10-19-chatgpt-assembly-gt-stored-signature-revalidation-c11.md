# #533 — C11 revalidación de constancia firmada GT desde BD
Fecha 2026-10-19. Base dev@478c70e224895ed6a2af7153dd13d78d49ad3f7c, main@6dfb9546a4873baff15955cf86abfd7d47e3d111 protegido. QA del glmdch en hilo separado.
## Objetivo
Cerrar riesgo de consumo de sello persistido C10 sin comprobar que al consultar todavía es confiable: lectura read-only de constancia del mismo Taller/Asamblea, lectura de eventos proponente/revisor, cotejo del documento original (bytes suministrados de almacén confiable futuro) y revalidación RSA-PSS contra registro de claves C7 incluyendo revocación. El resultado es diagnóstico de integridad, NO certificación histórica financiera ni sufragio.
## Seguridad
Se prohíbe endpoint HTTP nuevo, escritura DB, guardado de documentos o detalles sensibles en el resultado. Solo retorna códigos genéricos. Nada de clonar datos desde otro Taller. Revocaciones hacen que una constancia previamente válida sea inválida al siguiente consumo. No permitir sustitución del sobre firmado en BD, firma documental cruzada, clave/actor distinto, fechas de atestación incoherentes ni documento alterado. Código fail-closed.
## Pruebas
Tests unitarios con llaves RSA y datos sintéticos; prueba PostgreSQL lectura aislada y caso ausencia. Integración shard b si se agrega Postgres test. Exigir 3/3 PMGM CI, Showcase y QA Installable exact-head. No tocar main, QA físico, srv01 ni UI Claude.
## Pendiente mayor
La verificación no autoriza el saldo; sigue faltando evidencia financiera histórica desde GT, custodia institucional formal, revisión TI, autorización HTTP auditada y reglas RI/Tribunal, cierre/exportación de padrón.

## Entrega de archivos C11
- `AssemblyTreasurySignedReviewRevalidation.VerifyAsync`: consulta PostgreSQL `core.assembly_treasury_signed_reviews` por tres GUID (Asamblea/Taller/constancia), `AsNoTracking`, incluye únicamente propuesta y revisión relacionadas; no muta estado.
- `Check`: verifica vínculo exacto de actores, referencias, fechas, hash y origen, construye sobre desde datos almacenados (no parámetros externos) y llama `AssemblyGrandTreasuryServerTrustRegistry.Verify` con los bytes suministrados por futuro adaptador de almacenamiento. Cada lectura revalida clave y revocación.
- Resultado público es solo `ValidForDocumentaryReview`, `MayAuthorizeSuffrage=false` e identificadores genéricos de bloqueo; jamás expone RUT, nombres, referencias bancarias, montos o firma.
- Registro DI en `Program.cs`. Tests unitarios `AssemblyTreasurySignedReviewRevalidationTests`, prueba real PostgreSQL `AssemblyTreasurySignedReviewRevalidationPostgreSqlTests` en shard b; datos y claves sintéticos.
- **Limitación explícita**: aún no existe un adaptador S3/MinIO seguro que aporte los bytes de evidencia, ni endpoint RBAC de lectura/consulta, ni una certificación GT de deuda histórica. C11 es control interno preparado, NO habilitación operativa. CI exact HEAD 3/3 requerido.
