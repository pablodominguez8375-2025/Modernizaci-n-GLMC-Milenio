# #470/#479 — controles independientes de proveedor

Base dev 77557f87d2979b6ef23f636c7c99bec329f70618, main 6dfb9546a4873baff15955cf86abfd7d47e3d111. Issue #97/#470, ChatGPT. Reserva revisada: #479 mantiene Compose/preflight piloto, política IAM y ci.yml; no se modifican. #500 backend agenda, #501 inspector SSH, sin solapamiento con este alcance.

Archivos a trabajar: scripts/check-qa-storage-config.py (nuevo), scripts/preflight-srv01-qa.sh, scripts/verify-srv01-backup.sh, tests/test_qa_storage_guards.py (nuevo), import en tests/test_pilot_keycloak_db_isolation.py, propuesta docs/security/PMGM-SEC-470-storage-decision.md y este handoff. Cierre estimado 15-10-2026. Sin proveedor seleccionado, despliegue, secretos, main ni merges en esta entrega.

Drive diferencial: adenda QA registra preparación previa y checks #501; no hay aprobación de proveedor. Reutilizar IAM/SDK/HTTP/recuperación Silo integrados, no duplicar esos laboratorios. Plan: bloquear configuración QA insegura y validar integridad/path de respaldos independientemente del proveedor; decisión con costo/licencia/suministro y pasos Compose/provisioning/recuperación. Nuevo instalable de CI sobre HEAD exacto, marcado NO DESPLEGABLE hasta configuración/proveedor aprobados.
