# Alta de usuarios y recuperación de clave — operación

Implementación #362 / PR #363. No se han creado cuentas reales ni enviado credenciales reales durante el desarrollo. Despliegue QA pendiente; srv01/UAT #97 pausados. Este runbook prepara configuración, no autoriza levantar la pausa.

## Configuración del servidor

1. En el realm `pmgm`, crear un cliente confidencial `pmgm-account-admin` con autenticación de cliente y service accounts habilitadas. Deshabilitar flujos interactivos y direct access grants del cliente de servicio. Asignar a su service account los roles de `realm-management` necesarios para listar/crear/modificar usuarios y mapear `platform_superadmin`: `view-users`, `manage-users` y `view-realm`. Probar mínimo privilegio en QA; no usar credenciales humanas de administrador.
2. Crear un secreto exclusivo, fuera de GitHub/Pages. Configurar `PMGM_ACCOUNT_CLIENT_SECRET` en el servidor protegido, junto con `PMGM_ACCOUNT_CLIENT_ID`. `PMGM_ACCOUNT_IDENTITY_URL` es la URL base accesible desde API (por ejemplo red interna del contenedor); `PMGM_ACCOUNT_ISSUER` debe coincidir exactamente con el `iss` público del JWT del realm, incluido su path. No confundir Authority interna con issuer público.
3. Configurar `PMGM_ACCOUNT_SMTP_HOST`, `PORT` (587 por defecto), `USER`, `PASSWORD`, `FROM`, `AUTH` y `PMGM_ACCOUNT_LOGIN_URL` (URL HTTPS de acceso). El API exige STARTTLS. El remitente debe estar autorizado por el proveedor SMTP; validar conexión, certificado y envío con buzón ficticio de QA. Los secretos no se ingresan en el formulario de usuarios ni en la demo.
4. En Keycloak, habilitar recuperación de clave y configurar SMTP STARTTLS equivalente. Los realm JSON del instalable incluyen resetPasswordAllowed y referencias a estas variables. **Importar realm al arranque no actualiza automáticamente un realm ya persistido**: comprobar y aplicar la configuración en el realm existente, sin recrearlo ni borrar identidades.
5. En User Profile del realm existente, aplicar el perfil administrativo incluido en los componentes del realm JSON: username/email/nombres editables sólo por admin; atributos pmgm editables y visibles sólo por admin; atributos no gestionados ADMIN_EDIT. Mantener los atributos de administración `pmgm_managed`, `pmgm_account_kind`, `pmgm_member_id`, `pmgm_scope` y `pmgm_org` bajo edición exclusiva de administradores. Los usuarios no deben poder modificar alcance/Taller/identificadores mediante Account Console. Comprobar que los mappers vigentes producen los claims de alcance y Taller esperados.

Sin identidad/correo configurados, el API devuelve 503 y el formulario bloquea el alta. Esta función no usa las preferencias SMTP simuladas históricas de la demo como configuración del servidor.

## Prueba controlada antes de uso real

Con autorización operacional vigente, usar exclusivamente una ficha y un buzón sintéticos: verificar Hermano activo, Taller y correo; crear cuenta; confirmar vínculo Issuer/Subject, recepción del correo, primer acceso exige cambio, segundo acceso acepta clave personal y recuperación olvidada funciona. Confirmar que clave/correo no aparecen en logs/auditoría ni respuestas de alta, salvo correo autorizado en respuesta/formulario. Probar rechazo de retirado, correo inválido, destino manipulado, duplicado y creación de plataforma por administrador institucional.

Una falla muestra alta no completada: revisar correlación de auditoría y estado en Keycloak antes de reintentar. La compensación intenta deshabilitar y eliminar la identidad gestionada; una interrupción prolongada del proveedor puede impedirla y exige conciliación manual. No registrar contraseñas ni cuerpos del proveedor al diagnosticar. El correo ya aceptado no puede retirarse del buzón: la credencial de una cuenta compensada queda inutilizable. El reintento genera una nueva clave.

La vigencia posterior de cuentas existentes y el retiro institucional conservan sus procesos actuales; este incremento regula creación y no introduce desactivación automática futura ni cargos masónicos.
