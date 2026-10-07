# Mi ficha y Hospitalaria v2 — ChatGPT — 07-10-2026

Issue #354. Fuente Drive 1nQ9GAePY0GLTpfMb62OqGqE9FYdKyFea98nPcQp8e3w.
Base dev c2617b427e78af4cb70160848953cbf9f56f8dd4, main6dfb9546a4873baff15955cf86abfd7d47e3d111. Revisión de instrucciones v2 y Línea Base realizada: aporte mensual por Taller CLP6000, reposición por activo CLP1500 separados. PR #353 Claude reserva UI Mi ficha/App/CSS; no tocar esos archivos. Backend/contratos/datos demo/pruebas/migraciones reservados aquí. Gates/publicación pendientes; srv01/UAT pausados #97.

## Implementación backend en rama

PR draft #355 / Issue #354. Nuevos endpoints propios `/api/membership/me/{cargos,asistencias,hospitalaria}`; enlace institucional de identidad obligatorio, consultas no-store sin selector MemberId. Registro Hospitalaria del fallecimiento con reposición inmediata atómica, lock e idempotencia; decreto formal y vigencia no retroactiva. Aporte mensual fijo por Taller6000 separado, versiones, obligaciones únicas y pago/revisión auditados; worker y campos separados en rendiciones. Nueva migración additive 20261007120000; legacy decreto/RateId null preservado y sin backfill inventado. No cambia tarifas de Tesorería ni regularidad/ceremonias.

Clientes getOwnOffices/getOwnAttendance/getOwnHospitalaria con demo sintética. UI y conexiones de Hospitalaria siguen a cargo de Claude #353: no tocar su rama/archivos reservados. Registro completo GOV004 docs/modelo-datos/cambios/2026-10-07-issue-354-mi-ficha-hospitalaria.md. Build/lint y 2 contratos frontend PASS; gates estructurales PASS. Tests HTTP PostgreSQL nuevos pendientes de CI. No integrado ni publicado, no QA instalado/UAT. Completar contrato frontend Hospitalaria y gates antes de declarar instrucciones ejecutadas.

## Complemento contratos frontend y QA

PmgmApiClient agrega get/setHospitalariaContributionRate(s), getHospitalariaContributions, pay/reviewHospitalariaContribution y recordHospitalariaDeath; demo reproduce aporte6000 por Taller, registro automático y duplicado rechazado, decreto exige número/fecha/respaldo y vigencia no retroactiva. Prueba frontend adicional workflow aporte y reposición. Build/lint450 tests PASS. .NET10 SDK local obtenido: backend y tests compilan Release, cero warnings/errores; PostgreSQL sólo en CI. Catálogo de clasificación amplía13 campos sin nueva retención.

**Pendiente bloqueante de integración:** el frontend vigente HospitalariaPage aún llama setHospitalariaReplenishmentRate sin decretoNumber/decreeDate; debe ser adaptado por Claude con el panel asistente y controles de aporte/fallecimiento antes de fusionar. Mi ficha #353 mantiene Próximamente hasta conectar contratos. No fusionar #355 aisladamente dejando incompatible esa acción. Reservas UI respetadas. El handoff es técnico en curso, no acredita v2 ejecutada completa.

## Continuación e integración de las vistas

Tras merge de #353 en dev@50ac801, el PO pidió continuar. Se preservan las tres pestañas y patrón visual de Claude. Reserva #355 ampliada con componentes nuevos MemberOwnHistory y HospitalariaV2Panels; conexiones en MemberPortalPage/HospitalariaPage. Historiales propios sustituyen Próximamente, con filtros y resumen; aporte mensual por Taller visible en rendición local/bandeja Gran, pago/observación/conciliación y versiones; decreto formal y registro automático de defunción por selección mínima de miembros del Taller. Endpoint candidatos devuelve sólo id/nombre con permisos create Hospitalaria y no-store, excluye fallecidos. Pruebas/CI exactos por completar tras publicación; bloqueos anteriores de conexión UI supersedidos por esta implementación. No desplegar srv01 ni promover main.
