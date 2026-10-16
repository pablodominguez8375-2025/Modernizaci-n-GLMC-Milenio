# QA glmdch — preparación Silo (Issue #505)

Fecha 15-10-2026, agente ChatGPT; rama `feature/qa-glmdch-silo-20261015-gpt`; base `dev@3425663a367263bc6f27b2791e9c32f11c77f029`. Perfil QA histórico srv01 aún no desplegable: Compose MinIO latest/root API bloqueado por #503 (fusionada en dev). glmdch designado como QA; Docker 29.9.1 y Compose 5.6.0 reportados instalados; sin acceso SSH de esta sesión.

Alcance reclamado: preparar configuración candidata compatible con Silo 20261015 fijado por digest (sin elevar privilegios), pruebas sintéticas y documentación. No solapar archivos piloto reservados por #479 ni workflows/UI/servidor. El usuario pidió avanzar al montaje; la selección formal de proveedor/licencia, backup completo e inspección del servidor son todavía controles de aceptación, no garantías. No desplegar ni levantar stack sin superarlos.

Handoff a completar con HEAD, pruebas CI, listado de archivos, incompatibilidades y decisión Sponsor pendiente. No tocar main.
