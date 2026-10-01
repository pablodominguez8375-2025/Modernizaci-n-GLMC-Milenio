# Handoff — Claude — UX P2: cabecera, búsqueda global y barra lateral — 01-10-2026

- Issue #243 (`agente:claude`). Alcance P2 aprobado por el PO el 01-10-2026.
- Base: `dev@46e07bc` (PR #242). `main` sin cambios.

## Qué cambia (UI QA v0.65)

- **Franja de demo** (`.demo-strip`): queda sobre la cabecera y agrupa QA demostración, selector de perfil, SHA y versión. Solo existe con `useMocks`, así que no aparece en producción.
- **Cabecera:** logo, búsqueda global, rol, campana con contador y menú de usuario (`UserMenu.tsx`: Mi ficha, Mi calendario, Cerrar sesión y versión).
- **Búsqueda global** (`GlobalSearch.tsx`):
  - Encuentra los módulos permitidos al perfil, sin importar tildes y también por palabras clave.
  - Ofrece «Buscar en Fichas de miembros», que abre `MemberDirectoryPage` con `initialQuery`.
  - Atajos: Ctrl/⌘+K para abrir, flechas para moverse, Enter para elegir y Escape para cerrar.
  - Se oculta en pantallas de 720 px o menos, donde manda la barra inferior.
- **Barra lateral colapsable** (≥981 px): pasa a 76 px con íconos, contadores y tooltip. La preferencia se guarda en `localStorage` (`pmgm.sidebarCollapsed`, con try/catch).
- **Estilos y tests:** hoja nueva `header-p2.css`, cargada después de `role-navigation.css`, y tests nuevos en `header-p2.test.ts`.

## Verificación local

- 51 archivos y 254 tests en verde; oxlint con 0 advertencias; tsc OK.
- Playwright:
  - Sin desborde a 1440, 1024, 800 y 360 px.
  - La búsqueda lleva a Fichas con la consulta precargada.
  - La barra lateral colapsa a 76 px.
  - Sin errores de página.

## Pendiente

- P3 (tablas operativas, módulo por módulo): pendiente de aprobación.
- El lema sigue oculto.
- `srv01` sin cambios.
