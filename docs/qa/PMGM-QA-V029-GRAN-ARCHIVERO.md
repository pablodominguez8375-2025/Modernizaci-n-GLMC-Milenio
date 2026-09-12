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

## PostgreSQL
`GrandArchivePostgreSqlTests` valida incorporación de documento de Orden, rechazo de plancha, rechazo de documento de Taller y retiro sin borrado de las versiones documentales.

## Separación funcional
El texto y la navegación deben usar “Gran Archivero”. No se crea módulo CENDOC. Biblioteca Virtual continúa separada y conserva las planchas autorizadas.
