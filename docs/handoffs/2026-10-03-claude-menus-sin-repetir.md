# Handoff — Claude — menús sin funciones repetidas (Fase A) — 03-10-2026

- **Issue:** #306 (`agente:claude`). El PO aprobó las Fases A y B el 03-10-2026. La Fase B (insinuaciones e iniciación) es del dominio de Admisiones y quedó delegada a ChatGPT en Drive: «INSTRUCCIONES PARA CHATGPT — 2026-10-03 — Unificar menú de insinuaciones e iniciación (Fase B)».
- **Revisión previa de Drive:** solo la carpeta Proyecto Centenario. No hay archivos nuevos (la Línea Base fue consolidada).
- **Base:** `dev@c6810da`. Versión UI QA v0.86. Solo presentación y navegación.

## Cambios

- **Nombres únicos:** en el menú, la búsqueda global y las ayudas, «Mi calendario» pasa a **Agenda** y «Notificaciones» pasa a **Avisos**. Los títulos de esas vistas ahora son «Agenda» y «Avisos».
- **Menú de usuario:** queda solo con «Mi ficha» y «Cerrar sesión». Se quitan «Mi calendario» y «Tamaño de letra», que ya están en el menú.
- **Mi ficha:** se quitan los accesos «Ver mi agenda», «Ver mis avisos» y «Tenidas de mi Taller».
- **Inicio:** quedan 3 accesos (Agenda con la próxima actividad, Avisos e Insinuados). Antes había 2 accesos a la Agenda. Se muestran en 3 columnas, y en 1 columna en celular. La auditoría de Showcase se actualizó para 3 accesos.
- **Módulos relacionados como pestañas de una sola opción del menú** (`WorkspaceTabs` sobre la vista; en celular se ven como tarjetas):
  - Parámetros del sistema: pestañas «Parámetros del sistema» y «Configuración inicial».
  - Calidad de datos: pestañas «Hallazgos» y «Cola de corroboración».
  - Gestión Logial / Secretaría: pestaña «Ficha del Taller», para quienes tienen Gestión Logial. Quien no la tiene mantiene «Ficha del Taller» en el menú.
  - La opción del menú queda marcada como activa en cualquiera de sus pestañas.

## Resultado (opciones del menú lateral por perfil)

| Perfil | Antes | Después |
|---|---|---|
| Venerable Maestro | 14 | 13 |
| Secretaría del Taller | 8 | 7 |
| Autoridad de Gran Logia | 18 | 16 |
| Administrador | 10 | 9 |

Con la Fase B, el Venerable Maestro bajará a 11.

## Verificación

- 320 tests en verde. El test nuevo es `menus-sin-repetir.test.ts`.
- tsc y oxlint OK.
- Playwright en 1440 y 390 px: las 3 vistas con pestañas funcionan, la opción del menú queda activa y no hay desbordes ni errores de página.
- Auditoría móvil completa en local.

## Impacto en datos

Ninguno.
