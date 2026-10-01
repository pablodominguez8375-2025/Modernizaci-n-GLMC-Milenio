# PMGM-GAP-001 — Brechas entre implementación `dev` y flujos institucionales 2026

**Estado:** Vigente  
**Fecha:** 2026-09-11  
**Rama evaluada:** `dev`  
**Fuentes institucionales:** carpeta Google Drive `Proyecto Centerario` y documentos 2026 consolidados en `PMGM-SRC-001` / `PMGM-REQ-001`.

## 1. Propósito

Registrar brechas verificables entre la implementación actual y los procedimientos institucionales 2026, sin inferir requisitos no contenidos en las fuentes.

## 2. Hallazgos principales

### GAP-001 — Tipos de ceremonia incompletos
La implementación actual reconoce `initiation`, `wage_increase` y `exaltation`. El formulario institucional contempla además **Afiliación**, **Incorporación** y **Otra**.

**Acción requerida:** ampliar catálogo, endpoints, validaciones, DTOs, persistencia, UI y pruebas.

### GAP-002 — Falta visto bueno explícito de Gran Maestría
La política actual de elegibilidad evalúa Régimen Interior, Gran Tesorería, Gran Hospitalaria y, para iniciación, publicación del insinuado. El protocolo institucional indica que la autorización final requiere visto bueno de **Gran Maestría**.

**Acción requerida:** incorporar `grand_mastership` como validación requerida antes de autorizar/emitar plancha cuando corresponda.

### GAP-003 — Insinuación modelada con estados insuficientes
`CandidateIntake` dispone de estados de revisión por Gran Secretaría, pero las fuentes describen un flujo más amplio:
1. presentación en tenida de 1.er grado y Saco de Proposiciones;
2. espera mínima de 7 días;
3. deliberación en Cámara del Medio o Consejo de Administración;
4. votación abierta y unanimidad;
5. publicación en intranet;
6. permanencia mínima de 20 días corridos;
7. mínimo tres entrevistas, ampliables por decisión del Venerable Maestro, con resumen, resultado y archivo privado;
8. Cuestionario Confidencial y autobiografía;
9. conocimiento y votación abierta en 3.er grado;
10. balotaje definitivo en 1.er grado;
11. eventual solicitud de ceremonia.

**Acción requerida:** máquina de estados de expediente con transiciones backend y bitácora inmutable.

### GAP-004 — Nueva presentación después de rechazo
Las fuentes establecen que, tras rechazo, una persona no puede ser presentada nuevamente hasta transcurrido al menos **un año** y luego de subsanar las causas del rechazo.

**Acción requerida:** guardar rechazo anterior, fecha, motivo/causas y control temporal de nueva presentación.

### GAP-005 — Terminología/autoridad de dispensas no alineada
`AdvancementEligibilityPolicy` usa `CouncilApproved` y mensajes de “Consejo de Maestros”. El formulario/protocolo 2026 atribuye la dispensa de requisitos de aumento de salario/exaltación a la **Cámara del Medio**, conforme a los artículos citados en el formulario.

**Acción requerida:** alinear modelo, nombres y evidencia con la autoridad descrita en la fuente institucional. No migrar datos históricos sin estrategia explícita.

### GAP-006 — Matriz financiera demasiado agregada
La implementación usa una regularidad general de Tesorería/Hospitalaria. El protocolo exige evidencias específicas según el trámite, entre ellas:
- Cuadro del Taller del último pago;
- cuota mensual vigente;
- derecho de ceremonia;
- incorporación al Fondo de Defunción cuando aplique;
- reposiciones del Fondo de Defunción cuando aplique;
- cuota de Hospitalidad para logias de Santiago;
- identificación del beneficiario en el derecho de ceremonia.

**Acción requerida:** modelar requisitos financieros como ítems de evidencia y no sólo como un estado agregado.

### GAP-007 — Reglas especiales de afiliación faltantes
Para afiliación simple o con activación se exige Carta de Retiro Voluntario y el protocolo indica que el original debe contener firmas manuscritas; no se aceptan firmas digitalizadas o imágenes insertadas.

**Acción requerida:** checklist documental específico y estado de verificación manual del original.

