-- Base separada para Keycloak en el perfil piloto.
-- No contiene secretos y se ejecuta sólo al inicializar un volumen PostgreSQL nuevo.
CREATE DATABASE pmgm_keycloak OWNER pmgm_app;
