# AGENTS.md — Proyecto Centenario

Estas instrucciones son obligatorias para cualquier IA, agente de código, desarrollador o colaborador que continúe el **Proyecto Centenario / Modernización Gran Logia Mixta Milenio**.

## 1. Identidad del proyecto

- Existe **un solo Proyecto Centenario y un solo desarrollo continuo**.
- Repositorio oficial: `pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio`.
- Rama de referencia vigente: `main`.
- Google Drive oficial: carpeta `Proyecto Centenario`, ID `1P74Q8lhNPu6lHZ5zZR9ZD3AJFr_EtZyO`.
- Línea Base Maestra vigente: `LB-PC-2026-09-17`, Google Doc ID `1ncl0d--Bny8PzvtP38muPo5H2-JM5QUDGqH3lJ5ZtPM`.
- Sponsor / Product Owner: Pablo Domínguez.

Nombres técnicos como `Centenario.Authorization`, `RBAC Taller`, una carpeta, librería, servicio o demo son **componentes internos del mismo proyecto** y no autorizan crear proyectos, repositorios o arquitecturas paralelas.

## 2. Regla de arranque obligatoria

Antes de analizar, diseñar, programar, corregir, generar una demo o desplegar:

1. Obtener el HEAD actual de `main`.
2. Inspeccionar el árbol y el código realmente existente en `main`.
3. Leer `README.md`.
4. Leer `docs/REGLAS-CONTINUIDAD-Y-NO-REGRESION.md`.
5. Leer `docs/CONTROL-DE-CAMBIOS.md`.
6. Leer `docs/LINEA-BASE-PROYECTO-CENTENARIO-2026-09-17.md`.
7. Leer los ADR, migraciones, pruebas y configuraciones relacionados con la tarea.
8. Revisar en Google Drive la Línea Base Maestra y los documentos oficiales vigentes que afecten la tarea.
9. Comparar GitHub y Drive. Si existe una contradicción material, resolverla antes de cambiar el código afectado.
10. Recién entonces continuar el desarrollo.

**Nunca reconstruir el proyecto desde un chat, resumen, prompt antiguo, prototipo o memoria de una IA si existe una versión posterior en GitHub o Drive.**

## 3. Orden de prevalencia

Ante información incompatible, aplicar este orden:

1. Normativa institucional vigente y documentos oficiales aplicables.
2. Línea Base Maestra vigente y cambios formalmente aprobados.
3. Código actual de GitHub `main`, ADR y documentación técnica asociada.
4. Otros documentos oficiales vigentes de Google Drive.
5. Conversaciones, chats, resúmenes o memoria de una IA, solo como antecedente histórico.

La normativa puede exigir modificar código o línea base, pero el cambio debe documentarse y versionarse.

## 4. Regla de no regresión

No eliminar, degradar, sobrescribir ni reemplazar funcionalidades, acuerdos, modelos de datos, perfiles, permisos, flujos, migraciones, pruebas, documentación o código aprobados/versionados sin una decisión explícita registrada.

Antes de modificar una pieza existente:

- comprobar su versión actual;
- identificar dependencias;
- preservar las funcionalidades no afectadas por el cambio;
- registrar motivo e impacto si el cambio reemplaza una decisión anterior;
- actualizar pruebas y documentación relacionadas.

Está prohibido sustituir código existente por un ejemplo, mock, esqueleto o implementación simplificada solo porque la IA no haya leído el estado actual.

## 5. Un solo desarrollo con dos salidas obligatorias

Cada incremento funcional relevante debe mantener esta línea:

`Requisito aprobado → Código en main → Demo GitHub Pages → QA instalable → Prueba QA/UAT`

### Demo GitHub Pages

- Es la superficie navegable/demostrativa del mismo sistema.
- Usa datos ficticios o controlados.
- Debe reflejar los mismos perfiles, vistas, flujos y reglas aprobados.
- Toda función simulada debe identificarse como simulada.
- No se considera operacional una función solo porque aparezca en Pages.

### QA operacional

- Servidor: `srv01`.
- Ruta del repositorio: `/opt/centenario/app`.
- Ubuntu 26.04.1 LTS, 8 vCPU, ~8 GB RAM.
- Docker 29.1.3 y Docker Compose 2.40.3.
- La versión QA debe ser reproducible desde el repositorio oficial.
- QA valida backend, PostgreSQL, autenticación, permisos, persistencia, auditoría, archivos, correo e integraciones reales según el avance.

La demo y QA **no pueden evolucionar como productos separados**.

## 6. Arquitectura base vigente

No cambiar esta arquitectura sin ADR y aprobación:

- Backend: ASP.NET Core.
- Frontend: React o Blazor, aún sujeto a ADR definitivo si no existe uno posterior.
- Base de datos: PostgreSQL.
- Identidad: OpenID Connect / SSO.
- Contenedores: Docker.
- Proxy/publicación: Nginx o equivalente.
- TLS obligatorio.
- CI/CD: GitHub Actions.

Antes de decidir tecnología, buscar siempre un ADR posterior que haya resuelto una decisión pendiente.

## 7. Estado técnico conocido de referencia

Checkpoint conocido al redactar este archivo: `main` estaba en `c7cfa0dc0cc30b8adc0fe99ae90e42a101d4aeec`.

**Este SHA es solo un checkpoint histórico. Nunca debe usarse como sustituto de consultar el HEAD actual de `main`.**

Ya existe código funcional versionado para autorización normativa de Taller:

- `backend/Centenario.Authorization/`
- `backend/Centenario.Authorization.SmokeTests/`
- `database/001_rbac_taller.sql`
- `adrs/ADR-001-RBAC-NORMATIVO-TALLER.md`
- `docs/MATRIZ-FUNCIONAL-NORMATIVA-TALLER-v1.0.md`

