# PMGM-BASE-001 — Hilo Maestro y Estado Único del Proyecto Centenario

**Nombre vigente:** Proyecto Centenario / Modernización Centenario  
**Nombre histórico:** Proyecto Milenio / Modernización Gran Logia Mixta Milenio  
**Estado:** Activo  
**Sponsor / Product Owner:** Pablo Domínguez  
**Arquitectura funcional y técnica:** ChatGPT / Chatito  
**Desarrollo y automatización:** Codex  
**Rama de trabajo:** `dev`  
**Rama estable:** `main`  
**Repositorio histórico conservado:** `pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio`  
**Regla:** GitHub es la fuente única de verdad técnica y funcional; Google Drive es la fuente documental institucional.

> Este documento reemplaza los estados parciales repartidos entre conversaciones. Desde septiembre de 2026 todo acuerdo, cambio, UAT, decisión y pendiente del proyecto debe reflejarse aquí o en documentos formalmente vinculados desde este estado maestro.

## 1. Visión institucional

Centenario construye un ecosistema digital único para la Gran Logia Mixta de Chile, sustituyendo gradualmente planillas duplicadas, procesos manuales y sistemas aislados por una plataforma modular, trazable, segura y responsive, con identidad institucional única, base de datos maestra, permisos por rol/grado/contexto, auditoría y automatización de procesos.

Principio de ejecución aprobado: **QA primero; expansión modular después**.

### Requisito permanente de responsividad

Todas las páginas, módulos y flujos del sistema deben ser responsivos y fáciles de utilizar en computador, tablet y teléfono celular. Navegación, tablas, formularios y acciones deben adaptarse a cada tamaño de pantalla; las acciones operativas principales deben permanecer visibles, no debe existir desplazamiento horizontal global y los controles deben ser aptos para interacción táctil. Cada módulo debe validarse con la matriz de resoluciones y la evidencia visual establecidas en `docs/ui/PMGM-UI-001-identidad-visual-responsive.md`; la validación de una sola vista no certifica las demás.

Flujo oficial de trabajo:

`Requisito → Diseño → Aprobación → Desarrollo → CI/QA → UAT → Documentación → Promoción a main`

No se considera estable ningún incremento que rompa compilación, migraciones, seguridad, gates, pruebas, instalación o recuperación.

## 2. Arquitectura técnica vigente

- Frontend: React + TypeScript + Vite, responsive y mobile-first.
- Backend: ASP.NET Core / .NET 10, monolito modular con límites de dominio explícitos.
- Base de datos: PostgreSQL + Entity Framework Core / Npgsql.
- Identidad: OIDC/PKCE mediante Keycloak; no autenticación casera.
- Object Storage: S3-compatible con MinIO privado en QA/piloto.
- Antimalware: ClamAV integrado al ciclo documental.
- Contenedores: Docker / Docker Compose.
- Publicación: reverse proxy + HTTPS obligatorio fuera de QA local.
- CI/CD: GitHub Actions.
- Integridad documental: SHA-256, MIME/firma, versiones y reconciliación segura.
- Cultura: `es-CL`; zona horaria institucional `America/Santiago`.
- Datos sensibles: seguridad y privacidad desde el diseño conforme al marco del proyecto para Ley 21.719.

Ambientes consolidados:

1. **Showcase standalone:** demostración visual con datos ficticios, sin depender de OIDC/API/BD/MinIO/ClamAV.
2. **QA integrado:** stack autenticado completo para validación funcional/técnica.
3. **Primera implementación / piloto:** stack reproducible con OIDC real, backups, restore y recovery drill.
4. **Producción futura:** únicamente después de UAT, hardening, credenciales definitivas, TLS, respaldo externo y aprobación institucional.

## 3. Principios funcionales obligatorios

1. Una persona posee una identidad maestra única.
2. El RUT puede ser dato de negocio, nunca PK técnica.
3. Una persona conserva toda su historia institucional aunque cambie de Taller.
4. En traslado, el Taller de origen conserva su historial y el Taller receptor crea una nueva pertenencia; no se sobrescribe la historia anterior.
5. El grado masónico vigente acompaña al hermano según su historia institucional; no se reinicia por cambio de Taller.
6. Régimen Interior posee visión transversal autorizada de miembros, estados, movimientos y consistencia de datos.
7. Gran Secretaría emite documentos oficiales, decretos, comunicados, planchas de autorización y gestiona reservas institucionales según competencias.
8. Gran Tesorería es fuente de verdad de regularidad financiera.
9. Gran Hospitalaria es fuente de verdad de obligaciones y reposiciones de su ámbito.
10. La elegibilidad de una ceremonia se deriva de requisitos verificables; no se ingresa manualmente como un “sí/no”.
11. Toda autorización debe conservar un snapshot inmutable de las evidencias y vistos buenos que justificaron la decisión.
12. Los plazos, reglas y requisitos críticos deben ser configurables y versionados cuando corresponda.
13. Notificaciones y calendario son servicios transversales; el calendario proyecta información y no reemplaza las fuentes de verdad de cada dominio.
14. Toda acción sensible debe producir auditoría trazable.
15. Los binarios documentales permanecen fuera de PostgreSQL y privados por defecto.
16. El sistema debe funcionar correctamente en escritorio y móvil.

## 4. Módulos y acuerdos consolidados

### 4.1 Core / Membresía

**Terminología institucional obligatoria:**

- el conjunto oficial de hermanos pertenecientes a un Taller se denomina **Cuadro del Taller**;
- la vista consolidada institucional se denomina **Cuadro General de la Orden**;
- cuando corresponda, puede hablarse de **hermanos del Cuadro**;
- **Padrón** se reserva para el **Padrón de la Gran Asamblea**, es decir, el conjunto de **electores vigentes habilitados para participar en la Gran Asamblea**;
- no usar **padrón** como sinónimo de listado general de hermanos, membresía o Cuadro del Taller.

