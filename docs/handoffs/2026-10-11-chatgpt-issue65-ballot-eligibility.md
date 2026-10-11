# Proyecto Centenario — Issue #65: consistencia de elegibilidad de Iniciación

## Identificación y decisiones preservadas

- Fecha: 11-10-2026, Chile.
- Issue: #65, sprint de insinuaciones/ceremonias. PR: #444.
- Base al abrir: `dev@9b1e8e27bfff4b7bed1602e349f508c5f5a1cd59`.
- `main@6dfb9546a4873baff15955cf86abfd7d47e3d111` histórica, no modificar. Revalidar HEAD al integrar.
- Regla aprobada: deliberación inicial 7 días/unanimidad, publicación mínimo 20 días (versionado), revisión tercer grado, balotaje primer grado aprobado, validaciones institucionales, autorización por Gran Secretaría.
- Vigencia: ninguna modificación a `srv01`, UAT, reglas institucionales, permisos, migraciones ni nombres de cargos.
- Coordinación: `agente:chatgpt` en #65 y PR draft antes de programar; `frontend/src/App.tsx`, estilos y archivos calientes UI de Claude no tocados.

## Defecto reproducible por inspección del código base

`POST /api/ceremonias/solicitudes/{id}/autorizar` tenía `CandidateInitiationAuthorizationGuardMiddleware` que exige la **última** validación `candidate_final_ballot=approved`; sin embargo, `GET /api/ceremonias/solicitudes/{id}/elegibilidad` calculaba `canAuthorize` con `CeremonyEligibilityPolicy` sin consultar ese estado. Por tanto, podía mostrar elegibilidad favorable aun cuando el POST devolvía 409. También existían una bandeja de revisión sin ese requisito y una matriz de Gran Maestría que lo añadía manualmente.

## Cambio del PR #444

1. `CeremonyEligibilityPolicy.cs`: un requisito compartido `candidate_final_ballot`, aplicable **sólo a iniciaciones**. Sin validación o con pendiente/observado/excepción no aprobada → `observed` y bloqueo; rechazado → `rejected` y bloqueo; aprobado → `approved`.
2. `CeremonyEndpoints.cs`: obtiene el último balotaje por fecha registrada y lo pasa a la política tanto para GET elegibilidad como POST autorizar. El middleware existente permanece como defensa adicional.
3. `CeremonyEligibilityService.cs`: propaga el valor desde el diccionario de validaciones, evitando que evaluadores del dominio omitan silenciosamente el requisito.
4. `InstitutionalProjections/CeremonyReviewQueueEndpoints.cs`: carga en lote el estado más reciente para la bandeja, sin consulta por expediente; evita un botón de autorización engañosamente habilitado.
5. `GrandMasterCeremonyEndpoints.cs`: reemplaza su duplicación manual del requisito por la evaluación compartida; conserva la referencia privada de la validación en `evidence.finalBallotValidationId`.
6. `frontend/src/api/pmgmApi.ts`: sincroniza la demo sintética, inicializando el requisito como observado y actualizándolo al registrar balotajes aprobados/rechazados.
7. Pruebas de `CeremonyEligibilityServiceTests.cs`, `CeremonyInitiationBallotEligibilityPolicyTests.cs` y `frontend/src/api/pmgmApi.test.ts`: ausente, pendiente, observado, rechazado, excepción no autorizada, aprobado; requisito único; no afectación de otras ceremonias; paridad demo.

## Matriz de revisión puntual de #65 (código vs aceptación)

| Área / fuente | Rastro técnico observado en dev | Resultado de esta revisión |
| --- | --- | --- |
| Presentación, 7 días, unanimidad | `CandidateWorkflowEndpoints.cs`, `CandidateIntakeWorkflowPolicy.cs` | Implementación observada; UAT sigue pendiente |
| Publicación 20 días, fotografía y privacidad | `CandidatePublicationEndpoints.cs`, `CandidatePhotoPolicy.cs`, pruebas HTTP de publicación | Implementación previa observada; sin QA físico |
| Tres Maestros e informes privados | `CandidateInterviewAssignmentEndpoints.cs`, PRs #428/#432/#434/#435/#438, cierre #427 | Integrado y aceptado visualmente; scanner/almacenamiento real no certificado |
| Revisión tercer grado y balotaje | `CandidateWorkflowEndpoints.cs`, `CandidateIntakeWorkflowPolicy.cs` | Flujos persistentes; **inconsistencia de elegibilidad corregida en PR #444**, gates pendientes |
| Rechazo/re-presentación | `CandidateRejectionPolicy.cs` y flujos del expediente | No se declara conformidad integral sin matriz de prueba de reingreso por causa y plazo |
| Vistos buenos institucionales | `CeremonyEligibilityPolicy.cs`, Gran Maestría, Tesorería y Hospitalaria | Compuertas observadas; pruebas/UAT transversales pendientes |
| Emisión de Plancha | `GrandSecretariatEndpoints.cs` requiere ceremonia ya autorizada y evita duplicado | Implementación observada; verificar UAT documental |
| Ceremonia realizada e historial | `CeremonyEndpoints.RegisterInitiationAsync` crea Hermano, pertenencia, grado, estatus y auditoría | Iniciación observada; no extrapolar a todas las clases de ceremonia |
| Afiliación/Incorporación | `AdmissionCeremonyEndpoints.cs`, Issue #66 | Circuito distinto existente; aceptación institucional #66 pendiente |

**Resultado:** este PR cierra una brecha concreta del sprint #65, pero **no cierra #65** porque todavía falta acreditar matriz de aceptación integral (especialmente re-presentación, ceremonias distintas de iniciación) y UAT operativa suspendida por #97.

## Drive y coordinación

- Carpeta oficial Proyecto Centenario, ID `1P74Q8lhNPu6lHZ5zZR9ZD3AJFr_EtZyO`, consultada por diferencias al inicio.
- Archivos nuevos/modificados 10–11 de octubre: Línea Base Maestra y adenda de íconos/mesa de trabajo; adenda de UX sin impacto en la presente corrección backend. Se mantienen los cierres documentados #427, #48 y #441.
- Fuente funcional: `docs/PMGM-REQ-001-flujos-institucionales-2026.md`, §3–5.
- Pendiente de cerrar el registro: gates exact-head CI/Showcase/QA, estado final PR/dev, lectura de retorno de Drive, SHA publicado y ZIP. No afirmar una prueba que no se haya verificado.

## No hacer

- No fusionar en `main`; no desplegar en `srv01` ni certificar UAT real.
- No cerrar #65 ni #97 solo por CI verde.
- No agregar autorizaciones fuera de norma ni sustituir el flujo real por mocks.
