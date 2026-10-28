# QA525 — corrección de suministro Silo go-jose 4.1.6

Fecha: 2026-10-28. Agente: ChatGPT/Codex. Estado: reclamo inicial; PR borrador.

- Base observada: `dev@415d86b87f83705ad433f834f0fb46d9586c6565`.
- Problema: `QA Silo supply and recovery` falla cerrado por `CVE-2026-116587`, `github.com/go-jose/go-jose/v4` v4.1.4; versión corregida 4.1.6.
- Fuente pública candidata observada: `pgsty/silo@dd905b29f45d29bcef6aa894acef10f8071eb602`; su `go.mod` declara v4.1.6.
- Alcance: fijar nueva fuente, hashes, build y evidencia; conservar escaneo fail-closed y pruebas reales IAM/recuperación.
- Exclusiones: no tocar glmdch, main, cuentas, secretos, datos, DNS ni Compose activo.
- Estrategia: primera ejecución produce evidencia del binario nuevo; sólo después de revisar y fijar su identidad se repiten gates exact-head. No integrar ni generar release operativa con HIGH/CRITICAL efectivo o evidencia incompleta.

Coordinación: Issue #525, PR #771.