- Persona maestra única.
- Ficha del hermano con historial de grados, estados, cargos, pertenencias, movimientos y antecedentes autorizados.
- Traslado entre Talleres preservando historial del origen y creando nueva pertenencia en destino.
- Estados activos/no activos, retiros voluntarios/forzosos, reintegros, defunciones y condición Past cuando corresponda.
- Trazabilidad de fechas de iniciación, aumento de salario, exaltación, retiros y reintegros.

### 4.2 Régimen Interior

Debe poder consultar y reportar, a nivel de toda la Orden y por Taller:

- hermanos activos y no activos;
- retiros voluntarios y forzosos;
- defunciones;
- hermanos Past activos;
- estado de morosidad recibido desde Tesorería;
- fechas de movimientos y grados;
- consistencia histórica de miembros;
- ceremonias y movimientos informados;
- validaciones requeridas antes de autorizaciones.

Régimen Interior no reemplaza a Tesorería ni Hospitalaria como fuente de verdad de sus respectivos estados.

### 4.3 Gran Secretaría

Funciones consolidadas:

- decretos y comunicados;
- planchas oficiales de autorización de ceremonias;
- validación/gestión de solicitudes administrativas;
- uso de Gran Templo y salas de Secretaría sujeto a disponibilidad;
- muro institucional oficial, separado de canales fraternales;
- trazabilidad de emisión, versiones, destinatarios y lectura cuando corresponda.

Una plancha de autorización sólo puede emitirse después de cumplir la matriz institucional de requisitos y obtener los vistos buenos exigidos.

### 4.4 Gestión Logial

La Gestión Logial es el ámbito de administración propio de cada Taller. Incluye, como mínimo, **Secretaría del Taller, Tesorería del Taller, Hospitalaria del Taller y Docencia/Instrucción**. Estas funciones locales se integran con Gran Secretaría, Gran Tesorería y Gran Hospitalaria, pero no se confunden con ellas ni transfieren automáticamente sus atribuciones.

- Secretaría del Taller: tenidas, asistencia, actas, correspondencia, comunicaciones y tramitación de solicitudes;
- Tesorería del Taller: cuotas, aportes, pagos parciales, comprobantes, libro mayor, conciliación y rendición a Gran Tesorería;
- Hospitalaria del Taller: bolso, ayudas, aportes, reposiciones, transferencias y rendición a Gran Hospitalaria;
- Docencia/Instrucción: planificación por grado, sesiones, asistencia y seguimiento, bajo responsabilidad de los Vigilantes y del Ex Venerable Maestro conforme a sus cargos y a la normativa aplicable;
- ficha del Taller;
- cargos y períodos;
- tenidas por grado/tipo;
- asistencia y excusas;
- actas y extractos de acta;
- docencia/instrucciones;
- historial de movimientos y ceremonias;
- dashboard operativo del Taller;
- repositorio operativo del Taller dentro de los límites definidos.

### 4.5 Gran Tesorería

- estado financiero por hermano y por Taller;
- cuadro de pagos institucional;
- cuotas mensuales;
- derechos asociados a ceremonias;
- evidencias de pago;
- integración automática con elegibilidad de ceremonias;
- reportes y trazabilidad.

El objetivo es que, cuando Gran Tesorería ya posea la información, el sistema valide desde su fuente de datos y evite depender innecesariamente de comprobantes duplicados.

### 4.6 Gran Hospitalaria

- estado de obligaciones/reintegros/reposiciones según reglamentación;
- Fondo de Defunción cuando corresponda;
- cuotas de Hospitalidad aplicables;
- integración automática con elegibilidad de ceremonias;
- trazabilidad por Taller y persona cuando corresponda.

### 4.7 Insinuaciones / Portal de Insinuados

Flujo institucional consolidado desde el Protocolo 2026:

- formulario completo y maestro/a patrocinante;
- presentación en tenida de 1.er grado;
- espera mínima de 7 días antes de revisión inicial;
- aprobación unánime de la instancia correspondiente;
- publicación institucional;
- fotografía con validaciones del protocolo;
- posibilidad de observación/corrección sin duplicar expediente;
- permanencia publicada mínima: **20 días corridos**, parametrizable si la normativa cambia;
- mínimo tres entrevistas, ampliables por decisión del Venerable Maestro, con resumen, resultado y Word/PDF privado;
- Cuestionario Confidencial y autobiografía;
- revisión/votación de 3.er grado;
- balotaje en 1.er grado después de cumplir publicación y antecedentes;
- balotaje, votación y sufragio siempre anónimos: se conserva por separado la asistencia y la nómina de personas habilitadas; exclusivamente para la Gran Asamblea, dicha nómina corresponde al Padrón de la Gran Asamblea. Se conserva únicamente el recuento agregado de balotas blancas/negras, votos positivos/negativos o totales por candidatura; nunca existe vínculo entre persona y voto, conforme a `PMGM-ARCH-006`;
- rechazo con fecha, etapa y causas;
- nueva presentación sólo cuando se cumpla el plazo reglamentario y conste subsanación.

El insinuado no se convierte en miembro activo antes de la iniciación efectivamente registrada.

### 4.8 Ceremonias

Tipos contemplados por los formularios 2026:

- Iniciación;
- Afiliación;
- Aumento de Salario;
- Exaltación;
- Incorporación.

Requisitos comunes configurables:

- validación de Régimen Interior;
- regularidad de Gran Tesorería;
- regularidad de Gran Hospitalaria;
- visto bueno de Gran Maestría cuando corresponda;
- evidencia documental;
- identidad y Taller solicitante;
- estado agregado derivado de los requisitos.

Estados de requisito:

`pending | approved | observed | rejected | not_applicable | exception_approved`

No se autoriza mientras exista requisito obligatorio `pending`, `observed` o `rejected`.

#### Iniciación

