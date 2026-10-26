# #604 — QA Master bootstrap controlado / 25-10-2026

Autorización Sponsor expresa: finalizar Máster único admin en QA operativo glmdch. Solo QA; MFA opcional temporal en QA y obligatorio en producción.

Base: dev 2e03a3de2674b7f874e6234e0cac5cf039468765; main 6dfb9546a4873baff15955cf86abfd7d47e3d111, intacta.

PR reservada por ChatGPT: script de provisión QA seguro, pruebas sintéticas y esta guía; no UI, servidores, migraciones, main, ni secretos. Los conectores de esta sesión NO permiten acceder al host por SSH.

Contrato: operador root en host glmdch, token Keycloak y clave temporal en archivos root privados; origin HTTPS, issuer exacto; bloqueo local de ejecución única; DENEGAR si existe qa.admin u otro titular efectivo del rol platform_superadmin; no adoptar, transferir ni elevar cuentas existentes; ninguna contraseña en stdout/Git; errores con códigos sin PII. Fase preparación crea identidad admin deshabilitada, y solo se activa tras registro en host/API y comprobación de permisos.

QA físico y UAT: PENDIENTES de evidencia, no inferir de CI. Registrar InitialLoad:MasterSubject, InitialLoad:MasterIssuer y Authentication:Authority en configuración segura de la API, con respaldo/rollback y release firmado idéntico SHA. Si el realm trae qa.admin con rol global, el procedimiento BLOQUEA: exige migración revisada, nunca borrar automáticamente.

Gates por verificar: CI/Showcase/QA instalable exact-head. Sin cerrar #604 hasta UAT autenticado, garantía efectiva de exclusión de segundo Máster y recuperación excepcional.

## Código preparado y orden de operación QA

Archivos del PR: scripts/qa/prepare-master-qa.py, tests/test_prepare_master_qa.py, infraestructura de realm QA sintético (dos JSON), tests/test_qa_g8_contract.py, tests/test_keycloak_realm_hardening.py, infrastructure/docker-compose.qa-master.yml. El script exige UID 0, hostname glmdch, TLS/issuer canónico, token root privado, flock local y prueba de respaldo registrada privadamente en formato sha256:<digest>. Un recibo no prueba por sí mismo que se restauró el respaldo: el operador debe verificar su restaurabilidad antes de cualquier cambio. Solo acepta hostname QA; nunca producción.

Fuente Keycloak del realm esperado: pmgm. Endpoint público de gestión detrás del proxy: https://sistema.granlogiamixta.cl/identity ; issuer exacto https://sistema.granlogiamixta.cl/identity/realms/pmgm. Usar únicamente token de administrador de identidad QA con el mínimo permiso compatible; token/clave nunca CLI argv, chat, Drive, logs o Git. Rutas host root privadas recomendadas: /etc/pmgm/master-qa-token (0600), /etc/pmgm/master-qa-password (0600), /etc/pmgm/master-qa-backup-receipt (0600), /etc/pmgm/master-qa-subject (nuevo 0600), /etc/pmgm/master-qa.env (0600). Verificar propietarios, rutas padre y respaldos fuera del host antes de comenzar.

1. Auditar estado real de glmdch, backups PostgreSQL/Keycloak y permisos; verificar SHA instalado. Si existe el fixture conocido qa.admin (UUID aa820001-0000-4000-8000-000000000001) con rol Master, utilizar modo retire-legacy SOLO tras respaldo y comprobar que los demás roles y el usuario quedaron intactos. Ninguna migración se ejecuta implícitamente, cualquier ID/perfil distinto bloquea.
2. Ejecutar modo plan del script obtenido del SHA revisado. En realm ya limpio => PASS EMPTY_REALM_MASTER_SLOT_READY_FOR_STAGED_PREPARATION. Modo prepare, con archivos protegidos + recibo, crea cuenta admin DESHABILITADA con contraseña temporal; deja sujeto en archivo exclusivo 0600; si hay error, parar y hacer recuperación supervisada, NUNCA reintentar a ciegas ni borrar cuentas automáticamente.
3. Preparar /etc/pmgm/master-qa.env, modo 0600, con:
   InitialLoad__MasterSubject=<valor de archivo protegido subject>
   InitialLoad__MasterIssuer=https://sistema.granlogiamixta.cl/identity/realms/pmgm
   Authentication__Authority=http://keycloak:8080/identity/realms/pmgm
   Los corchetes en esta documentación son marcadores, NO valores reales.
   En /etc/pmgm/srv01.env (raíz privada) fijar PMGM_QA_MASTER_APP_ENV_FILE=/etc/pmgm/master-qa.env. Al levantar API usar infrastructure/docker-compose.qa-master.yml como ULTIMO -f overlay; solo el contenedor API recibe el archivo. Validar docker compose config/entorno efectivo con redacción de secretos, no publicar salida cruda. Conservar Authority interna actual salvo verificación contraria, comprobar que el issuer del JWT validado coincide exactamente con MasterIssuer.
