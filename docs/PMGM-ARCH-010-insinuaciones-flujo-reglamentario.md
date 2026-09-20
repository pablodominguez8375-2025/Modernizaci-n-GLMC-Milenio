# PMGM-ARCH-010 — Flujo reglamentario de insinuaciones

Estado: **integrado en `dev` mediante PR #106**  
Fecha: 2026-09-18

## 1. Fuentes

Este diseño continúa el flujo ya existente en `CandidateIntake` y `Ceremonies`; no crea un dominio paralelo.

Fuentes verificadas antes de implementar:

- Línea Base Maestra `LB-PC-2026-09-17` vigente en Google Drive.
- Protocolo para la tramitación de insinuaciones, afiliaciones y solicitudes de ceremonias 2026.
- Formulario institucional de Insinuación 2026.
- `START-HERE.md`, `AGENTS.md`, Estado Maestro y `PMGM-GOV-001`.
- implementación vigente de `CandidateIntakeWorkflowPolicy`, `CandidateWorkflowEndpoints`, `CandidatePublication` y ficha privada de insinuado.

## 2. Principio de expediente único

La ficha privada, las validaciones del flujo, la publicación y la solicitud de ceremonia permanecen ligadas al mismo `CeremonyRequestId`.

Una observación o corrección no crea una nueva insinuación. La ficha se actualiza y las decisiones del proceso se conservan como eventos/validaciones auditables.

## 3. Flujo

1. Secretaría registra la ficha y la fecha de presentación en 1.er grado.
2. La deliberación inicial sólo puede evaluarse desde 7 días después de esa presentación.
3. La aprobación inicial exige unanimidad de los presentes.
4. Gran Secretaría revisa y publica el insinuado.
5. La publicación aplica el plazo institucional vigente, actualmente 20 días corridos.
6. Se registran al menos tres entrevistas, con Word/PDF privado, resumen y resultado.
7. El expediente exige además referencia privada del Cuestionario Confidencial y autobiografía.
8. Se registra revisión y votación abierta de 3.er grado.
9. Cumplidos 20 días y con revisión de 3.er grado aprobada, se registra el balotaje de 1.er grado.
10. Con balotaje favorable, Secretaría puede enviar la solicitud formal de Iniciación.

## 4. Correcciones normativas incorporadas

### Fecha base de 7 días

La implementación anterior utilizaba `InsinuationDate` como fecha base. El protocolo exige contar desde la **presentación en 1.er grado**.

El endpoint usa ahora `FirstDegreePresentationDate` y bloquea el proceso si esa fecha no existe.

### Plazo no controlable por cliente

La implementación anterior admitía `minimumWaitingDays` desde el request HTTP. Esto permitía alterar una regla reglamentaria desde la interfaz.

El mínimo de 7 días queda definido server-side en `CandidateIntakeWorkflowPolicy.InitialDeliberationMinimumWaitingDays`; el request ya no acepta un override.

### Rechazos que generan antecedente transversal

El antecedente de la Orden se activa ante:

- rechazo de la revisión/votación de 3.er grado; o
- rechazo del balotaje definitivo de 1.er grado.

Ambos se muestran a Régimen Interior y en la alerta de una nueva presentación. El sistema diferencia el origen del rechazo.

## 5. Interfaz

`CandidateWorkflowPanel` expone el flujo sobre la ficha de Secretaría del Taller:

- estado de cada etapa;
- deliberación inicial;
- espera de publicación por Gran Secretaría;
- carga de entrevistas Word/PDF;
- revisión de 3.er grado;
- balotaje hasta tres trámites;
- solicitud formal de Iniciación.

La ficha puede quedar protegida después de publicación sin impedir que las etapas posteriores sigan operando.

## 6. Demo GitHub Pages

El cliente `CandidateIntakeApiClient` mantiene el mismo contrato para QA real y demo.

En modo demo:

- sólo se usan datos ficticios;
- los documentos se mantienen en memoria del navegador;
- se reproducen las precondiciones de las etapas;
- la publicación de ejemplo aplica 20 días;
- el flujo puede llegar hasta solicitud de Iniciación sin llamar servicios reales.

## 7. Seguridad y privacidad

- entrevistas: privadas y clasificadas como sensibles;
- sólo PDF/DOCX;
- QA real reutiliza validación de tipo real, Object Storage privado y ClamAV;
- la vista pública/institucional del insinuado no expone entrevistas, cuestionario, autobiografía ni observaciones internas;
- no se incorporan datos personales reales a Pages.

## 8. Pruebas

Cobertura agregada:

- regla permanente de 7 días;
- rechazo de 3.er grado como bloqueo transversal;
- rechazo de balotaje como bloqueo transversal;
- distinción del motivo del rechazo;
- recorrido demo completo desde flujo inicial hasta solicitud de Iniciación.

La integración a `dev` se realizó con CI del HEAD exacto del PR #106 en verde. El cierre de QA srv01 incorpora además el control `QA-022` para validar operacionalmente este flujo.

## 9. Relación con QA srv01

Issue #97 permanece abierto. Este incremento funcional no convierte QA en operacional.

Después de integrar a `dev`, la triple salida debe regenerarse desde el mismo SHA:

1. código en `dev`;
2. GitHub Pages;
3. instalable QA srv01.

Luego deberá desplegarse ese SHA en `srv01`, ejecutar smoke y la regresión vigente antes de UAT/promoción a `main`.


## 10. Solicitud de ceremonias y Plancha — regla de programación

La implementación se alinea con el **Protocolo para la tramitación de insinuaciones, afiliaciones y solicitudes de ceremonias 2026** (31-08-2026):

- sólo se completa la solicitud formal de ceremonia después de publicación cumplida, entrevistas, revisión/votación abierta de 3.er grado y balotaje favorable de 1.er grado;
- la solicitud conserva la fecha indicada como **referencial solicitada**, no como fecha programada;
- se mantiene la revisión de antecedentes y vistos buenos de Régimen Interior, Gran Tesorería, Gran Hospitalidad y Gran Maestría;
- Gran Secretaría emite la **Plancha de Autorización de Ceremonia** una vez cumplidos los requisitos y el visto bueno superior;
- ninguna ceremonia puede establecerse, reservarse o programarse antes de que exista esa Plancha emitida y vigente;
- la reserva vinculada a ceremonia se habilita sólo después de la Plancha. La Plancha puede emitirse dejando constancia de que la sala aún no está asignada;
- para cerrar la Tenida ceremonial y materializar el expediente son obligatorios el Extracto de Acta y la Plancha adjunta, vinculados a la misma solicitud. En Tenidas regulares sigue siendo obligatorio sólo el Extracto de Acta.

La API real y el adaptador mock aplican el mismo bloqueo; el backend es la autoridad final.


## 11. Fecha de ceremonia

La carga inicial de un nuevo insinuado no solicita ni almacena fecha de ceremonia. La fecha de insinuación y la presentación en 1.er grado son antecedentes del expediente, no una programación ceremonial.

La fecha tentativa sólo puede indicarse en la etapa posterior de solicitud de ceremonia/Plancha, después de completar entrevistas, revisiones y balotaje. Hasta que Gran Secretaría emita la Plancha de Autorización, esa fecha sigue siendo referencial y no habilita reserva ni programación.
