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
- tres entrevistas;
- Cuestionario Confidencial y autobiografía;
- revisión/votación de 3.er grado;
- balotaje en 1.er grado después de cumplir publicación y antecedentes;
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
- Segundo Vigilante para Aprendices, Primer Vigilante para Compañeros y Ex Venerable Maestro para Maestros, salvo regla institucional posterior parametrizada;
- asistencia individual específica a cada instrucción, con presente, ausente o justificada, separada de la asistencia general a Tenida aunque pueda vincularse a ella;
- historial formativo simple por hermano; evaluaciones y rúbricas quedan fuera del MVP;
- planchas asociadas al historial del hermano cuando corresponda;
- publicación en Biblioteca sólo cuando exista autorización y permisos compatibles;
- filtros por grado y Taller/contexto según política.

### 4.13 Calendario y Notificaciones

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

## 7. Estado técnico consolidado al 11-09-2026

### Aprobaciones ya registradas

- candidato visual institucional aprobado;
- baseline visual registrado;
- frontend responsive y fidelidad general validados;
- release candidate `v1.0.0-rc1` definido para UAT;
- paquete instalable pre-UAT y showcase forman parte del flujo de entrega;
- datos de demostración exclusivamente ficticios.

### Estado de rama

`dev` es la rama de integración vigente. `main` continúa reservada para la versión aprobada.

En la consolidación más reciente se incorporó a `dev` el commit:

`a54b46dfd73ea51707dc710245bb0facf8cfaf5e`

que integra la corrección del cliente MinIO (`quay.io/minio/mc`) y el ajuste de migraciones para enlazar `AdmissionCase` con `CeremonyRequest` después de crear las tablas correspondientes.

### CI / QA

En la ejecución anterior:

- infraestructura: verde;
- frontend: verde;
- privacy gate Ley 21.719: verde;
- data classification gate: verde;
- migration safety gate: verde;
- bloqueo identificado antes del build backend por imagen `minio/mc:latest` inexistente/rechazada;
- corrección integrada posteriormente en `dev`.

Tras la integración se reactivaron:

- `PMGM CI`;
- `Proyecto Centenario Pre-UAT Installable`.

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

---

**Este documento es la referencia canónica de continuidad funcional/técnica del Proyecto Centenario.**
