# Registro parcial de datos — identidad al crear admisiones

Issue #275 / PR draft #276. Continúa [identidad y búsqueda](2026-10-03-issue-275-identidad-admisiones.md). Base de rama `de6f7b7e1c6dd59aadef77b4a7e0077159bd16c5`; dev incorporado `32e02ab71d9c999cf040cfd66d71b11a290400d8`. Es un corte parcial de validación/acceso, no un diccionario completo ni certificación del schema desplegado.

## Diccionario y estructuras afectadas

| Elemento | Tipo / relación | Control implementado |
|---|---|---|
| CreateAdmissionCaseRequest.OrganizationId | UUID, destino lógico en core.organizations | Debe existir con Type=workshop; permiso de administración del destino o evaluador central vigente |
| PersonId | UUID, identidad en core.people | Existencia; no crea Persona nueva en este endpoint |
| MemberId | UUID nullable, core.members.PersonId enlaza identidad | Afiliación exige registro existente de la misma Persona (control preservado); incorporación rechaza valor no null y también rechaza Persona que ya tiene Member aunque se omita MemberId |
| AdmissionType | affiliation / incorporation | No cambia catálogo ni circuito; guard sólo se llama después de validar tipo y permiso |
| WithdrawalLetterGrantedDate | fecha civil nullable | Incorporación rechaza cualquier valor no null; afiliación mantiene política CRV y tres meses calendario existente |
| admission_cases.PersonId / OrganizationId / AdmissionType | Referencias lógicas al núcleo, índices existentes PersonId/OrganizationId | Secretaría sólo reutiliza Personas de expedientes de incorporación en su Taller; comprobación anterior a consultar existencia global de Persona. Evaluador central conserva alcance previo |

No nuevas tablas, columnas, PK/FK, cardinalidades, índices ni migración SQL. Se consultan estructuras existentes. No cambia Person, Member, Membership ni expedientes históricos. Las referencias entre contextos no se presentan como nuevas FK físicas. Incorporación no crea Member antes de resolución.

## Antes / después

Antes: POST verificaba existencia del destino pero aceptaba organizaciones no Taller; MemberId=null podía ocultar que PersonId ya correspondía a un hermano GLMCh; Secretaría podía reutilizar una Persona arbitraria fuera de su alcance y persistir fecha CRV en incorporación.

Después: guard de identidad previo a persistencia aplica los controles anteriores. Rechazo no inserta expediente ni modifica identidad; no devuelve ficha, RUT ni contactos. Afiliación conserva relación Person/Member, sin inferir retiro, autorización ni traslado desde la identidad. La fecha y modalidad siguen siendo validaciones de creación, no verificación documental definitiva.

La restricción local de reutilización requiere un expediente previo autorizado; no sustituye el alta externa nueva, que continúa pendiente y debe establecer su contexto propio sin usar el flujo de iniciación ni exponer candidatos privados. Evaluadores centrales mantienen reutilización de Personas existentes, con los mismos controles de identidad. No se inventa atribución institucional, plazo, firma, tarifa ni política de duplicación de expedientes.

## Demo / pruebas / límites

Demo usa fixtures sintéticos de búsqueda para comprobar Person/Member/destino y alcance local; rechaza CRV/modalidad en incorporación y exige Obediencia y grado como la API. No simula todavía el alcance central, alta externa nueva, comisión, materialización ni traslado. Once pruebas nuevas del adaptador; las pruebas CRV usan identidad sintética existente para no esconder fallos de fecha detrás de un ID inexistente.

Frontend actualizado: 353/353 PASS, lint/build PASS. La primera ejecución de la prueba nueva esperaba campos que el DTO de respuesta no declara; se corrigió la expectativa antes de publicar, sin cambiar el contrato. Una prueba HTTP PostgreSQL nueva verifica rechazos sin inserciones, permiso local/central/Tesorería, caso válido, identidad preservada y ausencia de Member prematuro. Backend depende de CI con PMGM_TEST_POSTGRES: sin SDK local. Gates y recibos del SHA resultante se registran en PR/Drive; evidencia anterior no certifica este corte.

Main congelado. Srv01 pausado, despliegue QA pendiente; sin QA física/UAT. No cerrado #275 ni #116.
