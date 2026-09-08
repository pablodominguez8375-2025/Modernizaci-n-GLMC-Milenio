# PMGM-BLG-053 — Ventana Fraterna

**Estado:** Planificado  
**Prioridad:** P1 comunidad  
**Requisito:** PMGM-REQ-032  
**Dependencias:** PMGM-REQ-026, PMGM-REQ-027, PMGM-REQ-028

## Objetivo
Implementar **Ventana Fraterna** como espacio comunitario interno de la intranet para colaboración entre hermanos, separado de los canales oficiales de Gran Secretaría.

## Alcance inicial
- [ ] Categorías configurables: empleo, búsqueda laboral, ayuda/información, servicios, compraventa, libros y avisos fraternales.
- [ ] Roles lector/autor/moderador/admin.
- [ ] Crear, editar, publicar, retirar y expirar avisos.
- [ ] Búsqueda, filtros y detalle.
- [ ] Reportar contenido.
- [ ] Moderación auditada con motivo.
- [ ] Adjuntos sobre ciclo binario seguro.
- [ ] Privacidad/minimización de datos de contacto.
- [ ] Integración con notificaciones.
- [ ] UI interna responsive.
- [ ] Pruebas de autorización, moderación y adjuntos.

## Reglas
1. Acceso interno autenticado; no público externo por defecto.
2. No constituye canal oficial de Gran Secretaría.
3. No procesa pagos ni garantiza transacciones en el primer corte.
4. Contenido ilegal, acoso, datos de terceros no autorizados o documentos institucionales reservados deben ser rechazables/moderables.
5. Adjuntos nunca se publican directamente desde Object Storage.
6. Toda moderación relevante queda auditada.

## Criterio de aceptación
Un hermano autorizado puede publicar un aviso dentro de una categoría, otro hermano puede encontrarlo, el autor puede retirarlo y un moderador puede ocultarlo con motivo y auditoría; los adjuntos pasan por validación de seguridad documental.