Requiere expediente de insinuación habilitado, publicación mínima cumplida, votaciones previas, antecedentes y pagos/obligaciones aplicables.

#### Aumento de Salario y Exaltación

Deben considerar antigüedad, actividad, asistencias, instrucciones/cámaras, planchas exigidas por la fuente normativa y dispensas formales cuando correspondan.

#### Afiliación

- modalidad simple o con activación;
- Taller de origen;
- grado e historial masónico;
- Carta de Retiro Voluntario;
- verificación humana de firma manuscrita del original cuando el protocolo lo exija;
- rechazo anterior/nueva presentación cuando aplique.

#### Incorporación desde otra Obediencia

- expediente separado;
- Obediencia/Taller de origen;
- grado e historial masónico legalizado;
- Carta de Retiro Voluntario;
- Pacto de Paz y Amistad;
- aprobación expresa de Gran Maestría cuando no exista pacto;
- vistos buenos institucionales aplicables.

No se crea membresía activa en el Taller destino antes de la resolución correspondiente.

### 4.9 Biblioteca Virtual

Módulo distinto de Gran Archivero.

Regla aprobada de acceso acumulativo por grado:

- 1.er grado → contenido autorizado de 1.er grado;
- 2.º grado → contenido autorizado de 1.er + 2.º grado;
- 3.er grado → contenido autorizado de 1.er + 2.º + 3.er grado.

El grado efectivo se deriva de Membresía; no es editable por el usuario.

Las **planchas de trabajo** pertenecen a Biblioteca Virtual cuando están autorizadas para publicación y quedan sometidas a grado/permisos. **No pertenecen al Gran Archivero.**

La tarjeta “Mi ficha > Biblioteca Virtual” debe mantener fidelidad a la maqueta aprobada, incluyendo visibilidad y usabilidad móvil.

### 4.10 Gran Archivero / Archivo Histórico Institucional

Módulo distinto de Biblioteca Virtual y separado de CENDOC.

Su función es custodiar y gestionar el histórico documental de la **Gran Logia**, con preservación, cadena de custodia, metadatos, permisos granulares, vigencias y auditoría.

Exclusiones funcionales consolidadas:

- no es repositorio de planchas de trabajo;
- no debe absorber automáticamente documentos propios de Talleres;
- descubrir una ficha y acceder al contenido son permisos distintos;
- acceso por grado, hermano, cargo, órgano/comisión, finalidad y vigencia según autorización;
- expedientes sensibles son restrictivos por defecto.

### 4.11 CENDOC

**Fuera del alcance del Proyecto Centenario.**

No se implementa CENDOC como módulo, dependencia ni sustituto de Biblioteca Virtual o Gran Archivero. Cualquier continuidad o migración propia de CENDOC se gestiona fuera de este alcance.

### 4.12 Docencia

- forma parte de la Gestión Logial de cada Taller;
- es gestionada por los Vigilantes y el Ex Venerable Maestro, con asignaciones configurables conforme a la normativa institucional;
- instrucciones por grado;
- registro de cada instrucción con fecha, tema, grado, responsable y material asociado;
- programación previa de instrucciones y Tenidas, proyectadas automáticamente al calendario;
- estados diferenciados: programada, realizada/cerrada y cancelada;
- Segundo Vigilante para Aprendices, Primer Vigilante para Compañeros y Ex Venerable Maestro para Maestros, salvo regla institucional posterior parametrizada;
- asistencia individual específica a cada instrucción, con presente o ausente en el MVP, separada de la asistencia general a Tenida aunque pueda vincularse a ella;
- registro y corrección de asistencia sólo después de la ejecución de la instrucción o Tenida;
- historial formativo simple por hermano; evaluaciones y rúbricas quedan fuera del MVP;
- planchas asociadas al historial del hermano cuando corresponda;
- publicación en Biblioteca sólo cuando exista autorización y permisos compatibles;
- filtros por grado y Taller/contexto según política.

### 4.13 Calendario y Notificaciones

- una Tenida o instrucción programada se refleja automáticamente sin volver a digitarla;
- calendario personal: reúne los eventos que el usuario puede ver según pertenencia, grado, cargo y permisos;
- calendario del Taller: muestra las actividades programadas del Taller a sus integrantes autorizados;
- calendario de la Orden: vista consolidada para perfiles institucionales habilitados, respetando visibilidad y minimización;
- notificaciones con plantillas, bandeja, cola, idempotencia y auditoría;
- calendario en vistas agenda/mes;
- exportación/sincronización cuando corresponda;
- eventos proyectados desde Ceremonias, Gestión Logial, Reservas, Docencia y Secretaría;
- para eventos protegidos, minimizar exposición de datos y utilizar estados neutros como “Ocupado” cuando corresponda.

## 5. Gestión documental y Ley 21.719

Controles incorporados al diseño y CI:

- clasificación de datos/campos;
- mínimo privilegio y permisos contextuales;
- almacenamiento privado;
- auditoría append-only donde corresponda;
- registro de actividades de tratamiento;
- retención y legal holds;
- derechos de titulares;
- encargados y transferencias internacionales;
- EIPD/impacto cuando corresponda;
- gestión de incidentes;
- gates automáticos de privacidad y clasificación en CI.

El tratamiento de datos sensibles no debe inferirse desde conveniencia técnica; debe responder a finalidad institucional definida y permisos explícitos.

## 6. Fuentes documentales institucionales 2026 incorporadas

La carpeta de Google Drive del proyecto contiene y se utiliza como fuente para modelar procesos, entre otros:

- Formulario de Insinuación 2026;
- Protocolo para la tramitación de insinuaciones, afiliaciones y solicitudes de ceremonias 2026;
- Formulario de Solicitud de Ceremonias 2026;
- Extracto de Acta 2026;
- Formulario Uso Gran Templo 2026;
- Juramento de Afiliación 2026;
- Juramento de Oficiales 2026;
- Modelos de Iniciación, Afiliación, Aumento de Salario y Exaltación 2026;
- Formularios CRF/CRV y cambio CRF/CRV;
- Formulario de Autobiografía 2026;
- Voluntad Testamentaria / Fondo de Defunción 2026;
- Cuadro de Pago Gran Tesorería.

