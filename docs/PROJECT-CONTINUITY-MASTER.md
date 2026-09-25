# Proyecto Centenario — Registro maestro de continuidad

| 25-09-2026 | Se verificó el corte posterior a PR #159 y PR #160. | `dev@69e649ff95abe58de0b791c17fd73131e1629504`; `main@6dfb9546a4873baff15955cf86abfd7d47e3d111` sin cambios. PR #159 amplía la evidencia responsive a 80 capturas en ocho breakpoints; PR #160 corrige límites de día ante el cambio horario de Chile. PMGM CI #1534, Showcase/Pages #795, QA Installable #433 y Pre-UAT #352 finalizaron SUCCESS sobre el mismo SHA. Issue #97 sigue abierto: instalación física, smoke, regresión y UAT no verificados. Mantener QA/UAT como siguiente gate y no iniciar otro incremento funcional sin alcance concreto aprobado o cierre operacional. |
| 24-09-2026 | Showcase de `dev@8522b0d` reveló etiquetas duplicadas en Cuotas y Cobranzas a 1440 px, aunque el móvil se veía legible. | PR #155 integrada por squash en `dev@eb9ca2f75523937b6871bcd24b23a7dd8a4b098a`. En móvil/tablet se conservan seis etiquetas y valores en tarjetas; en escritorio se muestran encabezados de tabla sin rótulos duplicados. Frontend 196/196, lint/build locales y CI #1517, Showcase/Pages #773, QA Installable #411 y Pre-UAT #347 SUCCESS. `qa-current.json` apunta al mismo SHA; el paquete QA tiene SHA-256 `2ac85751b88218ef525bd602cd5f2526cfcdd49cc331e1d15023197173cec320`. `main` intacta; QA física/UAT siguen pendientes por Issue #97. |
| 23-09-2026 | El Product Owner autoriza ampliar Tesorería del Taller y consulta su publicación en GitHub Pages. | El menú autorizado suma seis vistas: Resumen, Cuotas y Cobranzas, Ingresos y Egresos, Cuadro mensual, Configuraciones y Reportes. QA-037 verifica el alcance; ver `PMGM-ARCH-012` y `PMGM-QA-V066`. Egresos afectan caja sólo después de la aprobación del Venerable; cargos pagados generan ingreso automáticamente. La publicación Pages corresponde al flujo del repositorio al integrar en `dev`. |
| 23-09-2026 | El Product Owner autoriza incorporar mejoras compatibles de la referencia a Tesorería. | Se agregan cierres anuales auditados e inmutables, arrastre de saldo, bloqueo de movimientos retroactivos, reportes Debe/Haber/Neto, filtros, paginación y CSV. QA-038 y `PMGM-QA-V067` documentan la aceptación; no se automatiza la regla de cuotas por retiro/fallecimiento sin validación institucional. |
| 24-09-2026 | El Product Owner continúa el desarrollo y autoriza avanzar el siguiente pendiente priorizado en la Línea Base Maestra. | Se inicia la conciliación del derecho único de ceremonia contra el expediente, con abonos, recibo idempotente, saldo autoritativo y bloqueo de autorización. QA-039 / `PMGM-QA-V070`; dev base `d7b931b323c191733a09f392c74759f7994e844b`, main intacta. QA física srv01 y UAT siguen pendientes por Issue #97. |

**Última consolidación:** 25 de septiembre de 2026  
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

| 24-09-2026 | El Product Owner define como capacidad permanente aplicar contabilidad, auditoría contable e ingeniería de procesos a las mejoras del Proyecto Centenario. | Revisar integridad y clasificación de ingresos/egresos, conciliación y rendición, segregación de funciones, trazabilidad, controles preventivos/detectivos y eficiencia del flujo; documentar supuestos, evidencia y riesgos, y separar recomendaciones técnicas de reglas que requieran validación institucional. Esta perspectiva complementa, no reemplaza, las fuentes normativas/documentales ni la revisión profesional aplicable. |
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

Al abrir este corte, el entorno local no tenía .NET SDK, por lo que la compilación y las pruebas PostgreSQL quedaron asignadas a CI. QA-034 está en `docs/qa/PMGM-QA-V063-HOSPITALARIA-REPOSICION-CONYUGE.md`. Las fallas detectadas y sus reparaciones quedan registradas a continuación; despliegue en `srv01` y UAT permanecen pendientes por Issue #97. No promover a `main`.


