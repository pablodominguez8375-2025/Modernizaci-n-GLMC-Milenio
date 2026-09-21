# PMGM — Revisión de continuidad del PR #116 (Afiliación / Incorporación 2026)

**Corte:** 19-09-2026.  
**Base activa `dev`:** `40a0fbfba30b7efd2399ab5cb6e8f8017ae90d9e`.  
**`main` estable:** `6dfb9546a4873baff15955cf86abfd7d47e3d111`.  
**Rama:** `feature/admissions-2026-completion`. **PR #116:** draft, no fusionado. **Issue:** #66. **QA física:** #97.  
**Estado de este documento:** registro de avance; no equivale a autorización de merge ni a despliegue físico en `srv01`.

## Fuentes consultadas
GitHub: `START-HERE.md`, `AGENTS.md`, Estado Maestro, NEXT, ADR/arquitectura, PR #116, Issues #66/#97 y modelo de transferencias `PMGM-REQ-023` / `PMGM-DB-002`. Drive Proyecto Centenario: Línea Base Maestra, Constitución y Reglamento General (arts. 2.1–2.6), Protocolo de Trámites 31-08-2026 y Formulario de Solicitud de Ceremonias 2026.

## Cierre de hallazgos de revisión

1. **Comisión art. 2.5 — implementado en rama.** Se separó `standard | reentry | transfer` de `simple | activation`. Reintegro e Incorporación requieren comisión de tres Maestros; traslado sólo puede omitirla mediante dispensa expresa de Cámara del Medio. El Venerable nombra la comisión.
2. **Consistencia temporal de comisión — implementado y probado.** Conclusión vinculada al último `appointmentGroupId`, posterior al nombramiento y anterior a la decisión de 3.er grado. Una conclusión o dispensa registrada después del 3.er grado no sanea retroactivamente el expediente.
3. **Cierre ceremonial documental — implementado.** Materialización exige Tenida cerrada del mismo Taller/tipo/fecha, Extracto adjunto y Plancha de Gran Secretaría emitida y vinculada a la misma solicitud. Se reutiliza el cierre de Secretaría/Tenidas.
4. **Consistencia/idempotencia — implementado como consistencia recuperable.** Miembro/pertenencias/traslado/ceremonia se confirman en transacción institucional; la reconciliación del expediente es idempotente y recuperable por reintento. No se afirma transacción distribuida atómica entre `PmgmDbContext` y `AdmissionsDbContext`.
5. **Actor y permisos — implementado.** La decisión final conserva al actor real; en reintentos recupera actor desde auditoría. Backend y UI segregan Régimen Interior, Secretaría y Venerable. Se eliminó dependencia de `CanManageOrganization` en actuaciones de Admisiones donde era demasiado amplia.
6. **Calendario/estado — implementado.** La fecha efectiva debe coincidir con la Tenida cerrada y, cuando existe, con la fecha propuesta/autorizada. No se materializa desde una ceremonia no autorizada ni una Tenida sin cierre documental.
7. **Frontend/demo — implementado en la misma aplicación React.** Existe módulo Afiliaciones e incorporaciones, cliente API real/mock, cola de expedientes, selector minimizado de personas, flujo de comisión, votaciones agregadas, ceremonia y materialización; Showcase captura la nueva vista.
8. **QA automatizada — implementada en rama.** `QA-026` se añadió al kit/regression gate; `UAT-FLUJOS-017` (referencia histórica `UAT-017`) documenta el flujo sin confundirse con `UAT-RC1-017` de Biblioteca. Existen pruebas unitarias de política/proyector, pruebas frontend/mock y prueba PostgreSQL de materialización de traslado e idempotencia.

## Regla específica de traslado
Una Afiliación con traslado debe identificar `OriginOrganizationId` y no permite origen=destino. La materialización cierra la pertenencia activa de origen, conserva su historia, crea una nueva pertenencia en destino para el mismo `MemberId`, registra `MemberTransfer` ejecutado y el hito `workshop_transfer`. La operación no duplica al hermano.

## Privacidad de votaciones
La decisión de 3.er grado y el balotaje se almacenan como totales agregados. No se guarda identidad/preferencia individual, asociación votante-voto ni secuencia correlacionable.

## Respaldo del Formulario de Ceremonia
La fuente oficial 2026 contempla respaldo de Secretario/a y Venerable Maestro. El PR no inventa una cofirma digital nueva; esa eventual digitalización queda como decisión funcional separada. La Plancha institucional de Gran Secretaría y el cierre documental de Tenida sí son obligatorios en el flujo implementado.

