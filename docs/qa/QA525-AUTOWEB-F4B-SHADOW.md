# QA525 · auto-web F4-B en **sombra** (pantallas) — contrato v1 (INACTIVO)

Issue #1010. Base: propuesta de Claude 6879890856; ChatGPT pidió (6882544525) que F4-B vaya en un PR separado y primero en sombra.

## Qué es
F4-B dice si un cambio de pantallas **habría** podido salir solo. Su veredicto se guarda **junto** al de F4-A y no cambia nada:
- no toca el sobre, la política ni el clasificador F4-A, que está fijado por hash;
- el host sigue aplicando solo F4-A.

F4-B avanza a supervisado solo cuando la sombra demuestre que no produce falsos `auto-web`.

## Reglas (todas deben cumplirse para `F4B_WOULD_AUTO`)
1. **Estructura:** solo archivos `modified` con modo `100644`, sin altas, bajas ni renombres. Máximo 15 archivos y 400 líneas agregadas.
2. **Rutas:** `.ts/.tsx/.css` bajo `frontend/src/`, con la misma denylist de F4-A:
   - directorios `api/ auth/ oidc/ keycloak/ accounts/ config/ runtime/ env/`;
   - nombres con `auth`, `token`, `session`, `env`, `runtime`, `sw`, `vite`, `manifest`, `config`…;
   - `main.*` y `*.d.ts`.

   Los recursos estáticos pasan por F4-A tal cual.
3. **AST por archivo** (`scripts/qa/qa525-vnext-autoweb-f4b-analyze.cjs`, compilador TypeScript del propio `frontend`, no expresiones regulares). Compara conteos por tipo antes y después; cualquier aumento cuenta como hallazgo:
   - **conjunto de imports idéntico**;
   - **sin sumideros nuevos:**
     - red: `fetch`, `XMLHttpRequest`, `WebSocket`, `EventSource`, `sendBeacon`;
     - almacenamiento: `localStorage`, `sessionStorage`, `indexedDB`, `cookie`;
     - ejecución dinámica: `eval`, `new Function`, `import()`, `require`;
     - HTML: `innerHTML`/`outerHTML`/`insertAdjacentHTML`, `dangerouslySetInnerHTML`;
     - otros: `import.meta.env`, `window.open`, `location.href/assign/replace`, `postMessage`, `Worker`;
     - CSS: `@import`, `url()` externo, `expression()` y `behavior`;
   - **sin referencias nuevas** a nombres importados desde módulos `api/`, `auth/`, `accounts/`, `oidc/`, `keycloak/` o `*Api`;
   - **sin llamadas nuevas** a callbacks que mutan (`onSave`, `onDelete`, `onApprove`, `onSubmit`…);
   - el archivo parsea sin errores.
4. **E2E:** el candidato pasa la captura de todas las vistas de la demo sin errores de consola. El gate de CI se conecta en un PR posterior; mientras tanto la sombra registra `F4B_E2E_NOT_PASSED`.

## Calibración con datos reales (`dev`, últimos 150 commits)
De 11 commits de solo frontend, **F4-B en sombra habría automatizado 3** (c78bc03e, b27509d9, 0bd927bc). F4-A habría automatizado 0. Los 8 restantes van a manual por:
- import nuevo de un componente nuevo (6);
- `import.meta.env` nuevo (d5f82864);
- referencia nueva a la API de cuentas (a184936d);
- directorio denegado (2).

Siguiente mejora posible (F4-B v2, a decidir con datos de la sombra): admitir imports relativos hacia archivos **nuevos del mismo release** que a su vez pasen el AST, sin sumideros ni API.

## Límite honesto
Ningún análisis estático prueba que una pantalla no cambie datos. El riesgo lo acotan otras dos cosas:
1. Toda escritura pasa por la API, que aplica permisos y auditoría en el servidor y que no cambia en `auto-web` (capas idénticas y `web_only_port`).
2. F4-B impide que una pantalla agregue caminos **nuevos** hacia la API, el almacenamiento o la red sin que una persona lo mire.

## Código
- `scripts/qa/qa525-vnext-autoweb-f4b-analyze.cjs`: analizador AST, solo en CI.
- `scripts/qa/qa525-vnext-autoweb-f4b.py`: `shadow_verdict` puro y `collect_facts`, que corre en CI con git y node.
- `tests/test_qa525_vnext_autoweb_f4b.py`. El CI instala el toolchain y exige `QA525_REQUIRE_TS=1`: si falta TypeScript la prueba falla, no se salta.