### GAP-008 — Incorporación desde otra Obediencia incompleta
Se requieren antecedentes legalizados que acrediten iniciación, aumento de salario, exaltación y grado, según corresponda; además Carta de Retiro Voluntario. Si no existe Pacto de Paz y Amistad, la aceptación corresponde a Gran Maestría.

**Acción requerida:** expediente de procedencia, pacto vigente y ruta de aprobación condicionada.

### GAP-009 — Reserva de Gran Templo requiere anticipación mínima
El formulario de uso del Gran Templo exige ingreso con **al menos siete días hábiles de anticipación** y registra equipamiento/requerimientos técnicos.

**Acción requerida:** validación de plazo hábil, disponibilidad y recursos; impedir doble reserva.

### GAP-010 — Generación de plancha debe ser resultado del workflow
Las planchas 2026 son documentos de autorización emitidos por Gran Secretaría con visto bueno previo. La realización debe informarse posteriormente a Gran Secretaría y Régimen Interior para validación y boletín.

**Acción requerida:** plancha generada desde datos del expediente aprobado, numeración auditable, y cierre posterior de ceremonia.

### GAP-011 — Protección de información confidencial
Autobiografía, Cuestionario Confidencial, informes de entrevistas y antecedentes personales forman parte del expediente y no deben quedar disponibles por simple pertenencia al Taller.

**Acción requerida:** permisos explícitos por rol/etapa, auditoría de acceso y separación de DTOs públicos/internos.

### GAP-012 — CRV, CRF y cambio CRF→CRV deben ser eventos históricos
Los formularios institucionales identifican fecha, acta, fundamento, grado y firmas.

**Acción requerida:** modelar eventos de membresía append-only; un cambio CRF→CRV no elimina la CRF anterior, sino que registra un nuevo evento que modifica el estado vigente.

### GAP-013 — Fondo de Defunción requiere voluntad testamentaria asociada al miembro
El formulario registra beneficiario principal y subsidiario con identificación y contacto.

**Acción requerida:** módulo de voluntad testamentaria con acceso restringido, historial de versiones y fecha de vigencia.

### GAP-014 — Gran Tesorería requiere composición individual y excepciones
El cuadro de pago 2026 separa Maestros, Compañeros y Aprendices; registra RUT, cargo, valor de cuota, observaciones, rebajas por tercera edad/cónyuge/estudiante, Past Activo, transferencia/depósito y diferencia.

**Acción requerida:** cálculo mensual por miembro, excepción respaldada por plancha/vigencia, total esperado, pagos y conciliación.

## 3. Prioridad de implementación

### P0 — Bloqueantes del flujo institucional
- GAP-001 Tipos de ceremonia.
- GAP-002 Gran Maestría.
- GAP-003 Máquina de estados de insinuación.
- GAP-004 Reingreso post-rechazo.
- GAP-006 Matriz financiera desagregada.
- GAP-010 Plancha + cierre posterior.

### P1 — Reglas especializadas obligatorias
- GAP-005 Dispensaciones.
- GAP-007 Afiliación.
- GAP-008 Incorporación.
- GAP-009 Reserva Gran Templo.
- GAP-011 Confidencialidad.

### P2 — Historia institucional y módulos complementarios
- GAP-012 CRV/CRF.
- GAP-013 Voluntad testamentaria.
- GAP-014 Conciliación Gran Tesorería detallada.

## 4. Criterio de cierre

Una brecha se considera cerrada sólo cuando existe:
1. implementación backend;
2. persistencia/migración si corresponde;
3. autorización/seguridad;
4. interfaz o contrato API demostrable;
5. prueba automatizada;
6. caso UAT asociado;
7. documentación actualizada.

> **Lectura vigente:** la sección 6 sustituye los veredictos de la auditoría histórica de la sección 5. Esta última conserva errores documentados; no usarla para reimplementar funciones.

## 5. Auditoría de estado — 22-09-2026

**Rama evaluada:** `dev@225db8d3b2243ac7b746583410289076082c8eb8` (HEAD vivo al momento de la auditoría).
**Método:** inspección directa del código real (backend `PMGM.Api/Modules/*`, frontend `frontend/src/*`), no solo de este documento ni de conversaciones previas. Cada veredicto cita el archivo/símbolo revisado. Los gaps marcados "requiere revisión más profunda" no fueron confirmados como cerrados ni como abiertos con certeza suficiente; no dar por resueltos sin verificación adicional.

