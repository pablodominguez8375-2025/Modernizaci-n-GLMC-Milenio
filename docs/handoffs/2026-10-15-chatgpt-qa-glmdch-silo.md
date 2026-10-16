# QA glmdch — preparación Silo (Issue #505)

Fecha 15-10-2026, agente ChatGPT; rama `feature/qa-glmdch-silo-20261015-gpt`; base `dev@3425663a367263bc6f27b2791e9c32f11c77f029`. Perfil QA histórico srv01 aún no desplegable: Compose MinIO latest/root API bloqueado por #503 (fusionada en dev). glmdch designado como QA; Docker 29.9.1 y Compose 5.6.0 reportados instalados; sin acceso SSH de esta sesión.

Alcance reclamado: preparar configuración candidata compatible con Silo 20261015 fijado por digest (sin elevar privilegios), pruebas sintéticas y documentación. No solapar archivos piloto reservados por #479 ni workflows/UI/servidor. El usuario pidió avanzar al montaje; la selección formal de proveedor/licencia, backup completo e inspección del servidor son todavía controles de aceptación, no garantías. No desplegar ni levantar stack sin superarlos.

Handoff a completar con HEAD, pruebas CI, listado de archivos, incompatibilidades y decisión Sponsor pendiente. No tocar main.

## Estado de continuación y decisión de proveedor — 16-10-2026
- Decisión Sponsor registrada en Issue #97 comentario 6207122522 y adenda Drive: Silo autogestionado APROBADO sólo como candidato QA en glmdch con datos sintéticos. Requiere verificación de manifest/digest linux-amd64, SBOM/provenance/avisos, licenciamiento y pruebas de recuperación antes de despliegue; NO aprobado para producción.
- PR de trabajo #506 DRAFT. Cambios QA: Docker Compose Silo 20261015 por digest; administración y API documental/backup con identidades diferentes, policy S3 limitada, script de provisioning fail-closed; preflight de contraseñas/identidades/bind loopback/RFC1918; backup S3 sin root, restore sobre bucket comprobado vacío sin mirror --remove y error de ListBucket implica bloqueo; tests sintéticos y runbook docs/installation/QA-GLMDCH-SILO-CANDIDATE.md.
- La protección previa PR #503 fue integrada sólo a dev@3425663a367263bc6f27b2791e9c32f11c77f029. HEAD dev vivo posterior observado 2ffc99ab85fa7095ec4a35b35bae2bc584038448; se preservará el frontend añadido por Claude en cualquier rebase, sin tocar main.
- El perfil QA aún usa Keycloak/pmgm_app compartido en la DB; falta adaptar BD2 mínimo privilegio a QA y probar login OIDC. El mirror de objetos no valida versionado, Object Lock, metadatos/IAM ni respaldo coherente externo. No afirmar backup operativo completo.
- Último CI completo antes de commit reciente: sobre SHA eb329932ee3f9aaf7ad8391d6ba2f8d9963ffae9 Showcase 38844061309 y QA Installable 38844061290 SUCCESS; PMGM CI 38844061284 IN_PROGRESS al corte. Commits posteriores invalidan gates anteriores; comprobar CI+Showcase+QA exact HEAD final. La fixture tests/test_qa_storage_guards.py mantiene credenciales MinIO históricas y requiere ajuste/validación; una edición de esa fixture fue rechazada por el conector.
- Sin comandos SSH, cuentas, credenciales, contenedores, servicios, firewall, datos o instalaciones en glmdch ejecutados por ChatGPT desde este chat. Lo entregado es PR candidata, no QA montado. Issue #505 abierto y #97 seguimiento físico.
- Registro gemelo en Drive: https://docs.google.com/document/d/1I9bcJ7lKH4T2iQvxafY63HbOBr0ZsGZ8lDX3tSe2UfM ; registro PR #506 comentario 6207177980.


## Continuación de adaptación aprobada y verificación independiente — 16-10-2026

Se continuó la misma PR #506 y se preservaron commits concurrentes hasta 9dc8e94 con lease. Silo sólo QA aprobado, sin producción/main. 22 tests locales PASS; nuevo workflow dedicado ejecutó Docker 29.9.1/Compose 5.6.0 y el provisioning/IAM/backup/restore reales: 1/1 PASS. Ver informe docs/security/PMGM-SEC-470-silo-supply-validation.md y runbook actualizado. Índice, manifest amd64, config, LICENSE y attestation del publicador verificadas. **Trivy 0.76.0 detectó 11 HIGH y 0 CRITICAL en la imagen clásica fijada; gate BLOQUEADO, sin excepción ni despliegue propuesto.** Las dependencias PCRE2/Go/mcli y la coincidencia del módulo MinIO/fork se detallan en el informe. Se requiere artefacto corregido y repetir gates/instalable. No se abrió otra implementación ni se tocó reserva piloto #479, servidor o secretos institucionales.

La evidencia de cada HEAD final (CI, paquete, informe security) se registra en PR/#97/Drive; no atribuir al código posterior los resultados del run inicial b03e295. Recuperación actual lógica de QA no acredita versionado/retención/IAM/KMS completo ni respaldo cifrado externo.
