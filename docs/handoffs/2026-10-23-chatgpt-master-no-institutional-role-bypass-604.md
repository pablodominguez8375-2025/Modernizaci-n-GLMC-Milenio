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
