# PMGM-UAT-VISUAL-001 · Aceptación visual institucional

**Estado:** Preparado para UAT  
**Objetivo:** registrar la aceptación visual y responsiva del frontend de Proyecto Centenario antes de integrar la versión aprobada a `dev`.

## 1. Identificación de la versión a revisar

- Pull Request: `#62 · UI: alinear frontend con mockups PPT y reforzar responsividad`.
- Rama: `feat/ui-ppt-responsive`.
- SHA técnico validado antes de apertura formal de UAT: `46e786ae0ef6a7af200b9c093b9c62107607769f`.
- Gates técnicos de referencia: `PMGM CI #827` y `PMGM Showcase Demo #151`, ambos exitosos.
- Artefacto esperado: `pmgm-responsive-visual-evidence`.
- Fuente visual contractual: `Proyecto_Milenio_Mapa_QA_Mockups.pptx` y `Proyecto_Milenio_QA_Ejecutivo_v0_22_actualizado.pptx`.

> Si el SHA cambia antes de la aprobación, esta identificación debe actualizarse y los gates deben volver a quedar verdes. La aceptación visual se aplica al SHA exacto revisado, no al nombre de la rama.

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
- SHA aprobado: ______________________________
- Evidencia / run de Showcase: ______________________________
- Observaciones aceptadas: ______________________________

## 8. Acciones posteriores a la aprobación

1. Registrar el SHA aprobado en `PMGM-UI-BASELINE-001.md`.
2. Integrar PR #62 a `dev` sólo si el SHA aprobado continúa con CI y Showcase verdes.
3. Esperar el despliegue exitoso de GitHub Pages desde `dev`.
4. Validar la URL oficial de testing: `https://pablodominguez8375-2025.github.io/Modernizaci-n-GLMC-Milenio/`.
5. Generar nuevamente evidencia del SHA ya integrado.
6. Utilizar el baseline aprobado como referencia de regresión para desarrollos posteriores.

## 9. Regla de gobierno

**CI verde significa técnicamente validado; UAT visual aprobado significa institucionalmente aceptado. Son estados distintos y ambos son necesarios para fijar una baseline.**
