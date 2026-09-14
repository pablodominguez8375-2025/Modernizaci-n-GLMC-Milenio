# PMGM-UI-001 · Identidad visual y diseño responsivo

## Estado
Aprobado como requisito obligatorio por Product Owner.

## Fuente visual de verdad
Las presentaciones ejecutivas y mockups funcionales entregados para Proyecto Milenio / Proyecto Centenario constituyen la referencia visual de la interfaz. El frontend no debe limitarse a compartir colores generales: debe mantener una identidad reconocible y consistente con esas piezas.

Referencias principales:
- `Proyecto_Milenio_Mapa_QA_Mockups.pptx`
- `Proyecto_Milenio_QA_Ejecutivo_v0_22_actualizado.pptx`

## Principios visuales obligatorios

1. **Paleta institucional**
   - Azul profundo para navegación y encabezados: `#102c54` / `#0d2749`.
   - Azul secundario: `#173b6e`.
   - Dorado institucional para selección, énfasis y acentos: `#b48a37` / `#c79d45`.
   - Fondos claros: `#f3f6f9`, blanco y grises suaves.
   - Verde, rojo y azul informativo sólo para estados y semántica funcional.

2. **Tipografía**
   - Interfaz y lectura operativa: Aptos/Inter/system sans-serif.
   - Uso serif reservado para citas, elementos ceremoniales o títulos donde el mockup lo requiera.
   - No utilizar tipografías decorativas que rompan la sobriedad institucional.

3. **Iconografía**
   - Producción usa iconos SVG lineales, consistentes en tamaño, trazo y estilo.
   - Objetivo: 20–24 px, trazo visual homogéneo, color heredado del contexto.
   - La navegación principal, el Dashboard y los avisos utilizan el componente reutilizable `InstitutionalIcon` cuando corresponde.
   - No usar emojis ni glifos Unicode dependientes del sistema operativo como iconografía de producto.

4. **Navegación**
   - Escritorio: navegación lateral azul oscuro, activa en dorado.
   - Tablet: navegación compacta en rejilla de tres columnas, sin desplazamiento horizontal de la página.
   - Móvil: navegación táctil en rejilla de dos columnas dentro de una zona de altura acotada y desplazamiento vertical propio cuando la cantidad de módulos lo requiera.
   - El contenido principal debe ser visible inmediatamente debajo de la navegación en pantallas pequeñas, incluso para perfiles con muchos módulos como Gran Logia.
   - Ningún módulo debe quedar inaccesible por depender de arrastre horizontal.

5. **Tarjetas y paneles**
   - Superficies blancas, bordes finos, sombras suaves y radios moderados.
   - Jerarquía por espacio, tipografía y acento; evitar exceso de color.

6. **Tablas y formularios**
   - Deben permanecer utilizables en ancho reducido.
   - En móvil, las tablas pueden usar desplazamiento horizontal interno; nunca debe desplazarse horizontalmente toda la página.
   - Controles táctiles con altura mínima aproximada de 44 px.

## Breakpoints de aceptación

La interfaz debe validarse al menos en:
- 360 × 800 px — celular pequeño.
- 390 × 844 px — celular típico.
- 768 × 1024 px — tablet vertical.
- 1024 × 768 px — tablet horizontal / notebook pequeño.
- 1366 × 768 px — notebook estándar.
- 1440 × 900 px — escritorio.
- 1920 × 1080 px — escritorio Full HD.

## Criterios de aceptación visual

Una pantalla se considera aprobable sólo si:
- conserva la paleta, jerarquía y lenguaje visual de los mockups;
- no presenta scroll horizontal global;
- navegación y acciones principales siguen accesibles en móvil;
- el menú móvil no desplaza innecesariamente el contenido crítico fuera de la primera pantalla;
- textos no se cortan ni se superponen;
- tarjetas cambian de columnas a filas cuando corresponde;
- tablas quedan contenidas y desplazables internamente;
- botones e inputs son operables con tacto;
- foco de teclado es visible;
- iconografía final es SVG consistente;
- no hay estilos aislados que contradigan el sistema de diseño global.

## Implementación

El archivo `frontend/src/institutional-theme.css` concentra tokens y reglas globales de adaptación. Se carga al final de los estilos base para actuar como capa institucional sobre módulos existentes.

El archivo `frontend/src/InstitutionalIcon.tsx` concentra la iconografía SVG de navegación y acciones institucionales. Así el mismo icono mantiene geometría y aspecto en Windows, Android, iOS, macOS y Linux.

