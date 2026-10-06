# Continuidad #266 — Gran Hospitalaria

Base: dev 8c092e2e37aaa5b8532b3f127c0e26a25ba2fa70; main 6dfb9546a4873baff15955cf86abfd7d47e3d111.

PO autoriza continuar #266 el 06-10. Corte reclamado en Issue antes de programar. Reserva: Authorization/DynamicGrandHospitalariaAccess.cs, HospitalariaEndpoints.cs, pruebas nuevas, HospitalariaPage.tsx/test, api/dynamicAccess.ts, api/pmgmApi.ts, hook nuevo y registro datos nuevo. Solape no caliente pmgmApi.ts con histórica #116; no se toca su rama ni App/identidad/workflows/migraciones.

Drive: Línea Base vigente leída, cierre #347 y main corregida; no nueva regla aplicable. Alcance: restricciones técnicas de Orden encima de autoridad existente, sin conceder cargos. view consulta, create tarifa/sincronización explícita, write revisiones/regularidad manual. Revocación no retorna al acceso previo para sujetos administrados en Orden; asignaciones de Taller no conceden atribuciones de Orden.

Estado: reclamado, pendiente implementación y pruebas. No integrado/publicado. Despliegue QA pendiente; srv01/UAT pausados. #266 abierta para navegación global y otros módulos.

## Implementación PR #348

Proyección privada GET /api/hospitalaria/acceso y restricciones de Orden en tarifa/casos/rendiciones/regularidad administrativa; autoridad institucional preservada. create para tarifa/sincronización, write para revisiones/registro manual. Revocación/expiración no retorna legacy. Lectores mínimos de regularidad ajenos a la gestión de Hospitalaria conservados. API/demo/UI coinciden; pantalla invalida datos por sujeto/cliente/Taller/período/versión y descarta respuestas anteriores. Regularidad editable sólo se monta con write, por contexto. Navegación global/otros módulos siguen pendientes.

Datos: [registro GOV-004 y diccionario del contrato](../modelo-datos/cambios/2026-10-06-issue-266-gran-hospitalaria-acceso.md); sin migración/backfill ni cambios de cálculos/importes. Frontend 442 pruebas PASS; build PASS; gates privacidad/clasificación/migraciones PASS. Backend no ejecutado localmente (sin SDK); CI exact-head pendiente, incluido HTTP PostgreSQL nuevo y dos unitarias. No integrado ni publicado; no declarar terminado hasta gates/publicación/paquete. Main/srv01/UAT intactos; despliegue QA pendiente.

Archivos: DynamicGrandHospitalariaAccess.cs y sus pruebas unitarias/HTTP, HospitalariaEndpoints.cs, HospitalariaPage.tsx/test, api/dynamicAccess.ts, api/pmgmApi.ts, api/grandHospitalariaAccess.test.ts, api/hospitalariaApi.test.ts (actores institucionales explícitos), useGrandHospitalariaAccess.ts, este handoff/registro nuevo. Solape no caliente API con #116 declarado, sin tocar archivos calientes.
