# PMGM-BLG-001 — Backlog inicial del MVP

**Estado:** Activo  
**Versión:** 0.1

## Convención
- P0: bloqueante/cimiento.
- P1: imprescindible MVP.
- P2: siguiente incremento.
- Estado inicial: TODO salvo indicación contraria.

## EPIC 00 — Fundación

### PMGM-BLG-001 — Estructura de solución
**Prioridad:** P0
- Crear solución backend ASP.NET Core.
- Separar módulos/capas.
- Crear frontend base.
- Crear proyectos de tests.
- Agregar configuración por ambiente.
**Aceptación:** la solución compila y se ejecuta localmente sin secretos reales.

### PMGM-BLG-002 — PostgreSQL y migraciones
**Prioridad:** P0
- Conexión PostgreSQL.
- EF Core.
- primera migración.
- health check de BD.
**Aceptación:** despliegue limpio crea el esquema mediante migraciones versionadas.

### PMGM-BLG-003 — Docker de desarrollo
**Prioridad:** P0
- backend.
- frontend.
- PostgreSQL.
- proxy opcional de desarrollo.
**Aceptación:** un comando documentado levanta el stack de desarrollo.

### PMGM-BLG-004 — CI inicial
**Prioridad:** P0
- restore/build.
- pruebas.
- validación de formato/lint donde aplique.
**Aceptación:** cada PR a `dev` y `main` informa resultado reproducible.

## EPIC 01 — Identidad y seguridad

### PMGM-BLG-005 — Integración SSO
**Prioridad:** P0
**Aceptación:** usuario autenticado obtiene identidad y sesión mediante OpenID Connect.

### PMGM-BLG-006 — RBAC y contexto de organización
**Prioridad:** P0
**Aceptación:** permisos varían por rol y Taller; un rol local no accede automáticamente a otros Talleres.

### PMGM-BLG-007 — MFA administrativo
**Prioridad:** P1
**Aceptación:** perfiles administrativos requieren MFA según política del proveedor de identidad.

### PMGM-BLG-008 — Auditoría base
**Prioridad:** P0
**Aceptación:** se registran actor, acción, entidad, fecha, resultado y correlation ID para operaciones definidas.

## EPIC 02 — Base maestra institucional

### PMGM-BLG-009 — Personas
**Prioridad:** P1
- alta, consulta y edición controlada.
- identificador técnico interno.
- validación de duplicados.
**Aceptación:** RUT u otro identificador legal no funciona como clave primaria técnica.

### PMGM-BLG-010 — Miembros
**Prioridad:** P1
**Aceptación:** una persona puede vincularse a una ficha institucional sin duplicar sus datos maestros.

### PMGM-BLG-011 — Talleres/Logias
**Prioridad:** P1
**Aceptación:** administración de organizaciones con nombre, número, estado y jerarquía.

### PMGM-BLG-012 — Pertenencias
**Prioridad:** P1
**Aceptación:** se conserva historial de pertenencia y vigencia por Taller.

### PMGM-BLG-013 — Grados e historial
**Prioridad:** P1
**Aceptación:** cambios de grado crean hitos históricos y no borran los anteriores.

### PMGM-BLG-014 — Cargos y periodos
**Prioridad:** P1
**Aceptación:** se asignan cargos a miembro + organización + periodo con historial verificable.

## EPIC 03 — Intranet y experiencia

### PMGM-BLG-015 — Dashboard
**Prioridad:** P1
**Aceptación:** usuario autenticado ve accesos y datos relevantes según permisos, sin presentar módulos no autorizados como disponibles.

### PMGM-BLG-016 — Perfil del miembro
**Prioridad:** P1
**Aceptación:** el miembro ve sus datos institucionales permitidos y servicios desde un único acceso.

### PMGM-BLG-017 — Navegación modular responsiva
**Prioridad:** P1
**Aceptación:** navegación usable en escritorio y móvil, determinada por permisos.