Ninguno de los hallazgos de esta auditoría se cerró en este corte: es diagnóstico, no remediación. Los gaps confirmados como **abiertos** deben tratarse como pendientes reales del backlog, no como histórico.

| Gap | Veredicto | Evidencia |
|---|---|---|
| GAP-001 Tipos de ceremonia | **Parcial** — backend ya soporta más tipos de lo que el documento original asumía; el hueco real está solo en frontend | `backend/.../Ceremonies/CeremonyCodes.cs` ya define `Type.Affiliation` y `Type.Incorporation`. `frontend/src/CeremoniesPage.tsx` → `ceremonyTypeLabel()` solo reconoce `initiation` y `wage_increase`; cualquier otro valor (incluidos Affiliation/Incorporation reales) cae en `'Exaltación'` por defecto. Falta también el tipo "Otra". Documentado con test explícito en PR #126 (`CeremoniesPage.test.tsx`). |
| GAP-002 Visto bueno de Gran Maestría | **Abierto** | `CeremonyEligibilityService.MapRequirementCodeToValidationType` solo mapea `regimen_interior`, `gran_tesoreria`, `gran_hospitalaria`. No existe código de requisito para Gran Maestría en el motor de elegibilidad. |
| GAP-003 Máquina de estados de insinuación | **Parcial** | Existen `CandidateIntakeCodes.ReviewStatus`, `CeremonyCodes.RequestStatus` (`UnderReview`/`Authorized`/`Rejected`), `PublicationStatus` y validación de balotaje (`CeremonyValidations`/`ValidationStatus`). No se confirmó automatización de la espera mínima de 7 días antes de revisión inicial ni de la permanencia mínima de 20 días como bloqueo automático — requiere revisión adicional del flujo de publicación. |
| GAP-004 Reingreso tras rechazo (1 año) | **Abierto** | Sin resultados para lógica de espera de un año/365 días en `Modules/CandidateIntake` ni `Modules/Admissions`. |
| GAP-005 Terminología de dispensas | **Abierto** | `AdvancementEligibilityPolicy.cs` sigue usando `CouncilApproved` y el mensaje literal "El Consejo de Maestros del Taller no aprobó la dispensa", no "Cámara del Medio" como exige el protocolo 2026. |
| GAP-006 Matriz financiera agregada | **Abierto** | `Modules/Treasury/Entities/FinancialRegularitySnapshot.cs` es un único registro con un campo `Status` (string) agregado — sin ítems desglosados (cuota mensual vigente, derecho de ceremonia, Fondo de Defunción, cuota de Hospitalidad, etc.). |
| GAP-007 Firma manuscrita en afiliación | **Sin evidencia de control automatizado** | Existe `Modules/Admissions` (`AdmissionEligibilityPolicy`, `AdmissionWorkflowCodes`), pero no se encontró ningún campo o verificación relacionado con "firma manuscrita" del original de la Carta de Retiro Voluntario. Podría estar cubierto por control manual vía clasificación en Gestor Documental — requiere confirmación explícita del Sponsor sobre si eso satisface el requisito. |
| GAP-008 Incorporación desde otra Obediencia | **Requiere revisión más profunda** | El módulo `Admissions` existe; no se confirmaron campos específicos de Pacto de Paz y Amistad ni la ruta de aprobación condicionada a Gran Maestría cuando no existe pacto vigente. |
| GAP-009 Anticipación mínima Gran Templo | **Abierto** | No se encontró validación de anticipación mínima (7 días hábiles) en los endpoints de reserva de espacios de Gran Secretaría ni en `GrandSecretariatPage.tsx`. |
| GAP-010 Plancha como resultado del workflow | **Resuelto** | `GrandSecretariatPage.tsx` → `CeremonyAuthorizationPanel` emite la Plancha (`issueSecretariatCeremonyAuthorization`) directamente desde los datos del expediente de la cola de ceremonias ya autorizadas, con `SecretariatDocument` para numeración/trazabilidad. Coincide con el requisito de que la plancha se genere desde el workflow, no como entrada manual libre. |
| GAP-011 Protección de información confidencial | **Resuelto** | `DocumentManagementPage.tsx` implementa clasificación (`internal`/`confidential`/`sensitive`/`restricted`) y política de acceso (`library_authenticated`/`organization_authenticated`/`management_only`) por documento. Cubierto además por tests nuevos en PR #128 (`DocumentManagementPage.test.tsx`). |
| GAP-012 CRV/CRF como eventos históricos | **Abierto** | No se encontró un modelo de eventos CRV/CRF append-only en `Modules/Membership`. Los formularios NUEVO-FORMULARIO-CRV/CRF-2026 existen como documentos en Drive, pero no se evidenció su modelado como eventos históricos en el backend. |
| GAP-013 Voluntad testamentaria (Fondo de Defunción) | **Abierto** | Hospitalaria tiene la categoría de movimiento `death_replenishment` (reposición por fallecimiento), pero no existe módulo de voluntad testamentaria ni registro de beneficiario principal/subsidiario. |
| GAP-014 Composición individual Gran Tesorería | **Resuelto** | `Modules/Treasury/TreasuryCodes.cs` → `LodgeFeeType` define `Normal`/`Student`/`Senior`/`Spouse`/`PastActive`, reflejado en `frontend/src/LodgeTreasuryPanel.tsx` (`feeLabel`). La composición individual con excepciones ya está implementada. |

