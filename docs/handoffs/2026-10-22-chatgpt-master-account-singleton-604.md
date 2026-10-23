# Handoff #604 — Unicidad de administrador de plataforma (fase backend)

Fecha 22-10-2026, Chile. Agente propietario `agente:chatgpt`. Base `dev@b670395043ad90248c4bc7473a42249b8ca0784b`, main congelada `b77de59e170ebfa2abec71b418f6d8b0407ba7ad`.

## Contexto
El perfil de administrador técnico base ya tiene CRUD de perfiles personalizados, asignación y vistas, pero el Master único `admin` **NO está listo** (issue #604 abierto). El endpoint `POST /api/system/user-accounts` permite `PlatformAdministrator=true` a actores `platform_superadmin`; el proveedor Keycloak adjudica entonces `platform_superadmin` por credencial externa y no impide crear dos cuentas de plataforma con diferente email.

## Incremento reclamado
Agregar al alta de plataforma (exclusivamente) un guard de unicidad ante operaciones simultáneas, serializado mediante advisory lock PostgreSQL global y con lectura de cuentas gestionadas por el proveedor de identidad, antes de preparar la cuenta, enviar credencial o conceder rol. Fallar cerrado ante error del proveedor. Pruebas HTTP/PostgreSQL para primera cuenta, segunda denegada y no escalada ni correo enviado. Mantener usuarios logiales y controles existentes.

### Límites importantes
- Esto no prueba que `admin` ya exista, que su `username` literal sea `admin`, o que un usuario externo legacy con rol Keycloak `platform_superadmin` sea detectado por el inventario de cuentas gestionadas; se requiere reconciliación institucional de identidades.
- Master único no equivale a superadministradores de soporte múltiples; esos deberán gestionarse mediante perfiles dinámicos con mínimos permisos y auditoría, **no** por `PlatformAdministrator=true`.
- No se almacenarán ni compartirán contraseñas, seed de credenciales, emails reales, tokens ni datos logiales.
- MFA obligatorio, break-glass, gestión de Master por procedimiento especial, bloqueo de autoescalada por todos los flujos, edición y restauración auditada de perfiles base, y soporte `Ver como` siguen abiertos en #604, #574 y #556.
- Claude/Clau posee la UI del diseñador; solo actuar con handoff y PR propia al completar contrato. El 22-10 su última decisión registrada establece perfiles base separados «Bibliotecario» y «Comunicaciones», no Docencia.
- No main, glmdch ni producción. Revalidar triple CI exact HEAD y behind=0 antes de fusionar a dev.

Registro de salida GOV004: no cambia el esquema ni entidades, solo regla de autorización de alta y pruebas sintéticas.

## ACTUALIZACIÓN VINCULANTE DEL PO — 22-10-2026, 21:31 CL

> «El máster no puede crear otro máster, solo existe un máster, el máster solo puede crear super usuarios y usuarios».

Esta decisión **sustituye cualquier propuesta anterior que permita asignar el rol Máster desde la cuenta Máster**. El perfil técnico base ya existe (#266), pero el Máster real #604 no está operativo ni asegurado.

| Alta solicitada por el Máster | Permitida | Resultado seguro |
|---|---|---|
| **Usuario** | Sí | Cuenta ordinaria bajo requisitos institucionales vigentes |
| **Superusuario** | Sí | Cuenta técnica con uno o varios perfiles dinámicos de alcance limitado; no rol Máster ni `platform_superadmin` |
| **Máster** | **No, nunca** | Denegar toda alta, duplicación, reasignación, importación, promoción o cambio de rol hacia Máster, incluso por el propio Máster |

Implementación correcta pendiente de revisión de seguridad:
- El Máster único `admin` debe crearse exclusivamente mediante **bootstrap autorizado y controlado**, nunca por el `POST /api/system/user-accounts` ni por la interfaz. La recuperación excepcional se ejecuta mediante procedimiento institucional separado, sin duplicar la cuenta.
- El contrato de creación permitido desde Máster deberá tener exclusivamente `user | superuser`. La petición antigua `PlatformAdministrator=true` no representa un superusuario: actualmente concede `platform_superadmin` a través de Keycloak y puede crear un segundo administrador de privilegio global. Hasta rediseñar su ruta, debe bloquearse como camino de alta en lugar de reinterpretarlo silenciosamente.
- Un actor superusuario o usuario nunca puede crear/otorgar Máster. No se presume que puedan crear otros usuarios: requiere permiso específico aprobado en otra decisión.
- Garantía transversal de cuenta Máster única en Keycloak y datos locales, con denegación y auditoría ante duplicación, autopromoción, cambio de rol, importación y concurrencia, y fallo cerrado cuando no puede consultarse identidad.
- El acceso a datos logiales, «Ver como», editar perfiles base o administrar soporte se rige por permisos separados. Crear «superusuarios» no otorga firmas, roles reglamentarios ni acceso irrestricto.
- Clau/Claude conserva la UI y diseñará solo opciones «Usuario» / «Superusuario» cuando el backend tenga endpoint seguro. No reutilizar «Administrador de plataforma (excepción)» como si fuera superusuario.

**Estado real:** PR #641 se mantiene DRAFT/BLOQUEADA con **solo documentación**; el control de unicidad y de roles no está implementado. La herramienta rechazó previamente la modificación del endpoint de altas; **no intentar eludir dicho control**. No afirmar sistema administrativo listo, prueba UAT pasada, MFA activo ni cuenta Máster desplegada. `dev` verificado en este corte: `23d345e583cb076e7054502e10f32c313357a1e3`; `main` permanece `b77de59e170ebfa2abec71b418f6d8b0407ba7ad`. No se tocó QA físico.

Continuidad: Issue #604 (contrato actualizado), PR #641, GOV-003 multi-IA y adenda Drive Proyecto Centenario. No reabrir funcionalidad como terminada solo porque exista el diseñador de perfiles #266.
