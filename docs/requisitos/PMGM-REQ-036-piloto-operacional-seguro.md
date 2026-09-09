# PMGM-REQ-036 — Piloto operacional seguro

## Objetivo
Disponer de un perfil instalable para piloto institucional que separe claramente QA de operación real, manteniendo controles de seguridad, identidad y recuperación sin declarar aptitud productiva.

## Requisitos
1. Un único punto de entrada público HTTPS mediante reverse proxy.
2. PostgreSQL, MinIO, ClamAV, API y Keycloak no deben publicar puertos al host.
3. Keycloak debe ejecutarse en modo servidor (`start`), usar PostgreSQL persistente y publicarse bajo `/identity` para no colisionar con `/auth/callback` del SPA.
4. El cliente `pmgm-web` debe usar Authorization Code + PKCE S256 y tener Direct Access Grants deshabilitado.
5. El realm piloto no debe incluir usuarios QA ni contraseñas fijas. El primer administrador se parametriza por variables de entorno y su contraseña inicial es temporal.
6. `DemoData__Enabled` debe ser `false`.
7. Los secretos deben mantenerse fuera del repositorio. El preflight debe rechazar placeholders, HTTP público, secretos débiles/reutilizados y configuraciones localhost no autorizadas.
8. Debe existir smoke HTTPS que valide discovery OIDC, issuer, frontend, salud API/PostgreSQL, versión de API y protección 401 sin token.
9. El respaldo del piloto debe incluir las bases `pmgm` y `pmgm_keycloak`, además del bucket `pmgm-documents`, con manifiesto SHA-256 verificable.
10. La restauración debe requerir confirmación explícita y mantener los servicios públicos detenidos ante una falla parcial.
11. CI debe ejecutar un recovery drill sólo en localhost/entorno controlado y demostrar restauración de negocio, identidad y objetos.

## Privacidad y Ley 21.719
Los respaldos pueden contener datos personales e institucionales. No se versionan; deben protegerse con control de acceso, cifrado y política de retención aprobada. El piloto no crea una nueva finalidad de tratamiento: reproduce los tratamientos ya definidos por los módulos operativos y sus copias de seguridad.

## Fuera de alcance de v0.32
Alta disponibilidad, WAF administrado, KMS/HSM, secretos gestionados externamente, copia off-site/inmutable, monitoreo/SIEM institucional, RPO/RTO aprobados y proceso productivo formal.
