# Cierre documental de Tenidas v1 — PR #110

Fecha: 2026-09-19  
PR: #110 — `feat(tenidas): cierre documental regular y ceremonial`

## Decisión implementada

El circuito de Tenidas separa los estados:

`Programada → Realizada → Cerrada`

- **Realizada** registra que la Tenida se efectuó y habilita asistencia, escrutinios y documentación.
- **Cerrada** representa el cierre documental definitivo.

## Requisitos de cierre

### Tenida regular / no ceremonial

Requiere únicamente:

- Extracto de Acta PDF.

No condicionan el cierre:

- Plancha de trabajo del hermano;
- Acta completa.

### Tenida ceremonial

Iniciación, Aumento de Salario y Exaltación requieren simultáneamente:

- Extracto de Acta PDF;
- Plancha de Autorización de Ceremonia emitida por Gran Secretaría.

La Plancha se vincula por referencia al documento oficial ya emitido; no se duplica como archivo del Taller.

La validación comprueba:

- Taller;
- tipo de ceremonia;
- fecha propuesta de la autorización frente a la fecha de la Tenida;
- estado emitido/vigente de la Plancha.

## Backend y datos

- `LodgeMeetingClosurePolicy` centraliza el gate server-side.
- `HeldAtUtc` registra la realización.
- `ClosedAtUtc` registra el cierre documental.
- `LodgeSecretariatRecord.CeremonyAuthorizationDocumentId` vincula la autorización oficial.
- la migración `20260919013000_AddMeetingDocumentClosure` normaliza registros históricos `closed` a `held` y conserva la fecha previa en `HeldAtUtc`;
- auditoría distingue `lodge.meeting.held` y `lodge.meeting.closed`.

## Secretaría del Taller

La interfaz:

- muestra Extracto ✅/❌;
- muestra Plancha de Autorización ✅/❌ cuando la Tenida es ceremonial;
- lista sólo autorizaciones compatibles del Taller;
- habilita **Cerrar Tenida** únicamente cuando se cumplen los requisitos;
- mantiene separado el botón para remitir Extracto a Gran Secretaría.

## Demo GitHub Pages

El adaptador mock reproduce las mismas reglas con datos ficticios:

- Tenida regular Realizada;
- Tenida ceremonial de Iniciación Realizada;
- Plancha de Autorización ficticia emitida;
- bloqueos y cierre final equivalentes al backend.

## QA

Este corte consolida el trabajo pendiente del PR #107 sin fusionar su rama obsoleta:

- QA-022: flujo reglamentario integral de insinuaciones;
- QA-023: cierre documental regular y ceremonial.

El gate de regresión srv01 pasa a **23/23**.

## Estado

El PR #110 debe completar PMGM CI, Showcase y QA Installable sobre su HEAD exacto antes de merge. Después del merge se debe verificar la triple salida del mismo SHA y actualizar Línea Base Maestra/Issue #97. El despliegue físico en `srv01` continúa siendo validación operacional posterior.