### 23-09-2026 — Reparación y validación de PR #139

**Rama:** `feature/hospitalaria-death-replenishment-spouse-fee`, base `dev@f90e6af186a1fe81f39ac4a77b6f26fd8721a017`. **`main`:** `6dfb9546a4873baff15955cf86abfd7d47e3d111`, intacta.

#### Defectos encontrados y reparados

1. CI #1439 detectó que `frontend/src/api/pmgmApi.ts` estaba truncado y eliminaba métodos existentes. Se restauró el archivo completo desde `dev` y se reaplicaron los tipos, métodos y simulaciones de reposición; diff final respecto de `dev`: +25 líneas sin eliminaciones.
2. El migration gate rechazaba nueve FK posicionales; se convirtieron a argumentos explícitos. Se retiró el mapeo de `SubmissionNumber` de `TreasuryPayment`.
3. CI #1440 encontró `Membership` ambiguo como namespace y `SubmissionNumber` declarado en la entidad Obligation. Se calificó el tipo de entidad y se ubicó el número de intento en `DeathReplenishmentTransfer`, coherente con endpoints, tabla e índice.
4. CI #1441 compiló la API, pero las pruebas PostgreSQL fallaron al procesar `InsertData` porque las migraciones del proyecto son manuales y no generan `TargetModel`. La tarifa inicial se carga ahora con SQL explícito en la migración y se elimina con SQL explícito en `Down`.

#### Validaciones observadas

- Local: frontend **185/185**; lint y build productivo SUCCESS; `git diff --check` limpio; `python3 tests/migration_gate.py` aprobó **44 migraciones**. Aviso de build: bundle JS supera 500 kB.
- Commit actual de la PR: `b1bf40c308b2c2ce24dbcd8669d4aca8344b8a22`.
- CI #1442: **SUCCESS**, incluye build backend, pruebas unitarias e integración PostgreSQL/S3/ClamAV, smoke HTTPS/OIDC y recuperación.
- Showcase #684: **SUCCESS**, captura responsive generada. El paso de despliegue de Pages aparece `skipped` por ser ejecución de PR; todavía no representa una publicación desde `dev`.
- QA Installable #322: **SUCCESS**, paquete generado desde el mismo SHA.
- Pre-UAT depende de `push` a `dev`; queda pendiente de la integración. La publicación de Pages desde `dev` también debe verificarse después de integrar.

#### Estado y siguientes pasos

PR #139 continúa abierta como borrador, base `dev`. Antes de cerrar este corte: actualizar la rama con este registro, verificar otra vez gates exact-head, integrar sólo en `dev` según el flujo del proyecto y comprobar Pages, instalable y Pre-UAT sobre el SHA integrado. No reutilizar artefactos anteriores ni promover a `main`. Issue #97 mantiene pendiente despliegue físico en `srv01`, smoke, QA-001..QA-034 y UAT.

## 17. Incremento Tesorería — Decreto N.º 1.759 (23-09-2026)

**Base consultada:** `dev@dcb6b169f59da7649012f3d2e8fd20e5313ad3f8`; `main@6dfb9546a4873baff15955cf86abfd7d47e3d111`, sin cambios. Rama `feature/treasury-decree-1759`, creada desde `dev`. El acuerdo del Product Owner: cuota de reposición de $1.500 fijada por Gran Hospitalaria es independiente de Tesorería; Gran Tesorería fija el aporte por miembro y por Oriente, y cada Taller fija la cuota local superior, conservando la diferencia. Los Past Activos se excluyen del aporte de Gran Tesorería.

**Seguimiento de publicación:** PR #140 abierta hacia `dev`. El primer HEAD `bdfe1e2fba64d5d5529b62abca805ffc2aea337f` expuso un truncamiento en la transferencia del archivo grande `frontend/src/api/pmgmApi.ts`; se sustituyó por el contenido completo y se verificó que el blob GitHub coincida con el hash local. HEAD reparado observado `83379537f508302855b7b6a797b79b4a2a49b2b7`. Reruns CI #1446, Showcase #689 y QA Installable #327 se iniciaron para ese HEAD. El checkpoint documental posterior cambió el SHA de la PR; verificar gates del nuevo HEAD y no fusionar mientras falte alguno.

