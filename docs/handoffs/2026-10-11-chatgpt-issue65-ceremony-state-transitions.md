# Issue #65 — Matriz de estados de ceremonia

11-10-2026. Base dev@8d961b9887a133c05cffd4bc3ace6fcfd5a9df9f. Main sin cambios.

Brecha: POST autorizar cierra como rejected una solicitud que sólo carece de requisitos corregibles. Además no excluye request rejected/completed. Las proyecciones pueden mostrar canAuthorize=true para expedientes ya cerrados.

Objetivo: política única de estados, observaciones corregibles, bloqueos terminales y paridad entre consultas de elegibilidad, matriz y bandeja. Verificación y handoff en el PR.
