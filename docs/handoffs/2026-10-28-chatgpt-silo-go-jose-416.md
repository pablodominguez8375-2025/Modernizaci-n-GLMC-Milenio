# QA525 — corrección de suministro Silo go-jose 4.1.6

Fecha: 2026-10-28. Agente: ChatGPT/Codex. Estado: fase 1 implementada en PR #777; identidad binaria todavía pendiente de revisión.

- Base observada: `dev@415d86b87f83705ad433f834f0fb46d9586c6565`.
- Problema: `QA Silo supply and recovery` falla cerrado por `CVE-2026-116587`, `github.com/go-jose/go-jose/v4` v4.1.4; versión corregida 4.1.6.
- Fuente pública candidata observada: `pgsty/silo@dd905b29f45d29bcef6aa894acef10f8071eb602`; su `go.mod` declara v4.1.6.
- Alcance: fijar nueva fuente, hashes, build y evidencia; conservar escaneo fail-closed y pruebas reales IAM/recuperación.
- Exclusiones: no tocar glmdch, main, cuentas, secretos, datos, DNS ni Compose activo.
- Se separó la procedencia de la raíz vendor (`7a3ca0f...`) de la fuente reconstruida (`dd905b2...`): la firma OCI sigue autenticando la raíz conservada, mientras el Git archive y la procedencia del workflow vinculan el servidor nuevo.
- El Dockerfile fija la pseudo-versión `v0.0.0-20261027000322-dd905b29f45d` y el scanner exige exactamente `go-jose/v4 v4.1.6`; 4.1.4, ausencia o duplicación fallan cerrado.
- `SERVER_BINARY=None` es intencional en fase 1. El workflow debe construir y conservar `server-cve-triage.json`, pero bloquear la publicación hasta revisar el hash real del binario y fijarlo en un segundo commit.
- Validación local: `py_compile`, 13 pruebas de reconstrucción y 6 de archivo firmado PASS; `git diff --check` PASS.
- Estrategia: la primera ejecución produce evidencia del binario nuevo; sólo después de revisar y fijar su identidad se repiten gates exact-head. No integrar ni generar release operativa con HIGH/CRITICAL efectivo o evidencia incompleta.

Coordinación: Issue #525, actualización posterior PR #771, implementación PR #777.
