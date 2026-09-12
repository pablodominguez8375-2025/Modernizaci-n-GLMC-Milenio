# PMGM-REQ-028 — Notificaciones institucionales multicanal

**Estado:** Aprobado funcionalmente  
**Prioridad:** P1  
**Áreas:** Todo el ecosistema PMGM  
**Dependencias:** Identidad/RBAC, auditoría, ceremonias, Secretaría, Gestión Logial, documentos

## 1. Objetivo
Crear un servicio institucional de notificaciones que permita informar oportunamente a usuarios y responsables sobre eventos del sistema sin acoplar cada módulo directamente a un proveedor de correo.

## 2. Canales iniciales
- Notificación interna en la intranet.
- Correo electrónico institucional.

Canales futuros como push móvil, SMS o mensajería externa deberán incorporarse mediante adaptadores sin modificar la lógica de dominio.

## 3. Arquitectura funcional
Los módulos de negocio emitirán eventos de dominio o solicitudes de notificación. Un servicio de notificaciones será responsable de:
- resolver destinatarios;
- aplicar plantilla;
- seleccionar canales;
- encolar entrega;
- reintentar fallos transitorios;
- registrar resultado;
- evitar duplicados.

Se utilizará patrón Outbox o mecanismo transaccional equivalente para impedir pérdida de eventos entre la operación de negocio y el envío.

## 4. Tipos iniciales de notificación
- Solicitud de ceremonia creada.
- Ceremonia observada, aprobada, rechazada o autorizada formalmente.
- Regularidad de Taller que bloquea un trámite.
- Insinuado publicado y plazo cumplido.
- Reserva de templo/sala confirmada, modificada o cancelada.
- Documento publicado o reemplazado.
- Tenida creada, modificada o suspendida.
- Acta pendiente de aprobación o publicada.
- Solicitud de titular de datos personales asignada o próxima a vencer.
- Incidente de seguridad escalado a responsables autorizados.

## 5. Plantillas
Cada plantilla deberá ser versionada y contener:
- código estable;
- nombre;
- asunto por canal cuando aplique;
- cuerpo;
- variables permitidas;
- clasificación de sensibilidad;
- estado activo/inactivo;
- versión;
- fecha de vigencia.

No se permitirá que una plantilla inserte arbitrariamente campos no declarados.

## 6. Privacidad
Las notificaciones externas, especialmente email, deberán minimizar datos.

Reglas mínimas:
- no incluir RUT completo salvo fundamento explícito;
- no incluir datos sensibles innecesarios;
- no enviar saldos o antecedentes hospitalarios cuando basta un estado de regularidad;
- preferir enlaces autenticados a la intranet para consultar detalles;
- evitar información masónica sensible en asunto cuando pueda exponerse en pantallas bloqueadas o sistemas externos;
- registrar finalidad y categoría de notificación para auditoría.

## 7. Preferencias del usuario
Las notificaciones se clasificarán como:
- `mandatory`: institucionales/operativas obligatorias;
- `recommended`: recomendadas, con configuración limitada;
- `optional`: voluntarias.

Un usuario no podrá desactivar una notificación obligatoria definida por política institucional.

## 8. Entrega y resiliencia
Cada intento deberá registrar:
- mensaje lógico;
- destinatario;
- canal;
- proveedor;
- fecha programada;
- intento;
- resultado;
- código de error normalizado;
- fecha de entrega cuando exista confirmación;
- correlation ID.

Los reintentos deberán usar backoff y límite configurable. Los mensajes fallidos definitivamente pasarán a una cola de revisión o estado `dead_letter`.

## 9. Idempotencia
La misma operación de negocio no deberá generar múltiples correos equivalentes por reintentos técnicos.

Cada notificación lógica tendrá una clave de idempotencia derivada del evento, tipo de notificación y destinatario.

## 10. Seguridad
- Credenciales SMTP/API fuera del repositorio.
- TLS obligatorio para proveedores compatibles.
- Prohibido registrar cuerpos sensibles completos en logs técnicos.
- Sanitización de variables de plantillas.
- Rate limiting y protección contra abuso.
- Autorización explícita para reenvío manual.

## 11. Panel operativo
Gran Secretaría o administración autorizada deberá poder consultar:
- notificaciones pendientes;
- enviadas;
- fallidas;
- reintentadas;
- canal;
- tipo;
- fecha;
- destinatario en formato protegido cuando corresponda.

El panel no permitirá leer masivamente contenido sensible sin permiso adicional.

## 12. Criterios de aceptación
1. Una operación de negocio puede producir una notificación sin depender directamente de SMTP.
2. La transacción de negocio no se pierde aunque el proveedor de correo esté temporalmente caído.
3. Existe reintento controlado y estado final auditable.
4. Los duplicados quedan prevenidos mediante idempotencia.
5. Las plantillas son versionadas y parametrizadas.
6. El sistema soporta notificación interna y correo institucional.
7. Las notificaciones respetan roles, finalidad y minimización de datos.
8. Las credenciales del proveedor no se exponen en código, frontend ni logs.
9. Existen pruebas de integración para encolado, reintento e idempotencia.
