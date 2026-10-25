# Handoff ChatGPT — #604 preflight de unicidad Máster en Keycloak (2026-10-25)

Dev inicial: `a3cef7a8ee2a0418d6325936cb21616cae90b30f`. Main estable: `6dfb9546a4873baff15955cf86abfd7d47e3d111`.

Alcance reservado: `scripts/qa/check-keycloak-master-inventory.py`, `tests/test_keycloak_master_inventory.py` y este handoff. No modifica backend, Keycloak, cuentas, permisos ni el servidor. PR en borrador hacia dev.

Objetivo: comprobación remota estrictamente de solo lectura, con HTTPS verificado y token externo almacenado en archivo de credenciales protegido, para identificar si hay exactamente un rol global Máster `platform_superadmin` efectivamente asignado al usuario `admin` esperado, comparado con `InitialLoad:MasterSubject` e issuer OIDC configurados en host. Fallar cerrado ante inventario truncado, usuarios duplicados, API parcial/roles ambiguos o error de configuración. No imprimir identificadores, token, correo, nombres ni respuesta JSON; solo códigos estables.

Alcances negativos: una lectura no garantiza unicidad atómica frente a modificaciones concurrentes. Alta/reparación de `admin` requiere autorización expresa del Sponsor, bloqueo de escritor único, backup, MFA obligatorio en producción, auditoría y prueba física aparte. Las cuentas `qa.admin` del realm sintético no son el bootstrap definitivo `admin`. El preflight no crea ni promueve identidades, no concede permiso Máster, no exige MFA en QA (excepción temporal Sponsor) y NO certifica G8/G10/UAT.

Validación de entrega: pruebas Python offline sintéticas, gates exact HEAD CI/Showcase/QA Installable. Ninguna sesión QA real ni acceso físico durante esta PR. Issue #604 propietario ChatGPT, PR #731 UI de Claude no se modifica. Registrar espejo Drive al completar.
