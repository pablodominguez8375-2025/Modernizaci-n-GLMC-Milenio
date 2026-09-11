# PMGM-UAT-VISUAL-001 · Aceptación visual institucional

**Estado:** Preparado para UAT — pendiente de decisión del Product Owner  
**Objetivo:** registrar la aceptación visual y responsiva del frontend de Proyecto Centenario antes de integrar la versión aprobada a `dev`.

## 1. Identificación de la versión a revisar

- Pull Request: `#62 · UI: alinear frontend con mockups PPT y reforzar responsividad`.
- Rama: `feat/ui-ppt-responsive`.
- SHA de código visual candidato: `0112fd86f21f4f6c3be6036cc45e002594d8729f`.
- Gates técnicos exact-head: `PMGM CI #847` y `PMGM Showcase Demo #171`, ambos exitosos.
- Paquete instalable exact-head: `Proyecto Centenario Pre-UAT Installable #146`, exitoso.
- Artefacto visual: `pmgm-responsive-visual-evidence`, 21 PNG.
- Digest evidencia visual: `sha256:48755eddc5660247dec2ca1366b08808c703c058f987e3e17696198fd83ec4d7`.
- Artefacto instalable: `Proyecto-Centenario-preUAT-installable`.
- Digest artefacto instalable: `sha256:dee074fbad3b22b100ff96b1c653ba96247db93c782d76bc4135ba91f0ecbb87`.
- Fuente visual contractual: `Proyecto_Milenio_Mapa_QA_Mockups.pptx` y `Proyecto_Milenio_QA_Ejecutivo_v0_22_actualizado.pptx`.

> La aceptación visual se aplica al SHA de código visual identificado arriba. Los commits posteriores que modifiquen únicamente actas o documentación de aprobación no cambian por sí solos el candidato visual. Cualquier modificación posterior de frontend, CSS, iconografía o automatización que altere la evidencia requiere nueva validación y actualización de este SHA.

## 2. Criterios generales obligatorios

Para cada pantalla se debe verificar:

- [ ] Identidad azul profundo / dorado coherente con las PPT.
- [ ] Tipografía y jerarquía visual institucional coherentes.
- [ ] Iconografía SVG homogénea; sin emojis o glifos de sistema usados como iconos de producto.
- [ ] Navegación clara y consistente con el rol/perfil.
- [ ] Sin desplazamiento horizontal global.
- [ ] Sin textos, tarjetas, campos o botones superpuestos.
- [ ] Controles táctiles utilizables en móvil.
- [ ] Tablas anchas contenidas dentro de su propia zona de scroll cuando corresponda.
- [ ] Estados funcionales identificables sin depender sólo del color.
- [ ] Contenido principal visible sin que la navegación móvil consuma la pantalla completa.
- [ ] Demo identificada inequívocamente: etiqueta completa + SHA en escritorio y forma compacta `Demo · <SHA>` en móvil.
- [ ] Biblioteca Virtual accesible de forma visible desde Mi ficha y con alcance acumulativo por grado: 1° = General+1°; 2° = General+1°+2°; 3° = General+1°+2°+3°/todo lo autorizado.
- [ ] Apariencia suficientemente fiel a la PPT como para reconocer la misma línea institucional.

## 3. Pantallas representativas de aceptación

| ID | Pantalla | Perfil QA | Móvil 390×844 | Escritorio 1440×900 | Resultado |
|---|---|---|---|---|---|
| UV-01 | Inicio / Dashboard | Hermano | [ ] | [ ] | Pendiente |
| UV-02 | Mi ficha | Hermano | [ ] | [ ] | Pendiente |
| UV-03 | Biblioteca Virtual | Hermano | [ ] | [ ] | Pendiente |
| UV-04 | Gestión Logial | Autoridad Gran Logia | [ ] | [ ] | Pendiente |
| UV-05 | Gran Tesorería | Autoridad Gran Logia | [ ] | [ ] | Pendiente |
| UV-06 | Gran Hospitalaria | Autoridad Gran Logia | [ ] | [ ] | Pendiente |
| UV-07 | Gran Secretaría | Autoridad Gran Logia | [ ] | [ ] | Pendiente |
| UV-08 | Gran Archivero | Autoridad Gran Logia | [ ] | [ ] | Pendiente |

## 4. Matriz responsiva de Mi ficha

La vista `Mi ficha` actúa como pantalla patrón para validar todos los tamaños definidos en PMGM-UI-001.