## EPIC 04 — Secretaría y Régimen Interior

### PMGM-BLG-018 — Expediente institucional
**Prioridad:** P1
**Aceptación:** autoridades autorizadas consultan una vista consolidada sin replicar la ficha del miembro.

### PMGM-BLG-019 — Solicitudes institucionales
**Prioridad:** P1
- tipo.
- solicitante.
- responsable.
- estados.
- historial.
**Aceptación:** una solicitud conserva trazabilidad completa de cambios de estado.

### PMGM-BLG-020 — Ceremonias
**Prioridad:** P2
**Aceptación:** solicitudes de ceremonia se gestionan desde el acceso único y quedan asociadas al miembro/Taller correspondiente.

### PMGM-BLG-021 — Reportes de Régimen Interior
**Prioridad:** P2
**Aceptación:** reportes usan la base maestra y respetan permisos y filtros institucionales.

## EPIC 05 — Gestión Logial

### PMGM-BLG-022 — Tenidas
**Prioridad:** P1
**Aceptación:** crear y gestionar tenidas por Taller, tipo, grado, fecha y estado.

### PMGM-BLG-023 — Asistencia y excusas
**Prioridad:** P1
**Aceptación:** registrar presente/ausente/excusado/visita y generar reportes por periodo y grado.

### PMGM-BLG-024 — Actas y planchas
**Prioridad:** P2
**Aceptación:** documentos quedan asociados a la Tenida, versionados y protegidos por permisos.

## EPIC 06 — Documentos y Biblioteca

### PMGM-BLG-025 — Gestor documental base
**Prioridad:** P1
**Aceptación:** carga, clasificación, versión, descarga autorizada y hash de integridad.

### PMGM-BLG-026 — Biblioteca Virtual
**Prioridad:** P1
**Aceptación:** búsqueda y navegación por categorías, respetando nivel de acceso.

### PMGM-BLG-027 — CENDOC
**Prioridad:** P2
**Aceptación:** reutiliza el núcleo documental y no crea un repositorio aislado de identidades/metadatos.

## EPIC 07 — Datos y migración

### PMGM-BLG-028 — Inventario de fuentes legadas
**Prioridad:** P0
**Aceptación:** cada fuente queda clasificada como mantener/integrar/migrar/reemplazar/retirar.

### PMGM-BLG-029 — Perfilamiento y calidad de datos
**Prioridad:** P1
**Aceptación:** se identifican duplicados, campos incompletos, formatos y reglas de reconciliación antes de importar producción.

### PMGM-BLG-030 — Importador controlado
**Prioridad:** P2
**Aceptación:** importación repetible con logs, validación, modo dry-run y reporte de errores.

## EPIC 08 — Operación

### PMGM-BLG-031 — Backups y restauración
**Prioridad:** P0
**Aceptación:** backup automatizado y restauración probada y documentada.

### PMGM-BLG-032 — Logs y monitoreo
**Prioridad:** P1
**Aceptación:** health endpoints, logs estructurados y alerta básica para fallas críticas.

### PMGM-BLG-033 — Staging
**Prioridad:** P1
**Aceptación:** ambiente separado de producción, con secretos y BD propios.

## Orden recomendado de implementación
1. BLG-001 a 004.
2. BLG-002 + 008 + 031 desde el inicio.
3. BLG-005 y 006.
4. BLG-009 a 014.
5. BLG-015 a 019.
6. BLG-022, 023, 025 y 026.
7. Resto P2 según validación institucional.

## Criterio de MVP demostrable
El MVP se considera demostrable cuando un usuario puede:
1. autenticarse;
2. acceder a un dashboard según rol;
3. consultar/gestionar miembros y Talleres según permisos;
4. ver pertenencias, grados, cargos y periodos;
5. ejecutar al menos un flujo de Secretaría/Régimen Interior;
6. acceder a documentos/biblioteca autorizados;
7. dejar auditoría verificable de las operaciones anteriores.
