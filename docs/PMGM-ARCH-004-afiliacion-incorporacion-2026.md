# PMGM-ARCH-004 — Afiliación e incorporación 2026

## Objetivo
Separar los expedientes de **Afiliación** e **Incorporación** del flujo de insinuación y de aumento de salario/exaltación, aplicando el Reglamento General (arts. 2.1–2.6), el Protocolo de Trámites 2026 y el Formulario de Solicitud de Ceremonias 2026.

## Regla de arquitectura
`AdmissionCase` contiene el procedimiento, antecedentes y decisiones. `CeremonyRequest` representa la ceremonia institucional posterior. Nunca se crea una pertenencia activa en el Taller destino antes de que el expediente esté habilitado, la ceremonia esté autorizada y la Tenida ceremonial correspondiente haya sido cerrada documentalmente.

## Clasificación de Afiliación
La modalidad y el procedimiento son dimensiones distintas y no deben inferirse entre sí.

**Modalidad `AffiliationMode`:**
- `simple`: hermano activo de la Obediencia;
- `activation`: hermano en sueño que debe activarse.

**Procedimiento `AffiliationProcedure`:**
- `standard`: afiliación que no corresponde a reintegro ni traslado;
- `reentry`: reintegro;
- `transfer`: cambio desde un Taller de origen hacia un Taller destino.

La Incorporación desde otra Obediencia se identifica mediante `AdmissionType = incorporation`; no se disfraza como modalidad o procedimiento de Afiliación.

## Controles reglamentarios
El flujo debe conservar como decisiones/evidencias trazables:

- revisión del art. 2.3 por Régimen Interior y, si corresponde, indulto de Gran Maestría;
- presentación escrita y lectura en Cámara de Primer Grado;
- comisión de información del art. 2.5 cuando corresponda;
- decisión de tramitación en Cámara del Medio por al menos dos tercios de los Maestros presentes;
- balotaje secreto posterior en Cámara de Primer Grado, guardando sólo recuentos agregados;
- nueva presentación y subsanación cuando exista rechazo previo;
- reconocimiento de regularidad y aceptación especial de Gran Maestría cuando corresponda a Incorporación.

### Comisión del art. 2.5
El Venerable Maestro nombra una comisión de **tres Maestros** para reintegro e Incorporación. En Afiliación con traslado la comisión también se considera requerida, pero la Cámara del Medio puede dispensarla expresamente. Esa dispensa sólo es válida para `transfer` y debe registrar fecha, acta/fuente y actor.

Una conclusión de comisión debe corresponder al **último grupo nombrado** y no puede anteceder al nombramiento ni registrarse después de la decisión de 3.er grado que pretende habilitar. Una dispensa posterior a la decisión de 3.er grado tampoco sanea retroactivamente el expediente.

## Carta de Retiro Voluntario
La Carta de Retiro Voluntario debe estar vinculada como documento trazable. El sistema registra una **verificación humana** de la firma original de puño y letra; no intenta autenticar automáticamente una firma ni considera suficiente una imagen o firma digitalizada cuando la fuente institucional exige original manuscrito.

## Incorporación desde otra Obediencia
La persona puede no existir como Miembro GLMCh. Antes de materializar debe constar, según corresponda:

- Obediencia y Logia de origen;
- grado y antecedentes legalizados de iniciación/aumento/exaltación;
- Carta de Retiro y verificación manuscrita;
- reconocimiento de regularidad cuando la Obediencia no sea reconocida;
- existencia o no de Pacto de Paz y Amistad;
- aceptación especial de Gran Maestría cuando no exista Pacto;
- decisiones y antecedentes exigidos por el procedimiento y por la matriz de ceremonia.

La creación de `Member` y de la pertenencia GLMCh ocurre sólo en la materialización final.

## Afiliación con traslado y continuidad histórica
`transfer` exige `OriginOrganizationId` explícito, distinto del Taller destino, y una pertenencia activa del mismo `MemberId` en el Taller de origen.

Al materializar la ceremonia el sistema reutiliza la semántica institucional de `MemberTransfer`:

1. bloquea la solicitud de ceremonia dentro de la transacción principal;
2. cierra la pertenencia de origen con fecha de término igual al día anterior a la fecha efectiva;
3. conserva intactos los registros históricos del Taller de origen;
4. crea la nueva pertenencia en el Taller destino para el **mismo `MemberId`**;
5. crea `MemberTransfer` ejecutado y el hito `workshop_transfer`;
6. audita actor, origen, destino y referencias documentales.

No se crea un segundo Miembro por traslado y no se sobrescribe la pertenencia histórica.

## Cierre documental de la Tenida ceremonial
Afiliación e Incorporación usan el mismo flujo de Secretaría/Tenidas ya existente; no existe un cierre paralelo.

Para materializar se requiere:

- `CeremonyRequest` autorizada;
- Tenida del mismo Taller, mismo tipo de ceremonia y fecha efectiva;
- Tenida en estado `closed`;
- `LodgeSecretariatRecord` con **Extracto de Acta adjunto**;
- **Plancha de Autorización de Gran Secretaría adjunta**, emitida y vinculada a la misma solicitud de ceremonia.

Una Tenida regular conserva su regla vigente: Extracto de Acta obligatorio para cerrar. Una Tenida ceremonial exige Extracto + Plancha.

## Actor, permisos y privacidad
Las actuaciones se segregan por atribución:

- Régimen Interior: control del art. 2.3;
- Secretaría del Taller: creación/gestión del expediente, presentación, registro agregado de decisiones/votaciones, solicitud de ceremonia y materialización documental;
- Venerable Maestro: nombramiento de la comisión de tres Maestros; puede registrar las actuaciones de comisión autorizadas por el flujo;
- Gran Maestría, Gran Secretaría y otros órganos: mantienen sus permisos institucionales ya definidos para decisiones, autorización y plancha.

El frontend expone las acciones según capacidad, pero el backend sigue siendo la autoridad final.

El balotaje nunca almacena identidad del votante, preferencia individual ni una secuencia correlacionable; sólo totales agregados.

## Consistencia e idempotencia
Miembro/pertenencias/`MemberTransfer`/`CeremonyRequest` se actualizan dentro de la transacción principal del contexto institucional. La reconciliación de `AdmissionCase` vive en su contexto de admisiones y es **recuperable e idempotente**: un reintento sobre una ceremonia ya completada no crea otra pertenencia ni otro traspaso y repone, si fuera necesario, la decisión `ceremony_completed` conservando el actor original desde auditoría.

Esto es un mecanismo de consistencia recuperable entre contextos, no una afirmación de transacción distribuida atómica.

## Respaldo Secretario + Venerable
El Formulario de Solicitud de Ceremonias 2026 identifica a Secretario/a y Venerable Maestro como firmantes. Este incremento no inventa una nueva firma digital equivalente: conserva esa exigencia como respaldo documental/procedimental. Cualquier cofirma digital nativa debe definirse como decisión funcional separada y auditable.

## Modelo principal
`AdmissionCase` contiene, entre otros: `OrganizationId`, `AdmissionType`, `AffiliationMode`, `AffiliationProcedure`, `MemberId`, `PersonId`, `OriginOrganizationId`, datos de origen, grado, Pacto, regularidad, rechazo previo y estado.

`AdmissionEvidence` mantiene documento/version, tipo, fecha, revisión, actor y observaciones. `AdmissionDecision` mantiene decisiones append-only, fecha efectiva, fuente, datos estructurados y actor. Los nombramientos de comisión conservan un `AppointmentGroupId` para invalidar conclusiones de grupos reemplazados.

## Fuentes normativas vigentes
La implementación fue contrastada el 19-09-2026 con la Constitución y Reglamento General, Protocolo de Trámites de 31-08-2026 y Formulario de Solicitud de Ceremonias 2026 disponibles en la carpeta institucional Proyecto Centenario de Google Drive. Las futuras modificaciones normativas deben revisarse contra la fuente oficial vigente antes de cambiar estas reglas.
