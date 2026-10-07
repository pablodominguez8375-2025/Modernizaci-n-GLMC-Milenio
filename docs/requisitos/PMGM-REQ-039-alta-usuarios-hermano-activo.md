# PMGM-REQ-039 — Alta de usuarios desde Hermano activo

Fuente: decisión explícita PO, Issue #362 / PR #363. Extiende el sistema existente sobre dev05d01fba2126581f22487bbcd6efccbb46c476d4; #266 permanece cerrado.

- Toda cuenta institucional nueva exige vínculo obligatorio con un Hermano activo en un Taller vigente. El servidor valida pertenencia, fechas y último estado institucional efectivo; una afiliación marcada activa no habilita a un Hermano inactivo, retirado o fallecido. La regularidad financiera no se convierte en requisito de esta alta.
- Nombre y correo provienen de la ficha del Hermano. El cliente no puede sustituir el destinatario. Sin correo válido no hay alta. El correo será el usuario de acceso.
- El backend genera una clave criptográficamente aleatoria de 24 caracteres. Keycloak almacena una credencial temporal y exige UPDATE_PASSWORD al primer ingreso. La clave se envía exclusivamente al correo registrado mediante SMTP con STARTTLS; no aparece en respuesta API, demo, auditoría ni base institucional.
- La identidad permanece deshabilitada hasta aceptar el envío SMTP y persistir el vínculo. Fallos de envío o activación compensan la identidad y revocan el vínculo si ya fue confirmado. Un envío aceptado por SMTP no acredita recepción/lectura del buzón.
- Sólo el administrador de plataforma de alcance Orden puede crear otra cuenta de plataforma sin Hermano/Taller. La administración institucional no puede usar esta excepción.
- El alta no asigna cargos institucionales. Continúan los perfiles, permisos y asignaciones vigentes; el permiso técnico no crea atribuciones nuevas.
- Recuperación de clave olvidada: Keycloak con resetPasswordAllowed y SMTP configurado. La aplicación no expone ni recupera contraseñas personales.
- Demo Pages: mismo selector, correo de ficha y restricciones con adaptador sintético; envío y credencial explícitamente simulados, sin enviar correos reales.

Aceptación: rechazar vínculo ausente, Taller incorrecto, estado inactivo y sustitución de correo; impedir duplicados y excepción no autorizada; comprobar credencial temporal/cambio obligatorio, vínculo real y compensación ante fallos; conservar privacidad y controles dinámicos de acceso. Instalación y prueba real de SMTP/OIDC en srv01 siguen pendientes por #97.

Registro de datos: [Issue #362](../modelo-datos/cambios/2026-10-07-issue-362-alta-usuarios.md). Operación: [runbook](../operaciones/alta-usuarios-smtp.md).