### Resumen
- **Resueltos (3):** GAP-010, GAP-011, GAP-014
- **Abiertos, confirmados (7):** GAP-002, GAP-004, GAP-005, GAP-006, GAP-009, GAP-012, GAP-013
- **Parciales (2):** GAP-001 (backend listo, frontend pendiente), GAP-003 (estructura base presente, faltan validaciones temporales)
- **Requieren revisión adicional antes de veredicto (2):** GAP-007, GAP-008

### Siguiente paso recomendado
No cerrar ningún gap en este documento sin las 7 condiciones de la sección 4 (Criterio de cierre). Priorizar P0 según la sección 3 ya existente: de los P0, **GAP-002, GAP-004 y GAP-006 siguen completamente abiertos** y **GAP-001/GAP-003 son parciales** — ninguno de los P0 está resuelto todavía. De los P1, únicamente GAP-011 está resuelto. Del P2, únicamente GAP-014 está resuelto.


## 6. Revisión vigente — 01-10-2026 (#245 / PR #246)

Base inspeccionada: `dev@a1f9b8c8782a59d3d7843326f6753cadc278cf6e`; `main@6dfb9546a4873baff15955cf86abfd7d47e3d111` permanece congelado. Se contrastaron código, políticas, endpoints, pruebas, diferencias y comentarios de #131/#116/#60/#58/#1, Línea Base y adendas de Claude #242/#244. Los veredictos siguientes describen presencia técnica; no acreditan QA física/UAT, pendientes en #97.

### Corrección de la auditoría histórica y residual actual

La sección 5 subestimó funciones existentes: GAP-002/004 no carecen de implementación; GAP-003 tiene controles temporales; GAP-007/008 sí tienen contratos explícitos; GAP-012 tiene eventos históricos. #131 propuso corregirlo sobre un corte antiguo. Esta revisión conserva ese antecedente y actualiza su evidencia sin fusionar ni modificar la rama histórica. No se trasladan automáticamente sus conteos o cierres.

