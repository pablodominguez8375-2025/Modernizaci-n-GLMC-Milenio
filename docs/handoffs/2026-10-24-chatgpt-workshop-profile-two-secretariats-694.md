# Issue #694 — edición de ficha de Taller: sólo Secretarías autorizadas

24-10-2026 · ChatGPT · rama `fix/workshop-profile-694-two-secretariats-20261024-gpt` desde dev@09795cf1872259c7b7db849c513ad978de43b857.

## Decisión PO prevalente
En Issue #694 (23-10-2026) se define que después de la carga inicial pueden editar Oriente, ciudad y ficha del Taller: **Gran Secretaría** para cualquier Taller y **Secretaría del Taller** únicamente para su Taller. Máster de plataforma no puede; tampoco Régimen Interior. La autorización anterior en `InstitutionalAccessService.CanManageWorkshopProfile` y `CanEditAnyWorkshopProfile` también permitía Régimen Interior, contraria a esta decisión más específica.

## Cambio mínimo
Retirar `RegimenInterior` exclusivamente de las dos políticas de edición de ficha de Taller, sin quitar su capacidad de revisar, reportar o validar otras áreas. Conservar requerimiento `pmgm_scope=order` para Gran Secretaría y `pmgm_org` exacto para Secretaría Taller, y bloqueo histórico al rol Máster plataforma incluso con claims mixtos. Revisar tests de perfiles y añadir regresiones denegadas.

No tocar UI Claude, migraciones, rutas HTTP ni contratos. El endpoint existente PUT profile/metadata sigue auditando al guardar; su alcance de autorización queda alineado con lo definido por el PO. No exige datos personales reales. PR DRAFT, 3/3 PMGM CI+Showcase+QA Installable exact-head y behind0 antes de squash SOLO a dev; no main ni QA físico. Registro simultáneo GitHub/Drive.
