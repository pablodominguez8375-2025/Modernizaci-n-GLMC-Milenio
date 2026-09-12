# PMGM-GOV-001 — Instrucciones y decisiones consolidadas del Proyecto Milenio

**Estado:** Vigente  
**Fecha de consolidación:** 2026-09-08  
**Rama de trabajo:** `dev`  
**Rama estable:** `main`

## 1. Propósito
Este documento consolida las instrucciones funcionales, técnicas, operativas y de gobierno vigentes del **Proyecto Milenio — Modernización Gran Logia Mixta de Chile**. Ante una contradicción entre una definición antigua y una instrucción posterior, **prevalece la instrucción posterior aprobada** y debe corregirse la documentación afectada.

## 2. Hilo maestro único y gobierno del proyecto
- Este chat queda definido como **hilo maestro único** de dirección y continuidad del Proyecto Milenio.
- Requisitos, correcciones, decisiones, avances, QA, arquitectura, documentación y coordinación se continúan desde este hilo.
- Las conversaciones anteriores se consideran antecedentes históricos; las decisiones vigentes deben consolidarse aquí y en GitHub.
- GitHub privado continúa siendo la fuente única de verdad de código, documentación, backlog, arquitectura y evidencias.
- Sponsor / Product Owner: Pablo Domínguez.
- ChatGPT: arquitectura funcional/técnica, consolidación de requisitos, documentación, QA y coordinación del avance.
- Codex: desarrollo/automatización cuando corresponda.
- Flujo obligatorio: requisito → diseño → aprobación de arquitectura → desarrollo → pruebas → documentación → publicación.
- `dev` concentra el desarrollo activo.
- `main` es rama estable y **no se fusiona sin autorización expresa del Product Owner**.
- El PR es la vía de promoción controlada entre `dev` y `main`.
- Ningún incremento se declara estable si rompe compilación, migraciones, seguridad, gates o pruebas de CI.
- QA tiene prioridad máxima de corto plazo: trabajar en incrementos cerrados, comprobables y demostrables.
- La documentación de instalación/QA debe avanzar junto con el desarrollo.
- La presentación ejecutiva debe evolucionar junto con el sistema y mostrar estado **Hecho / En curso / Siguiente**.

## 3. Objetivo inmediato y ambientes
El objetivo prioritario es obtener cuanto antes un **QA instalable, usable y presentable en una máquina virtual**.

Primer corte demostrable priorizado:
1. Gestión Logial.
2. Biblioteca Virtual con seguridad por grado.
3. Gran Archivo / Gran Archivero.
4. Instalación QA reproducible y PPT ejecutiva con mockups.

Ambientes separados:
- DEV — desarrollo e integración continua.
- QA — validación funcional/técnica en VM.
- PROD — producción, sólo después de aprobación.
- DEMO — opcional, únicamente con datos ficticios/sintéticos.

## 4. Arquitectura técnica vigente
- Backend: ASP.NET Core / .NET 10.
- Persistencia: PostgreSQL 17 + Entity Framework Core/Npgsql.
- Frontend: React 19 + TypeScript 6 + Vite 8.
- Arquitectura: monolito modular con límites de dominio explícitos.
- Contenedores: Docker / Docker Compose.
- CI/CD: GitHub Actions.
- Identidad: JWT/OIDC genérico; proveedor definitivo pendiente.
- Cultura: `es-CL`.
- Zona horaria institucional: `America/Santiago`.
- Moneda: CLP.
- Binarios: Object Storage S3-compatible, privado por defecto.
- Desarrollo/CI/QA: MinIO privado.
- Antimalware: ClamAV.
- Integridad: SHA-256, validación MIME/firma real y ciclo de estados seguro.
- Diseño API-first, modular, auditable y security/privacy by design.

## 5. Portal público e intranet
- El portal público debe poder ser autogestionado por Comunicaciones mediante un CMS simple, plantillas, permisos por rol y flujo de aprobación.
- La intranet/plataforma institucional gestiona la operación interna y logial con permisos finos, historial y auditoría.
- Portal público e intranet no deben compartir permisos de administración de manera implícita.