4. Instalar únicamente paquete QA firmado del MISMO SHA integrado dev, con backups, migraciones, instalación reversible y pruebas de host #97/#525. Reiniciar API de forma controlada tras agregar overlay y comprobar configuración efectiva.
5. Con la API configurada, ejecutar modo activate; activa la cuenta ÚNICA admin solo cuando la instantánea Keycloak y archivo protegido concuerdan. Modo status posteriormente. El verificador de unicidad es FOTO, no impide a otros administradores externos manipular Keycloak: controlar sus permisos de escritura en el realm, restringir role-mappings y registrar eventos.
6. UAT físico autenticado: login/logout admin y cambio de clave temporal; sin ficha/Taller/cargo; menús Sistema; crear Usuario y Superusuario acotado; denegar segundo Master/cambios PlatformAdministrator e importación; permisos soporte Ver como, conceder/revocar corrección (Master autoriza, superusuario modifica), recuperación #556; auditoría, avisos; sin SMTP real afirmar correo enviado. Acreditar pruebas negativas concurrentes y que backend rechaza un segundo Master. MFA no obligatorio en QA por excepción Sponsor, obligatorio en producción.
7. Registrar estado exacto API/web/DB/Keycloak, SHA, gates, backups/restauración, comprobación por rol y resultados UAT SIN sujetos reales, contraseñas ni valores de configuración privados. Si falla algún paso, cuenta deshabilitada y restauración controlada; no declarar QA operativo hasta 26/26 checklist / gates aplicables.

**Bloqueo de esta sesión:** los conectores disponibles permiten GitHub/Drive, pero no SSH a glmdch ni acceso a Keycloak real, por lo que no pueden generarse secretos, realizar backups físicos, ejecutar los modos contra el realm ni acreditar UAT. La autorización Sponsor ya existe; falta canal técnico y evidencia real, NO nueva aprobación. Main y producción no se modifican.

Nota de conectividad: el overlay liga la administración Keycloak SOLO a 127.0.0.1:18180:8080 en glmdch; no abrir puerto 18180 en firewall/NAT. La utilidad acepta exclusivamente http://127.0.0.1:18180/identity (loopback), con issuer externo HTTPS inmutable. Verificar compose final y listener real antes de transmitir credenciales. 

## Precisión posintegración #604 — precedencia Docker Compose (PR #736)
En services.api del Compose base, Authentication__Authority ya está declarada en environment. Por especificación de Compose, environment toma precedencia sobre env_file incluso si este último se carga desde /etc/pmgm/master-qa.env; el chequeo previo del archivo protegido no garantiza que el contenedor utilice el valor esperado. El overlay QA fija explícitamente Authentication__Authority=http://keycloak:8080/identity/realms/pmgm en environment. Las variables InitialLoad__MasterSubject y InitialLoad__MasterIssuer continúan en el env_file de root, no en el repositorio.

ANTES de habilitar admin, inspeccionar dentro del contenedor API la presencia y coherencia de la Authority efectiva, el issuer y el subject, sin imprimir sus valores, contraseñas, tokens ni salidas crudas de docker compose config a GitHub/Drive. Una validación exitosa del archivo local o del script prepare-master-qa.py no acredita por sí sola la configuración de runtime. Si hay drift, mantener admin deshabilitado. Documentar únicamente PASS/BLOCKED.
