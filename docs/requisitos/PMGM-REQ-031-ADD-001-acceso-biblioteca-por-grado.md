# PMGM-REQ-031-ADD-001 — Biblioteca Virtual: acceso acumulativo por grado

**Estado:** Aprobado funcionalmente  
**Requisito base:** PMGM-REQ-031  
**Prioridad:** P0 de seguridad funcional para v0.16.0

## 1. Regla institucional
Todo recurso de Biblioteca Virtual que requiera segmentación masónica podrá definir `minimum_degree_required`.

La autorización será acumulativa ascendente:

| Grado mínimo del recurso | Hermanos autorizados |
|---|---|
| 1 | grado 1, 2, 3 o superior |
| 2 | grado 2, 3 o superior |
| 3 | grado 3 o superior |

La condición efectiva es:

`grado_actual_del_hermano >= grado_minimo_del_recurso`

La existencia de grados superiores futuros no requerirá cambiar la regla.

## 2. Material general
El material interno general destinado a todos los hermanos podrá marcarse con grado mínimo 1, siempre que el usuario cumpla las demás condiciones institucionales de acceso.

El acceso público externo, si alguna vez se autoriza, seguirá una clasificación distinta y no se inferirá a partir del grado 1.

## 3. Aplicación obligatoria
La regla deberá aplicarse en backend a:
- catálogo;
- búsqueda;
- filtros y facetas;
- detalle;
- portada/miniaturas protegidas;
- descarga;
- enlaces relacionados;
- favoritos/historial si se implementan;
- APIs futuras de indexación o full-text.

La UI podrá ocultar acciones no autorizadas, pero nunca será el control de seguridad principal.

## 4. No filtración
Un usuario con grado insuficiente no deberá recibir metadata de un recurso restringido salvo que exista una política explícita de catálogo que permita descubrir sólo una ficha limitada.

Por defecto en Biblioteca Virtual, el recurso no autorizado queda excluido de resultados, conteos y facetas para evitar inferencias.

## 5. Fuente del grado
El grado efectivo se obtendrá de la historia institucional vigente del hermano y no de un valor editable por el propio usuario.

Cambios de grado deberán producir efecto en autorización conforme a su vigencia institucional y auditoría.

## 6. Combinación con otros permisos
El grado mínimo podrá combinarse con:
- estado activo;
- Taller;
- cargo/rol;
- Grandes Oficiales;
- comisión/órgano;
- hermano específico;
- autorización especial.

La política deberá definir explícitamente si los criterios se combinan como `AND` u `OR`; por defecto, las restricciones declaradas son acumulativas (`AND`).

Un rol editorial no concede automáticamente derecho de lectura del contenido restringido fuera de las tareas necesarias y autorizadas.

## 7. Administración
Al publicar un recurso restringido, el editor/revisor deberá visualizar claramente:
- grado mínimo;
- otras restricciones;
- audiencia efectiva estimada;
- advertencia si cambia el grado respecto de una versión anterior.

Los cambios de grado mínimo de una publicación ya vigente deberán quedar auditados.

## 8. Pruebas obligatorias
Para un recurso de grado mínimo 2:
- grado 1 no aparece en catálogo;
- grado 1 no puede consultar detalle;
- grado 1 no puede descargar aunque conozca el ID;
- grado 2 puede consultar y descargar si cumple las demás reglas;
- grado 3 puede consultar y descargar si cumple las demás reglas.

Para un recurso de grado mínimo 3:
- grados 1 y 2 quedan denegados;
- grado 3 o superior queda autorizado si cumple las demás reglas.

Las pruebas deben cubrir autorización por ID directo para evitar IDOR/BOLA.

## 9. Auditoría
Auditar:
- publicación/cambio de grado mínimo;
- autorizaciones especiales;
- descargas restringidas;
- denegaciones sensibles cuando la política de seguridad determine que aportan valor sin generar exceso de datos.

## 10. Criterios de aceptación
1. La Biblioteca aplica la jerarquía acumulativa de grados en backend.
2. Un hermano nunca accede a material de grado superior al suyo por búsqueda, URL directa o descarga.
3. Un hermano de grado superior puede ver material de grados inferiores si cumple las demás restricciones.
4. Los conteos y facetas no revelan por defecto recursos no autorizados.
5. La regla dispone de pruebas automatizadas de catálogo, detalle y descarga.