Las capas `ppt-fidelity.css`, `dashboard-ppt-fidelity.css`, `archive-ppt-fidelity.css`, `regularity-ppt-fidelity.css` y `secretariat-ppt-fidelity.css` refinan pantallas específicas contra los mockups aprobados sin modificar reglas de negocio. `member-responsive-fix.css` contiene correcciones de composición detectadas mediante evidencia visual en tablet y notebook pequeño. `mobile-nav-compact.css` limita la altura de la navegación institucional en tablet y móvil, manteniendo disponibles todos los módulos mediante scroll vertical propio y sin alterar la lógica RBAC.

El archivo `frontend/src/institutional-theme.test.ts` actúa como gate automatizado del contrato PMGM-UI-001 dentro del `npm test` del CI. Verifica paleta, breakpoints, prevención de scroll horizontal global, tablas contenidas, controles táctiles y preferencia de reducción de movimiento.

El workflow `PMGM Showcase Demo` levanta el build demostrativo y publica el artefacto `pmgm-responsive-visual-evidence`. La captura multipantalla se ejecuta mediante `.github/scripts/capture-showcase-views.mjs`, que controla Chrome por DevTools, cambia el perfil QA desde el selector real y abre cada módulo mediante la navegación real sin introducir rutas ni parámetros exclusivos de testing en el runtime del producto.

## Matriz de evidencia automática

El artefacto visual contiene actualmente **21 capturas PNG**:

- **Mi ficha:** matriz completa en 360 × 800, 390 × 844, 768 × 1024, 1024 × 768, 1366 × 768, 1440 × 900 y 1920 × 1080.
- **Matriz representativa móvil/escritorio:** 390 × 844 y 1440 × 900 para Inicio, Biblioteca Virtual, Gestión Logial, Gran Tesorería, Gran Hospitalaria, Gran Secretaría y Gran Archivero.

El workflow exige que las 21 evidencias existan antes de considerar satisfactoria la validación visual automática. Los PNG se conservan temporalmente como artefacto de GitHub Actions para revisión de PR y UAT.

## Validación visual observada

La evidencia automática ya produjo hallazgos concretos y correcciones verificables:

1. La primera matriz completa detectó solapamiento entre la ficha personal e institucional en 768 × 1024 y 1024 × 768. `member-responsive-fix.css` corrigió la composición y una nueva ejecución confirmó ausencia de invasión entre tarjetas.
2. La matriz multipantalla detectó que, en 390 × 844 y con perfil de Gran Logia, la cantidad de módulos consumía una proporción excesiva de la primera pantalla antes de mostrar el contenido. `mobile-nav-compact.css` redujo la navegación a una zona scrollable de altura controlada; la revisión posterior confirmó que el contenido de Gestión Logial, Tesorería, Hospitalaria, Secretaría y Gran Archivero aparece inmediatamente debajo sin perder acceso a los módulos.
3. El frente quedó posteriormente validado de extremo a extremo con `PMGM CI #827` y `PMGM Showcase Demo #151` sobre el SHA técnico que abrió el UAT.

## Avance de comparación contra PPT

Con fidelidad específica aplicada y evidencia visual representativa:
- Dashboard / Inicio.
- Mi ficha del hermano.
- Biblioteca Virtual.
- Gran Archivero.
- Gran Tesorería.
- Gran Hospitalaria.
- Gran Secretaría.
- Gestión Logial, cuyo cockpit institucional azul/dorado fue preservado y revisado en móvil/escritorio sin rediseño innecesario.

## Gobierno de aprobación visual

La aprobación técnica y la aprobación institucional son estados distintos:

- `PMGM-UAT-VISUAL-001.md` define la revisión visual formal, la matriz de pantallas, el registro de observaciones y la decisión del Product Owner.
- `PMGM-UI-BASELINE-001.md` registra el SHA, evidencia y decisión que pasan a ser referencia institucional una vez aprobado el UAT.
- Una ejecución verde de CI/Showcase **no** marca por sí sola la baseline como aprobada.
- Si el SHA cambia antes de la aceptación, la nueva cabeza debe volver a quedar técnicamente validada y el acta debe identificar el SHA realmente aprobado.

## Pendientes antes de aprobación visual institucional

- Completar la decisión del Product Owner en `PMGM-UAT-VISUAL-001.md`.
- Registrar observaciones visuales institucionales, si existen, con severidad y decisión.
- Fijar el SHA aprobado en `PMGM-UI-BASELINE-001.md`.
- Integrar el PR aprobado a `dev` y verificar el despliegue real de GitHub Pages.
- Ampliar la matriz representativa a tablet para módulos específicos sólo si el UAT detecta una necesidad adicional; los siete tamaños de aceptación ya se mantienen cubiertos por Mi ficha.
