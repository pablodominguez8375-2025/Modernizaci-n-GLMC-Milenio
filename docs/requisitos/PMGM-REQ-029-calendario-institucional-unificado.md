# PMGM-REQ-029 — Calendario institucional unificado

**Estado:** Aprobado funcionalmente  
**Prioridad:** P1  
**Áreas:** Gran Secretaría, Gestión Logial, ceremonias, Talleres, administración  
**Dependencias:** reservas de espacios, ceremonias, Gestión Logial, RBAC, notificaciones

## 1. Objetivo
Construir un calendario institucional único que consolide, de acuerdo con permisos, las actividades relevantes de la Orden y sus Talleres, evitando agendas paralelas, reservas duplicadas y pérdida de trazabilidad.

## 2. Fuentes iniciales de eventos
El calendario no deberá duplicar información que ya tiene un módulo responsable. Consumirá o proyectará eventos desde:
- reservas de templos y salas;
- ceremonias autorizadas o en preparación;
- tenidas de Taller;
- reuniones institucionales;
- actividades de docencia;
- actividades públicas o conmemorativas autorizadas;
- vencimientos administrativos que deban visualizarse como hitos.

Cada evento conservará referencia a su entidad fuente.

## 3. Ámbitos
Todo evento deberá pertenecer a uno o más ámbitos controlados:
- Orden;
- Gran Logia/estructura central;
- Taller;
- grupo o comisión autorizada;
- privado administrativo cuando corresponda.

El usuario sólo verá eventos permitidos por su rol, alcance y política de visibilidad.

## 4. Datos mínimos
- identificador;
- título funcional;
- tipo de evento;
- fecha/hora inicio;
- fecha/hora término;
- zona horaria institucional;
- ubicación o espacio asociado;
- organización/Taller responsable;
- visibilidad;
- estado;
- referencia a entidad fuente;
- responsable;
- fecha de creación/modificación.

La presentación utilizará `America/Santiago` como zona horaria institucional y cultura `es-CL`, conservando UTC en persistencia cuando corresponda.

## 5. Estados
Como mínimo:
- `draft`
- `tentative`
- `confirmed`
- `cancelled`
- `completed`

Los estados podrán derivarse de la entidad fuente. Por ejemplo, una reserva cancelada deberá reflejarse automáticamente en calendario.

## 6. Conflictos
La existencia de un evento de calendario por sí sola no reemplaza las reglas transaccionales de reserva de espacios.

Para templos y salas:
- la fuente de verdad seguirá siendo el módulo de reservas;
- el calendario reflejará el resultado de la reserva;
- un evento no podrá forzar un solapamiento que el módulo de reservas prohíba.

Para otras actividades podrán existir advertencias configurables por coincidencia de fecha, Taller o responsable.

## 7. Visibilidad y privacidad
Se definirán niveles como:
- `public`
- `institutional`
- `lodge`
- `restricted`

La vista del calendario deberá minimizar datos. Un usuario puede tener derecho a saber que un espacio está ocupado sin conocer necesariamente la naturaleza sensible de la actividad.

Ejemplo: para usuarios no autorizados, una reserva sensible podrá mostrarse simplemente como `Ocupado`.

## 8. Vistas
Frontend mínimo:
- mes;
- semana;
- agenda/listado;
- filtros por Taller, tipo, espacio y estado;
- vista de disponibilidad de espacios desde el flujo de Gran Secretaría.

La experiencia debe ser responsive y utilizable desde teléfono móvil.

## 9. Exportación e integración
Se deberá preparar una interfaz para:
- exportación iCalendar (`.ics`) de eventos permitidos;
- suscripción de calendario mediante token revocable en una fase posterior;
- futura integración con Google Calendar o Microsoft 365 mediante conectores separados.

No se permitirá una URL pública permanente que revele eventos internos.

## 10. Notificaciones
El calendario se integrará con PMGM-REQ-028 para avisos de:
- creación/confirmación;
- cambio relevante de horario o espacio;
- cancelación;
- recordatorios configurados;
- conflicto detectado cuando corresponda.

## 11. Auditoría
Se auditarán:
- creación manual de eventos;
- modificación de fecha/hora;
- cambio de visibilidad;
- cancelación;
- vínculo/desvinculación con entidades fuente;
- exportaciones o suscripciones restringidas cuando corresponda.

## 12. Criterios de aceptación
1. El calendario consolida eventos sin duplicar la fuente de verdad de cada módulo.
2. Una reserva confirmada aparece automáticamente en calendario.
3. Una cancelación se refleja sin intervención manual adicional.
4. Los usuarios sólo ven eventos conforme a rol y alcance.
5. Los detalles sensibles pueden ocultarse mostrando únicamente ocupación.
6. Las vistas mes, semana y agenda funcionan en escritorio y móvil.
7. Los horarios se presentan correctamente para `America/Santiago`.
8. La exportación `.ics` contiene sólo eventos que el usuario está autorizado a visualizar.
9. El calendario se integra con el sistema de notificaciones sin duplicar mensajes.
10. Existen pruebas de autorización, proyección de eventos y manejo de cancelaciones.
