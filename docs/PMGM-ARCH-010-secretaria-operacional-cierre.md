# PMGM-ARCH-010 — Cierre operacional de Secretaría de Taller

**Fuente:** Issue #42 y flujo integral vigente de Secretaría.  
**Base:** `dev` `40a0fbfba30b7efd2399ab5cb6e8f8017ae90d9e`.  
**Estado:** implementado en rama; pendiente integración y UAT.

## Alcance agregado

- correspondencia recibida y enviada por Taller, con folio único, fecha, asunto, contraparte, canal, referencia y cierre;
- pendientes de Secretaría con vencimiento opcional, prioridad, responsable institucional y estados no destructivos;
- tabla de agenda con orden, título, detalle, vínculo opcional a una Tenida del mismo Taller y estados pendiente/tratado/postergado;
- auditoría de creación y cambios de estado;
- demo funcional con datos exclusivamente ficticios y los mismos contratos de API;
- vista de lectura para perfiles autorizados y mutaciones restringidas a Secretaría del Taller en su propio ámbito.

## Permisos

| Operación | Secretaría del Taller propio | Supervisor autorizado | Otro Taller | Gran Secretaría |
| --- | --- | --- | --- | --- |
| Consultar operación diaria | Sí | Venerable/Orador del propio Taller | No | No por atribución global |
| Crear/cambiar correspondencia | Sí | No salvo capacidad explícita | No | No |
| Crear/cambiar pendientes | Sí | No salvo capacidad explícita | No | No |
| Crear/cambiar agenda | Sí | No salvo capacidad explícita | No | No |

El backend mediante `IInstitutionalAccessService` es la autoridad final. La capacidad específica `CanReadLodgeSecretariatOperations` evita que el permiso de Régimen Interior para revisar carga histórica se extienda accidentalmente a correspondencia, pendientes o agenda privada. La UI no concede atribuciones.

## Privacidad y límites

- La operación diaria es privada del Taller y no se proyecta a Gran Secretaría.
- No se almacenan secretos, tokens ni contenido binario en estas entidades.
- Los documentos continúan en Gestión Documental privada; `Reference` es sólo una referencia institucional.
- No se usa “Padrón” para miembros, agenda, destinatarios ni correspondencia.
- No hay borrado destructivo en la UI: los flujos cierran, completan, cancelan o postergan conservando trazabilidad.

## Integración preservada

Este incremento no reemplaza carga histórica, reuniones administrativas, Tenidas, Consejo de Administración, archivo documental, Extracto de Acta, Plancha de Autorización ni la proyección mínima hacia Gran Secretaría. Los extiende sin modificar sus reglas vigentes.

## Aceptación pendiente

1. CI exact-head y pruebas PostgreSQL/migración.
2. Showcase Demo e instalable QA del mismo SHA.
3. UAT funcional de Secretaría de Taller y Gran Secretaría, con validación de lectura/escritura por rol y ámbito.
4. `srv01` permanece diferido por decisión del Product Owner del 19-09-2026.
