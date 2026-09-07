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
- `DataSubjectRequest` para acceso, rectificación, supresión, oposición, portabilidad y bloqueo;
- `DataProcessor` para encargados/proveedores;
- `InternationalDataTransfer` para transferencias internacionales;
- `PrivacySecurityIncident` para incidentes de privacidad/seguridad;
- `PrivacyImpactAssessment` para EIPD/DPIA;
- migración `AddAuditAndPrivacyCompliance` para PostgreSQL;
- rol técnico `privacy_officer` con alcance de Orden;
- autorización centralizada `CanManagePrivacy`;
- dashboard inicial de privacidad;
- API para actividades de tratamiento;
- API para solicitudes de derechos;
- API para políticas de retención;
- API para incidentes;
- API para EIPD;
- pruebas de RBAC para Privacy Officer;
- pruebas de códigos de derechos y acciones de retención;
- auditoría de creación de actividades, solicitudes, incidentes, EIPD y políticas de retención.

## Alcance pendiente
- CRUD/versionado completo de encargados/proveedores;
- CRUD/versionado de transferencias internacionales;
- flujo de resolución/cierre de derechos de titulares;
- cálculo de plazos legales desde configuración jurídica versionada;
- workflow de evaluación/notificación de incidentes;
- aprobación/cierre formal de EIPD;
- motor automático de aplicación de políticas de retención;
- anonimización/pseudonimización ejecutable;
- legal hold;
- catálogo de clasificación de datos por campo/módulo;
- DTOs mínimos adicionales por finalidad;
- gate automático de privacidad en CI/CD;
- pruebas de integración contra PostgreSQL;
- revisión jurídica final previa a producción.

## Principios técnicos obligatorios
1. un tratamiento sin finalidad/base jurídica registrada no puede promoverse a producción;
2. los datos sensibles deben tener controles reforzados y quedar fuera de logs/errores;
3. la retención no puede confundirse con conservación indefinida;
4. todo acceso se limita por rol, alcance y finalidad;
5. derechos de titulares deben ser trazables desde recepción a cierre;
6. incidentes deben registrar evaluación, medidas y decisiones de notificación;
7. tratamientos de alto riesgo deben poder asociarse a una EIPD;
8. transferencias internacionales y encargados deben quedar inventariados;
9. operaciones críticas deben dejar auditoría verificable;
10. Portal de Insinuados debe exponer sólo la proyección mínima autorizada.

## Criterios de aceptación
1. cada tratamiento relevante tiene finalidad/base jurídica registradas;
2. cada categoría de dato tiene clasificación y política de conservación;
3. el sistema puede registrar y seguir solicitudes de derechos;
4. las vistas de Portal de Insinuados no exponen datos restringidos por defecto;
5. Gran Secretaría consume estados mínimos de Tesorería/Hospitalaria;
6. proveedores con acceso a datos están inventariados;
7. transferencias internacionales están identificadas y revisables;
8. incidentes pueden registrarse, evaluarse y documentarse;
9. tratamientos de alto riesgo permiten registrar EIPD previa;
10. operaciones de riesgo dejan auditoría;
11. el gate de release bloquea producción si faltan finalidad, base jurídica, conservación o controles de acceso;
12. se realiza revisión de preparación legal antes del 01-12-2026.
