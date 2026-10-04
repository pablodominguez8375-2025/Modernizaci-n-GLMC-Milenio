# Handoff — Claude — administración ordenada — 04-10-2026

- **Issue:** #314 (`agente:claude`).
- **Pedido del PO (04-10-2026):** el menú del administrador está demasiado cargado y desordenado.
- **Revisión previa de Drive:** solo la carpeta Proyecto Centenario. ChatGPT consolidó la Línea Base y marcó la Fase B como EJECUTADA.
- **Base:** `dev@e26cad6`, que incluye el PR #267 de ChatGPT (perfiles técnicos dinámicos).
- **Versión:** UI QA v0.88.

## Problema

Para el administrador había **tres niveles de menú apilados**:

1. Pestañas «Parámetros del sistema / Configuración inicial» (en `App.tsx`).
2. Pestañas «Operación y respaldos / Perfiles y accesos / Parámetros (44)».
3. La «Consola del sistema» con su propio menú: Backup, Usuarios, Correo, Logos, Auditoría.

Además había formularios abiertos:

- restauración, correo e identidad;
- diseñador de perfiles con 33 campos visibles;
- 44 tarjetas de parámetros, cada una con su formulario editable. Ocupaban 10.209 px de alto.

## Cambios

- **Un solo menú de secciones** (`WorkspaceTabs`): Respaldos · Usuarios · Perfiles y accesos · Correo · Logos y colores · Auditoría de accesos · Parámetros (N) · Configuración inicial.
  - En celular se muestra como tarjetas.
  - En PC de 1440 px o más, como columna.
  - `SystemOperationsPanel` acepta `area` controlada. En ese modo oculta su propio menú y su encabezado, y queda montado para conservar lo escrito.
  - «Configuración inicial» entra como `bootstrapSlot`.
  - En `App.tsx` se quitó la fila de pestañas duplicada. El perfil que solo tiene Configuración inicial sigue viendo `BootstrapPage` sola.
- **Parámetros:**
  - Se filtran por categoría con pestañas (una categoría a la vez). Se eliminó «Todos».
  - Cada tarjeta muestra el **valor vigente** y el botón **«Cambiar valor»**, que abre un panel con valor, vigencia, fundamento y confirmación. «Ver historial» sigue disponible.
  - El aviso «Cambios con vigencia controlada» pasa a ayuda dentro de Parámetros.
- **Paneles:**
  - Respaldos: «Restaurar un respaldo» (con RESTAURAR).
  - Correo: muestra un resumen y tiene «Editar configuración de correo» más «Probar conexión».
  - Logos y colores: muestra un resumen con muestras de color y tiene «Editar logos y colores».
  - Usuarios: «Diseñar perfiles de acceso» abre el diseñador en un panel.
  - `UserAccessAssignments` (componente de ChatGPT, solo se envolvió): «Asignar perfil a un usuario» se abre en un panel.

## Medición

| Vista | Antes | Después |
|---|---|---|
| Parámetros (1440 px) | 10.209 px | 2.037 px |
| Usuarios: campos visibles | 33 | 0 |
| Usuarios: alto en celular | 4.485 px | 1.351 px |

- No hay desbordes en 1440 ni en 390 px.

## Tests

- Se ajustaron `listing.test.ts` y `menus-sin-repetir.test.ts` a la nueva estructura.
- Se agregó `admin-ordenado.test.ts`.
- Resultado: 403 tests en verde.

## Impacto en datos

Ninguno. Se mantienen los mismos handlers, API y permisos.
