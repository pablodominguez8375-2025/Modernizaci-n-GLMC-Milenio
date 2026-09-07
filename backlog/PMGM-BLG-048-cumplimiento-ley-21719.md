# PMGM-BLG-048 — Cumplimiento Ley 21.719

**Prioridad:** P0 transversal  
**Estado:** EN DESARROLLO — NÚCLEO EJECUTABLE  
**Requisito:** PMGM-REQ-026  
**Seguridad:** PMGM-SEC-003

## Objetivo
Implementar las capacidades técnicas y operativas necesarias para que Proyecto Milenio pueda cumplir la Ley 21.719 desde su entrada en vigencia y demostrar el tratamiento responsable de datos personales.

## Implementado en `dev`
- `AuditEvent` persistente con actor, acción, entidad, resultado, organización, correlation ID y metadatos controlados;
- `IAuditService` conectado al `PmgmDbContext`;
- `DataProcessingActivity` para inventario/versionado de actividades de tratamiento;
- `DataRetentionPolicy` para conservación, revisión, anonimización o supresión;
- `DataRetentionHold` para legal hold con autoridad identificada, motivo, evidencia y vigencia;
- `DataRetentionEvaluation` y motor de decisión para calcular vencimiento, acción recomendada y bloqueo por legal hold;
- API para listar/crear/liberar legal holds y ejecutar/listar evaluaciones de retención;
- revalidación de legal hold al momento exacto de ejecutar una política, evitando ejecutar sobre una evaluación antigua si apareció un hold posterior;
- estado de ejecución persistente en la evaluación: acción, fecha, ejecutor, evidencia y resultado técnico;
- anonimización ejecutable inicial para `DataSubjectRequest`, desligando `PersonId`, responsable y narrativas restringidas una vez vencido el plazo y sin hold;
- borrado duro bloqueado para ejecución automática; requiere revisión humana/adaptador específico antes de habilitarse;
- acciones `review` y entidades sin adaptador automático quedan derivadas a revisión manual;
- migraciones PostgreSQL para legal holds, evaluaciones y estado de ejecución de retención;
- `DataSubjectRequest` para acceso, rectificación, supresión, oposición, portabilidad y bloqueo;
- reglas jurídicas de plazos versionadas mediante `InstitutionalRuleSetting`, sin días hardcodeados en la API;
- resolución automática de fecha de vencimiento para solicitudes de derechos cuando existe regla jurídica vigente;
- validación que impide sobreescribir manualmente una fecha distinta de la calculada por la regla vigente;
- `DataProcessor` para encargados/proveedores;
- `InternationalDataTransfer` para transferencias internacionales;
- versionado no destructivo y baja lógica de encargados/proveedores;
- versionado no destructivo y baja lógica de transferencias internacionales;
- `PrivacySecurityIncident` para incidentes de privacidad/seguridad;
- workflow reforzado de incidentes con etapas separadas de evaluación, decisión de notificación, comunicación efectiva, medidas correctivas y cierre;
- decisiones independientes para autoridad y titulares, con fundamento y fecha de decisión;
- registro independiente del canal y evidencia de cada comunicación, sin confundir decisión con envío efectivo;
- política de cierre que bloquea el cierre si falta evaluación, decisión explícita, comunicación obligatoria o medidas correctivas;
- timeline de incidentes construido desde auditoría, sin duplicar narrativas sensibles en metadatos;
- `PrivacyImpactAssessment` para EIPD/DPIA;
- rol técnico `privacy_officer` con alcance de Orden;
- autorización centralizada `CanManagePrivacy`;
- dashboard inicial de privacidad con fecha institucional `America/Santiago`;
- API de actividades de tratamiento;
- API de solicitudes de derechos, incluida resolución/cierre con verificación de identidad;
- API de políticas de retención y ejecución controlada;
- API de incidentes y workflow de respuesta;
- API de EIPD, incluida aprobación formal y registro de riesgo residual;
- API de encargados/proveedores;
- API de transferencias internacionales;
- catálogo inicial versionado de clasificación de datos por módulo/entidad/campo;
- guardrails automáticos de clasificación: sensibles/restringidos fuera de logs, proyección pública explícita y no exportable por defecto;
- DTO mínimo específico para Portal de Insinuados, sin `PersonId`, `OrganizationId`, `CeremonyRequestId` ni identificadores internos;
- separación de respuesta administrativa y proyección mínima en Gran Tesorería y Gran Hospitalaria;
- proyección inter-módulo específica para elegibilidad de ceremonias que entrega sólo estado, fecha de corte y cumplimiento, sin IDs de snapshots, notas ni referencias internas;
- pruebas automáticas que fijan la superficie permitida de DTOs públicos e inter-módulo;
- pruebas de RBAC para Privacy Officer;
- pruebas de códigos de derechos, acciones de retención y cálculo de plazos;
- pruebas unitarias del motor de decisión de retención, política de ejecución y política de cierre de incidentes;
- auditoría de creación/resolución de actividades, solicitudes, incidentes, EIPD, políticas, proveedores, transferencias, legal holds, reglas jurídicas y bajas/versiones de terceros;
- minimización adicional de metadatos de auditoría para evitar duplicar identificadores personales o narrativas sensibles;
- gate estructural de privacidad y gate de clasificación de datos ejecutados automáticamente en CI/CD.

