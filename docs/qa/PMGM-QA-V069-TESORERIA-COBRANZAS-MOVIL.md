# PMGM-QA-V069 — Cuotas y Cobranzas legible en celular

## Hallazgo

La captura del Product Owner mostró que, aunque el botón de pago fue adaptado para móvil, las filas de la tabla quedaban como tarjetas recortadas por el costado derecho y sólo se veían algunos encabezados. Los valores no eran legibles.

## Cambio

- Cada campo muestra una etiqueta semántica y su valor en el propio HTML.
- En celular cada par etiqueta/valor ocupa una fila vertical; en tablet se presenta en dos columnas dentro de una tarjeta que respeta el ancho disponible.
- Se evita el desplazamiento horizontal global y el recorte de tarjeta.
- El botón de pago conserva altura táctil de al menos 44 px y ancho completo en la tarjeta.

## Validación automatizada

El workflow Showcase inspecciona, en los viewports móviles, tablet y escritorio, que la primera cobranza exponga seis etiquetas y seis valores visibles, que la tarjeta no exceda el ancho de viewport, que «Registrar pago» sea visible y tenga al menos 44 px de alto, y que la página no tenga desbordamiento horizontal global.

Resoluciones de captura existentes: 390×844, 768×1024 y 1440×900. La regresión permanente también exige 360×800, 820×1180, 1024×768 y 1366×768 para la matriz de UI completa.

## Alcance y estado

- Alcance de código: presentación de Cuotas y Cobranzas en Demo GitHub Pages y estilos de la misma vista en el producto.
- No cambia los permisos, reglas contables, montos ni persistencia del backend.
- La aceptación del fix depende de CI exact-head, Showcase con capturas y publicación post-merge.
- La QA física de srv01 y UAT siguen su flujo en Issue #97; una captura de Pages no las sustituye.