| Gap | Evidencia técnica vigente | Brecha real / aceptación pendiente |
| --- | --- | --- |
| GAP-001 Tipos de ceremonia | `CeremonyCodes.Type` y `frontend/src/ceremonyTypes.ts` reconocen cinco tipos; `ceremonyTypes.test.ts` verifica paridad y el código desconocido no se rotula Exaltación. | Los cinco tipos están implementados. «Otra» no existe en contrato backend; requiere definición normativa antes de añadirla. No reconstruir las etiquetas corregidas por #130. |
| GAP-002 Gran Maestría | `CeremonyEligibilityPolicy.EvaluateGrandMaster`, `GrandMasterCeremonyEndpoints` y `GrandMasterCeremonyAuthorizationPolicyTests` contienen requisito bloqueante y autorización por rol. | Implementado técnicamente; aceptación operacional pendiente. La búsqueda exclusiva en MapRequirementCodeToValidationType no demuestra ausencia. |
| GAP-003 Insinuación | `CandidateIntakeWorkflowPolicy` y `CandidateWorkflowEndpoints`: espera inicial de 7 días, publicación configurable de 20 días, entrevistas, tercer grado y balotaje; `CandidateIntakeWorkflowPolicyTests`. | Implementado técnicamente. Preservar pruebas HTTP #233 y evidencia histórica/política #239/#240; aceptación operacional pendiente. |
| GAP-004 Re-presentación tras rechazo | `CandidateIntakeWorkflowPolicy` usa `rejectionDate.AddYears(1)`; `CandidateRejectionPolicy.IsOrderBlocking` y `CandidateRejectionPolicyTests` controlan antecedente transversal. | Implementado técnicamente; no reemplazar por 365 días ni reabrir el rechazo ya controlado. |
| GAP-005 Dispensación | `AdvancementEligibilityPolicy` mantiene `CouncilApproved` y mensajes «Consejo de Maestros». | Abierto: terminología y circuito persistido de Cámara del Medio. #48 y #116 conservan alcances; esta revisión no cambia autoridad ni reglas. |
| GAP-006 Matriz financiera | Cuadro mensual por hermano, `CeremonyRightPayment` y `CeremonyEndpoints` registran pagos, monto/moneda/saldo por expediente; `CeremonyEligibilityService` consume `CeremonyRightPaid`. | Parcial: Gran Tesorería/Hospitalaria aún aportan snapshots agregados. No afirmar ausencia total de derechos ceremoniales ni desagregación completa del Fondo de Defunción/Hospitalidad. Preservar #190 y REQ-025. |
| GAP-007 Firma manuscrita | `AdmissionEndpoints` expone verificación de carta; `AdmissionEligibilityPolicy` exige `withdrawal_letter_handwritten_signature`; auditoría `admission.withdrawal_letter.handwritten_signature_verified`. | El control de verificación existe; no equivale al circuito completo de afiliación de #116 ni a las cuatro firmas de CRV/CRF. |
| GAP-008 Otra Obediencia | `AdmissionEligibilityPolicy.AddIncorporationRequirements` exige antecedentes legalizados y aceptación especial Gran Maestría cuando no hay pacto; `AdmissionEligibilityPolicyTests`. | Requisitos base implementados; comisión, cronología, materialización y UI integral de #116 no están integrados. |
| GAP-009 Gran Templo | `GrandSecretariatEndpoints` valida disponibilidad/conflictos y reserva vinculada. | Abierto: no existe validación de siete días hábiles ni calendario hábil parametrizado. No inventar feriados o excepciones. |
| GAP-010 Plancha y cierre | `GrandSecretariatEndpoints.IssueCeremonyAuthorizationAsync` registra autorización desde expediente aprobado; documentos y numeración existentes. | Parcial respecto de la decisión posterior: adjuntar Plancha firmada físicamente y cierre de Tenida con Plancha + Extracto de #116 siguen en candidato. No declarar esa decisión integrada ni crear un generador paralelo. |
| GAP-011 Confidencialidad | Gestor Documental clasifica y controla acceso; `DocumentManagementPage.test.tsx`. Portal minimizado/foto protegida probados por #233; #239 conserva evidencia original. | Controles técnicos presentes; validación operacional de identidad/objetos reales pendiente. El legado sin evidencia conserva `legacy_not_recorded`, sin reconstruir historia. |
| GAP-012 CRV/CRF | `WithdrawalEndpoints` agrega `InstitutionalStatusEvent`, termina pertenencia y conserva solicitud de retiro; firma Orador existente. | Parcial: CRF→CRV como nuevo acto y cuatro firmantes completos no están acreditados en dev; `WithdrawalSignaturePolicy` y migración específica siguen en #116. |
| GAP-013 Voluntad testamentaria | Hospitalaria registra reposiciones por fallecimiento y mantiene fondo separado. | Abierto: no existe módulo versionado de beneficiario principal/subsidiario. No confundir reposición con voluntad testamentaria. |
| GAP-014 Cuadro Gran Tesorería | `TreasuryMonthlyStatementLine`, categorías y tarifas oficiales/locales, respaldos y reportes por Taller. Perú ordinario USD 6 integrado por #213. | Implementación técnica presente; tarifario/aceptación restante #190. Past Activos no pagan cuota regular; reposición anual corresponde a Hospitalaria. No reabrir USD 6. |

