# PMGM-REQ-030-ADD-001 — Gran Archivo: catálogo y permisos granulares

**Estado:** Aprobado funcionalmente  
**Requisito base:** PMGM-REQ-030  
**Prioridad:** P0 de seguridad funcional para Gran Archivo

## 1. Propósito
Formalizar que el Gran Archivo puede custodiar documentación histórica de alta sensibilidad y que el **Gran Archivero administra quién puede descubrir, visualizar o descargar cada pieza o expediente**, dentro de la política institucional, privacidad y mínimo privilegio.

## 2. Documentos históricos incluidos
Además de los tipos ya definidos en PMGM-REQ-030, el archivo deberá admitir expresamente:
- planchas históricas;
- planchas reservadas o secretas;
- decretos y resoluciones;
- cartas y correspondencia;
- investigaciones;
- expedientes disciplinarios;
- amonestaciones;
- antecedentes de procesos internos;
- informes reservados;
- actas o documentos oficiales de relevancia histórica;
- documentación de autoridades, Talleres y órganos;
- cualquier otra pieza que el Gran Archivero determine que posee valor histórico institucional conforme a política de conservación.

La sensibilidad de un documento no impide su custodia histórica, pero sí condiciona acceso, descripción visible, conservación identificable y eventual publicación.

## 3. Dos niveles de autorización
El sistema distinguirá obligatoriamente:

### 3.1 Descubrimiento de catálogo
Determina si un usuario puede saber que el documento existe y qué metadata mínima puede ver.

Niveles configurables:
- `hidden`: el registro no aparece al usuario;
- `catalog_only`: muestra ficha mínima sin contenido;
- `catalog_extended`: muestra metadata ampliada autorizada;
- `content_allowed`: además permite contenido si pasa la política de contenido.

### 3.2 Acceso al contenido
Determina si el usuario puede:
- visualizar en línea;
- descargar derivado de consulta;
- solicitar acceso;
- acceder al original digital cuando exista autorización extraordinaria.

La autorización de catálogo jamás implica por sí sola acceso al contenido.

## 4. Criterios de permiso
El Gran Archivero podrá configurar permisos por:
- grado mínimo;
- grado exacto/lista de grados cuando corresponda;
- hermano específico;
- Taller;
- cargo o rol;
- comisión u órgano;
- Grandes Oficiales;
- finalidad declarada;
- fecha de apertura;
- autorización especial con vigencia;
- combinación de criterios.

Los criterios deberán aplicarse en backend.

## 5. Acceso por hermano
El sistema soportará `ArchiveAccessGrant` o concepto equivalente para autorizar a uno o más hermanos identificados institucionalmente.

Cada concesión registrará:
- documento/expediente/serie objetivo;
- hermano autorizado;
- alcance: catálogo, visualización, descarga o consulta física;
- otorgante;
- motivo/finalidad;
- fecha de inicio;
- fecha de expiración opcional;
- estado;
- revocación y motivo;
- referencia de autorización institucional cuando aplique.

Las concesiones no se heredan indefinidamente si la política no lo permite.

## 6. Acceso por grado
Podrá utilizarse una regla acumulativa de grado mínimo:
- mínimo 1 → grados 1, 2, 3 o superior;
- mínimo 2 → grados 2, 3 o superior;
- mínimo 3 → grado 3 o superior.

Sin embargo, el Gran Archivo podrá requerir reglas más restrictivas que la Biblioteca. Por ejemplo, un expediente de tercer grado puede requerir simultáneamente grado 3 **y** autorización individual.

## 7. Autoridad del Gran Archivero
`GRAN_ARCHIVERO` podrá:
- proponer y administrar restricciones archivísticas;
- otorgar/revocar accesos dentro de las competencias delegadas;
- definir visibilidad de catálogo;
- establecer fecha de apertura;
- aprobar solicitudes de consulta cuando la política lo permita;
- requerir aprobación adicional de otra autoridad para categorías especialmente sensibles.

El rol `GRAN_ARCHIVERO` no equivale a acceso irrestricto a todo contenido sensible. Las políticas podrán exigir doble autorización o separación de funciones.

## 8. Expedientes sensibles
Investigaciones, amonestaciones, expedientes disciplinarios y otros antecedentes personales deberán tener por defecto:
- catálogo oculto o mínimo;
- acceso de contenido restringido;
- auditoría reforzada;
- descarga deshabilitada salvo autorización explícita cuando corresponda;
- retención y disposición conforme a política institucional y PMGM-REQ-026;
- legal hold cuando proceda.

No se asumirá conservación identificable ilimitada sólo por estar en Gran Archivo.

## 9. Publicación hacia Biblioteca
Una pieza archivística no pasa automáticamente a Biblioteca Virtual.

Para publicar se requerirá:
- autorización de publicación;
- verificación de privacidad/restricciones;
- derivado de consulta cuando corresponda;
- referencia al registro archivístico origen;
- política de grado y audiencia propia de Biblioteca.

Retirar la publicación de Biblioteca no elimina el registro histórico del Gran Archivo.

## 10. Auditoría reforzada
Auditar:
- consulta de catálogo sensible cuando aplique;
- solicitud de acceso;
- aprobación/rechazo;
- concesión/revocación por hermano;
- cambios de grado/rol/audiencia;
- visualización de contenido sensible;
- descarga;
- préstamo físico;
- exportación;
- publicación hacia Biblioteca.

## 11. Criterios de aceptación
1. El Gran Archivo puede custodiar planchas, decretos, cartas, investigaciones, amonestaciones y expedientes históricos.
2. El Gran Archivero puede controlar por separado visibilidad de catálogo y acceso al contenido.
3. Puede otorgarse acceso a un hermano específico con alcance y vigencia definidos.
4. Puede limitarse por grado y combinar grado con autorización individual.
5. Conocer el ID de una pieza no permite eludir permisos.
6. Los expedientes sensibles parten de una política restrictiva.
7. Toda concesión, revocación y acceso sensible queda auditado.
8. Biblioteca sólo recibe una representación autorizada y nunca modifica el original archivístico.
