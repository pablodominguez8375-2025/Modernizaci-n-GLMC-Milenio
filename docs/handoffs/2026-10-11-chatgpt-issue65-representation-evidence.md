# #65 — Nueva presentación posterior a rechazo (11-10-2026)
Estado: draft. Base dev@b1707e0a4f4421d8e9298e14d5bee9d7562d7c95; main@6dfb9546a4873baff15955cf86abfd7d47e3d111.
Brecha observada: POST /api/insinuados/taller/solicitudes comprueba rechazo reciente usando UTC, pero después de un año permite nueva solicitud sin evidencia de subsanación; POST /api/ceremonias/solicitudes de iniciación no aplica esa protección transversal.
Objetivo: validar año completo en America/Santiago, constancia fechada y referencia verificable; mantener identidad, historia y auditoría, y cerrar bypass de segundo endpoint. No cambiar main ni srv01.
Pruebas e integración: pendientes de incorporar y verificar.
