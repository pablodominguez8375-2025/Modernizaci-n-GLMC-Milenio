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
