# Comprobar QA — #97

Reclamo ChatGPT/Codex. Base dev: 77557f87d2979b6ef23f636c7c99bec329f70618. main: 6dfb9546a4873baff15955cf86abfd7d47e3d111.

Alcance: workflow manual check-qa.yml, helper remoto restringido, cliente/validador, pruebas sintéticas y runbook. Sin aprovisionar servidor, cuentas, claves o secretos. Sin merge; PR para revisión. Cierre previsto: 15-10-2026 America/Santiago.

PR #479 reserva ci.yml; no se modifica ese archivo ni sus dependencias. PR #1 es promoción histórica bloqueada. Drive revisado por diferencias desde 01:50 UTC: solo adenda QA ya leída, sin cambio de alcance. glmdch reemplaza srv01, instalación/despliegue QA pendientes.

Implementación: PR #501 https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/pull/501. Ocho pruebas sintéticas PASS; YAML y bash -n PASS. actionlint no disponible, pendiente. Sin migraciones ni cambios UI/datos; capturas no aplican. Gates del commit final consultar en PR, no asumir SUCCESS. No merge autorizado en esta entrega.

Pendientes: revisión, CI exact-head, aprobación separada de activación mínima en main (default branch), provisión de cuenta/clave/helper root-owned y Environment protegido, conectividad/host key y pruebas sshd/PT Y/forwarding/SFTP/SCP, primera ejecución manual. Firewall/NAT y QA física/UAT no verificados; despliegue QA pendiente. Runbook docs/qa/COMPROBAR-QA.md.

Dev observado al cierre técnico conserva 77557f87d2979b6ef23f636c7c99bec329f70618; main conserva 6dfb9546a4873baff15955cf86abfd7d47e3d111. Código implementado sólo en rama; sin publicación de este incremento en Demo/instalable. Registro final de SHA/gates en PR y #97 para evitar autorreferencia de commit.
