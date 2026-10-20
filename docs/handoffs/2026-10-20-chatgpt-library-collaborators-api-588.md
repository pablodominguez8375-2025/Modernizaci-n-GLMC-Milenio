# #588 — Colaboradores por área: endpoints API y contrato UI

2026-10-20. Rama de desarrollo combinada: base de PR #590 (roles separados) + PR #592 (grants por categoría). **No promover a dev hasta fusionar y revalidar las dependencias.**

## Rutas propuestas para Clau (solo después de integrar a dev)

- `GET /api/biblioteca/colaboradores`, `POST /api/biblioteca/colaboradores`, `DELETE /api/biblioteca/colaboradores/{assignmentId}?expectedVersion=N`.
- `GET /api/documentos/colaboradores`, `POST /api/documentos/colaboradores`, `DELETE /api/documentos/colaboradores/{assignmentId}?expectedVersion=N`.

GET: `{version,items:[{assignmentId,subject,area,category,actions,effectiveFrom,effectiveTo,isActive}]}`. POST:
```json
{
  "subject": "sub-OIDC-exacto",
  "category": "magazine",
  "actions": ["view", "create", "edit"],
  "effectiveFrom": "2026-10-20",
  "effectiveTo": "2026-12-31",
  "expectedVersion": 7
}
```
Respuesta POST: `{version,assignmentId,area,category,actions,effectiveFrom,effectiveTo,active:true}`.


Códigos: 403 sin rol responsable, 400 ámbito/acción/fecha inválidos, 409 versión obsoleta o intervalo superpuesto, 404 revocación inexistente/fuera del área.

**Separación:** Biblioteca solo rol explícito `librarian` (o administrador institucional autorizado), Gestor Documental solo `grand_archivist` (o administrador institucional autorizado). Colaborador no puede delegar, cambiar perfiles globales, entrar al área contraria ni rebasar categoría. Roles y scope verificados en backend, no por UI. Los perfiles creados tienen códigos internos `collab-library-<guid>` / `collab-documentmanager-<guid>` y un solo grant de categoría canónica; sólo acciones `view/create/edit/delete`, con `view` obligatorio. Vigencia y revocación en asignación dinámica. Toda mutación usa el advisory lock único de Sistema, snapshots versionados y auditoría en la misma transacción. No se guardan contraseñas, RUT o motivo en el snapshot.

## Advertencia crítica de seguridad: falta conectar acciones sobre documentos

Estos endpoints **gestionan las delegaciones** y la API de permisos `IsCategoryGranted`, pero **todavía no habilitan una ruta de creación/subida/publicación de documentos por colaboradores**. Antes de presentar la UI de edición/publicación hay que conectar comprobaciones de acción/categoría desde el registro de documento resuelto en servidor, completar reglas de transición y escáner de archivos, y probar revocación inmediata. No trasladar autorización sólo al frontend. Se conserva compatibilidad transitoria del rol `document_manager` sin ampliar privilegios en estas rutas.

Pruebas HTTP/PostgreSQL: Bibliotecario asigna/revoca revista, no Gestor Documental; Gran Archivero asigna solo a DocumentManager; rol colaborador sin derecho a delegar; versiones obsoletas, auditoría y cruces de categoría. CI, Showcase e instalable deben pasar sobre HEAD exacto. QA físico/credenciales/srv01/main/prod no tocados.

## UI propuesta

Dos botones «Colaboradores» independientes en Biblioteca Virtual y Gestión Documental, tarjetas con categoría, acciones, fechas y estado, uso de ActionDrawer para alta y revocación, aviso al modificar una concesión caducada. Capturas a Sponsor antes de publicar. No usar ni mostrar «Editar documento» hasta conectar las rutas de recursos autorizados.
