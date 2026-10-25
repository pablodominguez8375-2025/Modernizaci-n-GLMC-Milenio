# PMGM #694 — fase H: prueba de identidad única de cuenta Máster

**Fecha:** 24-10-2026 · **Agente:** ChatGPT · **dev base:** fb3d51ff541a77768facdaedba3e40529d00533e.

## Necesidad
Por decisión del PO (Issue #694) sólo la cuenta Máster `admin` está habilitada a gestionar la carga inicial. `IsPlatformSuperAdmin` acredita rol y ámbito pero no identidad específica. La política E integrada por #704 exige `MasterAccountVerifiedByServer`, y ese valor **no debe recibirse de request JSON ni depender sólo del nombre visible/correo**.

## Alcance
Nuevo `InitialLoadMasterIdentityPolicy` puro y sin endpoints:
- entrada: `ClaimsPrincipal` ya autenticado por el middleware JWT; `IInstitutionalAccessService`; emisor verificado a partir del contexto JWT validado; configuración `MasterSubject` y `MasterIssuer` confiable del servidor.
- exigir autenticación previa, rol plataforma + ámbito `order`, `sub` exacto y único conforme al registro del Máster, `preferred_username=admin`, emisor exacto coincidente y no estar en sesión de impersonación `pmgm_support_operator`. Rechazar configuración incompleta.
- resultado limitado a `Verified` y códigos fijos de rechazo; no exponer subjects, tokens, correos, credenciales, identificadores, contraseñas ni valores de configuración.
- pruebas sintéticas para suplantación vía username, correo, rol, issuer, subject y modo soporte. **No afirmar que está conectado al IdP**.

## Limitaciones y futura integración
No define ni guarda el subject efectivo del Máster, ni instala claves, ni crea usuario o endpoint. El caller futuro está obligado a derivar `trustedIssuer` exclusivamente de un token **validado** por el middleware, nunca del payload/headers del usuario. Los settings `MasterSubject`/`MasterIssuer` deben derivar de configuración/registro autenticado del servidor y revisarse en QA; `MasterAccountVerifiedByServer` de #704 sólo puede poblarse con resultado `Verified`, nunca desde JSON. No habilita aplicación, recepción PII, apertura/cierre o migración. La aprobación Jurídico Ley 21.719 sigue pendiente. UI Claude separada y QA físico sin cambios.

## Archivos
- `backend/src/PMGM.Api/Modules/Bootstrap/InitialLoadMasterIdentityPolicy.cs` (nuevo)
- `backend/tests/PMGM.Api.Tests/InitialLoadMasterIdentityPolicyTests.cs` (nuevo)
- este handoff (nuevo)

Revisar ramas/PR/CI exact HEAD y detrás=0 antes de squash sólo a `dev`; no tocar main.
