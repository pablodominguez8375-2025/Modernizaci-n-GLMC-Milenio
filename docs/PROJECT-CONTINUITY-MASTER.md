# Proyecto Centenario — Registro maestro de continuidad

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
| 21-09-2026 | El Product Owner autoriza aplicar mejoras compatibles identificadas en el sistema logial de referencia, incluyendo Tesorería, Secretaría y Mi ficha. | Sobre el candidato PR #121 se incorpora cartola personal de Tesorería en Mi ficha, separación visible entre período de obligación y fecha efectiva de pago, consulta de comprobantes y protección contra pagos duplicados por referencia. Secretaría conserva sin cambios regresivos el circuito aprobado; sus mejoras referenciales de calendario y documentos ya están cubiertas por la implementación vigente. |
| 21-09-2026 | Se define la vista inicial del Gran Tesorero para el Cuadro Logial Mensual. | Gran Tesorería recibe por Taller y mes cantidades y montos por cuota normal, tercera edad, estudiante, cónyuge y Past Activo, más total exigible y total pagado. Sólo consulta datos mínimos individuales bajo una acción explícita para resolver diferencias; la regularidad se obtiene únicamente cuando el pago íntegro coincide con el total correspondiente. |
| 22-09-2026 | El Product Owner solicita simplificar las vistas de Tesorero y Gran Tesorería con el mismo criterio aplicado en Secretaría. | Se consolida un único menú por cargo. Tesorería del Taller contiene Resumen, Cuotas y cobranza, Egresos y Cuadro mensual; Gran Tesorería contiene Cuadros mensuales y Estado de Talleres. Se eliminan accesos laterales duplicados, se conserva la segregación funcional y se añade PMGM-ARCH-012 y QA-029. |
| 22-09-2026 | La revisión de la evidencia visual del candidato detecta recorte horizontal en la autorización móvil del Venerable. | Se limita el ancho intrínseco del módulo de Tesorería, se mantiene la tabla dentro de su desplazamiento interno y se compactan márgenes en móvil. QA-029 debe conservar capturas de 390×844 sin recorte global ni pérdida de acciones. |
| 22-09-2026 | El Product Owner ordena continuar en `dev` y completar la separación de menús por cargo. | Desde `dev` `822fd25b88caafdf5a01d89c27c53b6cd000f183` se inicia la consolidación de Secretaría del Taller y Gran Secretaría: una sola entrada lateral por cargo y funciones internas sin ampliar permisos. Se añade PMGM-ARCH-013, QA-030 y UI QA v0.58. `main` permanece intacto y la QA física de `srv01` sigue diferida. |
| 22-09-2026 | El Product Owner aclara que el Tesorero no posee atribuciones de Secretaría y no puede ver sus mismos menús o vistas. | Se corrige la herencia visual: `canManageLodgeTreasury` deja de habilitar Gestión Logial; el perfil Tesorero pierde acceso a Tenidas, actas, correspondencia, insinuados privados, Circuito, fichas y documentos generales. Conserva su menú Tesorería y vistas transversales mínimas. Se añade QA-031 y UI QA v0.59. |
| 22-09-2026 | El Product Owner solicita integrar el logo oficial de la Gran Logia Mixta de Chile en la plataforma junto a la identidad corporativa. | PR #137 integrada por squash en `dev@e0dd6e6`. SVG original, soporte blanco, proporciones y protección visual; Showcase prueba viewports y cabecera móvil. CI #1436, Showcase/Pages #676, QA #314 y Pre-UAT #329 SUCCESS. Demo e instalable corresponden al mismo SHA. QA-062 guarda digests y verificación live. `main` intacta; srv01 físico sigue pendiente en Issue #97. |

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

## Checkpoint vigente para continuidad — 22-09-2026

