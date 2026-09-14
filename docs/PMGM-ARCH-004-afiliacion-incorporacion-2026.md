# PMGM-ARCH-004 — Afiliación e incorporación 2026

## Objetivo
Separar correctamente los expedientes de **Afiliación** e **Incorporación** del flujo de aumento de salario/exaltación y del flujo de insinuación, respetando el Protocolo de trámites ante la Gran Secretaría y el Formulario de Solicitud de Ceremonias 2026.

## Regla de arquitectura
El sistema no debe asumir que toda ceremonia distinta de iniciación corresponde a una persona con pertenencia vigente al Taller solicitante. Esa regla sirve para aumento de salario y exaltación, pero no para afiliación ni para incorporación desde otra Obediencia.

## Afiliación
El sistema debe admitir al menos dos modalidades:

- **simple**;
- **con activación**.

El expediente debe registrar como mínimo:

- persona/hermano que solicita la afiliación;
- Taller solicitante (destino);
- Taller de origen y número;
- grado;
- fechas, logias y Obediencias de iniciación, aumento de salario y exaltación según corresponda;
- fecha, tipo y motivo del retiro;
- modalidad de afiliación;
- si corresponde a primera o nueva presentación;
- fecha de rechazo anterior cuando exista;
- constancia de subsanación de las causas del rechazo cuando corresponda;
- aprobación de tercer grado y su acta;
- balotaje de primer grado y su acta;
- documentos y comprobantes exigidos para solicitudes de ceremonia.

### Carta de Retiro Voluntario
La solicitud de afiliación debe incluir copia de la Carta de Retiro Voluntario del Taller de origen. El expediente debe registrar una verificación de que el original se encuentra firmado de puño y letra por quienes corresponda. El protocolo no acepta firmas digitalizadas ni imágenes de firmas insertadas electrónicamente.

No se implementará una validación automática de autenticidad manuscrita. Milenio almacenará el documento y la **declaración de verificación humana** realizada por el rol autorizado, conservando fecha, actor y evidencia documental.

## Incorporación desde otra Obediencia
La incorporación debe utilizar un expediente propio, porque la persona puede no existir todavía como miembro institucional de la GLMCh.

El expediente debe registrar:

- persona solicitante;
- Obediencia de origen;
- Taller de origen y número;
- grado masónico;
- fecha, logia y Obediencia de iniciación;
- fecha, logia y Obediencia del aumento de salario, cuando corresponda;
- fecha, logia y Obediencia de la exaltación, cuando corresponda;
- tipo, fecha y motivo del retiro;
- Carta de Retiro Voluntario y su verificación de firma manuscrita;
- documentos legalizados por la Obediencia de procedencia;
- autorización de Gran Maestría para asistir a tenidas, cuando corresponda;
- logias de la GLMCh visitadas y total de asistencias cuando esos antecedentes sean utilizados;
- existencia o no de Pacto de Paz y Amistad entre la Obediencia de origen y la GLMCh;
- aprobación específica de Gran Maestría cuando no exista Pacto de Paz y Amistad;
- vistos buenos de Régimen Interior, Gran Tesorería, Gran Hospitalidad y Gran Maestría;
- antecedentes generales exigidos para la Solicitud de Ceremonias.

### Regla por Pacto de Paz y Amistad
Cuando la Obediencia de origen **no mantenga Pacto de Paz y Amistad con la GLMCh**, la aceptación de la incorporación es facultad de quien ejerza la Gran Maestría. El sistema deberá bloquear el avance final mientras no exista una resolución registrada de Gran Maestría.

## Documentos comunes de solicitud de ceremonia
Según el protocolo, la tramitación puede requerir, según corresponda:

- Cuadro del Taller del último pago a Gran Tesorería;
- comprobante de pago de cuota mensual vigente;
- pago del derecho de ceremonia;
- incorporación al Fondo de Defunción en los casos aplicables;
- reposiciones del Fondo de Defunción;
- cuota de Hospitalidad para logias de Santiago;
- certificado de antecedentes para fines especiales y documento de identidad para personas chilenas;
- cumplimiento del Decreto n.º 1719 de 29 de abril de 2025 para personas extranjeras.

La arquitectura debe representar estos antecedentes como **requisitos documentales configurables**, no como columnas rígidas en la entidad principal.

## Nueva presentación después de rechazo
El protocolo extiende a iniciación y afiliación la regla de nueva presentación: al menos un año desde el rechazo y constancia de subsanación de las causas. Esta regla reutilizará `CandidateIntakeWorkflowPolicy.EvaluateRePresentation` o una política común equivalente, evitando duplicar lógica.

## Modelo propuesto

### AdmissionCase
Expediente de ingreso/afiliación separado de `CeremonyRequest`.

Campos conceptuales:

- `Id`;
- `OrganizationId` (Taller destino);
- `AdmissionType`: `affiliation` | `incorporation`;
- `AffiliationMode`: `simple` | `activation` | `not_applicable`;
- `MemberId` nullable;
- `PersonId`;
- `OriginOrganizationId` nullable;
- `OriginLodgeName`;
- `OriginLodgeNumber`;
- `OriginObedience`;
- `Degree`;
- `HasPeaceAndFriendshipPact` nullable;
- `PreviousRejectionDate` nullable;
- `RejectionCausesRemedied` nullable;
- `Status`;
- `CreatedAtUtc`.

### AdmissionEvidence
Antecedentes y documentos asociados al expediente:

- tipo de antecedente;
- documento/version documental;
- fecha del antecedente;
- referencia a acta o plancha;
- estado de revisión;
- actor que revisó;
- fecha de revisión;
- observaciones.

### AdmissionDecision
Decisiones append-only:

- deliberación/aprobación del Taller;
- revisión de Régimen Interior;
- Gran Tesorería;
- Gran Hospitalidad;
- Gran Maestría;
- aprobación especial por ausencia de Pacto de Paz y Amistad;
- resolución final.

## Integración con CeremonyRequest
`CeremonyRequest` seguirá representando la **ceremonia a autorizar**. El expediente `AdmissionCase` contendrá el procedimiento y sus antecedentes. Cuando el expediente quede habilitado, la solicitud de ceremonia se vinculará al `AdmissionCase` en vez de forzar al sistema a fingir una pertenencia vigente al Taller destino.

## Reglas de no sustitución

- No crear una membresía activa en el Taller destino antes de la resolución correspondiente.
- No sobrescribir el historial del Taller de origen.
- No convertir automáticamente una incorporación externa en afiliación interna.
- No considerar una imagen de firma como prueba de firma manuscrita.
- No inferir el contenido de los artículos 2.3, 2.4 y 2.5 del Reglamento General si su texto no está incorporado al repositorio documental; el sistema sólo debe registrar que el protocolo exige su cumplimiento hasta disponer de la fuente normativa completa.

## Siguiente implementación

1. agregar tipos `affiliation` e `incorporation` al catálogo de ceremonias;
2. crear `AdmissionCase`, `AdmissionEvidence` y decisiones append-only;
3. crear validación documental de Carta de Retiro Voluntario;
4. modelar Pacto de Paz y Amistad y aprobación especial de Gran Maestría;
5. conectar la matriz institucional de habilitación;
6. generar la plancha sólo con expediente habilitado y snapshot de evidencias.