Regla: estos documentos no se copian ciegamente a pantallas; se convierten en datos estructurados, reglas, estados, evidencias y documentos generables, preservando la fuente institucional.

## 7. Estado técnico consolidado al 13-09-2026

### Aprobaciones ya registradas

- candidato visual institucional aprobado;
- baseline visual registrado;
- frontend responsive y fidelidad general validados;
- release candidate `v1.0.0-rc1` definido para UAT;
- paquete instalable pre-UAT y showcase forman parte del flujo de entrega;
- datos de demostración exclusivamente ficticios.

### Estado de rama

`dev` es la rama de integración vigente. `main` continúa reservada para la versión aprobada.

En la consolidación más reciente se incorporó a `dev`, mediante el PR #70, el commit de integración:

`d83d37a22998f2750dce434e10dd728794fdc6f2`

Este corte corresponde a `UI QA v0.56` e integra el circuito operacional de iniciación, aprobaciones institucionales, 20 Talleres ficticios con 480 registros, carga temporal QA de fotografías y antecedentes Word/PDF, reportería de Régimen Interior alineada, cuadros de Gran Tesorería, actas, votación, notificaciones y fichas/traspasos.

### CI / QA

Sobre el mismo SHA de `dev` finalizaron correctamente:

- `PMGM CI` #1069 y #1070;
- `PMGM Showcase Demo` #305;
- `Proyecto Centenario Pre-UAT Installable` #265;
- 119 pruebas frontend, lint y build;
- backend, infraestructura, privacidad Ley 21.719, clasificación de datos, migraciones y release gates.

La promoción a `main` permanece bloqueada hasta completar controles y UAT.

## 8. UAT vigente

La aprobación visual **no equivale** a aprobación funcional completa.

Pendiente obligatorio:

- ejecutar y cerrar UAT funcional/operacional de 20 casos;
- verificar instalación reproducible;
- verificar autenticación/roles;
- verificar flujos críticos de membresía, insinuaciones, ceremonias, Biblioteca y Gran Archivero;
- verificar backups, restore y recovery drill;
- verificar experiencia móvil;
- documentar incidencias y evidencia;
- sólo después promover RC1 a `main`.

El plan histórico `PMGM-UAT-EXECUTION-PLAN-V100-RC1.md` conserva la trazabilidad del candidato congelado anterior `739ba0b3`. No debe utilizarse para atribuir aceptación al nuevo código de `dev`. La preparación vigente del siguiente ciclo está en `docs/qa/PMGM-PREUAT-V056-EXECUTION.md`.

## 9. Instalación definitiva prevista

Objetivo: montar una VM definitiva y continuar actualizando progresivamente la plataforma.

Baseline recomendado del proyecto:

- Ubuntu Server 24.04 LTS;
- Docker Engine + Docker Compose;
- recursos dimensionables según carga piloto y almacenamiento documental;
- PostgreSQL persistente;
- Keycloak;
- API + frontend;
- MinIO/S3 privado;
- ClamAV;
- reverse proxy con TLS;
- DNS/dominio institucional;
- correo SMTP institucional para notificaciones;
- backups automáticos de BD y objetos;
- copia de respaldo fuera de la VM;
- monitoreo/logs;
- credenciales de producción fuera del repositorio.

El instalador/runbook debe poder levantar el sistema sin depender del entorno de desarrollo de Codex o ChatGPT.

## 10. Backlog prioritario único

Orden vigente de trabajo:

1. cerrar CI de `dev` después de la corrección MinIO/migraciones;
2. cerrar paquete Pre-UAT instalable;
3. ejecutar UAT de 20 casos y corregir defectos P0/P1;
4. promover RC1 estable a `main` sólo con gates verdes y aprobación institucional;
5. completar automatización Tesorería/Hospitalaria desde sus fuentes de datos y formularios 2026;
6. terminar flujos documentales 2026 faltantes y generación de documentos oficiales;
7. reforzar dashboards/reportes de Régimen Interior, Tesorería, Hospitalaria y Secretaría;
8. completar Gran Archivero según su alcance real, sin planchas de trabajo ni absorción de documentos de Taller;
9. completar Biblioteca/Docencia y permisos por grado;
10. hardening de producción, observabilidad, operación y recuperación;
11. continuar módulos no críticos sin romper el baseline aprobado.

## 11. Exclusiones y límites que no deben reabrirse sin decisión formal

- CENDOC está fuera del proyecto.
- Biblioteca Virtual y Gran Archivero son dominios distintos.
- Planchas de trabajo no pertenecen al Gran Archivero.
- El traslado entre Talleres no destruye ni mueve físicamente el historial anterior: crea continuidad histórica.
- Calendario no se convierte en fuente maestra de los procesos.
- La aprobación visual no sustituye UAT funcional.
- `main` no recibe una RC con gates o UAT pendientes.
- No se usarán datos personales reales en showcase/demo.

## 12. Regla de continuidad del hilo

Desde este punto, toda conversación futura que diga “continúa”, “sigue con Centenario” o equivalente debe:

1. recuperar este estado maestro y el estado real de `dev`/CI;
2. conservar los acuerdos anteriores salvo cambio explícito del Product Owner;
3. registrar decisiones nuevas de forma trazable;
4. trabajar sobre el backlog prioritario único;
5. evitar crear líneas paralelas, nombres nuevos o arquitecturas alternativas sin una ADR formal;
6. distinguir siempre entre **diseñado**, **implementado**, **probado**, **aprobado en UAT** y **promovido a producción**.

## 13. Regla permanente de triple salida y Definition of Done