## QA-026 — criterios
- distinguir modalidad `simple/activation` de procedimiento `standard/reentry/transfer`;
- carta de retiro + verificación humana de firma manuscrita;
- art. 2.3 e indulto cuando corresponda;
- comisión art. 2.5 y dispensa sólo para traslado;
- conclusión/dispensa anteriores al 3.er grado y vinculadas al último nombramiento;
- decisión de 3.er grado por totales agregados y balotaje posterior en 1.er grado;
- regularidad/Pacto/Gran Maestría en Incorporación;
- Plancha + Extracto + Tenida cerrada antes de materializar;
- traslado: misma identidad, cierre origen, alta destino, historial preservado;
- reintento sin duplicar pertenencia/`MemberTransfer`;
- permisos por rol, auditoría y demo sin PII real.

## Estado operativo y handoff
PR #116 permanece **draft** y fuera de `dev`. Los workflows deben evaluarse siempre sobre el HEAD exacto posterior al último cambio; un verde de un SHA anterior no habilita merge. La publicación de GitHub Pages y el paquete QA instalable no equivalen a despliegue físico.

Antes de promover: exigir PMGM CI + PMGM Showcase Demo + Proyecto Centenario QA srv01 Installable en verde para el HEAD final y registrar ese SHA en este documento/Drive. Por decisión del Product Owner de 19-09-2026, la validación física en `srv01` queda temporalmente diferida y no bloquea el avance inmediato; debe permanecer pendiente y trazable en Issue #97, sin declarar el ambiente operacional. La revisión funcional, de permisos y `UAT-FLUJOS-017` continúan vigentes y esta excepción no constituye por sí sola autorización de merge.

Para Planchas oficiales y Decretos, la QA automatizada debe comprobar además que la interfaz mantiene deshabilitada la carga mientras Gran Secretaría no marque expresamente la declaración de firmas físicas; el cliente API no puede completar esa declaración de manera implícita y el backend conserva su validación independiente.

La Demo no puede mostrar el estado **Ceremonia autorizada** durante los vistos buenos o el control administrativo. Ese estado nace únicamente después de completar **Plancha y programación**; la activación como Aprendiz continúa separada y sólo ocurre tras registrar la ceremonia efectivamente realizada.

La etapa **Plancha y programación** no genera documentos. Gran Secretaría debe ingresar una descripción, adjuntar el PDF firmado físicamente y confirmar expresamente las firmas antes de continuar; el botón permanece bloqueado mientras falte cualquiera de esos elementos.


## Addendum — alineación con Protocolo de Trámites 2026

Se contrastó el circuito vigente con el documento oficial PROTOCOLO-PARA-LA-TRAMITACIÓN-DE-INSINUACIONES-AFILIACIONES-Y-SOLICITUDES-DE-CEREMONIAS-2026.docx (31-08-2026). Quedan explícitos estos controles: formulario completo y patrocinio; presentación en 1.er grado; espera mínima de 7 días y unanimidad inicial; publicación mínima de 20 días; tres entrevistas más Cuestionario Confidencial y autobiografía; revisión y votación abierta de 3.er grado; balotaje de 1.er grado; antecedentes y vistos buenos institucionales; y Plancha de Gran Secretaría.

La fecha ingresada antes de la autorización se conserva únicamente como fecha referencial solicitada. La reserva o programación de ceremonia está bloqueada hasta la existencia de Plancha de Autorización emitida. La Tenida ceremonial sólo puede cerrarse con Extracto de Acta y Plancha adjuntos; las Tenidas regulares mantienen sólo Extracto de Acta obligatorio.

Implementación de esta regla: HEAD 6a03030a12a67e72e88b986c084502c276b88625, PR #116 draft/no fusionado. Deben regenerarse los tres gates exact-head y publicar el mismo SHA en Pages/QA antes de UAT final.


## Addendum — fecha de ceremonia sólo en solicitud de Plancha

Se eliminó la fecha propuesta de ceremonia del formulario **+ Nuevo insinuado** y del contrato de creación del borrador. La carga inicial sólo registra la insinuación y sus antecedentes. La fecha tentativa se captura únicamente al solicitar la ceremonia/Plancha, después de las etapas reglamentarias; no permite reservar ni programar antes de la Plancha.

HEAD funcional de este ajuste: 166bd4f27b97cfd483594f3ed0a3c2ce542deec1. Se agregó prueba frontend que verifica que un nuevo insinuado queda sin fecha ceremonial.
