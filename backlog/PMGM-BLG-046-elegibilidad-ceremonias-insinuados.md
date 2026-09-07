# PMGM-BLG-046 — Elegibilidad de ceremonias y portal de insinuados

**Prioridad:** P1  
**EPIC:** Régimen Interior / Gran Secretaría  
**Estado:** EN PROGRESO

## Alcance
- exigir aprobación de Régimen Interior;
- exigir Taller al día con Gran Tesorería;
- exigir Taller al día con reposiciones de Gran Hospitalaria;
- permitir excepciones sólo si están formalmente autorizadas y auditadas;
- para iniciaciones, exigir publicación previa del insinuado;
- usar plazo mínimo configurable (20 días como valor inicial de configuración);
- conservar la regla vigente aplicada a cada trámite;
- mantener al insinuado como Persona/candidato sin crear membresía activa antes de la iniciación;
- exponer portal de publicaciones vigentes con minimización de datos;
- entregar a Gran Secretaría una matriz consolidada de validaciones y bloqueos.

## Implementado
- PMGM-REQ-025;
- modelo `CeremonyRequest`;
- modelo `CeremonyValidation`;
- modelo `CandidatePublication`;
- configuración versionada `InstitutionalRuleSetting`;
- códigos estables de ceremonia, validación y publicación;
- evaluador `CeremonyEligibilityService`;
- rol técnico `grand_hospitalaria`;
- migración PostgreSQL `AddCeremonyEligibility`;
- endpoint autenticado `GET /api/candidate-publications/active` como base API del portal;
- respuesta minimizada a nombre, Taller, fechas y estado de publicación, sin RUT, contacto ni datos administrativos;
- fuentes reales de regularidad de Gran Tesorería y Gran Hospitalaria conectadas al flujo de elegibilidad;
- proyección inter-módulo `ceremony-regularity` que entrega a Régimen Interior/Gran Secretaría sólo estado, fecha de corte y cumplimiento;
- separación entre vistas administrativas de Tesorería/Hospitalaria y DTOs mínimos consumibles por otros módulos;
- congelamiento de referencias de validación al momento de autorizar la ceremonia;
- pruebas unitarias para bloqueos de Tesorería, Hospitalaria y plazo de publicación;
- pruebas de superficie para impedir que DTOs inter-módulo incorporen IDs, notas o referencias administrativas.

## Pendiente
- frontend del portal de insinuados;
- configuración administrativa de campos visibles;
- mecanismo de observaciones si la institución lo habilita;
- integración con plantillas y correlativos de Gran Secretaría;
- auditoría completa de todos los endpoints de ceremonia con actor/correlation ID;
- eliminar o endurecer cualquier endpoint legado del portal que aún replique identificadores internos.

## Criterios de aceptación
1. No se autoriza ceremonia con validación financiera no habilitante.
2. No se autoriza ceremonia con reposiciones hospitalarias pendientes.
3. Iniciación no se autoriza antes del mínimo de publicación vigente.
4. El número de días se obtiene de configuración versionada.
5. Las excepciones quedan explícitas y auditables.
6. El insinuado no se convierte en miembro antes de la iniciación.
7. Gran Secretaría puede identificar exactamente qué requisito bloquea la autorización.
8. Tesorería y Hospitalaria no entregan notas, referencias o IDs internos a consumidores que sólo requieren regularidad.
