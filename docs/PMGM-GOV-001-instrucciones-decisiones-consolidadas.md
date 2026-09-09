# PMGM-GOV-001 — Instrucciones y decisiones consolidadas del Proyecto Milenio

**Estado:** Vigente  
**Fecha de consolidación:** 2026-09-08  
**Rama de trabajo:** `dev`  
**Rama estable:** `main`

## 1. Propósito
Este documento consolida las instrucciones funcionales, técnicas, operativas y de gobierno vigentes del **Proyecto Milenio — Modernización Gran Logia Mixta de Chile**. Ante una contradicción entre una definición antigua y una instrucción posterior, **prevalece la instrucción posterior aprobada** y debe corregirse la documentación afectada.

## 2. Gobierno y forma de trabajo
- Sponsor / Product Owner: Pablo Domínguez.
- ChatGPT: arquitectura funcional/técnica, consolidación de requisitos, documentación, QA y coordinación del avance.
- Codex: desarrollo/automatización cuando corresponda.
- GitHub privado: fuente única de verdad de código, documentación, arquitectura, backlog y evidencias.
- Flujo obligatorio: requisito → diseño → aprobación de arquitectura → desarrollo → pruebas → documentación → publicación.
- `dev` concentra el desarrollo activo.
- `main` es rama estable y **no se fusiona sin autorización expresa del Product Owner**.
- Ningún incremento se declara estable si rompe compilación, migraciones, seguridad, gates o pruebas de CI.
- QA tiene prioridad alta: trabajar en incrementos cerrados, comprobables y demostrables.
- La documentación de instalación/QA debe avanzar junto con el desarrollo.
- La presentación ejecutiva deberá incluir un tablero de avance visible: hecho / en curso / pendiente.

## 3. Arquitectura técnica vigente
- Backend: ASP.NET Core / .NET 10.
- Persistencia: PostgreSQL 17 + Entity Framework Core/Npgsql.
- Frontend: React 19 + TypeScript 6 + Vite 8.
- Arquitectura: monolito modular con límites de dominio explícitos.
- Contenedores: Docker / Docker Compose.
- CI/CD: GitHub Actions.
- Identidad: JWT/OIDC genérico, proveedor definitivo pendiente.
- Cultura: `es-CL`.
- Zona horaria institucional: `America/Santiago`.
- Moneda: CLP.
- Binarios: Object Storage S3-compatible, privado por defecto.
- Desarrollo/CI/QA: MinIO privado.
- Antimalware: ClamAV.
- Integridad: SHA-256, validación MIME/firma real y ciclo de estados seguro.

## 4. Identidad institucional y membresía
1. Una persona tiene una identidad maestra única.
2. El RUT nunca será PK técnica.
3. Un hermano conserva su historia aunque cambie de Taller.
4. En una transferencia, el Taller de origen conserva la historia y el Taller receptor crea la nueva pertenencia.
5. Fechas de iniciación, aumento de salario, exaltación, retiro, reintegro, defunción y demás eventos institucionales deben quedar historizadas.
6. El grado efectivo se obtiene desde la historia institucional vigente; nunca desde un valor editable por el usuario.
7. La vinculación entre identidad autenticada y miembro institucional debe ser centralizada y auditable.

## 5. Régimen Interior
Debe poder obtener reportes y controles institucionales, entre otros:
- hermanos activos/inactivos;
- retiros voluntarios y forzosos;
- defunciones;
- Past Actives;
- morosidad global y por Taller;
- fechas de retiro, reintegro, iniciación, aumento de salario y exaltación;
- historial suficiente para corroboración institucional.

## 6. Gran Secretaría
Gran Secretaría:
- emite decretos y comunicados;
- redacta y emite la autorización formal de ceremonias;
- sólo autoriza ceremonias cuando Régimen Interior, Gran Tesorería y Gran Hospitalaria han cumplido las validaciones requeridas;
- administra autorización/uso de templos y salas de Secretaría según disponibilidad;
- evita solapamientos de reservas;
- mantiene auditoría de documentos, autorizaciones y espacios.

### Muro Institucional de Gran Secretaría
Es el canal oficial dentro de la intranet para decretos, planchas oficiales, comunicados, circulares, resoluciones, llamados y avisos. Puede segmentar audiencia por grado, Taller, cargo/rol, órgano/comisión o hermano específico y puede exigir confirmación de lectura.

## 7. Ceremonias e insinuados
- Tipos iniciales: iniciación, aumento de salario y exaltación.
- Para autorizar una ceremonia el Taller debe estar al día con Gran Tesorería y con reposiciones de Gran Hospitalaria.
- Las validaciones quedan congeladas como evidencia para la autorización.
- En iniciaciones, el insinuado debe publicarse previamente durante un plazo configurable; valor inicial: 20 días.
- El insinuado no es miembro activo hasta que la iniciación quede registrada.
- Las excepciones críticas deben ser formales, justificadas y auditadas.

## 8. Ley 21.719 y privacidad
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

## 9. Dominios documentales y exclusión de CENDOC
**CENDOC no forma parte del Proyecto Milenio** como módulo, dependencia ni componente.

Los dominios documentales son distintos:
1. Gestión Documental Operativa.
2. Biblioteca Virtual.
3. Gran Archivo / Gran Archivero.

