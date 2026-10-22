# Perfiles Bibliotecario y Comunicaciones + Boletín mensual (#635 / PR #636) — UI QA v0.130

- Decisión del PO (22-10-2026): perfiles institucionales **Bibliotecario** (`librarian`) y **Comunicaciones** (`communications`), ambos de alcance Orden, distintos de los colaboradores por perfil dinámico de #588.
- **Comunicaciones** publica la revista *Horizonte Masónico* y el **Boletín**: crear borrador, cargar PDF, escaneo, publicar y retirar. No administra el resto de la Biblioteca, no fija el grado mínimo (General por omisión) ni accede a Gestión Documental o Gran Archivo.
- **Boletín**: informe resumido mensual de actividades de la Orden. Categoría `bulletin` (alias `boletin`/`boletines`) en catálogo, facetas («Boletines»), filtros, grants por categoría y metadatos; período mes/año obligatorio (se guarda el día 1); sólo PDF (415 en otro formato).
- El **Bibliotecario** también gestiona revistas y boletines y conserva la administración completa en la demo.
- Sesión: nuevas capacidades `canManageLibrary` y `canManageCommunications`. Keycloak: roles nuevos y cuentas sintéticas `qa.bibliotecario` / `qa.comunicaciones` (G8: 21 cuentas, 20 vínculos).
- Demo: perfiles en el selector, panel «Subir revista o boletín» para Comunicaciones, categoría Boletines con dos números ficticios; las herramientas de carga ya no se muestran a perfiles sin función editorial.
- Sin migraciones. No desplegado en srv01 ni aceptado en UAT (Issue #97).