No recrear estas piezas desde cero. Extenderlas o modificarlas desde su estado vigente.

## 8. Reglas funcionales mínimas que no deben perderse

- Multi-Taller con segregación de datos por Taller.
- Perfiles base de Taller: Venerable Maestro, Inmediato Ex-Venerable Maestro, Primer Vigilante, Segundo Vigilante, Orador/a, Secretario/a, Tesorero/a y Hospitalario/a.
- Subrogación: Venerable → Inmediato Ex-Venerable → Primer Vigilante → Segundo Vigilante.
- Docencia: Segundo Vigilante → Aprendices; Primer Vigilante → Compañeros; Inmediato Ex-Venerable → Maestros.
- Consejo de Administración como órgano colegiado.
- Tesorería y Hospitalaria separadas.
- CRV/CRF con Venerable + Tesorero + Orador + Secretario como firmantes funcionales.
- Historial longitudinal del Hermano y preservación de historial al trasladarse entre Talleres.
- Insinuaciones con expediente único/versionado, publicación mínima vigente de 20 días y mínimo 3 entrevistas, ambos parametrizables.
- Ceremonias con validaciones de Régimen Interior, Gran Tesorería, Gran Hospitalaria y Gran Maestría según corresponda; Gran Secretaría emite la plancha.
- Ley 21.719: privacidad desde el diseño, minimización, control de acceso, trazabilidad y protección reforzada de datos sensibles.
- Auditoría mínima: usuario, fecha/hora, IP, Taller/ámbito, menú, submenú, acción, entidad, ID y resultado.
- Menú Sistema: usuarios, perfiles, vistas, permisos, parámetros, flujos, catálogos, logos/colores, SMTP, notificaciones, backup y restauración.
- CENDOC como módulo único está fuera del alcance; Biblioteca Virtual y Gran Archivero son componentes separados.

La definición detallada debe leerse siempre en la Línea Base y documentos específicos antes de programar.

## 9. Reglas normativas de perfiles

Toda vista o acción de un cargo del Taller debe justificarse por una de estas categorías:

1. responsabilidad normativa directa;
2. protocolo/formulario institucional vigente;
3. control operativo aprobado para seguridad, trazabilidad o segregación de funciones.

Un control del software no crea una atribución masónica nueva.

Revisar siempre:

- `docs/PERFILES-TALLER-Y-FIRMAS.md`
- `docs/MATRIZ-FUNCIONAL-NORMATIVA-TALLER-v1.0.md`
- Constitución y Reglamento vigente en Drive.

## 10. Flujo obligatorio de implementación

Para cada cambio:

1. Identificar requisito y fuente.
2. Verificar si ya existe implementación relacionada.
3. Determinar impacto en datos, perfiles, vistas, permisos, flujos, API, frontend, Pages, QA, pruebas y documentación.
4. Implementar extendiendo lo existente.
5. Agregar/actualizar migraciones si corresponde.
6. Agregar/actualizar pruebas.
7. Actualizar demo GitHub Pages cuando el cambio sea visible o demostrable.
8. Actualizar instalación/configuración QA cuando el cambio requiera operación real.
9. Actualizar documentación técnica y funcional.
10. Registrar el cambio en `docs/CONTROL-DE-CAMBIOS.md` cuando corresponda.
11. Consolidar el acuerdo también en Google Drive en la misma iteración.
12. Verificar que no existan regresiones.

## 11. Definición de terminado

No declarar una funcionalidad terminada sin distinguir su estado:

- Definida/documentada.
- Implementada en código.
- Visible en demo GitHub Pages.
- Integrada en QA.
- Probada en QA/UAT.

La palabra **operacional** se reserva para funciones integradas y verificadas en QA, no solo mostradas en una demo.

## 12. Qué hacer si falta acceso

Si una IA no tiene acceso a Google Drive, debe:

- usar la documentación consolidada de GitHub como referencia técnica provisional;
- declarar que no pudo validar los documentos oficiales de Drive;
- no inventar ni reinterpretar normativa faltante;
- evitar cambios irreversibles en áreas normativas hasta que pueda verificarse la fuente oficial.

Si no tiene acceso al repositorio o no puede leer `main`, no debe reconstruir código desde chats.

## 13. Formato de handoff al terminar una sesión

Toda IA/desarrollador debe dejar un resumen de continuidad con:

- HEAD/commit final de `main` trabajado;
- archivos creados/modificados;
- funcionalidades implementadas;
- migraciones creadas/aplicadas;
- pruebas ejecutadas y resultado;
- estado de GitHub Pages;
- estado de QA `srv01`;
- documentación GitHub actualizada;
- documentación Drive actualizada;
- pendientes concretos y siguiente paso recomendado;
- cualquier discrepancia o riesgo abierto.

El handoff debe describir hechos verificables, no promesas de trabajo futuro.

## 14. Documentos obligatorios de lectura

Como mínimo:

- `README.md`
- `AGENTS.md`
- `docs/LINEA-BASE-PROYECTO-CENTENARIO-2026-09-17.md`
- `docs/REGLAS-CONTINUIDAD-Y-NO-REGRESION.md`
- `docs/CONTROL-DE-CAMBIOS.md`
- `docs/ESTRATEGIA-ENTREGAS-DEMO-Y-QA.md`
- `docs/PERFILES-TALLER-Y-FIRMAS.md`
- `docs/MATRIZ-FUNCIONAL-NORMATIVA-TALLER-v1.0.md`
- ADR relacionados con la tarea.

## 15. Regla final

**Continuar siempre el Proyecto Centenario existente. No empezar otro proyecto, no reconstruir desde memoria, no perder código vigente, no separar demo y QA, y no declarar como real lo que solo está simulado.**
