# PMGM-BLG-001 — Backlog inicial del MVP

**Estado:** Activo  
**Versión:** 0.5

## Convención
- P0: bloqueante/cimiento.
- P1: imprescindible MVP.
- P2: siguiente incremento.

## Prioridades consolidadas

### Fundación, seguridad y protección de datos
- P0 — backend ASP.NET Core / .NET 10.
- P0 — PostgreSQL + EF Core + migraciones.
- P0 — Docker y CI.
- P0 — SSO/OIDC.
- P0 — RBAC por Orden/Taller.
- P0 — auditoría transversal.
- P0 — backups y restauración.
- P0 — PMGM-BLG-048 Cumplimiento Ley 21.719.
- P0 — inventario de actividades de tratamiento y finalidades.
- P0 — clasificación de datos personales/sensibles.
- P0 — privacidad por diseño y por defecto.
- P0 — política de conservación, anonimización y supresión.
- P0 — flujo de derechos de titulares.
- P0 — gestión de incidentes de privacidad/seguridad.
- P0 — gate de privacidad previo a producción.

### Base maestra institucional
- P1 — Personas.
- P1 — Miembros.
- P1 — Talleres/Logias.
- P1 — Pertenencias históricas.
- P1 — Grados e historial.
- P1 — Cargos y períodos.
- P1 — Transferencias trazables entre Talleres (PMGM-BLG-044).
- P1 — conservación diferenciada por finalidad y base jurídica.

### Régimen Interior
- P1 — expediente institucional consolidado.
- P1 — reportes por Orden y Taller.
- P1 — activos/inactivos/retiros/reintegros/defunciones.
- P1 — consistencia histórica.
- P1 — elegibilidad explicable para ceremonias y reconocimientos.
- P1 — tablero ejecutivo.
- P1 — acceso a datos sensibles estrictamente por finalidad y alcance.

### Gran Secretaría
- P1 — decretos, comunicados y circulares.
- P1 — plantillas y correlativos.
- P1 — autorizaciones de iniciación, aumento de salario y exaltación.
- P1 — gestión de templos y salas.
- P1 — calendario institucional de espacios.
- P1 — consumo mínimo de estados de regularidad sin exponer detalle financiero innecesario.

### Ceremonias y validaciones inter-área
- P1 — PMGM-BLG-046 Elegibilidad de ceremonias y portal de insinuados.
- P1 — aprobación de Régimen Interior.
- P1 — validación de Taller al día con Gran Tesorería.
- P1 — validación de reposiciones al día con Gran Hospitalaria.
- P1 — excepciones formales y auditadas.
- P1 — publicación previa del insinuado en iniciaciones.
- P1 — plazo mínimo configurable; valor inicial 20 días.
- P1 — portal institucional de publicaciones vigentes.
- P1 — matriz única de requisitos y bloqueos para Gran Secretaría.
- P0 — minimización y política de visibilidad del Portal de Insinuados.

### Gestión Logial
- P1 — Tenidas.
- P1 — Asistencia y excusas.
- P2 — Actas y planchas.

### Documentos y Biblioteca
- P1 — gestor documental base.
- P1 — Biblioteca Virtual.
- P2 — CENDOC.
- P1 — clasificación, conservación y acceso por sensibilidad/finalidad.

### Datos y operación
- P0 — inventario de fuentes legadas.
- P1 — calidad y reconciliación de datos.
- P2 — importador controlado.
- P1 — logs, monitoreo y staging.
- P0 — inventario de encargados/proveedores con acceso a datos.
- P0 — inventario de transferencias internacionales de datos.
- P1 — soporte a EIPD/DPIA para tratamientos de alto riesgo.

## Orden recomendado de implementación
1. Fundación, BD, Docker y CI.
2. SSO/RBAC/auditoría.
3. Ley 21.719: inventario, clasificación, finalidad/base jurídica, conservación y gate de privacidad.
4. Persona/Miembro/Taller/Pertenencia/Grado/Cargo.
5. Transferencias entre Talleres.
6. Régimen Interior y reportes.
7. Regularidad Gran Tesorería + Gran Hospitalaria.
8. Solicitudes y motor de elegibilidad de ceremonias.
9. Portal de insinuados y configuración de plazos con minimización por defecto.
10. Gran Secretaría: autorizaciones, documentos oficiales y espacios.
11. Derechos de titulares, incidentes, proveedores y transferencias internacionales.
12. Dashboard e intranet.
13. Gestión Logial y documentación.

## Criterio de MVP demostrable
El MVP se considera demostrable cuando un usuario puede:
1. autenticarse;
2. ver un dashboard según rol;
3. consultar y gestionar miembros/Talleres según permisos;
4. reconstruir pertenencias, grados, cargos y estados históricos;
5. ejecutar una transferencia trazable entre Talleres;
6. consultar reportes de Régimen Interior;
7. solicitar una ceremonia;
8. visualizar validaciones de Régimen Interior, Gran Tesorería y Gran Hospitalaria;
9. bloquear automáticamente ceremonias cuando el Taller no cumple requisitos;
10. para una iniciación, publicar al insinuado y computar el plazo mínimo configurable;
11. impedir autorización antes de cumplir el plazo de publicación;
12. generar desde Gran Secretaría el documento oficial cuando todas las validaciones estén conformes;
13. reservar un templo/sala sin doble reserva;
14. conservar auditoría verificable de las operaciones anteriores;
15. demostrar minimización de datos por finalidad y alcance;
16. registrar finalidad, base jurídica y política de conservación de los tratamientos relevantes;
17. gestionar solicitudes de derechos de titulares;
18. disponer de flujo de incidentes de privacidad/seguridad;
19. impedir una salida a producción si un tratamiento personal relevante carece de finalidad, base jurídica, conservación o control de acceso definidos.
