# PMGM-BLG-054 — Muro Institucional de Gran Secretaría

**Estado:** Planificado  
**Prioridad:** P1 institucional  
**Requisito:** PMGM-REQ-033  
**Dependencias:** PMGM-REQ-022, PMGM-REQ-026, PMGM-REQ-027, PMGM-REQ-028

## Objetivo
Implementar un canal oficial en la intranet para decretos, planchas oficiales, comunicados, circulares, resoluciones, convocatorias y avisos emitidos por Gran Secretaría.

## Alcance inicial
- [ ] Roles editor/publicador/admin de Gran Secretaría.
- [ ] Tipos de publicación configurables.
- [ ] Estados draft/review/approved/scheduled/published/withdrawn/superseded/expired.
- [ ] Versionado oficial no destructivo.
- [ ] Muro cronológico, destacados y búsqueda.
- [ ] Audiencia: todos los hermanos autorizados o segmentación por grado, Taller, cargo, órgano/comisión o hermano específico.
- [ ] Regla de grado mínimo acumulativo cuando corresponda.
- [ ] Descarga segura de adjuntos.
- [ ] Confirmación de lectura opcional por versión.
- [ ] Integración con notificaciones.
- [ ] Transferencia posterior al Gran Archivo cuando exista valor histórico.
- [ ] Auditoría de emisión, audiencia, versión, retiro y lectura cuando aplique.

## Reglas
1. Es un canal oficial y se mantiene separado de Ventana Fraterna.
2. Toda segmentación se aplica en backend a listado, detalle y descarga.
3. Una audiencia no autorizada no debe inferir metadata sensible.
4. Una corrección genera nueva versión; no sobrescribe destructivamente la anterior.
5. La notificación por canal externo no incluye contenido reservado cuando el canal no sea adecuado.
6. El paso al Gran Archivo conserva procedencia y versión de origen.

## Criterio de aceptación
Gran Secretaría puede publicar una comunicación oficial para una audiencia definida, incluyendo grado mínimo, el usuario autorizado puede verla y descargar sus adjuntos, el no autorizado no puede descubrirla ni abrirla por ID directo, y la versión/actor/audiencia quedan auditados.