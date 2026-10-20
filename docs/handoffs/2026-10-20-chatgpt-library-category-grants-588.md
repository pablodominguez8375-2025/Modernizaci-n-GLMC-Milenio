# #588 — Permisos dinámicos por categoría documental (contrato backend parcial)

2026-10-20, rama desde `dev@4565831db7cef7444a3518408bfeaf15fefe7a70`.

## Extensión compatible del catálogo dinámico

`DynamicGrant` admite `Categories: string[]?` opcional. Los perfiles existentes con `Categories=null` siguen deserializando y conservan su autorización de vista. Para las vistas exactas `library` y `documentmanager` el editor del catálogo (solo administrador de Sistema) puede especificar categorías reconocidas `magazine`, `book`, `work_paper`, `official_document`, `video`. Combinaciones de categorías con otras vistas o valores desconocidos son rechazadas.

- `IsGranted(...view,action,...)` jamás concede acciones de escritura sin categoría a partir de un grant limitado: solamente `view` de la vista (menú) es general.
- `IsCategoryGranted(...view,action,category,...)` exige sujeto exacto, perfil activo no-sistema, vista activa, asignación vigente/ámbito exacto, `view` + acción, y categoría explícitamente listada. Los grants históricos sin categorías **NO** se convierten en un permiso implícito de editor por categoría.
- Las rutas de Biblioteca/DocumentManager deberán usar esta comprobación sobre la categoría canónica resuelta **desde el documento servidor**; no confiar en la categoría enviada por el cliente.
- Prueba de política negativa: Comunicaciones puede crear/editar solo `magazine`, no libros ni archivo documental; revocación temporal, sujeto ajeno y acción `delete` no delegada se rechazan.

## Pendientes para llevarlo a producción

Este paso **no habilita creación/publicación ni asignación por el Bibliotecario**: falta autorización de recursos y control de colaborador en endpoints específicos, contrato REST lista/asigna/revoca, auditoría transaccional y pruebas HTTP/PostgreSQL. El rol Librarian de PR #590 es independiente. El editor general de perfiles continúa exclusivo de CanConfigureSystem. No dar por operativa la función de colaboradores ni pedir UI a Clau antes de implementar esos endpoints con seguridad.

Sin cambios en `main`, producción, `srv01` o glmdch; QA físico en otro hilo.
