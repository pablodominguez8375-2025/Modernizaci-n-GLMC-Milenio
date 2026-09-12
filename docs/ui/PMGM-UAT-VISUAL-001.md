# PMGM-UAT-VISUAL-001 · Aceptación visual institucional

**Estado:** APROBADO por Product Owner
**Objetivo:** registrar la aceptación visual y responsiva del frontend de Proyecto Centenario antes de integrar la versión aprobada a `dev`.

## 1. Identificación de la versión a revisar

- Pull Request: `#62 · UI: alinear frontend con mockups PPT y reforzar responsividad`.
- Rama: `feat/ui-ppt-responsive`.
- SHA de código visual aprobado: `3f964e9764acdcc07b12ed626ca8f8aa005b9a4b`.
- SHA de validación aprobado: `a648ae6fab5180122462da11ccfd020818274196`.
- Gates técnicos exact-head: `PMGM CI #869` y `PMGM Showcase Demo #193`, ambos exitosos.
- Paquete instalable exact-head: `Proyecto Centenario Pre-UAT Installable #168`, exitoso.
- Artefacto visual: `pmgm-responsive-visual-evidence`, 21 PNG.
- Fuente visual contractual: `Proyecto_Milenio_Mapa_QA_Mockups.pptx` y `Proyecto_Milenio_QA_Ejecutivo_v0_22_actualizado.pptx`.

> La aceptación visual se aplica al SHA de código visual identificado para cada componente. Los commits posteriores que modifiquen únicamente actas o documentación de aprobación no cambian por sí solos el candidato visual. Cualquier modificación posterior de frontend, CSS, iconografía, lógica de catálogo o automatización que altere la evidencia requiere nueva validación y actualización del SHA correspondiente.

## 2. Criterios generales obligatorios

Para cada pantalla se debe verificar:

- [x] Identidad azul profundo / dorado coherente con las PPT.
- [x] Tipografía y jerarquía visual institucional coherentes.
- [x] Iconografía SVG homogénea; sin emojis o glifos de sistema usados como iconos de producto.
- [x] Navegación clara y consistente con el rol/perfil.
- [x] Sin desplazamiento horizontal global.
- [x] Sin textos, tarjetas, campos o botones superpuestos.
- [x] Controles táctiles utilizables en móvil.
- [x] Tablas anchas contenidas dentro de su propia zona de scroll cuando corresponda.
- [x] Estados funcionales identificables sin depender sólo del color.
- [x] Contenido principal visible sin que la navegación móvil consuma la pantalla completa.
- [ ] Demo identificada inequívocamente: etiqueta completa + SHA en escritorio y forma compacta `Demo · <SHA>` en móvil.
- [x] Biblioteca Virtual accesible de forma visible desde Mi ficha y con alcance acumulativo por grado: 1° = General+1°; 2° = General+1°+2°; 3° = General+1°+2°+3°/todo lo autorizado. **APROBADO por Product Owner el 2026-09-11 para el candidato visual `0112fd86...`.**
- [x] Apariencia suficientemente fiel a la PPT como para reconocer la misma línea institucional.

## 3. Pantallas representativas de aceptación

| ID | Pantalla | Perfil QA | Móvil 390×844 | Escritorio 1440×900 | Resultado |
|---|---|---|---|---|---|
| UV-01 | Inicio / Dashboard | Hermano | [x] | [x] | Aprobado |
| UV-02 | Mi ficha | Hermano | [x] | [x] | Aprobado |
| UV-03 | Biblioteca Virtual | Hermano | [x] | [x] | Aprobado |
| UV-04 | Gestión Logial | Autoridad Gran Logia | [x] | [x] | Aprobado |
| UV-05 | Gran Tesorería | Autoridad Gran Logia | [x] | [x] | Aprobado |
| UV-06 | Gran Hospitalaria | Autoridad Gran Logia | [x] | [x] | Aprobado |
| UV-07 | Gran Secretaría | Autoridad Gran Logia | [x] | [x] | Aprobado |
| UV-08 | Gran Archivero | Autoridad Gran Logia | [x] | [x] | Aprobado |

`[x] técnico` significa que la evidencia automática está validada, pero todavía falta la aceptación institucional explícita del Product Owner.

## 4. Matriz responsiva de Mi ficha

La vista `Mi ficha` actúa como pantalla patrón para validar todos los tamaños definidos en PMGM-UI-001.

