# GOV-004 — salida de datos y permisos preparatoria #574

Fecha: 2026-10-23. PR #656, rama `feature/574-support-view-as-policy-20261023-gpt` desde `dev@18fbbfed7cede2823c0f3fec2760f2aa528b5f48`.

| Contrato | Antes | Después del incremento | Persistencia |
|---|---|---|---|
| Política de soporte «Ver como» | Requisito institucional sin guard reutilizable | `SupportViewAsPolicy.EvaluateStart` / `EvaluateRead` con rechazo por defecto, máximo 30 min, motivo validado y objetivos no privilegiados | **Ninguna**: solo código puro; no se crea sesión real ni cuenta |
| Métodos HTTP en modo observación | Regla no centralizada | Solo GET/HEAD potencialmente autorizables; POST, PUT, PATCH, DELETE y métodos inesperados rechazados | Ninguna |
| Autorización de lectura | No existía `support_read` | Aun para GET/HEAD exige flag de grant de soporte **verificado en servidor**, permiso de lectura del objetivo y allowlist por recurso; datos personales restringidos se deniegan | Ninguna |
| Identidad y auditoría | No hay API de sesión de soporte | Política exige sujeto operador y objetivo distintos, motivo específico y prohibición de observar otras cuentas privilegiadas | **Pendiente**: futura sesión y eventos append-only |

Los flags booleanos son **precondiciones de un futuro handler servidor**, nunca campos aceptados desde cliente. El helper no es un autorizador global, no sustituye Keycloak ni se usa en endpoints hasta que existan RBAC, persistencia/auditoría, expiración/revocación y QA.

No se añaden tablas, migraciones, fixtures institucionales, credenciales, RUT ni información personal real. La demo y el instalable siguen sin funcionalidad «Ver como» porque no es seguro habilitarla aún. Coordinación: ChatGPT backend #574, Clau futura UI cuando API y pruebas reales estén integradas; no tocar PR #655 ni `main`.
