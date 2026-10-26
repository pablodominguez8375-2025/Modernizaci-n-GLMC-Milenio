# #604 — Corregir precedencia Docker Compose de Authority en Máster QA
Fecha de revisión: 25-10-2026. Repositorio único, rama desde dev@4d60a37be6d69ba4335a494ab7682fa06972da71; main estable no se toca.
Responsable: ChatGPT backend/infra. Los cambios de UI y perfiles dinámicos pertenecen a Claude (#735 / QA v0.162).

## Hallazgo verificable
En infrastructure/docker-compose.srv01.yml, services.api.environment fija Authentication__Authority a http://keycloak:8080/realms/pmgm. El overlay infrastructure/docker-compose.qa-master.yml solo inyectaba env_file root de Master. Docker Compose define que environment explícito tiene prioridad sobre env_file; así, aunque /etc/pmgm/master-qa.env contenga Authority correcta para QA HTTPS, la API podía conservar el emisor/metadata antiguos sin /identity. La herramienta de bootstrap sí verifica el archivo host pero no la configuración efectiva del contenedor. No se debe activar admin en esas condiciones.

## Solución acotada
Sobrescribir explícitamente Authentication__Authority en el bloque environment del overlay, con URL interna canonizada http://keycloak:8080/identity/realms/pmgm; conservar env_file root-only para InitialLoad__MasterSubject e InitialLoad__MasterIssuer, sin secretos en código. Añadir regression test estático para el contrato de precedencia y anotar que la comprobación del archivo root NO reemplaza la verificación de entorno efectivo dentro de API. El operador en glmdch debe comprobar el montaje correcto sin imprimir credenciales.

## QA físico / UAT
Sin SSH ni canal técnico de despliegue desde conectores; no se cambia glmdch, Keycloak, datos reales, secretos, Main ni puertos públicos. Bloquear activación de admin hasta backup/restauración Keycloak y PostgreSQL, validación de role-mapping y Authority/Issuer/Subject del contenedor real, UAT y cobertura de duplicación/break-glass. #604 y #97 siguen abiertos.

## Bloqueo de exposición acumulada
El Compose base tiene puerto Keycloak administrado por `PMGM_QA_BIND_ADDRESS` y `PMGM_QA_OIDC_PORT` (por defecto 8180), que con la fusión ordinaria de listas seguiría publicado junto a 18180; bajo una variable equivocada podría ser un listener público. El overlay usa `ports: !override`, requiere Docker Compose >= 2.24.4 (glmdch históricamente Compose v5.6.0) y fija SOLO `127.0.0.1:18180:8080`. El operador debe revisar `docker compose config` **solo de forma privada** y verificar el listener resultante, sin enviar valores de configuración o secretos a GitHub/Drive. Si hay 0.0.0.0, [::] o más de un puerto host Keycloak, BLOQUEAR.
Referencia normativa técnica Docker Compose: https://docs.docker.com/reference/compose-file/merge/#replace-value .
