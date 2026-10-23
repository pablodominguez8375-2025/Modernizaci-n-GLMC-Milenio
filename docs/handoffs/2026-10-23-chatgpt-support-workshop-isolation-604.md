# #604 — aislamiento de Taller 00 en directorio institucional (23-10-2026)

## Reclamo / coordinación multi-IA

Responsable backend ChatGPT (etiqueta `agente:chatgpt` del Issue #604); UI y UX siguen en manos de Clau/Claude conforme PMGM-GOV-003. Origen `dev@99cbc8ad084c804e31f8280de95709437a7ca445`, `main@b77de59e170ebfa2abec71b418f6d8b0407ba7ad`. PR draft a crear después de este primer handoff: GitHub rechaza PR sin commits (422).

Acuerdos PO 23-10: Máster único, soporte sin edición ordinaria; Taller 00 «Soporte GLMCh» con ejemplos base ficticios, separado de un Taller real, no editable, excluido de padrones, reportes, Biblioteca institucional, Gran Tesorería, Hospitalaria y directorios. Clau desarrolla la UI y la marca «Datos ficticios · solo lectura» cuando exista API/backend real.

## Alcance de este corte

- Formalizar código de tipo reservado `support` versus `workshop` con política independiente de la identidad y el número 00 (no basta el nombre para distinguir).
- Evitar que la API **general** `GET /api/institutional/organizations/options` incluya una futura organización `support`, aun si el solicitante tiene acceso ampliado a la Orden. No ocultar Talleres reales.
- Prueba de política de clasificación y filtro de directorio con mayúsculas/minúsculas.
- No crear entidad soporte, miembros, cuentas, pagos ni datos sintéticos todavía; tampoco crear autorización de soporte, ni modificar roles, Keycloak o perfil de Clau.

## Fases todavía pendientes, NO confundir con este cambio

1. Identidad persistente singleton e idempotente `support` y fixture sintético de ejemplo con IDs reproducibles y tablas permitidas.
2. Aislamiento de toda agregación financiera, padrón, reportes, consultas y endpoints por ámbito; acceso solo Master/superusuarios autorizados.
3. Bloqueo backend de toda escritura, incluso por ID directo, APIs indirectas y acciones administrativas; soporte ordinario solo lectura, función «Corregir como soporte» (#574) completamente auditada.
4. Bootstrap seguro del Máster único `admin`, MFA, break-glass, UAT y GUI de Clau.

No `main`, QA glmdch ni producción. Mantener evidencia CI exact-head y comparación con dev antes de cualquier fusión. Fuente normativa funcional: comentarios del PO en #604 del 23-10 y adenda Drive «Biblioteca separada y Máster único (UI QA v0.135)».
