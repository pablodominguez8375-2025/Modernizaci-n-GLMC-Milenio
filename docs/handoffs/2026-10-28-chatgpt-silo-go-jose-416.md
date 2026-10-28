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

## Fase 2 — revisión independiente del binario (28-10-2026)

Artefacto GitHub Actions revisado: run 40738446846, ZIP `silo-rebuild-review-a6d5bfce09ee3062d9f4c3c4013e2ea1d48671ea`, artifact ID **12838117399**. Se descargó y analizó el ZIP, sin introducir su imagen en QA físico.

Evidencia cotejada directamente contra los bytes archivados:

- `silo-qa-review-only.tar`: SHA-256 `7f58487d2fb24e2a853e1c5ef14f50e4ad06cd76e3ad2308b62696df59e46c11`, coincide con `rebuild-report.json`.
- OCI image config `sha256:a641da39c353934dbf62c9cf363ea27c80528f4fd7d20fc4a89886d0e4ebfd84`; layer `sha256:d630b11a28497b3663ca68d852c8160bdab5e13f10536baff6f05c1fb0e662e4`.
- Archivo real `/usr/bin/silo` dentro de la capa OCI: SHA-256 **`08f5df008efad6c1664e931a48a06b6e0ed9a09e0629338b774e8af07120d369`**, cotejado por lectura del binario y cálculo SHA-256 independiente, no extraído de un informe de CI.
- Archivo real `/usr/bin/mcli`: SHA-256 `30b88e6f289ddb589f3b36e48be825f0c57ae863fa950afff534a083ccb393a1`. Ambos coinciden con `runtime-binaries-and-rpms.txt`.
- `trivy-raw.json`, `sbom.cdx.json` y `runtime-binaries-and-rpms.txt`: hashes recalculados y coincidentes con los del informe. Trivy observó `go-jose/v4 4.1.6` y una CVE HIGH residual `CVE-2026-39414` en `github.com/minio/minio`.
- Se revisó la fuente upstream del fix `minio/minio@3252d5b7f349e0ea41987761e4487972ea977e15`: endurece S3 Select para imponer límites de tamaño y devolver `OverMaxRecordSize`. El gate de procedencia documenta su herencia en `pgsty/silo@dd905b29f45d29bcef6aa894acef10f8071eb602`; la prueba real de binario rechazó registros CSV y JSON Lines sobredimensionados.
- Pruebas de integración registradas en el mismo artefacto: IAM, recuperación de objetos, SDK .NET y rechazo del registro sobredimensionado, **4/4 PASS**.

Resultado: queda fijada en `SERVER_BINARY` la identidad del componente realmente revisado; sigue activo el chequeo estricto de imagen, procedencia, raw scan, VEX limitado al componente vinculado y bloqueo por cualquier otro HIGH/CRITICAL. Esta declaración VEX es **del proyecto, no del proveedor**, y el paquete sigue siendo **review-only**. La Advisory pública de MinIO no lista versión Go OSS parcheada; el proyecto debe demostrar la corrección de su *fork* por código, identidad binaria y pruebas. No afirmar eliminación de CVE en el ecosistema ni seguridad de otros módulos.

**Pendiente obligatorio:** ejecutar GitHub Actions sobre el HEAD nuevo del PR #777, revisar la evidencia de `trivy-reviewed.json` y el resultado final `CANDIDATE_VALIDATED_NOT_DEPLOYED` (si se alcanza). No fusionar ni crear `qa-signed-<SHA>` con vulnerabilidades efectivas o gates incompletos. No tocar glmdch, `main` ni cuentas reales. Revisar licenciamiento AGPL separado antes de uso institucional.
