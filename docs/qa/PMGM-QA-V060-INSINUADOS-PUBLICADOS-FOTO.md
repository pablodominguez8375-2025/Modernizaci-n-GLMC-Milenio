# PMGM QA v0.60 — Insinuados publicados y fotografía protegida

## Objetivo

Alinear la vista general de insinuados publicados con el principio de minimización: todos los hermanos autenticados pueden ver publicaciones vigentes de la Orden, pero sólo con datos autorizados para publicación.

## QA-032

La respuesta de `/api/candidate-publications/active`:

- incluye sólo publicaciones vigentes;
- no filtra por Taller del usuario autenticado;
- muestra nombre completo del insinuado, Taller, fechas de publicación y regla de plazo;
- no expone RUT, contacto, dirección, patrocinantes, entrevistas, documentos privados ni observaciones;
- entrega `photoUrl` únicamente cuando la ficha privada tiene una fotografía vinculada;
- usa una ruta protegida `/api/candidate-publications/{publicationId}/photo`;
- no expone rutas internas, claves S3/MinIO ni `documentVersionId`.

## Criterio de regresión

Una publicación sin fotografía debe seguir apareciendo en la lista general, con `photoUrl = null` para que la interfaz use iniciales o marcador visual sin intentar descargar contenido inexistente.