CI #1448 (sobre `5fcd5aec…`) confirmó frontend, infraestructura, smoke, piloto y PostgreSQL build, pero falló 1/297 test: `OrganizationProjectionTests.OrganizationOptionSurface_IsPurposeMinimized`, porque la zona de cuota se había agregado al DTO general. Corregido: el DTO y perfil generales recuperan su proyección minimizada; lectura de Oriente ahora usa rutas dedicadas protegidas para Gran Tesorería/Tesorero del Taller. Validación local después del ajuste: 186/186 frontend, lint/build, 45 migraciones y QA-035 gate SUCCESS. Nuevo exact-head CI/Showcase/Installable pendiente; el último fallo funcional ya fue atendido.

CI #1449 volvió a mostrar esa prueba porque `OrganizationEndpoints.cs`, aunque corregido en el checkout, no se incluyó en el push al no figurar como diff local respecto de la base. Se detectó comparando blob SHA de rama y local. Forzar la restauración del archivo de proyección minimizada en el siguiente commit y verificar el hash remoto antes de lanzar CI exact-head de nuevo.

### Cierre de Tesorería — PR #140 (23-09-2026)

PR #140 fusionada a `dev` por squash. SHA funcional integrado: `7747eab76f339395efa3356e92017c19f5abb0f7`. Gates del mismo SHA: CI #1451 SUCCESS; Showcase/Demo y publicación GitHub Pages #695 SUCCESS; QA Installable #333 SUCCESS; Pre-UAT Installable #332 SUCCESS. `main` sigue intacta en `6dfb9546a4873baff15955cf86abfd7d47e3d111`. La documentación de cierre se añade en PR separada, sin alterar el SHA funcional anterior.

Implementado: tarifario 2026 por Oriente, clasificación editable sólo por Gran Tesorería, lecturas dedicadas protegidas por rol, componente oficial no editable por el Tesorero local, cuota del Taller configurable sobre el mínimo oficial, diferencia retenida localmente, cónyuge y Past Activos excluidos del componente GT, derechos ceremoniales listados, QA-035 y migración de Oriente. Validaciones locales: 186/186 frontend, lint/build, gate de 45 migraciones y QA template de 35 controles SUCCESS. Perú USD 6 aún no genera cargos porque el motor es CLP; no hay conversión. Cuotas por cesantía y conciliación de derechos ceremoniales contra solicitudes quedan pendientes. QA física srv01/UAT sigue pendiente por Issue #97; no promover a `main`.

Siguiente línea funcional a decidir/continuar: conectar pagos/conciliación de derechos ceremoniales y regularidad de Gran Tesorería a cada expediente de ceremonia, después abordar moneda Perú y aplicación de cuotas por cesantía. Los tres son trabajos separados; no se deben declarar cubiertos por el catálogo de tarifas actual.

La fuente normativa es el Decreto 1.759, emitido el 15-12-2025 y vigente desde el 01-01-2026: cuotas ordinarias CLP Santiago/otros Orientes $21.000/$15.000; cónyuge $13.000/$10.000; tercera edad $10.000/$8.000; estudiante $8.000/$8.000. Cuota ordinaria Perú USD 6. Derechos únicos: Iniciación $41.000, Aumento de Salario $31.000, Exaltación $41.000, Afiliación $26.000, Incorporación $31.000. La cuota local es configurable y no puede quedar bajo el aporte oficial. Ejemplo cónyuge Santiago: local $15.000 = institucional $13.000 + Taller $2.000.

Cambios en curso en código: clasificación de Oriente por Gran Tesorería; resolución server-side de tarifas oficiales; bloqueo de cuotas inferiores o aporte institucional manipulado; cálculo del margen; exclusión de Past Activos; catálogo de derechos ceremoniales; demo con tarifas por Oriente; migración reversible `20260923140000_AddTreasuryTerritoryToOrganizations`. QA-035 incorpora aceptación del decreto. El catálogo ceremonial es informativo por ahora: conciliación del derecho contra pago por expediente ceremonial queda pendiente. Perú no convierte USD a CLP ni crea tarifas inventadas; cuotas de cesantía documentadas en el decreto quedan fuera de aplicación automática de este corte.