## 6. Identidad institucional y membresía
1. Una persona tiene una identidad maestra única.
2. El RUT puede ser dato institucional cuando corresponda, pero **nunca será PK técnica**.
3. Un hermano conserva su historia aunque cambie de Taller.
4. En una transferencia, el Taller de origen conserva la historia y el Taller receptor crea la nueva pertenencia.
5. Fechas de iniciación, aumento de salario, exaltación, retiro, reintegro, defunción y demás eventos institucionales deben quedar historizadas.
6. El grado efectivo se obtiene desde la historia institucional vigente; nunca desde un valor editable por el usuario o enviado por frontend.
7. La vinculación entre identidad autenticada (`issuer/sub`) y miembro institucional debe ser centralizada, única y auditable.
8. El hermano puede modificar únicamente datos personales/contacto expresamente autorizados; no puede alterar su historial institucional.

## 7. Gestión Logial — ficha del hermano
La ficha personal del hermano debe permitir consultar, según permisos:
- datos institucionales y personales autorizados;
- Taller actual e historial de Talleres;
- grado vigente e historial de grado;
- cargos y períodos;
- fechas institucionales relevantes;
- asistencia a tenidas;
- historial de instrucciones;
- planchas de trabajo propias;
- historial/estado de cuotas y comprobantes;
- anotaciones institucionales autorizadas.

El hermano podrá editar campos personales apropiados, por ejemplo domicilio, teléfono, correo y fotografía. No podrá editar iniciación, aumento de salario, exaltación, retiros, reintegros, defunción, grado ni demás eventos institucionales protegidos.

## 8. Gestión Logial — Taller y autoridades
Cada Taller es una entidad institucional con identidad propia dentro de la Gran Logia.

Su perfil puede incluir:
- nombre/número;
- descripción o “acerca de”;
- historia/origen;
- fecha de creación;
- información relevante;
- datos de contacto autorizados;
- logo/identidad visual propia.

El Taller puede mantener contenido propio mediante perfiles autorizados. Los datos institucionales críticos permanecen sujetos a gobierno de la Gran Logia y toda modificación relevante queda trazada.

El **Venerable Maestro** es la máxima autoridad del Taller.

## 9. Secretaría del Taller
La Secretaría de cada Taller administra operativamente:
- tenidas;
- registro de asistencia a tenidas por grado;
- actas y antecedentes secretariales del Taller;
- registro/propuesta de fechas institucionales del hermano conforme al flujo autorizado;
- anotaciones institucionales autorizadas;
- solicitudes de ceremonias;
- solicitudes/reservas de salas y templos.

La Secretaría del Taller solicita, entre otras:
- iniciaciones;
- aumentos de salario;
- exaltaciones;
- reserva de templos;
- reserva de salas de Secretaría u otros espacios habilitados.

Gran Secretaría es quien aprueba/autoriza cuando corresponda y emite la documentación formal institucional.

## 10. Docencia e instrucciones por grado
La instrucción se modela inicialmente como una **clase/instrucción realizada**, no como progreso independiente por tema.

Responsables vigentes:
- **Segundo Vigilante:** instrucción de hermanos de primer grado / Aprendices.
- **Primer Vigilante:** instrucción de hermanos de segundo grado / Compañeros.
- **Inmediato Ex Venerable Maestro:** instrucción de Maestros, tercer grado y superiores.

Cada instrucción registra como mínimo:
- fecha;
- Taller;
- grado correspondiente;
- responsable/instructor;
- tema tratado;
- asistencia o inasistencia por hermano;
- observaciones cuando corresponda.

En la ficha del hermano, por ahora el historial de instrucción debe mostrar principalmente:
- fecha;
- tema tratado;
- asistió / no asistió.

Evaluaciones, puntajes o avance curricular por temas se dejan para una etapa posterior.

## 11. Planchas de trabajo
Una **plancha de trabajo** es un trabajo preparado por un hermano, asociado a título/tema, autor, Taller y grado, y puede ser presentado en una tenida o instancia de trabajo.

Regla vigente:
- vive en Gestión Logial asociada al hermano/Taller;
- forma parte del historial del hermano;
- **no se envía al Gran Archivo por el solo hecho de ser una plancha de trabajo**;
- si se autoriza su publicación, se proyecta a la sección **Planchas de Trabajo** de Biblioteca Virtual;
- su acceso en Biblioteca respeta grado efectivo y demás permisos;
- Biblioteca mantiene referencia al origen sin sustituir el registro de Gestión Logial.

## 12. Plancha de Secretaría y decreto
No confundir categorías:
- **Plancha de trabajo:** obra de un hermano; Gestión Logial → eventualmente Biblioteca.
- **Plancha de Secretaría:** documento administrativo/oficial de Gran Secretaría, por ejemplo autorizaciones de ceremonia, autorizaciones de aumento de salario y comunicaciones internas formales.
- **Decreto:** documento oficial decretativo, categoría distinta y formal.

