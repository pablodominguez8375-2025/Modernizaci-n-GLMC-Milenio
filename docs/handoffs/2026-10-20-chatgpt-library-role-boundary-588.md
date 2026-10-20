# #588 — Frontera de roles Biblioteca / Gran Archivo (fase 2, preparatoria)

20-10-2026. Branch desde dev@ef4d3f0e1f817771ff67b4c36b4a5763190f2a5e.

## Cambio de autorización sin efecto en rutas existentes

- `InstitutionalRoles.Librarian = "librarian"`.
- `IInstitutionalAccessService.CanManageLibrary(user, organizationId)`: únicamente cuenta autenticada a través del middleware vigente, scope `order`, con rol `librarian` o `grand_lodge_admin` (superadministrador sólo cuando posee scope de Orden). El parámetro de organización se reserva para el contrato, no introduce gestión por Taller.
- `CanManageGrandArchive` continúa reservado a `grand_archivist` y `grand_lodge_admin` con scope de Orden.
- `CanManageDocuments` permanece como API histórica **sin modificaciones** para no romper operaciones existentes antes de migrar sus rutas. No confundir este legado con autorización final separada.
- Pruebas negativas verifican que Librarian no hereda CanManageDocuments, CanManageGrandArchive ni CanConfigureSystem; Archivist no hereda Biblioteca ni gestor de documentos; DocumentManager legado no obtiene rol Bibliotecario; sin scope Orden, rol librarian se rechaza.

**IMPORTANTE:** crear el predicado **NO habilita todavía al Bibliotecario a administrar la Biblioteca**. Ningún endpoint llama al nuevo método; la UI no debe mostrar funciones administrativas basadas sólo en este commit. No se asignaron roles a personas ni se cambiaron claims de sesión.

## Pendientes para implementación operativa segura

1. Separar rutas y políticas por recurso (Biblioteca vs gestor documental): catalogar documentalmente las rutas `/api/documentos` que usan Biblioteca; no conceder funciones por el tipo `magazine`.
2. Compatibilidad supervisada del rol histórico `document_manager` y accesos actuales de Taller, con pruebas por acción/perfil, sin reasignación automática.
3. Crear concesiones dinámicas de colaboradores por ámbito, categoría documental, acción y vigencia. Solo Bibliotecario y Gran Archivero podrán delegar en su área; destinatario no delega.
4. Auditar alta/revocación, publicación y modificaciones, y filtrar en el servidor. Añadir capacidades `/api/session/me` sólo cuando las rutas las soporten.
5. Conectar UI de Clau después del contrato definitivo; entregar capturas antes de publicación. QA físico es otro hilo.

Sin cambios en `main`, producción, `srv01` ni cuentas.
