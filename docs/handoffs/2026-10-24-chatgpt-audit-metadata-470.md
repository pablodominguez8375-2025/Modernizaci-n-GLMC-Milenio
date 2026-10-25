# #470 P0 — sanitización defensiva de metadatos de auditoría

24-10-2026 Chile. ChatGPT. Base de trabajo `dev@3c599bd22f35dc47de55490245be51331e408064`. Este cambio acotado **no reemplaza** los pendientes principales #470 de privilegios DB separados, proxy IP real y credenciales MinIO. Complementa el trigger append-only ya presente.

**Hallazgo reproducible por lectura de código:** `AuditMetadataSanitizer.ForbiddenKeys` bloquea Email/MemberId/Content y otros campos, pero no bloquea denominaciones comunes como `rut`, `access_token`, `password`, `api_key`, `phone`, `passport` ni sus variantes con separadores. Riesgo: una acción futura puede adjuntar esos campos a MetadataJson y dejar rastro sensible persistente pese al trigger append-only.

**Alcance reservado:** `backend/src/PMGM.Api/Modules/Audit/AuditService.cs` (solo clasificador de claves; no cambiar el evento ni endpoints), `backend/tests/PMGM.Api.Tests/Audit/AuditMetadataSanitizerTests.cs` (casos recursivos sintéticos, nombres variantes y metadatos técnicos preservados), y este handoff. No modificar estructuras, contratos, migraciones ni logs existentes. No reescribir historial; datos ya persistidos requieren revisión restringida por operador autorizado fuera del alcance.

Regla: eliminar pares completos cuando la **clave** normalizada sea sensible o un secreto. No inspeccionar valores arbitrarios o inferir cumplimiento total: nombres desconocidos y valores libres bajo claves permitidas siguen riesgos que requieren política/minimización de emisores. Registrar que el servicio auditor no decide licitud jurídica; se limita a proteger y auditar. Mantener #470 abierto, QA real no instalado, sin main.

## Registro de datos PMGM-GOV-004
- [Cambio de saneamiento MetadataJson #470](../modelo-datos/registros/2026-10-24-chatgpt-audit-metadata-sanitizer-470.md). Sin migración; contracto de emisión de auditoría más restrictivo.

24-10 noche: PR #723 preflight de marca integrado solo dev@c132891d98475b4d313e6d9790c00baf292b0880. Rebase seguro de este PR por árbol de dev vivo, preservando archivos QA de #723 y permisos UI #722 Claude. Los gates anteriores NO se reutilizan: comprobar nuevos 3/3 exact HEAD.