| Resolución | Tipo | Aprobado | Observación |
|---|---|---|---|
| 360×800 | celular pequeño | [ ] | |
| 390×844 | celular típico | [ ] | |
| 768×1024 | tablet vertical | [ ] | |
| 1024×768 | tablet horizontal / notebook pequeño | [ ] | |
| 1366×768 | notebook estándar | [ ] | |
| 1440×900 | escritorio | [ ] | |
| 1920×1080 | Full HD | [ ] | |

## 5. Hallazgos técnicos ya corregidos antes del UAT

1. **Solapamiento en Mi ficha** a 768×1024 y 1024×768. Se corrigió la composición y las capturas posteriores confirmaron separación correcta entre ficha personal e institucional.
2. **Navegación móvil demasiado alta** para perfiles con muchos módulos. Se compactó la navegación a una zona de altura controlada y scroll vertical propio; el contenido de Gestión Logial, Tesorería, Hospitalaria, Secretaría y Gran Archivero queda visible inmediatamente debajo.
3. **Identificación de versión en Showcase de PR.** El build inicialmente mostraba el SHA del merge temporal creado por GitHub. Se corrigió para mostrar el SHA real del head del PR y mantener el SHA real de `dev` en publicaciones por push.
4. **Identificación móvil de la demo.** El rótulo completo se compactó a `Demo · <SHA>` en pantallas estrechas para conservar trazabilidad sin saturar la cabecera.
5. **Regresión del paquete instalable entre ramas.** El candidato visual absorbió el instalador guiado, checklist, manifest portable y documentación de #61.
6. **UV-OBS-001 — Biblioteca Virtual por grado poco visible en Mi ficha.** La maqueta establece Biblioteca como acceso principal y la regla acumulativa `1°→1°`, `2°→1°+2°`, `3°→1°+2°+3°`. El acceso fue movido a la tarjeta de Información institucional, inmediatamente bajo el grado, se reemplazó el glifo anterior por icono SVG institucional y se muestra el alcance acumulativo vigente. El backend conserva el filtrado real por grado efectivo para catálogo, facetas, detalle y descarga. Evidencia #171 revisada en móvil, tablet y escritorio.

Estos puntos deben volver a revisarse como casos de regresión dentro del UAT.

## 6. Registro de observaciones

Cada observación debe registrar al menos:

| Campo | Contenido |
|---|---|
| ID | `UV-OBS-XXX` |
| Pantalla | módulo afectado |
| Resolución | ancho × alto |
| Severidad | Bloqueante / Alta / Media / Baja |
| Descripción | qué se desvía del mockup o de la usabilidad esperada |
| Evidencia | nombre de captura o referencia |
| Decisión | corregir antes de aprobar / aceptar / backlog |
| Responsable | asignado |
| Estado | abierto / corregido / aceptado |

Una observación **Bloqueante** o **Alta** impide fijar baseline hasta que exista decisión explícita del Product Owner.

## 7. Resultado de UAT visual

Seleccionar una sola opción:

- [ ] **APROBADO** — puede fijarse como baseline visual institucional e integrarse a `dev`.
- [ ] **APROBADO CON OBSERVACIONES** — puede integrarse sólo con observaciones aceptadas y registradas.
- [ ] **RECHAZADO** — requiere correcciones y nueva evidencia antes de integrar.

### Aprobación

- Product Owner / Sponsor: ______________________________
- Fecha: ______________________________
- SHA de código visual aprobado: ______________________________
- Evidencia / run de Showcase: ______________________________
- Observaciones aceptadas: ______________________________

## 8. Acciones posteriores a la aprobación

1. Registrar el SHA aprobado en `PMGM-UI-BASELINE-001.md`.
2. Integrar PR #62 a `dev` sólo si el código visual aprobado mantiene sus gates técnicos verdes y no existen cambios visuales posteriores sin revisar.
3. Esperar el despliegue exitoso de GitHub Pages desde `dev`.
4. Validar la URL oficial de testing: `https://pablodominguez8375-2025.github.io/Modernizaci-n-GLMC-Milenio/`.
5. Confirmar que el SHA corto visible corresponde a la versión integrada.
6. Generar nuevamente evidencia del SHA ya integrado.
7. Utilizar la baseline aprobada como referencia de regresión para desarrollos posteriores y para el frontend de la VM institucional.

## 9. Regla de gobierno

**CI verde significa técnicamente validado; UAT visual aprobado significa institucionalmente aceptado. Son estados distintos y ambos son necesarios para fijar una baseline.**