Comparten infraestructura segura de binarios cuando corresponde, pero mantienen responsabilidades, autorización y ciclos de vida separados.

## 10. Biblioteca Virtual
La Biblioteca Virtual publica y permite consultar material autorizado; no asume custodia archivística.

### Acceso acumulativo por grado
- grado mínimo 1 → puede acceder grado 1, 2, 3 o superior;
- grado mínimo 2 → puede acceder grado 2, 3 o superior;
- grado mínimo 3 → puede acceder grado 3 o superior.

Regla efectiva: `grado_actual >= grado_minimo_requerido`.

La regla debe aplicarse en backend a catálogo, búsqueda, filtros, facetas, detalle, miniaturas protegidas, descarga y acceso por ID directo. Un recurso no autorizado no debe aparecer por defecto en resultados, conteos ni facetas.

### Planchas de trabajo — decisión vigente
Las **planchas de trabajo de los hermanos no se transfieren al Gran Archivo por el solo hecho de ser planchas**.
- Cada hermano conserva sus planchas en su ficha/historial institucional.
- Cuando una plancha sea autorizada para publicación, se proyectará a la sección **Planchas de Trabajo** de la Biblioteca Virtual.
- La publicación se filtra por grado y demás permisos aplicables.
- Biblioteca conserva la referencia al registro de origen; no sustituye la historia del hermano.
- Sólo si una plancha adquiere además valor archivístico por una decisión institucional específica y una transferencia formal independiente podrá existir un registro archivístico derivado de esa decisión; no es el flujo por defecto.

## 11. Gran Archivo / Gran Archivero
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

### Seguridad del Gran Archivo
- Descubrir una ficha archivística y acceder al contenido son permisos distintos.
- Visibilidad y contenido pueden depender de grado, hermano, Taller, cargo/rol, órgano/comisión, finalidad, fecha de apertura, grant individual y vigencia.
- Investigaciones, amonestaciones, expedientes disciplinarios e informes reservados parten con política restrictiva.
- El Gran Archivero **no es superusuario universal**.
- Conocer un ID nunca permite eludir autorización.

## 12. Ventana Fraterna
Módulo comunitario interno, separado del Muro Institucional oficial. Categorías iniciales:
- trabajo/ofertas laborales;
- búsqueda de trabajo;
- ayuda e información;
- servicios;
- compraventa;
- libros;
- avisos comunitarios.

Debe incluir moderación, reportes, caducidad de publicaciones, auditoría y adjuntos seguros. En primera etapa no procesa pagos ni garantiza transacciones.

## 13. Notificaciones y calendario
- Notificaciones deben desacoplarse de módulos de negocio.
- Calendario institucional unificado debe proyectar eventos desde sus fuentes sin duplicar la fuente original.
- Debe integrar ceremonias, tenidas, decretos/avisos, reservas y recordatorios según permisos.

## 14. QA, instalación y operación
El proyecto debe poder desplegarse en un servidor QA reproducible mediante material versionado:
- `infrastructure/docker-compose.qa.yml`;
- `.env.qa.example`;
- `docs/PMGM-QA-001-despliegue-servidor-qa.md`;
- `docs/PMGM-QA-002-checklist-validacion.md`.

Principios QA:
- PostgreSQL, MinIO y ClamAV no se exponen innecesariamente al host;
- web detrás de HTTPS/reverse proxy;
- secretos fuera del repositorio;
- backups de PostgreSQL y Object Storage;
- procedimiento de actualización y rollback;
- datos sintéticos de prueba;
- evidencia de versión/commit desplegado;
- CI valida configuraciones dev y QA.

## 15. Seguridad documental
- Buckets privados.
- Object Keys opacos: sin RUT, nombre, correo, título, Taller ni clasificación sensible.
- No exponer ObjectKey, SHA, referencias de scan ni nombre físico en DTOs públicos de Biblioteca.
- Descarga sólo cuando la versión está `available`.
- Error del scanner nunca equivale a archivo limpio.
- No sobrescribir originales de preservación.
- Restricciones combinadas se consideran `AND` salvo política explícita distinta.
- La UI nunca será la frontera real de autorización.

## 16. Orden de ejecución vigente
1. Consolidar instrucciones y corregir contradicciones documentales.
2. Cerrar seguridad de Biblioteca Virtual por grado institucional vigente.
3. Probar grado 1/2/3, facetas y acceso directo por ID.
4. Completar workflow editorial/taxonomías de Biblioteca.
5. Implementar Gran Archivo: estructura, catálogo seguro, permisos y transferencias.
6. Gran Archivo: digitalización, preservación, consultas y préstamos.
7. Muro Institucional de Gran Secretaría.
8. Ventana Fraterna.
9. Notificaciones y calendario.
10. Proveedor OIDC/PKCE definitivo.
11. OpenAPI, Docencia e integraciones externas.
12. Hardening y operación productiva.

## 17. Regla de mantenimiento
Toda nueva instrucción aprobada debe:
1. actualizar el requisito afectado;
2. actualizar backlog/arquitectura si cambia alcance o diseño;
3. actualizar este documento si modifica una regla transversal;
4. agregar o ajustar pruebas cuando afecta seguridad o comportamiento;
5. conservar trazabilidad en GitHub.
