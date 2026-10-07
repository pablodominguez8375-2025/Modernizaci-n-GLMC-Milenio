# GOV-004 — Issue #362: alta de usuarios vinculada a Hermano activo

## Identificación y alcance

Decisión PO explícita; Issue #362, PR #363, rama feature/active-member-accounts-20261007-gpt; base dev05d01fba2126581f22487bbcd6efccbb46c476d4. Autor ChatGPT. Registro previo: cierre de #266 y su modelo de acceso dinámico. Este corte afecta cuentas, vínculo de identidad, contratos, SMTP y auditoría; conserva membresía, cargos, finanzas y perfiles institucionales. Implementación en PR; SHA final y evidencias exactas se incorporan al recibo del handoff/Issue. No atribuye schema a una instalación física.

## Diccionario del alcance

| Entidad / campo | Tipo, tamaño, nulabilidad / default | Regla y finalidad | Clasificación / persistencia |
|---|---|---|---|
| CreateUserAccountRequest.MemberId, OrganizationId | JSON UUID nullable; sin default | Obligatorios para cuenta institucional; sólo relación activa efectiva en organización workshop | restricted; contrato, no nuevo campo SQL |
| PlatformAdministrator | JSON boolean, false | Excepción sólo platform_superadmin de alcance order | internal; contrato |
| PlatformName, PlatformEmail | JSON string nullable; nombre máximo 200, correo máximo 320 | Sólo excepción plataforma; nombre no vacío y dirección simple válida, sin display name ni encabezados | confidential; contrato/identidad externa |
| AccountCandidate.MemberId / OrganizationId | JSON UUID no nulo | Derivados de Membership vigente y estado institucional más reciente | restricted; derivado |
| AccountCandidate.Name / Email / Workshop | JSON string no nulo | Person.FirstNames + LastNames / Person.Email / Organization.Name; correo válido obligatorio | confidential; derivado sin copiar ficha a nueva tabla |
| AccountIdentity.Subject / Issuer | Identidad externa string / URI | Subject generado por Keycloak; Issuer exacto del JWT configurado | restricted; vínculo SQL existente |
| AccountUser.Subject, Name, Email, Enabled, Kind | JSON string/string/string/bool/string | Listado sólo cuentas gestionadas, kind member o platform | restricted/confidential/internal; no proyección pública |
| Clave inicial | String efímero 24 caracteres no nulo | RandomNumberGenerator, clases de caracteres y mezcla segura; sólo credencial temporal Keycloak y correo STARTTLS | restricted; nunca base institucional, API ni auditoría; descartar referencia después del envío |
| pmgm_managed / account_kind / member_id / scope / org | Keycloak atributos string[] | active-member-accounts; member/platform; UUID o vacío en excepción; organization/order y UUID de Taller | restricted; edición administrativa exclusiva; claims institucionales conservados |
| Respuesta creación | subject, email, memberId?, organizationId?, platformAdministrator, requiresPasswordChange, initialPasswordEmailSent | 201 después de enviar, persistir y activar; cambio obligatorio true; no clave en respuesta | restricted/confidential; sólo administrador autorizado, no-store |
| Configuración SMTP / cliente OIDC | Host/URL/puerto/bool/string; secretos vacíos de ejemplo | Configuración protegida de servidor; no expuesta en frontend; STARTTLS y login HTTPS | internal endpoints; secretos restricted sin logs/exportación |
| AuditEvent.Action / MetadataJson | Strings según modelo existente | system.users.prepared/created/creation_failed; MemberId, excepción, flags o razón genérica; sin correo/clave/cuerpo proveedor | restricted vínculo institucional; auditoría privilegiada existente |

## Estructuras y relaciones existentes

**Sin migración ni nueva tabla.** Se usa core.member_identity_links de migración 20260909005000_AddLibraryDegreeSecurity:

