# QA525 — Continuidad de hardening root (1 noviembre 2026)

Trabajo ChatGPT en PR #834 (DRAFT). Inicio: dev f457628d7ecd185bde5408f6324725ec57372bfb, main 6dfb9546a4873baff15955cf86abfd7d47e3d111. API/WEB QA físico en 41d9c490, sin despliegue.

Se corrigió uso de la herramienta de atestación por búsqueda de PATH: preparador QA525 exige /usr/bin/gh root-safe y entorno mínimo (e3a0b468). Se exige Python aislado y sin carga de site para preparador e instalador (116086e2, 6090bc1e). Tests adversariales 07bd18eb, ddac1c64, 6d5b3de. CI detectó que la prueba de compatibilidad se ejecutaba en Python normal y falló; la prueba se corrigió sin relajar producción en bc497aca. Offline reviewer CI exact-head 1898aad1: SUCCESS #41337274910; readonly SUCCESS #41337275137; otros cinco gates aún activos en última revisión. Runbook actualizado 1898aad1.

G6: 7 informes PASS solo restauración aislada same-host, DR-other-host PENDING, snapshot atómico NOT_PROVEN e IAM NOT_INCLUDED. Release dev f457628d autenticada SHA256 y Sigstore como usuario glmdch, no contiene todavía PR834. El instalador y bootstrap nuevos no se ejecutaron en glmdch.

Siguiente IA: verificar 7/7 gates exact-head PR834, revisión independiente del primer programa privilegiado y del rollback; exigir futura release exacta firmada con instalador y verificador, G6 de corte adecuado y aprobación launcher-only con vigencia/nonce. API/WEB requiere acto separado. No tocar main, producción, migraciones, Compose, Keycloak, SMTP, srv01 ni segunda VM. Registrar resultado en GitHub y Drive.
