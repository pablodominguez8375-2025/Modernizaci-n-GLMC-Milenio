# #533 C24 — inventario PRIVADO de fuente Membresía / cargos V.M.
Fecha 23-10-2026. Dev de base 074fd7951beb11b47682cfc8b546f3bc9714fbf5. Agente ChatGPT.

## Reclamo de trabajo
Nueva lectura interna desde la base canónica PmgmDbContext para obtener hechos sobre miembros con cargos históricos Venerable Maestro y sus membresías al corte, sin inventar certificación. Incluye detección de inconsistencias de identidad/organización/período y exclusión del Taller técnico soporte. El valor de salida **no puede** ser usado directamente como lote acreditado por C22/C23: necesita fuente de certificaciones institucionales y derechos RI/Tribunal/GT. No endpoints públicos, sin nueva migración.

Archivos a modificar (exclusivos):
- backend/src/PMGM.Api/Modules/RegimenInterior/AssemblyMemberSourceInventory.cs (nuevo)
- backend/tests/PMGM.Api.Tests/Integration/AssemblyMemberSourceInventoryPostgreSqlTests.cs (nuevo)
- .github/backend-integration-shards.json (registro de la prueba, archivo de coordinación CI)
- docs/handoffs/2026-10-23-chatgpt-assembly-member-source-inventory-c24-533.md
- docs/modelo-datos/registros/2026-10-23-chatgpt-assembly-source-inventory-c24-533.md

No tocar UI/endpoints de Clau #639/#640, PR #665 RBAC, main ni QA físico. Requiere CI, Showcase, QA Instalador 3/3 del HEAD exacto antes de merge.
