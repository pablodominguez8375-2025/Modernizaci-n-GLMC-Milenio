# PMGM-REQ-033 — Muro Institucional de Gran Secretaría

**Estado:** Aprobado funcionalmente  
**Prioridad:** P1 institucional  
**Áreas:** Gran Secretaría / Intranet / Notificaciones / Gestión Documental  
**Dependencias:** PMGM-REQ-022, PMGM-REQ-026, PMGM-REQ-027, PMGM-REQ-028

## 1. Objetivo
Incorporar en la intranet del Sistema Milenio un **Muro Institucional de Gran Secretaría** para publicar decretos, planchas oficiales, comunicados, circulares, convocatorias, resoluciones y otros documentos institucionales dirigidos a los hermanos autorizados.

Este módulo constituye un canal oficial y se mantiene separado de Ventana Fraterna.

## 2. Emisor autorizado
La publicación institucional deberá ser realizada por Gran Secretaría o por roles expresamente delegados.

Roles iniciales:
- `GRAN_SECRETARIA_EDITOR`;
- `GRAN_SECRETARIA_PUBLICADOR`;
- `GRAN_SECRETARIA_ADMIN`.

Toda publicación conservará actor, fecha/hora, órgano emisor y versión documental.

## 3. Tipos de publicación
El catálogo deberá soportar, como mínimo:
- decretos;
- planchas oficiales emitidas por Gran Secretaría;
- comunicados;
- circulares;
- resoluciones;
- convocatorias;
- avisos institucionales;
- documentos normativos o informativos autorizados.

Los tipos deberán ser configurables.

## 4. Alcance y segmentación
Cada publicación podrá definir uno o más criterios de audiencia:
- todos los hermanos autenticados autorizados en la intranet;
- miembro activo;
- grado mínimo requerido;
- grado específico cuando la regla institucional así lo determine;
- Taller o conjunto de Talleres;
- cargo o función;
- Grandes Oficiales;
- comisión u órgano;
- hermano específico;
- combinación de criterios;
- autorización especial.

La política se aplicará obligatoriamente en backend para listado, búsqueda, detalle, adjuntos y descarga.

## 5. Publicación por grado
Cuando un documento tenga restricción por grado, el sistema deberá impedir tanto el acceso al contenido como la filtración de metadata no autorizada.

La publicación podrá utilizar la misma semántica de **grado mínimo requerido** definida para Biblioteca Virtual cuando corresponda:
- grado mínimo 1: visible a grados 1, 2, 3 o superiores;
- grado mínimo 2: visible a grados 2, 3 o superiores;
- grado mínimo 3: visible a grado 3 o superior.

También podrá existir una regla de grado exacto o lista explícita si un documento excepcional lo requiere.

## 6. Estados
Estados mínimos:
- `draft`;
- `review`;
- `approved`;
- `scheduled`;
- `published`;
- `withdrawn`;
- `superseded`;
- `expired`.

La retirada de una publicación no eliminará destructivamente la versión oficial ni su auditoría.

## 7. Versionado
Un documento oficial publicado no se sobrescribirá destructivamente.

Cuando exista corrección, reemplazo o nueva versión:
- conservar versión anterior;
- identificar versión vigente;
- registrar motivo;
- indicar documento reemplazado;
- mantener fecha y responsable;
- permitir visualizar historial a usuarios autorizados cuando corresponda.

## 8. Visualización
La intranet deberá permitir:
- muro cronológico de publicaciones vigentes;
- destacados/fijados;
- búsqueda por título, número, tipo y período;
- filtros por tipo y fecha;
- visualización de detalle;
- descarga segura de adjuntos;
- indicador de documento nuevo/no leído cuando se habilite seguimiento de lectura.

## 9. Confirmación de lectura
La publicación podrá definir opcionalmente `requires_acknowledgement`.

Cuando se active:
- registrar hermano;
- fecha/hora de visualización o confirmación explícita;
- versión confirmada;
- estado pendiente/confirmado;
- reportes agregados para Gran Secretaría según autorización.

La confirmación no deberá utilizarse para finalidades distintas de la comunicación institucional sin base y política aplicable.

## 10. Notificaciones
Integración con PMGM-REQ-028 para notificar nuevas publicaciones según audiencia, incluyendo correo institucional o notificación interna cuando esté habilitado.

La notificación no deberá contener contenido reservado si el canal no tiene el nivel de seguridad apropiado; podrá limitarse a informar que existe una nueva publicación en la intranet.

## 11. Integración documental
Los adjuntos y versiones utilizarán el núcleo documental seguro de PMGM-REQ-027 y PMGM-ARCH-003.

Documentos con valor histórico podrán transferirse posteriormente al Gran Archivo sin duplicar innecesariamente el binario y conservando la referencia al documento institucional de origen.

## 12. Auditoría
Auditar:
- creación/edición;
- revisión/aprobación;
- publicación/retiro;
- cambios de audiencia;
- cambios de versión;
- descargas restringidas;
- confirmaciones de lectura cuando proceda;
- acciones administrativas.

## 13. Criterios de aceptación
1. Gran Secretaría puede publicar documentos oficiales en la intranet.
2. Es posible publicar para todos los hermanos autorizados o segmentar por grado, Taller, cargo, órgano o hermano.
3. La autorización se aplica en backend.
4. Una publicación restringida no filtra contenido ni metadata sensible a usuarios no autorizados.
5. Los documentos oficiales mantienen versión e historial.
6. Se puede exigir confirmación de lectura de forma opcional.
7. El módulo se integra con notificaciones.
8. Los documentos históricos pueden transferirse al Gran Archivo preservando procedencia.
