# #635/#588 — Colaboradores independientes de Bibliotecario y Comunicaciones
**Fecha:** 30-10-2026
**Base:** `dev@e7f4c3d4b2c54f2a441261c8dd47a71609558014`
**Rama:** `fix/communications-isolated-collaborators-20261030-gpt`
**Estatus:** Implementación en PR; no confundir con QA desplegada o UAT.

## Decisión del Sponsor
Bibliotecario y Comunicaciones son dos cargos institucionales separados. Cada uno puede crear, **modificar** y **eliminar (revocar)** sus propios colaboradores sin consultar ni editar colaboradores del otro. La categoría de Comunicaciones se limita exclusivamente a revista institucional `magazine` y boletín mensual `bulletin`, según #635. Los permisos de contenido de Bibliotecario que preexistían siguen vigentes (no se reabren sus funciones editoriales mediante este cambio). El Máster técnico conserva excepción administrativa auditada conforme a la política institucional existente.

## Diagnóstico
`DocumentAreaCollaboratorEndpoints` solo mapeaba `/api/biblioteca/colaboradores` (responsable Bibliotecario) y `/api/documentos/colaboradores` (Gran Archivero); omitía Comunicaciones. `LibraryPage` mostraba `AreaCollaboratorsSection` solo con `editorRole === "librarian"`. Los contratos solo tenían POST (alta), GET y DELETE (revocación), sin PUT para cambiar una asignación.

## Implementación
- Área nueva `communications` con API `GET/POST/PUT/DELETE /api/comunicaciones/colaboradores` y `GET /candidatos`. Acceso exclusivo `CanManageCommunications` bajo scope Orden (más Máster autorizado); Bibliotecario tiene área `library` independiente. Gran Archivero `documentmanager` sigue intacto.
- Perfiles internos distintos: `collab-communications-{uuid}` vs `collab-library-{uuid}`, con lista y mutaciones filtradas por prefijo exacto. `communications` es **área de administración**, no un nuevo permiso de contenido: los colaboradores delegados usan `DynamicGrant("library", acciones, [magazine|bulletin])` para reusar los controles reales de categoría; nunca reciben `CanManageLibrary`, `CanManageCommunications` ni privilegio de delegar. No se crea vista dinámica global ni rol Keycloak, ni migración.
- PUT de asignación activa: valida propietario, versión vigente del catálogo, acciones, categoría, fechas e intersecciones; actualiza perfil y asignación en una transacción con el lock institucional y nuevo snapshot; auditoría `library.collaborator.updated`. Sujetos de otra área dan 403 para roles distintos, o 404 de id fuera del área para Máster.
- DELETE sigue siendo revocación lógica inmediata y auditada; los registros revocados permanecen en snapshots/auditoría pero no vuelven a mostrarse como colaboradores activos. Nunca se eliminan históricos.
- Interfaz: Bibliotecario ve sus colaboradores en Biblioteca; Comunicaciones ve panel de su área y solo revista/boletín. Ambos ven **Agregar / Modificar / Revocar**. Cliente HTTP y demo usan rutas/ámbitos separados. Se mantiene versión optimista, vigencia y no exposición de correos/RUT en candidatos.

## Criterios de aceptación y pruebas
1. Bibliotecario no puede GET/POST/PUT/DELETE ni consultar candidatos de Comunicaciones y viceversa; tampoco pueden hacerlo respecto de Gran Archivero.
2. Comunicaciones no delega libros, documentos oficiales ni otros tipos; solo revista y boletín, nunca acceso global a Biblioteca.
3. Delegación de Comunicaciones habilita por permisos dinámicos solo la categoría y las acciones autorizadas. Revocación retira de inmediato el permiso efectivo. Otros perfiles no pueden redelegar.
4. PUT sobre mismo ID permite cambiar sujeto, categoría, acciones, inicio/término y audita el cambio; rechaza versión desactualizada, periodos solapados, otra área y categorías prohibidas.
5. UI/mocks y sistema real muestran botones específicos por perfil, sin mezclas de colaboradores.
6. Ejecutar CI exact-head backend/frontend/integración PostgreSQL, Showcase e instalable antes de merge a `dev`. Postmerge exigir gates y release firmada; recién allí tratar despliegue supervisado QA por #525.

## Riesgos/alcance excluido
- Este cambio no redefine el control editorial por *propietario de publicación*: ambos cargos ya pueden administrar revista/boletín según #635. Solo separa la gestión de **sus colaboradores** y sus delegaciones. Cualquier exigencia adicional de aislar documentos por `publisherProfile` requiere revisión específica y tests HTTP.
- Ninguna variación de permisos de Tesorería, Gran Secretaría, Biblioteca completa, Documental, identidad, roles, migrations o Compose; sin `main`, `srv01` ni QA físico automático.

**Continuidad:** GitHub PR y estado ejecutivo Drive deben registrar CI, merge, SHA final y necesidad de UAT real de ambas cuentas en `glmdch`.
