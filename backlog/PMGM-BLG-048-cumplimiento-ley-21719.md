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
- migración PostgreSQL para legal holds y evaluaciones de retención;
- `DataSubjectRequest` para acceso, rectificación, supresión, oposición, portabilidad y bloqueo;
- `DataProcessor` para encargados/proveedores;
- `InternationalDataTransfer` para transferencias internacionales;
- versionado no destructivo y baja lógica de encargados/proveedores;
- versionado no destructivo y baja lógica de transferencias internacionales;
- `PrivacySecurityIncident` para incidentes de privacidad/seguridad;
- `PrivacyImpactAssessment` para EIPD/DPIA;
- migración `AddAuditAndPrivacyCompliance` para PostgreSQL;
- rol técnico `privacy_officer` con alcance de Orden;
- autorización centralizada `CanManagePrivacy`;
- dashboard inicial de privacidad;
- API de actividades de tratamiento;
- API de solicitudes de derechos, incluida resolución/cierre con verificación de identidad;
- API de políticas de retención;
- API de incidentes, incluida evaluación, decisión de notificación y cierre;
- API de EIPD, incluida aprobación formal y registro de riesgo residual;
- API de encargados/proveedores;
- API de transferencias internacionales;
- pruebas de RBAC para Privacy Officer;
- pruebas de códigos de derechos y acciones de retención;
- pruebas unitarias del motor de decisión de retención;
- auditoría de creación/resolución de actividades, solicitudes, incidentes, EIPD, políticas, proveedores, transferencias, legal holds y bajas/versiones de terceros;
- gate estructural de privacidad ejecutado automáticamente en CI/CD.

## Alcance pendiente
- cálculo de plazos legales desde configuración jurídica versionada;
- workflow más detallado de notificación y comunicaciones de incidentes;
- motor automático de ejecución material de políticas de retención sobre entidades soportadas;
- anonimización/pseudonimización ejecutable;
- catálogo de clasificación de datos por campo/módulo;
- DTOs mínimos adicionales por finalidad;
- pruebas de integración contra PostgreSQL;
- revisión jurídica final previa a producción.

## Principios técnicos obligatorios
1. un tratamiento sin finalidad/base jurídica registrada no puede promoverse a producción;
2. los datos sensibles deben tener controles reforzados y quedar fuera de logs/errores;
3. la retención no puede confundirse con conservación indefinida;
4. todo acceso se limita por rol, alcance y finalidad;
5. derechos de titulares deben ser trazables desde recepción a cierre;
6. incidentes deben registrar evaluación, medidas y decisiones de notificación;
7. tratamientos de alto riesgo deben asociarse a una EIPD aprobada antes de producción;
8. transferencias internacionales y encargados deben quedar inventariados y versionados sin pérdida de historial;
9. operaciones críticas deben dejar auditoría verificable;
10. Portal de Insinuados debe exponer sólo la proyección mínima autorizada;
11. un legal hold vigente debe impedir toda ejecución de eliminación o anonimización asociada a la política afectada.

## Criterios de aceptación
1. cada tratamiento relevante tiene finalidad/base jurídica registradas;
2. cada categoría de dato tiene clasificación y política de conservación;
3. el sistema puede registrar, seguir, resolver y cerrar solicitudes de derechos;
4. las vistas de Portal de Insinuados no exponen datos restringidos por defecto;
5. Gran Secretaría consume estados mínimos de Tesorería/Hospitalaria;
6. proveedores con acceso a datos están inventariados y conservan historial de versiones;
7. transferencias internacionales están identificadas, revisables y conservan historial de versiones;
8. incidentes pueden registrarse, evaluarse, documentarse y cerrarse;
9. tratamientos de alto riesgo permiten registrar y aprobar EIPD previa;
10. operaciones de riesgo dejan auditoría;
11. el gate de release bloquea producción si faltan finalidad, base jurídica, conservación o controles de acceso;
12. las evaluaciones de retención calculan vencimiento y no permiten ejecutar una acción destructiva mientras exista legal hold vigente;
13. se realiza revisión de preparación legal antes del 01-12-2026.