## Alcance pendiente
- revisión jurídica y carga inicial de los valores efectivos de cada plazo legal versionado;
- ampliar adaptadores de anonimización/pseudonimización a entidades adicionales sólo cuando exista política aprobada y prueba de no pérdida indebida de trazabilidad;
- definir, con revisión jurídica, si alguna categoría admite borrado físico automático y bajo qué doble control;
- ampliar el catálogo de clasificación a todo nuevo campo incorporado al modelo;
- ampliar DTOs mínimos por finalidad a otros reportes y exportaciones no cubiertos todavía;
- pruebas de integración contra PostgreSQL;
- revisión jurídica final previa a producción.

## Principios técnicos obligatorios
1. un tratamiento sin finalidad/base jurídica registrada no puede promoverse a producción;
2. los datos sensibles deben tener controles reforzados y quedar fuera de logs/errores;
3. la retención no puede confundirse con conservación indefinida;
4. todo acceso se limita por rol, alcance y finalidad;
5. derechos de titulares deben ser trazables desde recepción a cierre;
6. los plazos normativos se obtienen de reglas versionadas y con fuente jurídica, no de constantes hardcodeadas;
7. incidentes deben registrar por separado evaluación, decisiones de notificación, comunicaciones realizadas, medidas correctivas y cierre;
8. tratamientos de alto riesgo deben asociarse a una EIPD aprobada antes de producción;
9. transferencias internacionales y encargados deben quedar inventariados y versionados sin pérdida de historial;
10. operaciones críticas deben dejar auditoría verificable;
11. Portal de Insinuados debe exponer sólo la proyección mínima autorizada;
12. un legal hold vigente debe impedir toda ejecución de eliminación o anonimización asociada a la política afectada;
13. toda acción automática de retención debe revalidar el hold inmediatamente antes de mutar datos;
14. el borrado duro no se habilita por defecto y requiere una decisión jurídica/técnica explícita por categoría;
15. las consultas entre módulos deben usar DTOs ligados a una finalidad y no reutilizar entidades administrativas completas;
16. cada campo de riesgo debe tener clasificación, finalidad, reglas de log/exportación/proyección y política de conservación identificada.

## Criterios de aceptación
1. cada tratamiento relevante tiene finalidad/base jurídica registradas;
2. cada categoría de dato tiene clasificación y política de conservación;
3. el sistema puede registrar, seguir, resolver y cerrar solicitudes de derechos;
4. las vistas de Portal de Insinuados no exponen identificadores internos ni datos restringidos por defecto;
5. Gran Secretaría y Régimen Interior consumen estados mínimos de Tesorería/Hospitalaria sin notas, referencias ni IDs internos;
6. proveedores con acceso a datos están inventariados y conservan historial de versiones;
7. transferencias internacionales están identificadas, revisables y conservan historial de versiones;
8. incidentes no pueden cerrarse sin evaluación, decisiones explícitas, comunicaciones requeridas y acciones correctivas;
9. tratamientos de alto riesgo permiten registrar y aprobar EIPD previa;
10. operaciones de riesgo dejan auditoría;
11. el gate de release bloquea producción si faltan finalidad, base jurídica, conservación o controles de acceso;
12. las evaluaciones de retención calculan vencimiento y no permiten ejecutar una acción destructiva mientras exista legal hold vigente;
13. la ejecución de anonimización vuelve a comprobar el legal hold en tiempo real y deja resultado verificable;
14. el gate de clasificación rechaza datos sensibles/restringidos habilitados para logs o proyecciones públicas inconsistentes;
15. las solicitudes de derechos calculan su fecha de vencimiento con la versión de regla jurídica vigente a la fecha de recepción;
16. se realiza revisión de preparación legal antes del 01-12-2026.
