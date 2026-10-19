# QA G10 · Sistema vuelve a Inicio · 19-10-2026

## Evidencia física del Sponsor

- En `https://sistema.granlogiamixta.cl/` funciona login sintético con `qa.admin`; el cambio de contraseña se había confirmado con `RESET_QA_ADMIN_OK`.
- Sponsor: «No, sí aparece el menú; si lo selecciono vuelve a inicio».
- **No se recibió captura ni traza de red autenticada** que confirme qué elemento tocó. El texto «Sistema» es un encabezado de grupo; el botón navegable es **«Parámetros del sistema»**. En la página Sistema hay una pestaña «Usuarios».
- Evitar diagnóstico categórico antes de verificar en QA física. No imprimir capturas de tokens/cookies ni datos reales.

## Riesgo de navegación detectado en dev

`App.tsx` tenía `useEffect(() => { if (!activeViewAllowed) setView('dashboard') }, [activeViewAllowed])`. `useViewAccess` puede retornar `null` durante validación inicial, revalidación al recuperar foco o falla de red. `null` no equivale a rechazo 403; navegación vuelve a Inicio silenciosamente. Esto es una **causa candidata** compatible con el reporte, no confirmación de que haya ocurrido.

## Cambio propuesto en PR #559 DRAFT, solamente hacia dev

- Separar verificación pendiente y rechazo confirmado en `systemNavigationGate.ts`.
- Retener destino Sistema/Bootstrap aunque sus permisos se consulten o denieguen, **sin renderizar la consola** hasta confirmar autorización `view=system` y capacidad `CanConfigureSystem` / `CanBootstrap`.
- Mostrar aviso «Verificando acceso a Sistema…» o «Acceso a Sistema no confirmado» en lugar de saltar a Inicio; proporcionar retorno explícito.
- Continuar denegando por servidor y cliente toda operación no autorizada. No cambiar backend, roles, Keycloak, credenciales ni asignaciones dinámicas.
- Tests `systemNavigationGate.test.ts` cubren pendiente/denegado/autorizado/otros módulos.

## Siguiente comprobación en QA física

1. Navegador móvil: abrir menú, pulsar el **botón «Parámetros del sistema»** (no el encabezado «Sistema»), luego pestaña «Usuarios»; reportar resultado sin información sensible.
2. Una vez que la PR sea integrada a `dev` tras todos los gates exact-head y el candidato QA firmado sea desplegado mediante proceso autorizado, revalidar: no retorno silencioso, render de Sistema y Usuarios para `qa.admin`; prueba negativa para cuenta sin permisos.
3. Si aparece «Acceso a Sistema no confirmado», investigar claims privados, `/api/session/me` y `/api/session/view-access` únicamente mediante diagnóstico autenticado que **no registre tokens, secretos ni identificadores personales**. No forzar vista autorizada solo desde frontend.
4. Issue #556 (recuperación de contraseñas por administrador) sigue separado, no implementado aún; botones de creación/listado no equivalen a reset.
5. GitHub #97 registra avances QA. Drive documento de bitácora `1I9bcJ7lKH4T2iQvxafY63HbOBr0ZsGZ8lDX3tSe2UfM`.

`main` no se modifica sin orden expresa; `srv01` pausado, Silo solamente sintético. 
