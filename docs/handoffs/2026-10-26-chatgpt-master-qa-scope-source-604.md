# Handoff #604 — bootstrap Máster exige scope institucional en Keycloak (26-10-2026)

**Origen de verdad y concurrencia**: rama creada desde dev@ab2d03259c120e81dfde2040efa5f1e2ef6fcc0c (después del padrón UI v0.164 de Claude); main protegida e intacta. PR #736 de hardening Compose y PR #737 de operadores QA siguen aparte, sin solapamiento de archivos. Esta entrega solo modifica el script fuente de bootstrap y sus tests.

## Causa
`InstitutionalAccessService.IsPlatformSuperAdmin` requiere tanto el rol realm `platform_superadmin` como el claim institucional `pmgm_scope=order` para aplicar permisos globales. El mapper del cliente Keycloak lee el atributo `pmgm_scope` del usuario, pero el bootstrap original `prepare-master-qa.py` creaba `admin` con solo el marcador `pmgm_master_qa_bootstrap`. Esto podía dejar una cuenta única correctamente provisionada en Keycloak pero sin derechos de plataforma en API.

## Reparación
- El `POST /users` solo QA prepara admin inicialmente deshabilitado con los atributos exactos `pmgm_scope: ["order"]` y `pmgm_master_qa_bootstrap: ["issue-604"]`.
- `require_master_scope` lee el usuario **completo** desde Keycloak Admin API (el listado paginado no contiene necesariamente attributes). Verifica ID, username, scope y marcador, antes de guardar subject/otorgar rol y al completar prepare. También impide activate y status si hay drift; error fijo `BLOCKED MASTER_SCOPE_NOT_PROVISIONED`, sin PII.
- Las pruebas sintéticas verifican registro exacto, ausencia de rol o escritura adicional si Keycloak pierde el atributo, activación denegada y bloqueo de estado al manipular scope/marcador.

## QA físico y compatibilidad
El operador de #737 incluye helper local `master-qa-scope-local.py` para una cuenta **ya creada** y deshabilitada en el corte 4d60a37. Esta PR NO ejecuta reparación de cuentas existentes, NO asigna roles remotos y NO edita el helper de #737. Para cualquier cuenta ya creada sin scope, el cambio al script fuente no la repara retroactivamente; intervención protegida local con inventario, copia/restore y evidencia readback sigue obligatoria.

No afirmar UI Master funcional ni cerrar #604: falta issuer/claim en JWT reautenticado, mapper Keycloak, permisos Sistema en API, identidad única, denegaciones y QA/UAT físico. No reiniciar host, no credenciales, no cambios de producción ni de main. Registro espejo en GitHub #604 y Drive Estado ejecutivo.