Por decisión del Sponsor / Product Owner, desde este punto todo incremento funcional del Proyecto Centenario debe mantener tres salidas sincronizadas del mismo desarrollo y del mismo SHA:

1. **Programación real en la aplicación vigente:** el cambio debe implementarse sobre la arquitectura y módulos existentes de `dev`, con backend/frontend/datos/permisos/auditoría/migraciones/pruebas/documentación según corresponda.
2. **Demo funcional GitHub Pages con datos ficticios:** la demo debe permitir navegar y ejecutar los mismos flujos funcionales, formularios, estados, validaciones y permisos visuales utilizando exclusivamente datos ficticios/sintéticos y adaptadores mock compatibles con los contratos del sistema real. No se acepta una maqueta estática o un frontend paralelo como sustituto de la funcionalidad desarrollada.
3. **Instalable reproducible para QA en `srv01`:** cada corte funcional debe dejar generado o actualizado un paquete instalable trazable desde el mismo SHA, con aplicación, infraestructura, migraciones, scripts/runbook, manifiesto/checksums y configuración segura de ejemplo suficiente para montar el stack operacional de QA.

Un incremento no se considera terminado por el solo hecho de estar fusionado a `dev`, compilar o tener CI verde. La Definition of Done exige código integrado, demo actualizada/publicable y artefacto instalable actualizado. El despliegue en `srv01`, smoke y QA/UAT constituyen la validación operacional posterior. Si no existe acceso al servidor, debe registrarse explícitamente “despliegue QA pendiente” y nunca declararse operacional.

GitHub Pages y el instalable QA son dos salidas del mismo producto, no desarrollos separados. La demo no usa datos personales reales, secretos ni servicios institucionales reales. El instalable QA valida la arquitectura operacional vigente: API, PostgreSQL, OIDC/Keycloak, MinIO/S3, ClamAV, proxy/TLS y demás componentes definidos en el corte.

Flujo permanente:

`Requisito aprobado → feature/* desde dev → programación y pruebas → PR/CI exact-head → merge a dev → publicación Demo GitHub Pages → generación instalable del mismo SHA → despliegue/smoke srv01 → QA/UAT → promoción controlada a main`


## 14. Corte operativo vigente — regla de frescura y siguiente hito

> Esta sección prevalece sobre checkpoints históricos de las secciones 7, 8 y 10 cuando exista discrepancia de estado.

### Regla de frescura obligatoria

El SHA exacto de trabajo **no se toma de un valor fijado en este documento**. Toda sesión, chat, Work, Codex u otra IA debe consultar al inicio:

1. HEAD vivo de `dev`;
2. PRs/CI asociados al corte;
3. Issue #97 para el artifact QA y estado operacional vigente;
4. Línea Base Maestra de Google Drive.

Los SHA y artifact IDs citados en changelogs o secciones históricas son evidencia de un corte anterior, no punteros permanentes.

### Desarrollo integrado

La línea vigente incluye, como mínimo:

- Consejo de Administración auditable;
- Definition of Done de triple salida;
- demo GitHub Pages funcional con datos ficticios;
- instalable QA reproducible para `srv01`;
- kit de regresión QA de 24 controles (QA-001..QA-024);
- deploy post-merge de Pages resuelto;
- Keycloak QA persistido en PostgreSQL;
- backup/restore de PostgreSQL + MinIO;
- deploy/rollback de `srv01` por release;
- publicación en Pages de `downloads/qa-current.json` y del ZIP QA del mismo SHA.

### Demo GitHub Pages

URL oficial:

`https://pablodominguez8375-2025.github.io/Modernizaci-n-GLMC-Milenio/`

Cada publicación desplegable debe mantener la paridad:

`HEAD dev = SHA visible demo = sourceSha de qa-current.json = BUILD-INFO del ZIP QA`.

### Instalable y despliegue QA

La referencia operacional dinámica es **Issue #97**.

El método preferido está documentado en:

`docs/installation/QA-SRV01-AUTODEPLOY.md`

Flujo:

1. actualizar el clon `/opt/centenario/app` a `origin/dev`;
2. ejecutar `deploy-srv01-from-pages.sh` con el SHA vivo;
3. verificar SHA-256 externo e interno;
4. generar backup previo si existe versión instalada;
5. instalar;
6. ejecutar smoke autenticado;
7. preparar regresión QA;
8. cerrar QA-001..QA-025;
9. corregir P0/P1;
10. recién entonces congelar UAT.

### Siguiente incremento funcional

Issue #97 continúa siendo el gate operacional de despliegue físico/QA. El Sponsor / Product Owner mantiene autorización expresa para continuar desarrollo funcional en paralelo sin atribuir aceptación operacional al código no desplegado.

El flujo reglamentario de insinuaciones ya está integrado por PR #106 y el Cuadro Mensual de Tesorería por PR #112. El incremento funcional activo es **Hospitalaria del Taller + Gran Hospitalaria**, PR #114, reutilizando el módulo existente y el Consejo auditable conforme al Art. 12.13 y matrices vigentes de Drive.

No reutilizar la RC1 histórica ni evidencia de un SHA anterior para atribuir aceptación al código vivo.


## 15. Secretaría integral — estado integrado y reglas de continuidad

El flujo integral de **Secretaría del Taller + Régimen Interior + Gran Secretaría** quedó integrado mediante PR #105. Merge SHA: `88e6fd70493b972467759fddc1d6180eb07927d1`.

Reglas funcionales vigentes:

- **Cuadro del Taller:** Secretaría puede cargar hermanos activos individualmente o por XLSX; la carga histórica admite fechas desconocidas sin inventarlas; RI aprueba, observa o rechaza y sólo la aprobación modifica el Cuadro oficial.
- **Identidad histórica:** RUT y número institucional identifican transversalmente al hermano; el grado actual puede quedar validado aunque falten fechas exactas de hitos.
- **Tenidas:** estados Programada, Realizada y Cancelada; modalidad Presencial o Virtual.
- **Plancha de trabajo del hermano:** opcional, PDF/DOCX, asociada al hermano autor; puede cargarse antes o después de la Tenida; no existe para iniciación, aumento de salario ni exaltación.
- **Extracto de Acta:** PDF remitible a Gran Secretaría únicamente después de que la Tenida esté Realizada.
- **Acta completa:** PDF/DOCX opcional y privada del Taller.
- **Reuniones y Consejos:** privados del Taller; no se remiten a Gran Secretaría.
- **Gran Secretaría:** accede sólo a datos básicos de Tenidas remitidas y al Extracto PDF; no accede a plancha del hermano, acta completa, asistencia, votaciones, enlace virtual, Reuniones ni Consejos.
- **Plancha de Gran Secretaría:** documento formal separado de Decreto. Puede ser comunicado formal (`formal_communication`) o autorización formal de ceremonia (`ceremony_authorization`). La Plancha de Autorización de Ceremonia no constituye Decreto.
- **Gestión documental:** reutiliza MinIO/S3, versionado, integridad/hash, ClamAV y permisos del Taller.
- **No regresión:** Tenidas, Consejo y Gestión Documental existentes se reutilizan; no se duplican entidades ni repositorios.

Fuentes técnicas canónicas del incremento:

- `docs/PMGM-ARCH-009-secretaria-flujo-integral.md`
- `changelog/2026-09-18-secretaria-flujo-integral-v1.md`
- `changelog/2026-09-18-secretaria-flujo-integral.md`
- PR #105.

La Línea Base Maestra de Google Drive contiene el mismo acuerdo en su sección **32. Secretaría integral — Cuadro del Taller, Tenidas y Gran Secretaría**.

> Nota de frescura: este apartado describe reglas integradas. El SHA operativo vigente debe obtenerse siempre del HEAD vivo de `dev`; los SHA históricos citados aquí no son punteros de trabajo.


## 16. Regla funcional aprobada — cierre documental de Tenidas

Por decisión del Sponsor / Product Owner se fija para continuidad la siguiente regla:

- el flujo funcional objetivo de una Tenida es **Programada → Realizada → Cerrada**;
- **Realizada** significa que la Tenida se efectuó, pero todavía puede estar completando documentación;
- **Cerrada** significa cierre documental definitivo.

### Tenida regular / no ceremonial

Para cerrar una Tenida regular, el único documento obligatorio es el **Extracto de Acta en PDF**.

La Plancha de trabajo del hermano continúa siendo opcional. El Acta completa continúa siendo opcional y privada del Taller.

### Tenida ceremonial

Para cerrar una Tenida de **Iniciación, Aumento de Salario o Exaltación** son obligatorios:

1. **Extracto de Acta en PDF**; y
2. **Plancha de Autorización de Ceremonia emitida por Gran Secretaría**, vinculada/adjunta a esa misma Tenida.

La Plancha de Autorización no es una Plancha de trabajo ni un Decreto. Debe corresponder al Taller y a la ceremonia autorizada y conservar la trazabilidad con la solicitud y las aprobaciones previas.

El sistema deberá impedir el cierre cuando falte cualquiera de los documentos exigidos.

Trazabilidad objetivo de ceremonia:

`solicitud → aprobaciones → Plancha de Autorización → Tenida Realizada → Extracto de Acta → Tenida Cerrada`

**Estado de implementación:** integrado en `dev` mediante PR #110, merge funcional `3833418b8875bd97607557bec07006c924aa56d4`. El backend separa Realizada/Cerrada, bloquea cierres incompletos, vincula la Plancha oficial de Gran Secretaría por referencia y audita el cierre. La migración normaliza los antiguos `closed` a `held` preservando la fecha en `HeldAtUtc`. El frontend y el mock de Pages muestran los requisitos documentales y aplican el mismo flujo. El kit QA incorpora QA-022 y QA-023, pasando a 23 controles. Los gates pre-merge y PMGM CI/Showcase/QA Installable/Pre-UAT post-merge quedaron en `success`. Pages publicó el mismo merge SHA y el ZIP QA público `Proyecto-Centenario-QA-srv01-3833418b8875.zip` con SHA-256 `5d115390bc531cde3c485a2f429f8b099e10d9a487b8086f97315cde66783d50`. El despliegue físico srv01 y la regresión 23/23 siguen pendientes en Issue #97.



## 17. Incremento integrado — Cuadro Mensual de Tesorería y segregación de funciones

El PR #112 `feat(tesoreria): segregar Cuadro Mensual Taller y Gran Tesorería` continúa la automatización financiera desde las fuentes institucionales 2026, reutilizando el dominio Treasury ya existente.

Fuentes verificadas:

- Constitución/Reglamento, art. 12.12: el Tesorero es guardián/depositario del tesoro del Taller e integra las cotizaciones del Cuadro completo en Gran Tesorería dentro de los primeros diez días de cada mes;
- `CUADRO PAGO GRAN TESORERÍA.xlsx`: Cuadro completo, valor de cuota, rebajas respaldadas, transferencias/depósitos y Diferencia;
- Matriz Funcional Normativa y Matriz Perfiles/Vistas/Firmas: Tesorero responsable financiero del Cuadro Mensual.

Reglas del incremento:

- Tesorero del Taller prepara/genera el Cuadro de su propia organización;
- transferencias y depósitos se registran antes del envío;
- el envío se bloquea mientras `DifferenceAmount != 0` o existan identidades pendientes;
- una vez enviado, el Taller no puede agregar pagos silenciosamente;
- Gran Tesorería puede listar/revisar los Cuadros enviados y conserva la conciliación institucional;
- sólo la conciliación institucional genera/actualiza `FinancialRegularitySnapshot`;
- Ceremonias continúa consumiendo la regularidad derivada, sin duplicar comprobantes;
- la demo distingue perfil Tesorero del Taller de Gran Tesorero;
- QA-024 protege la segregación y eleva la matriz srv01 a 24 controles.

