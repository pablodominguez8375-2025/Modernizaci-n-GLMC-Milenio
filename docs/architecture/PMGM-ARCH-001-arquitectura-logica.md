# PMGM-ARCH-001 — Arquitectura lógica inicial

**Estado:** Propuesta base para implementación  
**Versión:** 0.1  
**Rama de trabajo:** `dev`

## 1. Objetivo
Definir la arquitectura lógica del ecosistema Proyecto Milenio para soportar una plataforma institucional única, modular, segura, auditable e integrable, con identidad única y una base maestra institucional.

## 2. Principios
1. Un usuario debe utilizar una sola identidad institucional.
2. Los datos maestros se registran una sola vez y se reutilizan transversalmente.
3. Los módulos no deben crear copias independientes de miembros, Talleres, cargos o periodos.
4. Todo cambio relevante debe quedar auditado.
5. Seguridad, privacidad y mínimo privilegio son requisitos estructurales.
6. La plataforma debe permitir reemplazar progresivamente sistemas legados sin exigir una migración total en un solo hito.
7. La lógica de negocio debe permanecer separada de la interfaz de usuario.
8. Toda integración externa debe ingresar mediante contratos/API controlados.

## 3. Vista por capas

### 3.1 Experiencia
- Portal público institucional.
- Intranet institucional responsiva.
- Consola administrativa.
- Experiencia móvil futura/PWA o aplicación dedicada.

### 3.2 Identidad y acceso
- Proveedor de identidad compatible con OpenID Connect/OAuth 2.0.
- Inicio de sesión único (SSO).
- MFA configurable por perfil de riesgo.
- RBAC para roles institucionales.
- Permisos específicos cuando el RBAC no sea suficiente.
- Sesiones, recuperación de acceso y revocación centralizadas.

### 3.3 Dominio / módulos
Núcleo inicial:
- Identidad y autorización.
- Miembros.
- Talleres / Logias.
- Cargos y periodos.
- Secretaría.
- Régimen Interior.
- Gestión Logial.
- Tenidas y asistencia.
- Documentos y Biblioteca.
- Auditoría.

Extensiones:
- Tesorería.
- Hospitalaria.
- Docencia.
- CENDOC.
- Museo/Archivo Histórico.
- Ceremonias y solicitudes.
- Calendario.
- Comunicaciones y notificaciones.
- Reportería institucional.
- Integraciones.

## 4. Estilo arquitectónico
Se adopta inicialmente un **monolito modular** con límites de dominio explícitos.

Razones:
- Reduce complejidad operativa durante el MVP.
- Facilita consistencia transaccional en la base maestra.
- Permite entregar una primera versión demostrable rápidamente.
- Evita introducir microservicios antes de contar con carga y necesidades de escala verificadas.
- Conserva la opción de extraer módulos a servicios independientes en el futuro.

Cada módulo deberá exponer contratos claros y no podrá acceder indiscriminadamente a tablas de otros módulos desde la capa de aplicación.

## 5. Componentes propuestos

### Backend
- ASP.NET Core Web API.
- Arquitectura limpia/modular por dominio.
- Servicios de aplicación y dominio separados.
- Entity Framework Core para persistencia transaccional.
- OpenAPI para documentación de API.

### Frontend
Decisión final pendiente mediante ADR entre React y Blazor. Independientemente de la elección, debe consumir la API mediante contratos versionados.

### Persistencia
- PostgreSQL como base institucional principal.
- Almacenamiento de documentos desacoplado del motor relacional mediante objeto/archivo gestionado.
- Redis opcional cuando exista necesidad comprobada de cache, sesión distribuida o colas simples.

### Infraestructura
- Ubuntu Server 24.04 LTS como referencia.
- Docker para empaquetado.
- Nginx o proxy equivalente para publicación y TLS.
- GitHub Actions para CI/CD.
- Ambientes separados: desarrollo, pruebas/staging y producción.

## 6. Flujo de una operación
1. El usuario se autentica mediante el proveedor de identidad.
2. El frontend recibe identidad y contexto autorizado.
3. La solicitud llega a la API a través del proxy seguro.
4. La capa de autorización valida rol, permiso, contexto de Taller y operación.
5. El módulo correspondiente ejecuta reglas de negocio.
6. La persistencia aplica la transacción en PostgreSQL.
7. La auditoría registra actor, acción, fecha, entidad y resultado.
8. Cuando corresponda, se genera una notificación o evento de integración.

## 7. Separación portal público / intranet
El portal público y la intranet compartirán infraestructura y servicios solamente cuando sea seguro y conveniente, pero tendrán superficies de acceso separadas. Ningún dato interno deberá exponerse por reutilización accidental de endpoints públicos.

## 8. Integración con sistemas actuales
Cada sistema legado se clasificará como:
- Mantener.
- Integrar.
- Migrar.
- Reemplazar.
- Retirar.

Las integraciones deberán implementarse mediante adaptadores, jobs controlados o APIs; se evitará que el nuevo núcleo dependa directamente del esquema de bases legadas.

## 9. Auditoría transversal
La auditoría es un servicio de plataforma y no una función aislada. Debe registrar como mínimo:
- Usuario/servicio actor.
- Fecha y hora.
- Dirección/origen cuando corresponda.
- Acción.
- Entidad afectada.
- Identificador de registro.
- Resultado.
- Valores anteriores/posteriores cuando sea legal y técnicamente apropiado.
- Correlation ID para trazabilidad técnica.

## 10. Disponibilidad y resiliencia inicial
Para el MVP se prioriza simplicidad operativa con:
- respaldos automatizados;
- pruebas periódicas de restauración;
- monitoreo de salud;
- logs estructurados;
- límites y timeouts en integraciones;
- despliegues reproducibles.

Alta disponibilidad activa-activa se evaluará cuando existan métricas reales de uso y criticidad.

## 11. Criterios de aceptación arquitectónica
La primera implementación debe demostrar:
- SSO funcional.
- RBAC mínimo.
- CRUD gobernado de miembros y Talleres.
- relación miembro–Taller–cargo–periodo sin duplicaciones;
- auditoría básica;
- API documentada;
- despliegue por Docker;
- PostgreSQL con migraciones versionadas;
- pruebas automatizadas mínimas en CI.

## 12. Decisiones pendientes
- React vs Blazor.
- Proveedor concreto de identidad.
- Estrategia definitiva de almacenamiento documental.
- Solución de correo/notificaciones.
- Política de observabilidad productiva.
- Modelo de alta disponibilidad y contingencia.