| Resolución | Tipo | Aprobado | Observación |
|---|---|---|---|
| 360×800 | celular pequeño | [ ] | |
| 390×844 | celular típico | [ ] | Acceso Biblioteca aceptado; resto de la pantalla pendiente |
| 768×1024 | tablet vertical | [ ] | Acceso Biblioteca aceptado; resto de la pantalla pendiente |
| 1024×768 | tablet horizontal / notebook pequeño | [ ] | |
| 1366×768 | notebook estándar | [ ] | |
| 1440×900 | escritorio | [ ] | Acceso Biblioteca aceptado; resto de la pantalla pendiente |
| 1920×1080 | Full HD | [ ] | |

## 5. Hallazgos técnicos ya corregidos antes/durante el UAT

1. **Solapamiento en Mi ficha** a 768×1024 y 1024×768. Se corrigió la composición y las capturas posteriores confirmaron separación correcta entre ficha personal e institucional.
2. **Navegación móvil demasiado alta** para perfiles con muchos módulos. Se compactó la navegación a una zona de altura controlada y scroll vertical propio; el contenido de Gestión Logial, Tesorería, Hospitalaria, Secretaría y Gran Archivero queda visible inmediatamente debajo.
3. **Identificación de versión en Showcase de PR.** Se corrigió para mostrar el SHA real del head del PR y no el merge temporal de GitHub.
4. **Identificación móvil de la demo.** El rótulo completo se compactó a `Demo · <SHA>` en pantallas estrechas para conservar trazabilidad sin saturar la cabecera.
5. **Regresión del paquete instalable entre ramas.** El candidato visual absorbió el instalador guiado, checklist, manifest portable y documentación de #61.
6. **UV-OBS-001 — Biblioteca Virtual por grado poco visible en Mi ficha.** El acceso fue llevado a la zona institucional, se usa icono SVG y se refleja el alcance acumulativo. El backend conserva filtrado real por grado efectivo para catálogo, facetas, detalle y descarga. **Estado UAT: ACEPTADO por Product Owner el 2026-09-11 para `0112fd86f21f4f6c3be6036cc45e002594d8729f`.**
7. **UV-OBS-002 — Biblioteca Virtual completa.** Se incorporó catálogo semántico real por tipo de publicación, filtros/búsqueda por metadatos, migración PostgreSQL y endpoint de administración. El showcase usa nueve publicaciones exclusivamente ficticias para representar el catálogo. La evidencia anterior expuso overflow horizontal en 390×844; fue corregido y existe gate automático contra overflow global en los viewports móviles. **Estado UAT: PROGRAMADO Y VALIDADO en `dc965d7de0ea8c513f572cb456e1e751af765051`; pendiente de aceptación del Product Owner.**

Estos puntos deben volver a revisarse como casos de regresión en versiones posteriores que modifiquen materialmente el componente afectado.

## 6. Registro de observaciones y aprobaciones parciales

Cada observación debe registrar al menos ID, pantalla, resolución, severidad, descripción, evidencia, decisión, responsable y estado. Una observación Bloqueante o Alta impide fijar baseline hasta que exista decisión explícita del Product Owner.

### 6.1 UV-OBS-001 · Aprobación parcial registrada

- Componente: `Mi ficha → acceso a Biblioteca Virtual por grado`.
- Decisión: **APROBADO / ACEPTADO**.
- Product Owner / Sponsor: Pablo Domínguez.
- Fecha: `2026-09-11`.
- SHA visual aprobado: `0112fd86f21f4f6c3be6036cc45e002594d8729f`.
- Regla funcional aceptada: `1°→1°`, `2°→1°+2°`, `3°→1°+2°+3°`, además del contenido general autorizado según política de acceso.
- Referencia visual: tarjeta destacada `SEGÚN TU GRADO / Biblioteca Virtual / Abrir Biblioteca` de la maqueta revisada durante UAT.
- Alcance: esta aprobación **no equivale a aprobar toda Mi ficha, toda Biblioteca Virtual ni el UAT visual completo**.
- Regla de versiones: si una versión posterior cambia diseño, posición, icono, texto, navegación, responsividad o lógica de acceso de este componente, deberá ejecutarse **una nueva revisión y aprobación**.

### 6.2 UV-OBS-002 · Biblioteca Virtual completa

