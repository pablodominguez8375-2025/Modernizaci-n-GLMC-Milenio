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
   - Producción debe usar iconos SVG lineales, consistentes en tamaño, trazo y estilo.
   - Objetivo: 20–24 px, trazo visual homogéneo, color heredado del contexto.
   - Los símbolos Unicode actuales son temporales de QA y deben ser reemplazados antes de aprobación visual institucional.
   - No usar emojis como iconografía de producto.

4. **Navegación**
   - Escritorio: navegación lateral azul oscuro, activa en dorado.
   - Tablet: navegación compacta y desplazable sin pérdida de funciones.
   - Móvil: navegación táctil, legible y usable sin zoom ni desbordes horizontales de la página.

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
- textos no se cortan ni se superponen;
- tarjetas cambian de columnas a filas cuando corresponde;
- tablas quedan contenidas y desplazables internamente;
- botones e inputs son operables con tacto;
- foco de teclado es visible;
- iconografía final es SVG consistente;
- no hay estilos aislados que contradigan el sistema de diseño global.

## Implementación

El archivo `frontend/src/institutional-theme.css` concentra tokens y reglas globales de adaptación. Se carga al final de `frontend/src/main.tsx` para actuar como capa institucional sobre estilos de módulos existentes.

## Deuda explícita antes de aprobación institucional

- Reemplazar símbolos Unicode de navegación y acciones por componentes SVG reutilizables.
- Ejecutar revisión visual pantalla por pantalla contra los mockups PPT.
- Añadir pruebas de viewport / captura para tamaños críticos dentro del pipeline de UI.
