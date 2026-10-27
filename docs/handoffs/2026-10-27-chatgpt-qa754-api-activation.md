# QA #754 — activación de identidad API, borrador supervisado (27-10-2026)

Issue: #754 (asignado agente:chatgpt). Rama desde dev@731fa29ee30a18a13c50b5a03b5847746242313f; main@6dfb9546a4873baff15955cf86abfd7d47e3d111 protegida.

Objetivo: preparar operador de despliegue **exclusivo de API QA glmdch**, con chequeo de reversión y G6 cifrado externo verificado. SMTP Google de prueba RECIBIDO; copia cifrada G6 ID Drive 15UgqmcXRu4RR5ypE1cIu51HZgh7EqHyU, SHA256 e4184cb798e3b44414c09d85735db6e6b2002e75027de67da8ed5b930a7ca8a6 verificado. Stage secreto root0600 existente, no exportar.

Trabajo reservado: scripts/qa/operators/pmgm-qa754-api-identity-activate.py; tests/test_qa754_api_identity_activate.py; este handoff. Se entregará operador primero en modo read-only y activación física solo por operador autorizado bajo confirmación explícita. No hay QA físico ejecutado por IA. No tocar otros servicios, DB, Keycloak, WEB, Máster, SMTP, Taller Libertad23 ni main. No afirmar DR/restore independiente PostgreSQL+Silo, UAT completa o SMTP reset Keycloak (pendientes).

Pruebas y estado de integración: pendientes de actualización de este borrador. En PR draft, no fusionar antes de gates exact-head SUCCESS CI/Showcase/QA Installable.
