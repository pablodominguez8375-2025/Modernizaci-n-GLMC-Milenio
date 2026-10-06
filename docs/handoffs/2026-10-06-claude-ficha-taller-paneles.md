# Handoff — Claude — Ficha del Taller sin formularios abiertos — 06-10-2026

- **Issue:** #333 (`agente:claude`).
- **Origen:** pedido del PO del 06-10-2026 de seguir mejorando el proyecto.
- **Revisión previa de Drive:** solo la carpeta Proyecto Centenario. La Línea Base fue consolidada el 06-10. ChatGPT tiene en ejecución las instrucciones de seguridad (PR #332).
- **Auditoría de formularios abiertos** (8 perfiles, todas las vistas y pestañas, a 1440 px). Quedan abiertos:
  - **Ficha del Taller:** edición con 5 campos y delegación de acceso. → *Se corrige en este PR.*
  - **Carga de insinuados (25 campos) y Circuito de Iniciación (4):** dominio de ChatGPT. Ya están delegados en «INSTRUCCIONES PARA CHATGPT — 2026-10-03».
  - **Filtros de consulta** (Templos y salas, reporte de Régimen Interior) y **Configuración inicial** (asistente): permitidos por el patrón.

## Cambios (`LodgeProfilePage.tsx`)

- **Origen y pertenencia del Taller:**
  - Siempre se muestra en solo lectura (nombre, fecha, ciudad/Oriente, país).
  - Quien puede editar ve el botón **«Editar ficha del Taller»**, que abre el formulario en panel (incluye Oriente y logo, del PR #318).
- **Accesos delegados:**
  - El formulario se abre con el botón **«Delegar acceso de consulta»**, con confirmación.
  - La lista con «Revocar» queda visible.
- **Test:** se agregó un contrato en `operational-view.test.ts`.

## Observación para ChatGPT / PO (no se modificó)

La nota de la ficha dice: «La clasificación de cuotas se administra aparte por Gran Tesorería … No se deduce de la ciudad ni del país». Esto contradice la regla del PO del 04-10 («los Talleres toman los valores según su Oriente en la Ficha del Taller»), pese a que el PR #318 agregó el Oriente en la ficha. Hay que revisar si la zona tarifaria ya se deriva de `orienteCode` y corregir el texto.

## Impacto en datos

Ninguno.
