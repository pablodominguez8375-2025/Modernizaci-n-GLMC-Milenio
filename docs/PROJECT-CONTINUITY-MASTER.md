# Proyecto Centenario — Registro maestro de continuidad

## Corte activo Secretaría — 20-09-2026

- PR #116 permanece `draft` y no debe fusionarse todavía.
- La aceptación funcional del circuito de insinuación se conserva; la fecha tentativa sólo se captura al solicitar Plancha de Autorización.
- Secretaría quedó organizada por función y el retiro aprobado exige las cuatro firmas de Venerable Maestro, Tesorería, Orador y Secretaría antes de materializarse.
- Planchas de Trabajo pasan a expediente independiente: múltiples por hermano, historial, grado, fecha, tema y vínculo opcional a Tenida no ceremonial.
- Una plancha nace privada. Secretaría puede solicitar Biblioteca, pero no autorizar ni publicar por sí sola; Gran Archivero queda fuera de este flujo.
- `srv01` y QA físico permanecen diferidos por instrucción del Sponsor, no aprobados ni eliminados. Demo Pages e instalable deben salir del mismo SHA candidato.
- Secretaría inicia directamente expedientes de Aumento de Salario y Exaltación con hermano, tipo, antecedentes y fecha sólo tentativa. El backend controla grado vigente y bloquea expedientes activos duplicados.
- Una Tenida ceremonial no puede programarse sin Plancha de Autorización emitida por Gran Secretaría y coincidente en Taller, tipo y fecha. Desde la Plancha se crea la Tenida y se vincula automáticamente la autorización.
- La elegibilidad de Aumento de Salario y Exaltación se calcula con evidencias reales del grado de origen y reglas institucionales versionadas: 24 meses continuados, asistencias a Tenidas, asistencias a instrucciones y dos Planchas de Trabajo. Para Aumento rigen 30 Tenidas y 10 instrucciones de 1.er grado; para Exaltación, 10 Tenidas y 10 instrucciones de 2.º grado.
- Una dispensa exige acuerdo afirmativo de Cámara del Medio, acta, identificación del requisito y validación de Régimen Interior. Nunca puede reducir un requisito en más de 50%; Gran Secretaría no puede autorizar mientras los controles ordinarios o la dispensa permanezcan incumplidos.
- Al cerrar una Tenida de Aumento de Salario o Exaltación ya realizada, Secretaría debe contar con Extracto de Acta y Plancha de Autorización emitida por Gran Secretaría para el mismo hermano, Taller, tipo y fecha. El cierre materializa automáticamente el nuevo grado, registra el hito de grado con sus evidencias y completa la solicitud ceremonial.
- La materialización es idempotente y reconciliable: repetir el cierre no duplica el hito ni las auditorías. Un grado previo incompatible o un hito contradictorio bloquea el cierre para revisión por Régimen Interior.
- Corte funcional validado: PR #116 HEAD `41f0d4df66e062a4913f66b59c88efbe1c44745d`; PMGM CI #1372, Showcase Demo/Pages #598 y QA srv01 Installable #236 en `SUCCESS`.
- Artefacto instalable exact-head: ID `10612825318`, digest `sha256:641940edfd6f204ec23b340bb7336e5b6e0c05179f3a083f96dfd2c824042a91`.
- PR #116 continúa `draft` y no fusionado. `dev` permanece en `5704400c6fdd0b787dcb4ec17e36be9e69f4484a`; `main`, en `6dfb9546a4873baff15955cf86abfd7d47e3d111`. `srv01` físico sigue diferido/no validado.
- Drive incorporó tres fuentes institucionales nuevas: guía de uso del logotipo GLMCh, formato de papelería y guía SENADIS de documentos accesibles. El corte siguiente alinea paleta y parámetros con los colores oficiales, adopta reglas de accesibilidad y deja expresamente pendiente el archivo gráfico oficial —no se extrae una captura del manual ni se redibuja el isotipo—. Contrato: `docs/PMGM-UI-002-identidad-accesibilidad-institucional.md`.
- Aclaración funcional del Product Owner: las Planchas oficiales y Decretos no se generan en el sistema. Gran Secretaría registra tipo, título y descripción, confirma bajo su responsabilidad operativa que el PDF contiene las firmas físicas requeridas y carga ese archivo. El sistema conserva, protege, permite descargar y audita el PDF y la declaración del operador; no realiza validación criptográfica ni sustituye la revisión material de las firmas. La Demo reproduce el mismo circuito con archivos ficticios.
- Resguardo de arquitectura: se eliminaron los métodos heredados no publicados que construían documentos o autorizaciones desde JSON sin PDF. Sólo permanecen las rutas de carga del PDF firmado; una prueba HTTP bloquea la reaparición de los contratos históricos sin archivo.
- Resguardo documental: los requisitos canónicos, plan P0, brechas y UAT quedaron alineados con la misma regla. CI ejecuta `tests/official_documents_gate.py` para impedir que vuelva a documentarse generación interna de Planchas oficiales o Decretos.
- Corte funcional validado en `e4b30d1a9a1507275ca45a6ae4fc7cbffaf9b456`: PMGM CI #1380, Showcase Demo/Pages #606 y QA srv01 Installable #244 en `SUCCESS`. Artefacto exact-head ID `10619081026`, digest `sha256:6da4df2fa1a37a93cf01129aead9caec777146e1be811f656a4346312f7f82ac`. PR #116 sigue `draft`, sin fusión; `srv01` físico continúa diferido.

