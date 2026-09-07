# PMGM-BLG-048 — Cumplimiento Ley 21.719

**Prioridad:** P0 transversal  
**Estado:** EN DESARROLLO  
**Requisito:** PMGM-REQ-026  
**Seguridad:** PMGM-SEC-003

## Objetivo
Implementar las capacidades técnicas y operativas necesarias para que Proyecto Milenio pueda cumplir la Ley 21.719 desde su entrada en vigencia y demostrar el tratamiento responsable de datos personales.

## Alcance MVP
- inventario de actividades de tratamiento;
- clasificación de datos personales/sensibles;
- finalidad y base jurídica por tratamiento;
- política de conservación versionada;
- privacidad por diseño y por defecto;
- flujo de derechos de titulares;
- minimización de Portal de Insinuados;
- separación de estados de regularidad versus detalle financiero;
- registro de encargados/proveedores;
- registro de transferencias internacionales;
- gestión de incidentes;
- soporte a EIPD/DPIA;
- auditoría de operaciones de alto riesgo;
- gate de privacidad previo a producción.

## Implementación sugerida
1. `DataProcessingActivity`.
2. `DataRetentionPolicy`.
3. `DataSubjectRequest`.
4. `DataProcessor` / `ProcessorAgreement`.
5. `InternationalDataTransfer`.
6. `PrivacySecurityIncident`.
7. `PrivacyImpactAssessment`.
8. integración con auditoría transversal.
9. políticas/DTO de minimización por módulo.
10. dashboard de cumplimiento para perfiles autorizados.

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