Las planchas de Secretaría, decretos y otros documentos oficiales pueden pasar a custodia histórica del Gran Archivo cuando corresponda.

## 13. Tesorería del Taller — gobierno y cuotas
Cada Taller tiene su propio **Tesorero del Taller**, responsable de administrar la tesorería del Taller.

La cuota mensual se compone de **dos montos independientes y configurables por vigencia**:
1. monto que el Taller cobra al hermano;
2. monto que el Taller debe pagar a Gran Tesorería por ese hermano.

Ejemplo funcional: el hermano puede pagar $26.000 mientras el Taller debe $20.000 a Gran Tesorería. Ambos valores pueden cambiar en una fecha posterior sin reescribir períodos anteriores.

Cada configuración de cuota debe registrar vigencia desde/hasta, monto, responsable y auditoría.

## 14. Tesorería del Taller — pagos y estado de cuenta
- Se permiten pagos completos y **pagos parciales/abonos**.
- Cada pago se imputa a uno o más meses/períodos específicos.
- Debe quedar monto pagado, saldo, período devengado, medio/referencia y actor que registró el pago.
- Al registrar un pago se genera un comprobante.
- El sistema debe enviar por correo electrónico el comprobante al hermano indicando monto, período/mes aplicado y saldo cuando corresponda.
- El hermano puede consultar su estado de cuenta y descargar sus comprobantes desde su ficha.
- Correcciones/anulaciones no deben borrar el movimiento original; deben quedar trazadas mediante reversa/ajuste autorizado.

## 15. Tesorería del Taller — ingresos, egresos y contabilidad
Además de cuotas, Tesorería puede registrar otros ingresos extraordinarios.

Puede registrar egresos operativos, por ejemplo:
- insumos;
- útiles;
- elementos para funcionamiento del Taller;
- celebraciones autorizadas;
- tortas/champaña u otros gastos asociados a actividades particulares;
- otros derechos, compras o gastos institucionales debidamente registrados.

Reportes mínimos:
- ingresos por mes;
- egresos por mes;
- saldo;
- tipo de movimiento;
- resumen mensual;
- libro/detalle de movimientos;
- navegación desde resumen a detalle (“drill-down”);
- proyección de ingresos;
- proyección de pagos/egresos;
- comparación proyectado vs realizado;
- exportación a PDF y Excel cuando corresponda.

## 16. Tesorería del Taller — rendición a Gran Tesorería
El sistema debe generar una nómina mensual de lo que el Taller debe transferir a Gran Tesorería.

La nómina podrá incluir según política de minimización y autorización:
- nombre del hermano;
- RUT cuando sea necesario para la rendición institucional;
- Taller;
- cargo;
- grado;
- período;
- monto que corresponde pagar a Gran Tesorería;
- monto efectivamente pagado/transferido;
- diferencia.

Debe existir conciliación automática y alertas tempranas cuando lo pagado no coincide con lo esperado, tanto por defecto como por exceso.

Ejemplo: si correspondían $325.000 y se transfieren $315.000 o $330.000, el sistema debe advertir la diferencia y conservar evidencia de conciliación/corrección.

## 17. Hospitalaria del Taller — Bolso de Hospitalaria
Cada Taller dispone de una **Hospitalaria del Taller**.

Su fuente habitual de ingreso es el **Bolso de Hospitalaria / ofrendas**, colecta voluntaria realizada en las tenidas.

Estos fondos pueden destinarse, conforme a autorización, a:
- ayuda económica a hermanos;
- préstamos o apoyos fraternales;
- pago de una obligación particular por dificultad económica;
- donaciones;
- ayudas por incendios, terremotos u otras contingencias;
- actividades benéficas.

Todo ingreso, egreso, ayuda, préstamo, devolución o donación debe quedar contabilizado y auditado.

## 18. Hospitalaria — reposiciones
La Hospitalaria del Taller gestiona la recaudación asociada a **reposiciones** y su transferencia a Gran Hospitalaria.

Reglas base:
- puede existir un monto de incorporación al fondo cuando un hermano adquiere el derecho correspondiente;
- cuando fallece un hermano con derecho a reposición, los hermanos activos sujetos a la regla pagan una cuota de reposición;
- el monto debe ser configurable por vigencia; como referencia actual se ha mencionado $1.500, pero no debe quedar fijo en código;
- Hospitalaria del Taller controla quién debe pagar, quién pagó, deuda/saldo y monto total recaudado;
- el Taller transfiere la recaudación a Gran Hospitalaria;
- Gran Hospitalaria gestiona el pago correspondiente a deudos/herederos conforme a las reglas institucionales;
- todo el circuito debe poder conciliarse y auditarse.

