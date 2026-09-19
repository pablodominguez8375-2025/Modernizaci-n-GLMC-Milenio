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

PR #110 fusionado a `dev`. Merge SHA: `3833418b8875bd97607557bec07006c924aa56d4`.

Evidencia post-merge:

- PMGM CI: `success`;
- Showcase/Pages: `success`; Pages desplegó `3833418b8875bd97607557bec07006c924aa56d4`;
- QA srv01 Installable: `success`;
- Pre-UAT Installable: `success`;
- ZIP QA público: `Proyecto-Centenario-QA-srv01-3833418b8875.zip`;
- SHA-256 ZIP público: `5d115390bc531cde3c485a2f429f8b099e10d9a487b8086f97315cde66783d50`;
- artifact QA Actions ID: `10573539603`;
- digest artifact: `sha256:053cd3e60f4ab3b75cc4e582df69ae5b3ae6a87da57c3fe5ec2e437cc1406333`;
- regresión versionada: 23 controles, con QA-022 y QA-023.

Pendiente operacional: despliegue físico en `srv01`, smoke/regresión 23/23 y UAT. La Línea Base Maestra e Issue #97 deben apuntar al HEAD vivo de `dev` después de la sincronización documental final.
