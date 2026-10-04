# Registro parcial — alta externa de incorporación

Issue #275 / PR draft #276. Continúa [identidad POST](2026-10-03-issue-275-identidad-post.md). Base propia `3647bb4db7395d6af02d5348de53b8d832aebbeb`, dev incorporado `14eceafa6b1e014147fd0046f8e5f47cd1a028dc` (Claude #295/UI v0.81).

## Contrato y diccionario

POST `/api/admisiones/incorporaciones/persona-nueva` recibe destino OrganizationId UUID; FirstNames/LastNames obligatorios hasta 160; RutOrInstitutionalId obligatorio normalizado (quita puntos, guiones y espacios, mayúsculas, alfanumérico hasta 16); OriginObedience obligatoria hasta 240; Degree apprentice/fellowcraft/master; OriginLodgeName nullable hasta 240 y OriginLodgeNumber nullable hasta 80.

Persistencia: `core.people` (PK Id UUID; FirstNames/LastNames; Rut string existente, índice único); `core.admission_cases` (PK Id; PersonId referencia lógica a people, OrganizationId a organizations, MemberId null); `core.audit_events` evento admission.external.intake.created (referencia de expediente, actor y destino, sin identificación ni nombres en metadata). Persona tiene 0..N expedientes, sin Member ni Membership al alta. Una transacción Serializable comparte conexión entre contextos y confirma las tres escrituras juntas. No nuevas tablas, columnas, PK/FK, índices ni migración SQL; no presenta PersonId como nueva FK física.

Respuesta 201: mismo DTO mínimo AdmissionCaseResponse con Id, OrganizationId, PersonId, MemberId null, AdmissionType incorporation, AffiliationMode/WithdrawalLetterGrantedDate null, Status under_review, CreatedAtUtc. Sin identificación, contactos ni ficha privada. 400 datos inválidos, 403 destino sin permiso, 404 destino no Taller, 409 identidad existente/conflicto concurrente: mensaje genérico sin Id ni datos de tercero.

## Antes / después y motivo

Antes: la vista sólo podía buscar/reutilizar Personas de expedientes autorizados; no permitía registrar una identidad externa nueva. Después: acción propia crea Persona y expediente de incorporación juntos, sin pasar por insinuaciones/CeremonyRequest. No reutiliza ni modifica automáticamente una identidad existente; requiere revisión autorizada para evitar duplicados/exposición. Grado declarado determina aplicabilidad de evidencia de aumento (Compañero/Maestro) y exaltación (Maestro), conforme al protocolo 2026; evidencia aún no acreditada. Pacto queda null, sin aprobación implícita.

Fuente oficial: PROTOCOLO-PARA-LA-TRAMITACIÓN-DE-INSINUACIONES-AFILIACIONES-Y-SOLICITUDES-DE-CEREMONIAS-2026: antecedentes legalizados según grado, Carta de Retiro Voluntario con original manuscrito y autoridad de Gran Maestría cuando no exista Pacto. Registrar identidad no sustituye esos requisitos. No nuevas tarifas, firmas ni atribuciones.

El contrato reutiliza la capacidad histórica Person.Rut/identificación hasta 16; no crea catálogo de pasaportes/país ni verifica autenticidad documental o dígito RUT. Identificaciones que excedan esa capacidad requieren revisión de modelo: no truncar ni inventar identificación alternativa. Normalización protege altas de este endpoint; no certifica ausencia de duplicados históricos entre países/todos los módulos. 409 no es recibo idempotente del alta previa.

## Demo / verificaciones / pendientes

ActionDrawer con identidad, procedencia/grado y resumen/confirmación. Borrador conserva campos al cerrar; cambiar Taller remonta el panel para evitar traslado de datos al destino equivocado. Demo guarda sólo fixtures introducidos en memoria de la instancia, busca nueva Persona en su Taller y bloquea repetición normalizada; no es DB ni autoridad central. Usa el contrato real, sin alta de Member ni circuito de iniciación.

Frontend 361/361 PASS, lint/build PASS. Siete pruebas nuevas: búsqueda local, privacidad de otro Taller, duplicación, validación y transporte no-store. Una prueba HTTP PostgreSQL nueva verifica permisos, datos inválidos, alta, repetición normalizada, rollback tras fallo de guardado del expediente (sin Persona/auditoría parcial), conservación de identidad, evidencia aplicable y carrera concurrente (una alta/otro conflicto). Ejecución backend depende de CI/PMGM_TEST_POSTGRES, sin SDK local. No navegador Chromium local disponible: revisión visual del panel nuevo pendiente; Showcase global no sustituye prueba visual específica del asistente.

Pendientes: autoridad/evidencia completa, cronología, comisión, materialización/traslado idempotente, revisión de duplicación en POST reutilización, UI operativa insinuados/balotaje y UAT institucional. Sin despliegue: schema operacional no certificado. Main congelado, srv01 pausado, despliegue QA pendiente.
