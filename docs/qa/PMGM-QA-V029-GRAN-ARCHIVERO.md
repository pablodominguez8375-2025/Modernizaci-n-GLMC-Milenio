# PMGM-QA-V029 — Gran Archivero

## Objetivo
Validar que el archivo histórico opere como catálogo institucional independiente de Biblioteca Virtual y que no rompa las garantías del Gestor Documental.

## Casos mínimos
1. Usuario sin `grand_archivist`/administración central recibe 403.
2. Gran Archivero lista registros y filtra por estado, tipo y búsqueda.
3. Candidatos sólo incluyen versiones `available`, de scope Orden y fuera del archivo actual.
4. Documento de Taller no puede incorporarse.
5. Plancha de trabajo no puede incorporarse.
6. Código archivístico duplicado produce conflicto.
7. Una versión ya archivada no se registra por segunda vez.
8. Retiro cambia el catálogo a `withdrawn` y mantiene documento/versión fuente.
9. Contenido sólo se entrega para registro activo y mediante bearer auth.
10. No existe URL pública directa de S3/MinIO.
11. UI showcase permite listar, incorporar y retirar con datos ficticios sin red.
12. Gate Ley 21.719, clasificación y migration safety permanecen verdes.


## Perfil QA de la demo

La demo pública debe ofrecer el perfil seleccionable Gran Archivero (grandArchivist) y mostrar la vista con esa capacidad específica. El escenario no debe usar la autoridad amplia de Gran Logia: verifica que aparezca “Gran Archivero” y que no aparezcan Sistema, Secretaría, Tesorería, Hospitalaria ni Gestión Logial. El perfil es ficticio y no modifica roles reales. El alcance order conserva las vistas institucionales ya disponibles conforme a los permisos existentes; esta adición no los amplía.

Fuente institucional: Constitución y Reglamento.pdf en Drive, actualizada el 17-09-2026, enumera Gran Archivero como cargo y su Art. 20.7 le atribuye custodia/catastro de documentos institucionales. PMGM-REQ-036 confirma rol técnico grand_archivist con ámbito Order y reserva administración central a grand_lodge_admin. La prueba refleja esa separación.

## PostgreSQL
`GrandArchivePostgreSqlTests` valida incorporación de documento de Orden, rechazo de plancha, rechazo de documento de Taller y retiro sin borrado de las versiones documentales.

## Separación funcional
El texto y la navegación deben usar “Gran Archivero”. No se crea módulo CENDOC. Biblioteca Virtual continúa separada y conserva las planchas autorizadas.
