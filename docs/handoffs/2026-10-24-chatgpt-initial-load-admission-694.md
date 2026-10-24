# #694 fase E — autorización de transición de carga inicial (sin ejecutor)

Fecha 24-10-2026 · Agente ChatGPT · base `dev@b3c4b9369b010d31b3e44eed666918d51f34a65a`.

## Estado y propósito

PR #698 A (plantillas), #700 B (prevalidación), #702 C (forma CSV) y #703 D (vista previa) están integradas SOLO a dev con tres gates por PR. Ninguna está habilitada para tratar nóminas reales en QA. El siguiente paso es formalizar la política de autorización antes de diseñar transacciones de carga. No se interpretan los resultados de un plan estático como consentimiento ni como permisos de ejecución.

## Alcance fase E

- Implementar un **evaluador puro de condiciones de admisión** `InitialLoadAdmissionPolicy` con ventana NotOpened/Open/Closed y operaciones RequestOpen, Preview, RequestApply, RequestClose.
- Para toda operación, requerir `IInstitutionalAccessService.IsPlatformSuperAdmin` (rol plataforma + ámbito `order`) Y evidencia de identidad **de la cuenta única Máster `admin`** (`MasterAccountVerifiedByServer`, resuelta desde registro confiable del servidor). El rol técnico por sí solo nunca habilita apertura, preview o carga; superadmins de soporte sin identidad Máster deben fallar cerrado. Prohibir que perfiles de Gran Secretaría, Taller, soporte o claims combinados hereden escritura.
- Apertura sólo desde NotOpened (NO reabrir Closed), sujeto a constancia de aprobación legal verificada por el servidor, fundamento auditado y medio para auditoría inmutable; cierre sólo desde Open, con motivo, auditoría y cero transacciones pendientes.
- Preview del contexto de carga real sólo mientras Open y legal aprobado; Apply exige además vista previa válida/reciente, idempotencia, transacción y reversión disponibles. Sin ellos, fallar cerrado. **Incluso cuando todos se cumplan, CanApply=false y WritesPerformed=0 incondicionalmente en esta fase**, porque no hay ejecutor HTTP/DB.
- Devolver sólo códigos fijos, estado propuesto y flags no operativos. Nunca pasar al resultado identidad, motivos ni valores CSV. No almacenar ni registrar datos.
- Tests xUnit: Máster solo con ámbito order; rol ajeno; cierre irreversible; fail-closed de aprobación legal/auditoría/preview y requisitos contables; no eco de secretos.
- Archivos nuevos exclusivamente `backend/src/PMGM.Api/Modules/Bootstrap/InitialLoadAdmissionPolicy.cs`, `backend/tests/PMGM.Api.Tests/InitialLoadAdmissionPolicyTests.cs` y este handoff. Sin migraciones ni Program.cs, frontend o modificaciones a ramas ajenas.

## Restricciones

No crea estado persistido, aprobación jurídica auténtica, permisos para recibir PII ni función apply. Los flags de evidencia en una futura ruta deben obtenerse de fuentes confiables del **servidor**, nunca del JSON del cliente; el motivo auditado debe persistirse y registrarse por una operación futura transaccional. No habilitar cargador de nómina ni datos reales en QA sin aprobación jurídico-legal Ley 21.719 y arquitectura auditada. Issue #694 queda abierto. PR draft con CI/Showcase/QA Installable exact-head antes de squash únicamente a dev; no promocionar main, no instalar glmdch.

## Continuidad

Registrar avances y blockers en GitHub Issue #694, PR y Línea Base Drive. Tras fase E, pendientes: modelo DB de sesión de carga y snapshot, autorizaciones verificadas, plan/apply serializable idempotente y reversible, cierre explicitamente auditado, edición del Taller por Secretaría propia / Gran Secretaría, UI de Claude y UAT real.