### PR e issues históricos: decisión de continuidad

| Caso | Ya integrado / diferencia comprobada | Continuidad |
| --- | --- | --- |
| #131 | La corrección propuesta no estaba en dev. Esta sección corrige los falsos negativos y contempla avances posteriores. Las filas históricas de #126–#130 se conservan en PROJECT-CONTINUITY-MASTER. | Sustituir #131 sin merge solo después de integrar esta revisión y comprobar publicación/Drive. No editar su rama. |
| #116 / #66 | Alta de insinuado, Secretarías, navegación por rol, planchas personales, publicación y políticas avanzaron en dev. Siguen ausentes `AdmissionsPage`, `AdmissionNormativeEndpoints`, `WithdrawalSignaturePolicy` y `CompleteAdmissions2026`. | Mantener draft y «NO FUSIONAR TODAVÍA». Las aprobaciones de insinuación/Secretarías no aceptan Afiliación/Incorporación completa. No existe decisión posterior que levante ese límite en los comentarios consultados. Recuperar residual en rama nueva y revisar su aceptación específica antes de integrar. |
| #60 / #59 | Hay códigos de estado, eventos de retiro/traslado y exclusión tarifaria Past Activo; no están `InstitutionalStatusEndpoints/Policy`, `TreasuryOrdinaryDuesEligibilityService` ni movimiento de reincorporación del candidato. | Mantener abierto; no confundir categoría de cuota con transición institucional. Preservar Hospitalaria anual separada y coordinar con Admisiones. |
| #58 / #36 | `MemberSelfEndpoints` ya proyecta `treasuryAccount` desde `LodgeMemberCharges`, pagos, recibos/imputaciones/ajustes; `MemberPortalPage` muestra cartola, períodos y monedas separadas. `MemberSelfServicePostgreSqlTests` cubre consulta propia y datos operacionales. | La cartola ya existe: se corrige el diagnóstico del 30-09 que la daba totalmente pendiente. El candidato agrega un ledger paralelo CLP (`TreasuryLedgerDbContext`, `AddMemberTreasuryLedger`), cargos genéricos/vencimientos y comprobante documental. Conciliar esas diferencias con el modelo vigente, privacidad/no-cache, crédito visible y #191 antes de reemplazar/cerrar. No fusionar el ledger histórico ni declarar todo #58 cubierto. |
| #191 | Recepción/caja separada del período de cuota, múltiples períodos, anticipos, crédito, correcciones/anulación append-only y concurrencia ajuste/ajuste integradas (#221/#228). | Abierto: presentación financiera institucional de anticipos/recuperaciones, validación operacional y carreras ajuste/cierre o imputación como cobertura adicional. No inventar cuentas, tarifas ni devolución efectiva. |
| #1 | Comparador vivo dev→main. | Abierto, no fusionar: main congelado; promoción requiere autorización separada y UAT institucional. |

### Fuentes y límites

START-HERE, AGENTS, GOV-001/002/003, Estado Maestro, NEXT, ARCH-004/012, ADR de expediente/snapshot, REQ-025 y handoffs de cierre #237/#240 y Claude P1/P2 consultados. Drive: Línea Base Maestra, instrucciones del 29-09 EJECUTADA y adendas UX. P3 sigue pendiente de aprobación; no se toca identidad/UI. Las instrucciones ejecutadas no se repiten.

Los resultados de 368 backend / 233 frontend y 823 checksums pertenecen a `d8bf696d`; no certifican esta revisión. Gates/publicación/paquete del nuevo SHA se registrarán en el recibo final #245/#246 y handoff posterior, con lectura de retorno GitHub/Drive. Sin instalación, QA física ni UAT; srv01 permanece pausado.