- Repositorio: `pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio`.
- `dev`: `e0dd6e66fe5d6ab7f6be6a2d4c7170f4729c06a0` (PR #137, v0.62).
- `main`: `6dfb9546a4873baff15955cf86abfd7d47e3d111`, intacta.
- Gates del mismo SHA: CI #1436, Showcase/Pages #676, QA Installable #314, Pre-UAT #329; todos SUCCESS. Suite frontend local 183/183.
- Demo verificada: https://pablodominguez8375-2025.github.io/Modernizaci-n-GLMC-Milenio/; muestra `E0DD6E6`, SVG oficial cargado con soporte blanco y sin solapamiento de cabecera.
- Google Drive, carpeta Proyecto Centenario: `Logo Gran Logia Mixta de Chile.svg`, ID `1_BLXseShbQX-ioGMNLd5xKPYGtLMEvFm`. SHA-256 versionado: `d8e4660f95ffbdccb8c62fdc39eb8acd8f1aa5f27e8c17845c4bda64853d24bb`.
- Pages artifact #10723212264, `sha256:1be0343b760f6f65d5acfcf066582d56a6e4c76638955bf0eefb73cf44df375c`, vence 23-09-2026 22:56 UTC.
- QA artifact #10723132819, `proyecto-centenario-qa-srv01-e0dd6e66fe5d6ab7f6be6a2d4c7170f4729c06a0`, `sha256:a42964269eb9af176feacfd3f0f546b51072647fb828b8af062b2bded180497b`, vence 22-10-2026 22:55 UTC.
- Issue #97 abierto: srv01 físico sigue diferido; instalar el corte vigente, validar `SOURCE_SHA`/`MANIFEST`, smoke, QA-001..QA-034, UAT y registrar evidencia. No promover a `main` sin aprobación expresa. No abrir otro alcance funcional mientras este gate siga bloqueando, salvo autorización concreta del Product Owner.

La sección **15** de `PMGM-NEXT-001-siguiente-corte-tecnico.md` es el próximo punto de trabajo detallado. Consultar HEAD vivo y fuentes Drive/GitHub antes de retomarlo; los artifacts temporales no sustituyen al paquete recién generado desde el HEAD vigente.

## 16. Corte activo — Hospitalaria y Gran Hospitalaria (23-09-2026)

El Product Owner autoriza expresamente continuar este alcance funcional pese al gate general pendiente de Issue #97: (a) reposición automática de $1.500 por cada hermano activo del Cuadro de cada Taller cuando se registra una defunción, según tarifa efectiva; (b) cobranza individual trazable por Hospitalaria; (c) transferencia completa a Gran Hospitalaria, conciliación y visto bueno; (d) regularidad consumida por validaciones ceremoniales; (e) cuota de cónyuge de referencia $15.000 CLP mensuales, configurable por vigencia. El aporte independiente a Gran Tesorería sigue obligatorio y no se inventa un monto.

Base y rama: `dev@f90e6af186a1fe81f39ac4a77b6f26fd8721a017`; `main@6dfb9546a4873baff15955cf86abfd7d47e3d111` intacta. Rama activa `feature/hospitalaria-death-replenishment-spouse-fee`. Cambios locales: entidades/mapeos/migración de casos, obligaciones, pagos y transferencias; endpoints con control de acceso y auditoría; demo funcional con datos ficticios; UI de Hospitalaria y plan de cuota conyugal. Una transferencia observada puede reenviarse como nuevo intento numerado; se preserva historial y regularidad se basa en el último intento.

Pendiente inmediato: cerrar pruebas y revisión del backend. El entorno no dispone de .NET SDK (`dotnet: command not found`), por lo que CI debe compilar y ejecutar integración PostgreSQL. QA-034 queda documentado en `docs/qa/PMGM-QA-V063-HOSPITALARIA-REPOSICION-CONYUGE.md`. Aún no hay commit/PR ni gates de GitHub para esta rama. Demo Pages, instalable QA y despliegue físico no se consideran actualizados hasta generar artefactos sobre un SHA común; srv01 sigue diferido por Issue #97. No promover a `main`.