| Campo SQL | Tipo / nulabilidad / default | Restricción |
|---|---|---|
| Id | uuid no nulo, generado por API | PK |
| MemberId | uuid no nulo | FK core.members.Id; borrado RESTRICT |
| Issuer | varchar(500) no nulo | issuer configurado |
| Subject | varchar(320) no nulo | sujeto Keycloak |
| CreatedAtUtc | timestamptz no nulo, API UTC | fecha creación |
| CreatedBySubject | varchar(320) no nulo | actor autenticado |
| RevokedAtUtc | timestamptz nullable, null inicial | revocación/compensación |

Unique parcial (Issuer, Subject) WHERE RevokedAtUtc IS NULL; índice (MemberId, RevokedAtUtc). Un Hermano puede tener vínculos históricos; el servicio impide alta nueva con vínculo activo incompatible, sin modificar índices existentes. Cuenta plataforma no inserta vínculo de Hermano. Member→Person conserva FK y 1:1; Member→Membership es 1:N; Membership→Organization N:1. Keycloak es relación lógica externa, sin FK SQL.

## Antes y después

| Elemento | Antes | Después / impacto |
|---|---|---|
| Alta UI | Usuarios en estado local ficticio con nombre/correo/clave manual | API real o adaptador demo; selector de Hermano/Taller; correo ficha no editable; sin clave manual |
| Condición de alta | No validación servidor de cuenta | Afiliación activa fechada y último estado efectivo active/reinstated; retirado/inactivo/fallecido rechazado; legacy sin evento usa afiliación activa vigente |
| Identidad | Vínculo existente para resolver Hermano, no alta | Reutiliza vínculo; identidad deshabilitada, credencial temporal, correo, commit, revalidación y activación; compensación ante error |
| Excepción | Sin excepción controlada de alta | Sólo plataforma/order; rol técnico platform_superadmin, sin inventar cargos |
| Recuperación | resetPasswordAllowed false en realm distribuido | true y SMTP por variables; realm ya instalado exige configuración explícita, sin recreación automática |

No backfill ni modificación masiva de cuentas existentes. Locks transaccionales por Hermano/correo y aislamiento Serializable reducen duplicados; Keycloak conserva unicidad de username/correo. Reintento sólo de identidad deshabilitada propiedad de este flujo con mismo Hermano/tipo/correo; actualiza Taller/claims desde ficha vigente. No reemplaza cuentas ajenas ni activas.

Permisos: CanConfigureSystem más filtros dinámicos system:view/create vigentes; la excepción además exige IsPlatformSuperAdmin. La cuenta ordinaria no recibe cargo ni realm role institucional. Exportaciones/reportes existentes sin cambio; listado privado con no-store. Clasificación en catálogo de seguridad; no se define nuevo plazo legal: vínculo conserva RET-IDENTITY y auditoría RET-AUDIT existentes; credencial inicial es efímera en aplicación y credencial hasheada gestionada por Keycloak. Retención del mensaje en buzón/proveedor depende de operación y revisión legal pendientes.

Rollback: revertir incremento deshabilita nueva alta; no borrar vínculos/cuentas existentes en bloque. Conciliar manualmente fallos de compensación del proveedor por correlación, sin secretos en diagnóstico. No hay SQL Down ni transformación de datos.

## Evidencia y control

Pruebas nuevas: password aleatoria/clases, dirección simple, representación Keycloak deshabilitada/credencial temporal/sin cargos; HTTP PostgreSQL con cuenta vinculada, correo ficha, duplicados, retirados, plataforma no autorizada y fallos SMTP/activación. Demo valida selección/correo/roles con datos sintéticos; nunca envía credenciales. CI/Showcase/QA exact-head pendientes en este registro preintegración; recibo final en handoff/Issue debe completar resultado y SHA. Drive revisado recursivamente sin novedades normativas desde cierre #266; decisión PO registrada, lectura de retorno y cierre pendientes.

Main congelado. Despliegue QA pendiente, srv01/QA física/UAT #97 pausados. No afirmar operación SMTP real ni recuperación probada en instalación por estos tests.
