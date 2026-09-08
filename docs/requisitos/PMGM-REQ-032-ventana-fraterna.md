# PMGM-REQ-032 — Ventana Fraterna

**Estado:** Aprobado funcionalmente  
**Prioridad:** P1 comunidad  
**Áreas:** Intranet / Comunidad / Administración / Privacidad  
**Dependencias:** PMGM-REQ-026, PMGM-REQ-028

## 1. Objetivo
Incorporar al Sistema Milenio un espacio comunitario interno denominado **Ventana Fraterna**, destinado a facilitar ayuda y colaboración entre hermanos dentro de la intranet institucional.

Ventana Fraterna no reemplaza los canales oficiales de Gran Secretaría ni constituye un medio de comunicación institucional formal.

## 2. Usuarios
- Acceso únicamente a usuarios autenticados con permiso institucional vigente.
- La publicación podrá restringirse a miembros activos según política configurable.
- Administración y moderación mediante roles específicos.

Roles iniciales:
- `FRATERNA_LECTOR`.
- `FRATERNA_AUTOR`.
- `FRATERNA_MODERADOR`.
- `FRATERNA_ADMIN`.

## 3. Categorías iniciales
La plataforma deberá permitir categorías configurables, incluyendo como base:
- ofertas de trabajo;
- búsqueda de trabajo;
- solicitud de información o ayuda;
- oferta de servicios profesionales o técnicos;
- compraventa entre hermanos;
- libros y publicaciones;
- avisos comunitarios;
- otros avisos fraternales autorizados.

No se contempla en la primera etapa procesamiento de pagos, intermediación financiera ni garantía institucional de las transacciones entre usuarios.

## 4. Publicaciones
Cada publicación podrá contener:
- identificador único;
- autor;
- fecha/hora;
- categoría;
- título;
- descripción;
- imágenes o documentos adjuntos autorizados;
- datos de contacto que el autor decida publicar dentro del alcance permitido;
- vigencia o fecha de expiración;
- estado;
- Taller del autor cuando la política institucional permita mostrarlo;
- etiquetas;
- fecha de edición;
- indicador de publicación destacada cuando corresponda.

Estados mínimos:
- `draft`;
- `published`;
- `expired`;
- `hidden_by_moderation`;
- `withdrawn_by_author`;
- `rejected`.

## 5. Interacción
La primera versión deberá admitir:
- navegación por publicaciones vigentes;
- búsqueda por texto;
- filtros por categoría y fecha;
- visualización de detalle;
- creación, edición y retiro por el autor mientras la política lo permita;
- reporte de una publicación a moderación.

Comentarios, reacciones, mensajería directa y favoritos podrán habilitarse posteriormente como capacidades configurables.

## 6. Moderación
Los moderadores podrán:
- ocultar publicaciones;
- rechazar contenido pendiente si se configura revisión previa;
- registrar motivo;
- reactivar contenido cuando corresponda;
- revisar reportes de usuarios;
- aplicar reglas de vigencia.

Toda acción de moderación deberá quedar auditada.

## 7. Reglas de contenido
No se permitirá utilizar Ventana Fraterna para:
- suplantación de identidad;
- publicación no autorizada de datos personales de terceros;
- contenido ilegal;
- amenazas, acoso o discriminación;
- difusión de información institucional reservada;
- publicación de documentos históricos o secretos cuyo canal correcto sea Gran Archivo;
- publicación de decretos o comunicaciones oficiales como si fueran emitidos por Gran Secretaría;
- productos o servicios cuya publicación esté prohibida por la normativa aplicable o por políticas institucionales.

Las reglas deberán ser administrables y visibles para los usuarios.

## 8. Privacidad
- Aplicar minimización de datos conforme PMGM-REQ-026.
- El sistema no deberá obligar a publicar teléfono o correo personal si existe un mecanismo interno de contacto futuro.
- Datos de contacto visibles deberán responder a decisión explícita del autor y política institucional.
- Las publicaciones retiradas dejarán de ser visibles al público interno, conservándose únicamente la evidencia necesaria según política de auditoría/retención.

## 9. Seguridad de adjuntos
Los adjuntos reutilizarán PMGM-REQ-027 y PMGM-ARCH-003:
- Object Storage privado;
- límites de tamaño y tipos permitidos;
- validación MIME/firma;
- SHA-256;
- antimalware antes de disponibilidad;
- descarga sólo mediante autorización.

## 10. Integración con notificaciones
PMGM-REQ-028 podrá generar notificaciones por:
- publicación aprobada/rechazada;
- reporte/moderación;
- próxima expiración;
- nuevas publicaciones de categorías seguidas cuando esa función sea habilitada.

## 11. Auditoría
Auditar como mínimo:
- creación y edición;
- publicación/retiro;
- reportes;
- moderación;
- cambios de categoría o vigencia;
- operaciones administrativas.

No se auditará contenido sensible innecesario dentro de metadata de auditoría.

## 12. Criterios de aceptación
1. Ventana Fraterna existe como módulo comunitario separado de comunicaciones oficiales.
2. Sólo usuarios autorizados de la intranet acceden al módulo.
3. Se pueden publicar y filtrar avisos por categorías.
4. El autor puede retirar sus publicaciones conforme a política.
5. Existe reporte y moderación auditada.
6. Los adjuntos pasan por el ciclo documental seguro.
7. No existe visibilidad pública externa por defecto.
8. No se procesa dinero ni se garantiza la transacción en la primera etapa.