Estado integrado:

- PR #112 fusionado a `dev`;
- merge SHA funcional: `f2679b7cccd652d7cbb9fcd7688eee4d0c3a4bc3`;
- PMGM CI post-merge: `success`;
- Showcase/Pages post-merge: `success`;
- QA srv01 Installable: `success`;
- Pre-UAT Installable: `success`;
- ZIP QA público: `Proyecto-Centenario-QA-srv01-f2679b7cccd6.zip`;
- SHA-256 ZIP público: `cae2bae623554ec0b42daedc78dda3703dd1042a63d47b059d480485b45ba159`;
- artifact QA Actions ID `10575826365`, digest `sha256:3447dbabb50194cd2a0da38c24d66e5c08028c4f296653d6f9db187d5d1ed640`;
- Pages artifact ID `10575473010`, digest `sha256:a7c152f4674fc466f9fab80e7ccd6f79f3a0472d1189ec44cc714d06743f3b4d`;
- kit de regresión vigente: **QA-001..QA-024**, total 24 controles;
- Issue #97 y Línea Base Maestra de Drive actualizados al corte funcional.

El despliegue físico en `srv01`, smoke real, regresión 24/24 y UAT continúan pendientes y no se declaran ejecutados desde este chat.

Siguiente bloque funcional: **Hospitalaria del Taller + Gran Hospitalaria**, respetando independencia del Tronco de Beneficencia respecto de Tesorería, Art. 12.13 y fuentes documentales vigentes de Drive.


## 18. Incremento integrado — Hospitalaria integral Taller + Gran Hospitalaria

PR #114 `feat(hospitalaria): flujo integral Taller y Gran Hospitalaria`.

Fuentes verificadas:

- Constitución y Reglamento General, art. 12.13;
- Matriz Funcional Normativa de Cargos de Taller;
- Matriz Perfiles/Vistas/Firmas Proyecto Centenario;
- Línea Base Maestra vigente.

Reglas implementadas en la rama:

- el Tronco de Beneficencia permanece independiente de Tesorería;
- Hospitalario registra aportes, movimientos y socorros de su Taller;
- todo egreso requiere respaldo y nace pendiente de autorización;
- Venerable Maestro puede inspeccionar y aprobar sin editar el libro del Hospitalario;
- el Consejo puede autorizar socorros sólo mediante acuerdo real `benevolence_aid_proposal` aprobado y por monto coincidente;
- Secretaría del Taller no administra Hospitalaria;
- Gran Hospitalaria no tiene acceso al detalle local de beneficiarios/ayudas;
- la rendición mensual exige revisión Hospitalaria del Consejo;
- la proyección superior contiene sólo agregados, reposiciones, comprobante y referencias institucionales;
- Gran Hospitalaria observa o concilia;
- la conciliación genera `HospitalariaRegularitySnapshot`, reutilizado por Ceremonias;
- el endpoint manual histórico de regularidad se conserva para compatibilidad/excepción;
- QA-025 eleva la regresión a 25 controles.

Frontera de privacidad:

- local reservado: referencia interna, destino resumido, respaldo y observación minimizada;
- Gran Hospitalaria: no recibe `memberReference`, destino, observación ni detalle de beneficiario;
- no se registran diagnósticos médicos detallados como parte de este incremento.

Estado integrado:

- PR #114 fusionado a `dev`;
- merge funcional: `44b6cc90a56e508c8c45e85d040e928738e1b5d8`;
- PMGM CI post-merge: `success`;
- Showcase/Pages post-merge: `success`;
- QA srv01 Installable: `success`;
- Pre-UAT Installable: `success`;
- ZIP QA público: `Proyecto-Centenario-QA-srv01-44b6cc90a56e.zip`;
- SHA-256 ZIP público: `c5474a135c879e7df99768bc19130f7774e0583b2e8f06560e99f11bf2d2a6e6`;
- artifact QA Actions ID `10576091439`, digest `sha256:33556345f71ccbfcfdf87eea7acc646db0423848ca4d87ec53cbcb6819fc64d7`;
- Pages artifact ID `10576321235`, digest `sha256:c44b93d565c8cfba3557d3544e1191473961e485ca75d362d3848f74203231dd`;
- Pre-UAT artifact ID `10576206258`, digest `sha256:747436181f673c14538ca8171cc5085443ad0fe8e49742647c35199d26a7b9d6`;
- kit de regresión vigente: **QA-001..QA-025**, total 25 controles;
- Issue #97 y Línea Base Maestra de Drive actualizados al corte funcional.

El despliegue físico en `srv01`, smoke real, regresión 25/25 y UAT continúan pendientes y no se declaran ejecutados desde este chat.

El siguiente incremento funcional debe seleccionarse sólo después de reconsultar backlog vigente, documentos oficiales de Drive y HEAD vivo de `dev`; no se fija por conversaciones antiguas.

## 20. Incremento activo — navegación operativa de Secretaría por cargo

El 22-09-2026 se inicia `feature/secretariat-role-navigation` desde `dev` `822fd25b88caafdf5a01d89c27c53b6cd000f183`.

Alcance:

- menú lateral único **Secretaría** para Secretaría del Taller;
- menú lateral único **Gran Secretaría** para Gran Secretaría;
- navegación interna a las tareas existentes de cada cargo;
- conservación del circuito aprobado de insinuaciones, ceremonias, Tenidas, extractos, correspondencia y pendientes;
- sin ampliación de permisos ni mezcla con Tesorería, Hospitalaria o Docencia;
- Planchas, Decretos y demás documentos oficiales se describen y se cargan en PDF después de su firma física; el sistema no los genera ni firma.

