# Handoff — continuidad de perfiles dinámicos

Issue #266 / PR #267 (ChatGPT). Dev inicial `3addd763d8bddf58eaa4328ec392ec1828ac9246`; main `6dfb9546a4873baff15955cf86abfd7d47e3d111`.

Drive revisado dentro de Proyecto Centenario y novedades desde cierre #311: adenda Claude #313 (1Wxi-shDllh_MEKz-fI_g5bBTqxRZP3gkWVUVIf8BhZQ), menús simples ya integrados; preservar App/navGroups/TextSizeControl/tema. Gates postmerge de dev #313 SUCCESS. Línea Base registra #311 cerrada y reserva #267 propia vigente.

Revisión de rama #267 detectó: diff viejo eliminaba política de publicación de insinuados, almacenamiento varchar(1000) insuficiente, evaluación omitía OrganizationId, ausencia de concurrencia/pruebas HTTP y UI guardaba otros settings. Este corte reconstruye desde dev vivo y sólo conserva cambios pertinentes de #267. SystemConfiguration y su política CandidatePublicationEvidenceStore permanecen intactos.

Se implementan snapshot JSONB versionado, compare-and-swap bajo bloqueo/transaction PostgreSQL con auditoría, catálogo conocido, cargos base protegidos, seis acciones por vista, perfiles custom, baja, asignación por sujeto exacto/Taller/fecha y revocación. Diseñador, asignaciones y revisión usan API real y mismo contrato demo; impresión administrativa se autoriza/audita en backend. No modifica App/identidad ni roles institucionales. No afirmar que perfiles técnicos generan cargos o reemplazan autorizadores de dominio.

Datos: DB-005/SEC-004 y migración 20261004031137_AddDynamicAccessSnapshots. Up sólo añade tabla e índice; no modifica tarifas/miembros/permisos institucionales. Despliegue QA pendiente.

Archivos: Authorization/**, PmgmDbContext/Program/migración específica; AccessProfileDesigner/UserAccessAssignments/AccessReviewPanel, su conexión mínima en SystemOperationsPanel; api/pmgmApi + dynamicAccess + hook, tests; DB-005/SEC-004 y este handoff. Ningún PR abierto de Claude al inicio. No editar START-HERE en PR funcional.

Frontend: 398 PASS, lint/build PASS local. Backend y proyecto de pruebas compilan sin errores/advertencias; 2 pruebas unitarias del evaluador PASS. Las 3 pruebas HTTP nuevas requieren PMGM_TEST_POSTGRES y se verificarán con PostgreSQL en CI (no ejecutadas localmente). Gates exact-head y publicación se completarán en recibos del PR y Línea Base. No reutilizar CI del head fa1b3dd; dev vivo debe ser padre antes de merge.

Pendientes: revisión funcional del contrato técnico y pruebas PostgreSQL, CI/Showcase/QA exact-head, integración cuando el alcance esté completo, luego Pages/qa-current/ZIP/manifiesto del SHA integrado. Main/srv01/UAT pendientes; #116 histórico NO FUSIONAR TODAVÍA. Adenda #313 pendiente de consolidación en Línea Base. Continuar sólo desde GitHub/Drive y consultar reservas antes de cambios.
