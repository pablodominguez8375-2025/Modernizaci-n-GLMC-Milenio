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
7. tres entrevistas;
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