Referencia: `docs/PMGM-ARCH-013-secretaria-navegacion-por-cargo.md`. Prueba: QA-030 / UI QA v0.58. Validación local inicial: 153 pruebas frontend y build productivo aprobados. El incremento aún requiere PR, CI exact-head, Demo y paquete instalable del mismo SHA.

## 21. Corrección de segregación — Tesorero del Taller y Secretaría

La capacidad `canManageLodgeTreasury` habilita únicamente el espacio financiero del Taller. No habilita Gestión Logial, Secretaría, Tenidas, asistencia, actas, correspondencia, pendientes, retiros, expedientes de insinuación, Circuito de Iniciación, Cuadro del Taller, Ficha del Taller ni Gestor Documental general.

El Tesorero conserva las vistas transversales mínimas del usuario autenticado y el menú **Tesorería**, con Resumen, Cuotas y Cobranzas, Ingresos y Egresos, Cuadro mensual, Configuraciones y Reportes. Los egresos pendientes no reducen la caja hasta la autorización del Venerable Maestro; los pagos de cuotas se contabilizan desde su comprobante para evitar duplicar ingresos. El backend ya separa `CanManageLodgeTreasury` de `CanManageLodgeOperations`, `CanReadLodgeSecretariat`, `CanManageLodgeSecretariat` y `CanManageDocuments`; QA-031 exige la misma separación en frontend y Demo.

## 19. Incremento activo — Tesorería operacional del Taller

El 21-09-2026 se inició `feature/treasury-completion-20260921` desde el HEAD vivo de `dev` `5704400c6fdd0b787dcb4ec17e36be9e69f4484a`, manteniendo sin cambios el candidato aceptado de Secretaría/Gran Secretaría y la QA física de `srv01` diferida.

Alcance del primer corte:

- menú operativo único **Tesorería**, separado de Gestión Logial y de **Gran Tesorería**;
- consulta de cargos individuales del período, saldo y cartola resumida;
- registro de pagos parciales o totales con medio, referencia y comprobante correlativo;
- registro de egresos con descripción y referencia del PDF de respaldo;
- egreso siempre pendiente hasta autorización del Venerable Maestro;
- Venerable puede consultar y autorizar, pero no editar la operación de Tesorería;
- Gran Tesorería deja de tener permiso para administrar la Tesorería local y conserva revisión/conciliación del Cuadro mensual;
- demo Pages con datos exclusivamente ficticios y contratos equivalentes al backend.

Validación local: 149 pruebas frontend aprobadas y build de producción exitoso. El backend deberá compilarse y probarse en PMGM CI porque el entorno local de esta intervención no dispone del SDK .NET. Este corte todavía no está integrado en `dev`, publicado en Pages ni empaquetado para QA: requiere commit, PR draft, CI exact-head y gates de publicación.

---

**Este documento es la referencia canónica de continuidad funcional/técnica del Proyecto Centenario.**

## 22. Derechos ceremoniales por expediente — integrado en `dev`

El 24-09-2026 se inicia `feature/ceremony-rights-ledger` desde `dev@d7b931b323c191733a09f392c74759f7994e844b`, atendiendo la continuación priorizada en Drive: enlazar el pago/conciliación de derechos ceremoniales con cada expediente y su elegibilidad. `main@6dfb9546a4873baff15955cf86abfd7d47e3d111` permanece intacta.

El alcance registra abonos parciales o totales en un libro ligado a `CeremonyRequest`; fija monto y moneda desde el Decreto N.º 1.759; emite comprobante y registra actor/auditoría; aplica idempotencia; presenta monto pagado y saldo en una vista financiera mínima de Gran Tesorería y en la evaluación del expediente; y bloquea la autorización mientras haya saldo. Sólo Gran Tesorería registra este derecho institucional; el Tesorero local no obtiene acceso a la bandeja de ceremonias de la Orden. El mock de Pages usa exclusivamente datos ficticios. QA-039 / `docs/qa/PMGM-QA-V070-DERECHOS-CEREMONIALES.md` define aceptación física.

El incremento quedó incorporado en `dev` por el commit `8522b0dfd3e9db6eb964f6fdce5e8479c2a3525c`. El HEAD vivo revisado es `42a415ea9dbd235bde58454a5789eb352f952b06`; `main` permanece en `6dfb9546a4873baff15955cf86abfd7d47e3d111`. La integración preserva los pagos parciales, libro asociado a `CeremonyRequest`, montos CLP del Decreto N.º 1.759, comprobante, actor/auditoría, idempotencia, bloqueo de autorización con saldo y separación de Gran Tesorería frente a Tesorería del Taller. QA-039 queda como aceptación física pendiente.

Gates exact-head del `dev` vivo: PMGM CI #1519, Showcase/Pages #776, QA Installable #414 y Pre-UAT #348, todos SUCCESS. El Showcase publicado usa datos ficticios y sus capturas incluyen Gran Tesorería en 390×844, 768×1024 y 1440×900; la vista móvil muestra el acceso a Derechos ceremoniales sin desbordamiento horizontal global. Pages artifact #10829655132 (`sha256:2290457be3647fe5ad229f1845aa95683874ea07d5b4fa91c1e0fa8fbf21919c`); evidencia responsive #10828807432 (`sha256:9883852182184238ea3068c992b9f735bb833336a81d4f19b31bd8c3ece94158`). QA artifact #10829506011 (`sha256:15177174ec8090a78cb3a83401e371574444a22e99d51ac1dc0f3a9c6397a99a`) fue descargado y su `BUILD-INFO.txt` confirma `SOURCE_SHA=42a415ea9dbd235bde58454a5789eb352f952b06`; el manifiesto debe verificarse al instalarlo.

La integración y publicación no cierran Issue #97: despliegue físico en `srv01`, verificación del manifiesto, smoke autenticado, regresión QA-001..QA-039 y UAT institucional siguen pendientes/diferidos. No promover a `main` ni declarar aceptación operacional hasta completar esos pasos.