**Última consolidación:** 13 de septiembre de 2026  
**Repositorio:** `pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio`  
**Fuente de código y decisiones:** rama de desarrollo del proyecto

## Propósito

Este documento reúne los acuerdos funcionales, técnicos y de gobierno definidos en las conversaciones del Proyecto Centenario / Modernización Gran Logia Mixta de Chile. Sirve como punto único de continuidad cuando una conversación anterior no aparece en la interfaz.

## Regla permanente de continuidad

Toda conversación, instrucción, corrección o decisión relacionada con el proyecto debe registrarse en este documento único. Cada actualización incorporará:

- fecha y descripción de la conversación;
- decisión o requisito aprobado;
- módulos y documentos afectados;
- implementación, pruebas y publicación asociadas;
- pendientes que pasan al siguiente ciclo.

El documento se actualiza junto con cada avance relevante y se versiona en GitHub. Si se cambia de hilo, se pierde una conversación o se retoma el proyecto después de una pausa, este archivo constituye la referencia oficial para reconstruir el contexto.

### Registro de actualizaciones

| Fecha | Conversación / decisión | Resultado |
|---|---|---|
| 13-09-2026 | Se solicita centralizar todas las conversaciones del proyecto en un único documento para preservar continuidad. | Regla permanente adoptada; este documento queda como registro maestro. |
| 13-09-2026 | Se detecta diferencia entre el reporte de Régimen Interior y el padrón QA. | Reporte alineado a 20 Talleres, 480 integrantes y distribución 12/5/5/2 por Taller. |
| 13-09-2026 | Se solicita demo operacional con cargas temporales. | Demo QA habilitada para fotografías JPG/PNG y antecedentes Word/PDF durante la sesión. |
| 13-09-2026 | Se solicita alerta transversal por rechazo en Cámara del Medio. | Antecedente protegido consultable por Régimen Interior y visible como alerta al Taller. |
| 14-09-2026 | Se solicita un menú Sistema ampliamente parametrizable por administradores. | Se implementa catálogo versionado y auditable para flujos, plazos, Biblioteca, Gestor Documental, Gran Archivo, Tesorería, Hospitalaria, notificaciones y seguridad. |
| 14-09-2026 | Se continúa la parametrización de perfiles y permisos. | Sistema incorpora matriz administrable de vistas y acciones para cargos de Taller, docencia, grandes dignatarios y administradores. |
| 14-09-2026 | Se continúa el control administrativo de parámetros. | Se agrega historial, cambios futuros programados y restauración no destructiva como nueva versión auditada. |
| 14-09-2026 | Se amplía el menú Sistema. | Se incorporan consolas QA para backup/restauración, usuarios con perfiles, correo SMTP e identidad visual; el instalable reutiliza scripts protegidos y Keycloak. |
| 14-09-2026 | Se solicita independencia para configurar vistas y perfiles. | Se incorpora diseñador de perfiles con alcance, vistas y acciones editables, duplicación y persistencia versionada separada del cargo institucional. |
| 14-09-2026 | Se continúa el diseñador de accesos. | Se agrega simulación de acceso efectivo, prevención de escalamiento Taller→Orden y protección contra bloqueo del administrador principal. |
| 14-09-2026 | Se continúa la aplicación de perfiles. | Se agregan asignaciones múltiples por usuario, alcance, vigencia, acceso acumulado y revocación histórica. |
| 14-09-2026 | Se continúa la gobernanza de accesos. | Se agrega auditoría, clasificación de riesgo, certificación/revocación y exportación de revisión de accesos. |

## Última conversación recuperada

La última conversación documental solicitó revisar la carpeta de Google Drive **Proyecto Centenario** y comprobar que los cargos y firmantes de los documentos oficiales estuvieran incorporados al sistema.

La matriz de firmantes a validar es:

- Taller: Venerable Maestro, Secretario, Orador y Tesorero.
- Orden: Régimen Interior, Gran Tesorero, Gran Hospitalaria, Gran Secretaría y Gran Maestra.

La verificación inicial en Google Drive confirma la separación de cargos y añade evidencia documental:

