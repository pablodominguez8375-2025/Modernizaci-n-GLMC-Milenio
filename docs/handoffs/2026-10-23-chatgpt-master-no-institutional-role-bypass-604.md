# #604 — contrato de separación Máster de plataforma / cargos logiales

Fecha: 2026-10-23, autor ChatGPT. Rama `fix/master-institutional-role-bypass-604-20261023-gpt` desde `dev@5da54b07d5b913efe97e35e84e4fa8fe5ed8bda1`.

## Mandato y coordinación
Sponsor 22-10: solo existe cuenta Master `admin`; puede administrar usuarios y superusuarios de permisos limitados, nunca crear otro Máster ni asumir cargos. Clau/Claude se reserva UI y Keycloak; ChatGPT backend de autorización, pruebas y documentación (PMGM-GOV-003). No tocar otros PR, `main`, QA físico ni producción.

## Hallazgo comprobado
`InstitutionalAccessService.HasRole(user, roles)` empieza por `if (IsPlatformSuperAdmin(user)) return true`. Este camino concede de forma implícita los cargos de Gran Logia y Taller al Máster incluso en aprobación, autorización, manejo de Secretaría, archivos y datos privados. La prueba histórica `PlatformSuperAdmin_WithOrderScope_HasAdministrativeBypass` afirma esta herencia y debe sustituirse conforme al mandato.

## Entrega propuesta
- Retirar la herencia de roles a Máster en `HasRole`. Los cargos deben reclamarse explícitamente, nunca por ser `platform_superadmin`.
- Conservar capacidad propia de `CanConfigureSystem` para Máster y la administración de cuentas que ya exige `IsPlatformSuperAdmin`.
- Nueva prueba de matriz negativa: Máster sin cargo no puede aprobar traslados, ceremonias, gastos, documentos, publicar insinuados, operar Secretaría, hospitalaria, Tesorería, biblioteca, cargos ni actos logiales. Las pruebas positivas para cargos institucionales genuinos deben permanecer.
- Sin endpoint de «Ver como», escritura por soporte ni cambio de comportamiento en datos: #574 y resto de #604 quedan abiertos hasta permisos acotados, MFA/Keycloak, bootstrap único, UAT e integración controlada.
- Debe evaluarse la compatibilidad de lecturas administrativas de la Orden antes de aceptar; cuando se requiera solo lectura, usar contrato de autorización específico, no `HasRole` genérico.
- Pruebas: xUnit, CI + Showcase + QA Instalable del HEAD exacto. No se infiere prueba física glmdch por CI.

## Estado
Primer commit documental para reclamar PR borrador según GOV-003. Aún no hay cambios de código ni CI; actualizar este archivo tras pruebas/merge para cualquier otra IA.


## Implementación y pruebas (23-10-2026)
- `InstitutionalAccessService.HasRole` solo reconoce el propio rol explícito `platform_superadmin` cuando el usuario es Máster; nunca equivale a `grand_lodge_admin`, `grand_master` o un cargo de Taller. La función `IsPlatformSuperAdmin` sigue habilitando `CanConfigureSystem` y los controles independientes de alta de usuario/superusuario.
- `HasExplicitRole` y `HasOrganizationClaim` deniegan atributos de membresía logial a la cuenta Máster, incluso si Keycloak entregó claims accidentales de Taller o Gran Logia; `CanManageLodgeCouncilSummaryAccess` rechaza la delegación del Venerable.
- Pruebas xUnit reforzadas en `InstitutionalAccessServiceTests`: Máster solo con plataforma + `pmgm_org` erróneo **no** lee fichas de Taller ni firma/aprueba/gestiona pagos, Secretaría, Gran Archivo o otros dominios; caso adversarial Máster con roles logiales adicionales también rechaza; cargos legítimos Venerable/Gran Secretaría conservan funciones con ámbitos válidos.
- **Regresión de seguridad:** esta modificación reduce permisos de lectura concedidos previamente por atajo del Máster. No se simula lectura universal; un módulo de «Ver como» auténtico necesitará política y auditoría específicas bajo #574. Clau debe mantener las vistas de administración, pero no puede usar el rol técnico como carta blanca para leer fichas personales sin auditoría.
- Sin migración ni acceso a credenciales, `main` o despliegues. Verificar triple CI exact-head antes de integrar a `dev` y luego preparar UAT de Keycloak, roles y perfil soporte.

Estado del PR: https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/pull/660 (borrador al registrar). Dev inicial: `5da54b07d5b913efe97e35e84e4fa8fe5ed8bda1`. El cierre de #604 requiere todavía unicidad real, MFA, recuperación y UAT.

### Endurecimiento adicional de identidad incompleta
Si Keycloak entrega el rol `platform_superadmin` sin `pmgm_scope=order`, la cuenta técnica **tampoco** debe heredar un cargo logial si por error vienen otros `pmgm_role` y `pmgm_org`. El nuevo `HasPlatformMasterClaim` detecta el rol técnico aun con scope incompleto: `HasRole` solo reconoce rol plataforma cuando además el scope es Orden; `HasExplicitRole` y `HasOrganizationClaim` siempre rechazan equivalencias logiales para cualquier token de Máster. Se agregó caso adversarial xUnit `MalformedMasterWithoutOrderScope_CannotFallBackToLodgeOrGrandRoles`. No habilita Master sin scope para cuentas/perfiles ni altera roles legítimos. Nuevo HEAD de PR sujeto a CI exacto y conciliación en vivo.
