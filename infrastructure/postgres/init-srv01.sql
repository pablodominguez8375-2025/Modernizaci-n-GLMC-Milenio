-- Proyecto Centenario — PostgreSQL QA srv01
-- Se ejecuta sólo al inicializar un volumen PostgreSQL vacío.

SELECT 'CREATE DATABASE pmgm_keycloak OWNER pmgm_app'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'pmgm_keycloak')\gexec
