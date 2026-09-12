# PMGM-ADR-002 — Frontend React + TypeScript + Vite

## Estado
Aceptada.

## Contexto
El Proyecto Milenio requiere una intranet institucional responsiva, un portal de insinuados, consola administrativa y evolución futura hacia experiencia PWA/móvil. El frontend debe permanecer desacoplado de la lógica de negocio del backend ASP.NET Core y consumir contratos API versionados.

## Decisión
Se adopta el siguiente stack para el frontend:

- **React 19** como biblioteca de interfaz.
- **TypeScript 6** con modo estricto.
- **Vite 8** como herramienta de desarrollo y build.
- CSS propio con variables de diseño institucional durante el MVP, evitando introducir un framework visual pesado antes de consolidar el sistema de diseño.
- Consumo de API mediante `fetch` tipado y un cliente centralizado.
- Preparación para autenticación OIDC/PKCE y autorización por claims/roles; el proveedor definitivo de identidad sigue siendo una decisión separada.
- Despliegue como artefactos estáticos detrás de Nginx/proxy equivalente.

## Razones
1. Mantiene una frontera clara entre UI y dominio/backend.
2. TypeScript reduce errores de contratos entre frontend y API.
3. React tiene un ecosistema amplio para tablas, formularios, accesibilidad, calendarios y PWA.
4. Vite entrega un ciclo de desarrollo y build rápido y genera artefactos estáticos simples de desplegar.
5. La experiencia móvil y el portal público/intranet se benefician de una SPA liviana sin descargar el runtime .NET completo al navegador.
6. Permite que el backend continúe evolucionando en .NET sin acoplar la capa de presentación al mismo runtime.

## Alternativa evaluada: Blazor
Blazor WebAssembly ofrece reutilización de C# y buena integración con ASP.NET Core. Se descarta para el MVP de interfaz porque agrega el runtime y ensamblados .NET al arranque del cliente y reduce la independencia tecnológica de la capa de experiencia. Puede reevaluarse para herramientas internas específicas si aparece una ventaja demostrable.

## Consecuencias

### Positivas
- Separación técnica clara.
- Buen soporte para UI responsiva y PWA.
- Tipado fuerte en contratos.
- Despliegue estático simple.
- Mayor disponibilidad de perfiles frontend y librerías especializadas.

### Costos
- Se incorpora Node/npm al toolchain.
- Backend y frontend usan lenguajes distintos.
- Será necesario automatizar generación o validación de contratos OpenAPI para evitar deriva.

## Reglas de implementación
- No almacenar secretos en el bundle frontend.
- No confiar en permisos del cliente: toda autorización efectiva permanece en la API.
- La URL base de API debe ser configurable por ambiente.
- Toda pantalla debe diseñarse primero para uso móvil/responsivo.
- Componentes que muestren datos personales deben aplicar mínimo privilegio y minimización visual.

## Referencias técnicas
- https://react.dev/learn/typescript
- https://vite.dev/
- https://learn.microsoft.com/aspnet/core/blazor/host-and-deploy/webassembly/