- **EXTRACTO-DE-ACTA-2026.docx:** campos de firma para Venerable Maestro, Secretario/a y Orador/a; además enumera en el cuadro del Taller a I∴ ex V∴M∴, Primer Vigilante, Segundo Vigilante, Orador, Secretario y Tesorero.
- **JURAMENTO-DE-OFICIALES-2026.docx:** exige firma individual de Orador/a, Tesorero/a y Secretario/a; el apartado de vigilantes exige Primer/a y Segundo/a Vigilante.
- **FORMULARIO-SOLICITUD-DE-CEREMONIAS-2026.docx:** establece como firmantes representantes del Taller al Secretario/a y V∴M∴; el protocolo exige copias a Régimen Interior, Gran Tesorería, Gran Hospitalidad y Gran Secretaría, con aprobación final de Gran Maestría.
- **PROTOCOLO-PARA-LA-TRAMITACIÓN-DE-INSINUACIONES-AFILIACIONES-Y-SOLICITUDES-DE-CEREMONIAS-2026.docx:** confirma el orden de insinuación, entrevistas, Cámara del Medio, balotaje y revisión de Régimen Interior/Tesorería/Hospitalidad/Secretaría/Maestría; también exige registrar fecha de rechazo y subsanación en una nueva presentación.

La auditoría completa de todos los modelos y cartas sigue siendo una tarea de cierre; las pantallas y permisos ya deben usar esta matriz como contrato mínimo.

## Acuerdos funcionales consolidados

1. La plataforma es multi-Taller y mantiene una ficha institucional única por persona.
2. Un cambio de Taller conserva el historial de origen y crea la nueva pertenencia con trazabilidad.
3. El circuito de iniciación tiene etapas separadas: ingreso, deliberación, publicación, entrevistas, Cámara del Medio, balotaje, solicitud formal, Régimen Interior, Tesorería, Hospitalaria, Gran Secretaría, Gran Maestra, plancha y ceremonia.
4. Se exigen al menos tres entrevistas; pueden existir más por decisión del Venerable Maestro. En la ficha sólo queda resumen y resultado favorable/desfavorable; el Word/PDF queda como antecedente.
5. Un rechazo en Cámara del Medio genera un antecedente transversal protegido y una alerta para Régimen Interior y cualquier Taller que intente una nueva presentación.
6. Los egresos de Tesorería y Hospitalaria requieren autorización del Venerable Maestro; el Orador participa como firmante cuando corresponda, especialmente en comunicaciones y retiros.
7. Los Talleres deben estar al día con Gran Tesorería y Gran Hospitalaria antes de autorizar ceremonias.
8. La publicación del insinuado debe cumplir el plazo configurable antes de continuar.
9. El padrón de asambleístas separa calidad histórica, habilitación para asistir y habilitación para sufragar; nunca registra el voto individual.
10. Biblioteca Virtual y Gran Archivero son ámbitos separados; el Gran Archivero no forma parte del alcance operativo actual.
11. El diseño considera minimización, trazabilidad, control de acceso y retención conforme a la Ley 21.719.

## Cargos del Taller: separación obligatoria

### Gestión logial y firmantes administrativos

En cada Taller, la gestión logial propia y sus documentos administrativos deben identificar, según el acto, a:

- **Venerable Maestro:** autoridad principal del Taller; autoriza los actos administrativos que requieren decisión del Taller, incluidos los egresos de Tesorería y Hospitalaria.
- **Secretario:** lleva la gestión documental, actas, comunicaciones, solicitudes y trámites del Taller.
- **Orador:** revisa y firma los documentos en que interviene institucionalmente, incluyendo la carta de retiro cuando corresponda.
- **Tesorero:** administra ingresos, egresos, cuotas, pagos y rendición económica del Taller; sus actos quedan sujetos a la autorización del Venerable cuando corresponda.

### Cargos docentes del Taller

La docencia y el seguimiento formativo se asignan separadamente a:

- **Primer Vigilante:** responsabilidad docente y seguimiento de Compañeros.
- **Segundo Vigilante:** responsabilidad docente y seguimiento de Aprendices.
- **Ex Venerable Maestro:** apoyo docente, experiencia institucional y acompañamiento formativo.

Estos cargos no deben reemplazarse entre sí en los permisos, formularios, firmas, circuitos de aprobación ni reportes. La matriz queda respaldada preliminarmente por los documentos consultados de Google Drive **Proyecto Centenario**; cualquier excepción por acto debe documentarse antes del cierre de v1.0.

## Estado de la demo QA

- UI QA v0.56 publicada en GitHub Pages.
- 20 Talleres ficticios.
- Cada Taller: 12 Maestros, 5 Compañeros, 5 Aprendices y 2 PAS activos; total 24 por Taller y 480 en la Orden.
- Carga temporal de fotografías JPG/PNG y antecedentes Word/PDF durante la sesión del navegador.
- Los datos son ficticios; no se deben cargar antecedentes reales.
- CI, Showcase e instalador Post-RC aprobados.

## Próximo trabajo prioritario

Cerrar la auditoría documental de Google Drive: extraer cargos, firmantes, orden de aprobación y requisitos de cada documento, compararlos con los endpoints y pantallas, y registrar las brechas como requisitos o pruebas UAT antes de declarar v1.0.
