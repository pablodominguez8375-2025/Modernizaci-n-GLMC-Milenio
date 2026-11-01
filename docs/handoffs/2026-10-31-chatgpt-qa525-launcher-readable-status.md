# QA525 #822 — Estado legible del launcher (31-10-2026)

**PRE:** ChatGPT IA. Rama `feature/qa525-launcher-status-20261031-gpt` desde `dev@ceeb70d5eee4fae2cd4dedb7ff02848aaca771bc`; `main@6dfb9546a4873baff15955cf86abfd7d47e3d111`. Revisión GitHub PR abiertos, #822, AGENTS §12, GOV-003 §16; Drive por diferencias (adenda 13 UI v0.190). Sin nuevos datos físicos glmdch.

**Hallazgo / mejora:** el inspector antiguo informa `LAUNCHER_OBSERVED_UPGRADE_NOT_AUTHORIZED` pero no emite una clasificación separada que pueda consumir un estado QA para indicar qué procedimiento falta. Contrato seguro: sin alterar `status` ni el consumidor Sigstore, añadir `launcher_transition=INSTALLER_UPDATE_REQUIRED` **solo tras inspección root/host satisfactoria de plantilla y hashes**; errores quedan bloqueados con `launcher_transition=LAUNCHER_INSPECTION_BLOCKED`. No deducir release, firma, despliegue ni G6 de esa etiqueta.

**Alcance:** inspector read-only, pruebas adversariales y documentación local. Sin cambios al instalador, firma, Compose, EF100, API/WEB, Keycloak, SMTP, roots, G6, main, srv01, segunda VM. Cualquier ejecución física requiere paquete verificable y autorización propia.

**DURANTE/POST:** registrar commits, pruebas exact-head, fallos, estado de PR/merge y espejo Drive. No actualizar status de release ni declarar éxito de QA físico sin prueba real. La PR #829 trabaja en archivos diferentes y aún no se ha integrado.