PR a `dev`, CI exact-head, Showcase/Pages, instalable QA y Pre-UAT del mismo SHA todavía pendientes hasta publicar la rama. El entorno local no tiene .NET SDK; backend/PostgreSQL dependen de CI. QA física `srv01`, UAT y promoción a `main` continúan pendientes conforme a Issue #97. Después de integrar, verificar las salidas de GitHub Pages, paquete instalable y Pre-UAT; no reutilizar artefactos de otro SHA.


## 19. Incremento en curso — Cuadro de Tesorería alineado al Excel oficial (23-09-2026)

Base: `dev@8d06c8e684cd79047202971bd1c20244e7d1eecd`; `main@6dfb9546a4873baff15955cf86abfd7d47e3d111`, intacta. Rama de trabajo: `feature/treasury-statement-excel-alignment`. Fuente Drive: `CUADRO PAGO GRAN TESORERÍA.xlsx`, ID `1nPEZsVr5QPNS-Z_SBmjEs33wjfqsUNTN`.

La vista del Taller queda alineada a la estructura oficial: secciones por grado (Maestros, Compañeros y Aprendices), RUT, nombre y apellidos, grado, cargo(s) abreviados, tipo y valor de cuota, y referencia de respaldo. Cónyuge, estudiante y tercera edad requieren Plancha de autorización; la generación, ingreso manual y envío del Cuadro bloquean cuotas especiales sin respaldo. La API sólo incluye RUT/nombres en proyecciones detalladas autorizadas; Gran Tesorería mantiene vista inicial agregada/minimizada. Se conservan todos los cargos activos al corte.

QA-036 amplía la regresión física a 36 controles. Verificaciones locales: frontend 188/188, lint/build SUCCESS, JSON de clasificación válido y gate QA de 36 controles SUCCESS. PR #142 abierta hacia `dev`; primer SHA publicado `808687b9146fcc5628c35b6feeedd6c2c9f0dffe`. CI #1454, Showcase #699 e instalable QA #337 SUCCESS sobre ese SHA. El despliegue de Pages se omite en PR y Pre-UAT no tiene disparador pull_request; confirmar ambos flujos para el SHA integrado. No hay .NET SDK local; backend/PostgreSQL depende de CI. QA física `srv01` y UAT pendientes. `main` intacta.


## Corrección de usabilidad móvil — Cuotas y Cobranzas (24-09-2026)

La captura aportada por el Product Owner mostró que el ajuste previo de responsividad no permitía leer los datos en teléfono: las filas se recortaban a la derecha y sólo quedaban visibles encabezados. En la rama `feature/treasury-mobile-card-fix`, desde `dev@6eccbc64f75d0347ae277f1e3a7ceee9b8902e9f`, la propuesta sustituye las etiquetas CSS generadas por pares etiqueta/valor explícitos en el HTML, reduce cada fila a tarjeta ajustada al contenedor y apila el contenido en móvil. El chequeo de Showcase añade verificaciones de seis etiquetas, seis valores visibles, borde derecho de tarjeta, botón táctil y ausencia de overflow global. QA-V069 registra los criterios. Estado al registrar: PR y gates pendientes; no se desplegó ni fusionó. `main` permanece en `6dfb9546a4873baff15955cf86abfd7d47e3d111`; QA física/UAT continúan en Issue #97.


## Nomenclatura institucional de cargos — 25-09-2026

El Product Owner solicita conservar como referencia permanente la estructura de Dignidades (Las Luces), Oficiales Mayores y Oficiales Menores con sus abreviaturas institucionales. Se sincroniza la lista completa en la Línea Base Maestra de Google Drive y en `docs/PMGM-ARCH-007-perfiles-taller-responsabilidades-vistas.md`, mediante PR #162 a `dev`. La convención es para escritura y presentación; no crea cargos ni modifica responsabilidades, firmantes, autorizaciones o permisos. Cambio documental; CI exact-head ejecutándose al registrar este handoff. Issue #97 continúa abierto para QA física/UAT y `main` permanece intacta.