## 19. Régimen Interior
Debe poder obtener reportes y controles institucionales, entre otros:
- hermanos activos/inactivos;
- retiros voluntarios y forzosos;
- defunciones;
- Past Actives;
- morosidad global y por Taller;
- fechas de retiro, reintegro, iniciación, aumento de salario y exaltación;
- historial suficiente para corroboración institucional;
- estado y trazabilidad de cambios relevantes de membresía.

## 20. Gran Secretaría
Gran Secretaría:
- emite decretos y comunicados;
- redacta y emite la autorización formal de ceremonias;
- sólo autoriza ceremonias cuando las validaciones institucionales requeridas son conformes;
- administra autorización/uso de templos y salas de Secretaría según disponibilidad;
- evita solapamientos de reservas;
- mantiene auditoría de documentos, autorizaciones y espacios.

### Muro Institucional de Gran Secretaría
Es el canal oficial dentro de la intranet para decretos, planchas oficiales, comunicados, circulares, resoluciones, llamados y avisos. Puede segmentar audiencia por grado, Taller, cargo/rol, órgano/comisión o hermano específico y puede exigir confirmación de lectura.

## 21. Ceremonias e insinuados
- Tipos iniciales: iniciación, aumento de salario y exaltación.
- Para autorizar una ceremonia el Taller debe estar al día con Gran Tesorería y con reposiciones de Gran Hospitalaria.
- Las validaciones quedan congeladas como evidencia para la autorización.
- En iniciaciones, el insinuado debe publicarse previamente durante un plazo configurable; valor inicial: 20 días.
- El insinuado no es miembro activo hasta que la iniciación quede registrada.
- Las excepciones críticas deben ser formales, justificadas y auditadas.

## 22. Ley 21.719 y privacidad
La Ley 21.719 es transversal y prioritaria. Aplicar:
- privacy by design/default;
- finalidad y base de licitud;
- minimización;
- conservación y eliminación;
- derechos de titulares;
- encargados y transferencias internacionales;
- incidentes;
- EIPD/Evaluación de Impacto;
- legal holds;
- anonimización/seudonimización cuando corresponda;
- auditoría y clasificación de datos.

Los gates de privacidad, clasificación y seguridad de migraciones son obligatorios en CI.

## 23. Dominios documentales y exclusión de CENDOC
**CENDOC no forma parte del Proyecto Milenio** como módulo, dependencia ni componente.

Los dominios documentales son distintos:
1. Gestión Documental Operativa.
2. Biblioteca Virtual.
3. Gran Archivo / Gran Archivero.

Comparten infraestructura segura de binarios cuando corresponde, pero mantienen responsabilidades, autorización y ciclos de vida separados.

## 24. Biblioteca Virtual
La Biblioteca Virtual publica y permite consultar material autorizado; no asume custodia archivística.

### Acceso acumulativo por grado
- grado mínimo 1 → puede acceder grado 1, 2, 3 o superior;
- grado mínimo 2 → puede acceder grado 2, 3 o superior;
- grado mínimo 3 → puede acceder grado 3 o superior.

Regla efectiva: `grado_actual >= grado_minimo_requerido`.

La regla debe aplicarse en backend a catálogo, búsqueda, filtros, facetas, detalle, miniaturas protegidas, descarga y acceso por ID directo. Un recurso no autorizado no debe aparecer por defecto en resultados, conteos ni facetas.

Las restricciones por grado se combinan por defecto con estado institucional, Taller/organización, rol/cargo y demás criterios mediante `AND`, salvo política explícita diferente.

## 25. Gran Archivo / Gran Archivero
El Gran Archivo custodia el patrimonio histórico institucional de la Gran Logia.

El rol `GRAN_ARCHIVERO` puede gestionar:
- fondos, secciones, series, expedientes y piezas;
- transferencias documentales formales;
- procedencia y cadena de custodia;
- ubicación física;
- digitalización y preservación;
- restricciones y fechas de apertura;
- solicitudes de consulta y préstamos;
- integridad y versiones de preservación.

Tipos institucionales potenciales incluyen decretos, planchas de Secretaría, cartas/correspondencia, actas, investigaciones, amonestaciones, expedientes disciplinarios y otros documentos históricos formalmente transferidos.