- Estado: **PROGRAMADO / VALIDADO / EMPAQUETADO · PENDIENTE DE APROBACIÓN**.
- SHA funcional/visual a revisar: `dc965d7de0ea8c513f572cb456e1e751af765051`.
- Evidencia: Showcase #192, 21 PNG; revisar especialmente `biblioteca-390x844.png` y `biblioteca-1440x900.png`.
- Gates: CI #868 ✅ · Showcase #192 ✅ · Pre-UAT Installable #167 ✅.
- Paquete #167 verificado fuera del runner: checksum externo OK, `MANIFEST.sha256` OK con 489 entradas, `INSTALAR.sh --help` OK y `BUILD-INFO.txt` con `SOURCE_SHA=dc965d7...`.
- Seguridad: la presentación visual no reemplaza ni relaja el filtrado backend acumulativo por grado, detalle y descarga.

#### Contrato de catalogación

**Plancha de Trabajo**
- Es un trabajo masónico realizado por un hermano.
- Metadatos: título, hermano autor, Taller, fecha, grado y **descripción corta**.
- **No utiliza abstract.** El backend rechaza una plancha que intente guardar abstract.

**Libro**
- Metadatos: título, autor, tema, grado, edición y **abstract**.
- Puede ser material General cuando corresponda a la política de acceso.

**Documento Oficial**
- Comprende, entre otros, Reglamentos, Constituciones y Rituales.
- Se clasifica por tipo de documento y grado aplicable; puede existir material General cuando corresponda.

**Video**
- Categoría y metadatos preparados para una versión futura.
- Este candidato no declara habilitada todavía la reproducción/publicación audiovisual.

#### Búsqueda y filtros

El catálogo puede buscar por título, hermano/autor, Taller, tema, edición, descripción corta, abstract y tipo de documento oficial. Existen filtros por categoría, grado, tema de libro y tipo de documento oficial. El backend aplica primero la autorización acumulativa por grado y sólo después los filtros de consulta.

#### Regresión automatizada

La prueba `LibraryCatalogMetadataHttpTests` fija como contrato que:
- una Plancha de Trabajo no acepta abstract;
- una plancha conserva descripción corta, hermano autor, Taller y grado;
- un libro expone/busca su abstract, tema, autor y edición;
- los documentos oficiales pueden localizarse por tipo;
- las nuevas facetas de tema/tipo oficial son consultables.

## 7. Resultado de UAT visual

Seleccionar una sola opción al cierre global:

- [x] **APROBADO** — puede fijarse como baseline visual institucional e integrarse a `dev`.
- [ ] **APROBADO CON OBSERVACIONES** — puede integrarse sólo con observaciones aceptadas y registradas.
- [ ] **RECHAZADO** — requiere correcciones y nueva evidencia antes de integrar.

**Estado global actual:** APROBADO por el Product Owner el 2026-09-11. Puede fijarse la baseline visual e integrarse el PR #62 a `dev`.

### Aprobación global

- Product Owner / Sponsor: Pablo Domínguez.
- Fecha: `2026-09-11`.
- SHA de código visual aprobado: `3f964e9764acdcc07b12ed626ca8f8aa005b9a4b`.
- SHA de validación aprobado: `a648ae6fab5180122462da11ccfd020818274196`.
- Evidencia / run de Showcase: `PMGM Showcase Demo #193`; 21 PNG; digest `sha256:4831db547fe98e0c37066ac66e8d2d1c702fb5e9aa9e3de5cb580ee0ccd80e95`.
- Observaciones aceptadas: `UV-OBS-001` y `UV-OBS-002`, cerradas mediante aprobación global.

## 8. Acciones posteriores a la aprobación global

1. Registrar el SHA aprobado en `PMGM-UI-BASELINE-001.md`.
2. Integrar PR #62 a `dev` sólo si el código visual aprobado mantiene gates técnicos verdes y no existen cambios visuales posteriores sin revisar.
3. Esperar el despliegue exitoso de GitHub Pages desde `dev`.
4. Validar la URL oficial de testing: `https://pablodominguez8375-2025.github.io/Modernizaci-n-GLMC-Milenio/`.
5. Confirmar que el SHA corto visible corresponde a la versión integrada.
6. Generar nuevamente evidencia del SHA ya integrado.
7. Utilizar la baseline aprobada como referencia de regresión para desarrollos posteriores y para el frontend de la VM institucional.

## 9. Regla de gobierno

**CI verde significa técnicamente validado; UAT visual aprobado significa institucionalmente aceptado. Son estados distintos y ambos son necesarios para fijar una baseline.**

Las aprobaciones pueden registrarse por componente durante el UAT. Cada aprobación queda ligada al SHA visual revisado y debe repetirse si una versión posterior modifica materialmente ese componente.
