# PMGM Web — Frontend del Proyecto Milenio

Frontend institucional responsive del Proyecto Milenio.

## Stack
- React 19.
- TypeScript 6 en modo estricto.
- Vite 8.
- CSS institucional propio durante el MVP.

Decisión registrada en `docs/adrs/PMGM-ADR-002.md`.

## Requisitos locales
- Node.js 24 recomendado para desarrollo/CI.
- npm 10+.
- Backend PMGM.Api disponible cuando se use modo real.

## Inicio rápido
```bash
cd frontend
npm ci
cp .env.example .env.local
npm run dev
```

Por defecto `.env.example` activa `VITE_USE_MOCKS=true`, de manera que el dashboard y el portal pueden demostrarse mientras el proveedor OIDC definitivo sigue pendiente.

## Scripts
```bash
npm run dev
npm run lint
npm test
npm run build
npm run preview
```

## Conexión con la API
- `VITE_API_BASE_URL`: base pública de la API. Vacío usa mismo origen.
- En desarrollo, Vite proxifica `/api` y `/health` a `PMGM_API_PROXY_TARGET`, cuyo valor por defecto es `http://localhost:5000`.
- `VITE_USE_MOCKS=true`: usa datos explícitamente demostrativos.
- `VITE_USE_MOCKS=false`: usa la API real.

## Seguridad
Las variables `VITE_*` se incorporan al bundle del navegador. **Nunca** deben contener secretos, client secrets, contraseñas ni access tokens.

La autorización efectiva siempre se valida en el backend. El cliente API recibe el access token del adaptador OIDC/PKCE sin acoplar la UI al proveedor de identidad.

## Primer alcance visual
- Shell institucional responsive.
- Dashboard MVP.
- Portal de insinuados en período de publicación.
- Búsqueda por persona/Taller.
- Indicador visual del cumplimiento del plazo.
- Fechas presentadas en `es-CL` y zona `America/Santiago`.

## Autenticación

Implementación OIDC Authorization Code + PKCE disponible. Ver [configuración, sesión y aceptación](OIDC.md) y [ADR-005](../adrs/PMGM-ADR-005-frontend-oidc-pkce.md). Demo requiere `VITE_USE_MOCKS=true` explícito.