### Seguridad del Gran Archivo
- Descubrir una ficha archivística y acceder al contenido son permisos distintos.
- Visibilidad y contenido pueden depender de grado, hermano, Taller, cargo/rol, órgano/comisión, finalidad, fecha de apertura, grant individual y vigencia.
- Investigaciones, amonestaciones, expedientes disciplinarios e informes reservados parten con política restrictiva.
- El Gran Archivero **no es superusuario universal**.
- Conocer un ID nunca permite eludir autorización.

## 26. Ventana Fraterna
Módulo comunitario interno, separado del Muro Institucional oficial. Categorías iniciales:
- trabajo/ofertas laborales;
- búsqueda de trabajo;
- ayuda e información;
- servicios;
- compraventa;
- libros;
- avisos comunitarios.

Debe incluir moderación, reportes, caducidad de publicaciones, auditoría y adjuntos seguros. En primera etapa no procesa pagos ni garantiza transacciones.

## 27. Notificaciones y calendario
- Notificaciones deben desacoplarse de módulos de negocio.
- Calendario institucional unificado debe proyectar eventos desde sus fuentes sin duplicar la fuente original.
- Debe integrar ceremonias, tenidas, decretos/avisos, reservas y recordatorios según permisos.
- Los comprobantes y avisos de Tesorería/Hospitalaria podrán utilizar el servicio transversal de notificaciones cuando ese módulo esté disponible.

## 28. QA, instalación y operación
El proyecto debe poder desplegarse en un servidor QA reproducible mediante material versionado:
- `infrastructure/docker-compose.qa.yml`;
- `.env.qa.example`;
- `docs/PMGM-QA-001-despliegue-servidor-qa.md`;
- `docs/PMGM-QA-002-checklist-validacion.md`.

El entregable QA debe incluir guía paso a paso, scripts/configuración, migraciones, datos iniciales/sintéticos, health checks, backup/restauración, actualización/rollback y checklist.

Principios QA:
- PostgreSQL, MinIO y ClamAV no se exponen innecesariamente al host;
- web detrás de HTTPS/reverse proxy;
- secretos fuera del repositorio;
- backups de PostgreSQL y Object Storage;
- evidencia de versión/commit desplegado;
- CI valida configuraciones DEV y QA.

## 29. Seguridad documental
- Buckets privados.
- Object Keys opacos: sin RUT, nombre, correo, título, Taller ni clasificación sensible.
- No exponer ObjectKey, SHA, referencias de scan ni nombre físico en DTOs públicos de Biblioteca.
- Descarga sólo cuando la versión está `available`.
- Error del scanner nunca equivale a archivo limpio.
- No sobrescribir originales de preservación.
- Restricciones combinadas se consideran `AND` salvo política explícita distinta.
- La UI nunca será la frontera real de autorización.

## 30. Orden de ejecución vigente
1. Consolidación de instrucciones y corrección de contradicciones.
2. Cerrar seguridad de Biblioteca Virtual por identidad/grado institucional vigente.
3. Probar grado 1/2/3, facetas y acceso directo por ID.
4. Llevar al QA el núcleo de Gestión Logial: ficha de hermano, Taller, Secretaría, instrucciones/asistencia y planchas.
5. Incorporar Tesorería del Taller al corte QA: cuotas, abonos, comprobantes, contabilidad, proyección y conciliación con Gran Tesorería.
6. Incorporar Hospitalaria del Taller: Bolso, ayudas y reposiciones.
7. Implementar Gran Archivo: estructura, catálogo seguro, permisos y transferencias.
8. Gran Archivo: digitalización, preservación, consultas y préstamos.
9. Completar workflow editorial/taxonomías de Biblioteca.
10. Muro Institucional de Gran Secretaría.
11. Ventana Fraterna.
12. Notificaciones y calendario.
13. Proveedor OIDC/PKCE definitivo.
14. OpenAPI e integraciones externas.
15. Hardening y operación productiva.

## 31. Regla de mantenimiento
Toda nueva instrucción aprobada en el hilo maestro debe:
1. incorporarse a la continuidad de este chat;
2. actualizar el requisito afectado;
3. actualizar backlog/arquitectura si cambia alcance o diseño;
4. actualizar este documento si modifica una regla transversal;
5. agregar o ajustar pruebas cuando afecta seguridad o comportamiento;
6. conservar trazabilidad en GitHub.
