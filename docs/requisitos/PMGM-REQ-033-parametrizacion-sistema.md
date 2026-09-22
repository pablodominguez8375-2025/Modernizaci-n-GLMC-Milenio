# PMGM-REQ-033 — Parametrización administrable del sistema

**Prioridad:** P0
**Estado:** Implementado en dev; identidad institucional v0.61 integrada mediante PR #132 (`e1eff39fa380144ef69e200010ced86c787e23a4`)

## Objetivo

Permitir que el Administrador del Sistema y los administradores de Gran Logia autorizados configuren reglas operativas sin modificar código, manteniendo vigencia temporal, fundamento y auditoría.

## Ámbitos iniciales

- pasos del flujo de aprobación de iniciaciones;
- días mínimos de publicación;
- número mínimo de entrevistas;
- plazo de bloqueo posterior a rechazo;
- tipos y campos de catalogación de Biblioteca Virtual;
- series documentales y conservación del Gran Archivo;
- extensiones y tamaño máximo del Gestor Documental;
- corte mensual de Tesorería;
- plazo de reposición de Hospitalaria;
- anticipación de notificaciones;
- duración de sesión.
- perfiles administrativos, cargos firmantes y cargos docentes del Taller;
- vistas y acciones habilitadas para cada perfil.

## Reglas obligatorias

1. Sólo usuarios con permiso `CanConfigureSystem` pueden consultar o modificar parámetros.
2. Cada cambio crea una nueva versión con fecha de vigencia y fundamento obligatorio.
3. La versión anterior se retira sin eliminarse.
4. Los cambios no alteran retroactivamente expedientes iniciados bajo una versión anterior.
5. Toda creación de versión genera auditoría.
6. Los códigos administrables pertenecen a una lista blanca; no se admiten parámetros arbitrarios.
7. La configuración base conserva valores derivados de los documentos 2026 y de los acuerdos del proyecto.
8. El administrador puede consultar todas las versiones y los cambios futuros programados.
9. Restaurar no sobrescribe antecedentes: carga el valor histórico como borrador y crea una nueva versión auditada.
10. El menú incorpora consola de backup/restauración, usuarios y perfiles, correo SMTP e identidad visual.
11. La restauración exige respaldo verificado, confirmación reforzada, ventana de mantenimiento, auditoría y smoke posterior.
12. Las cuentas se administran mediante Keycloak; las contraseñas no se almacenan en la base funcional.
13. Las credenciales SMTP se almacenan como secretos del servidor y nunca se devuelven completas a la interfaz.
14. Logos y colores aceptan vista previa antes de crear una versión institucional.
15. Los administradores pueden crear, editar y duplicar perfiles técnicos independientes de los cargos institucionales.
16. Cada perfil define por separado vistas visibles, acciones permitidas y alcance Orden/Taller.
17. Las definiciones se guardan como configuración versionada y auditable antes de asignarlas a usuarios.
18. Los perfiles base pueden modificarse, pero no eliminarse accidentalmente.
19. Antes de guardar, el administrador puede simular las vistas permitidas, bloqueadas y acciones efectivas.
20. Un perfil con alcance Taller no puede escalar a administración global de usuarios o del Sistema.
21. El perfil Administrador del Sistema no puede perder simultáneamente la vista Sistema y sus acciones críticas.
22. Las combinaciones dudosas generan advertencias antes de la aplicación.
23. Un usuario puede recibir múltiples perfiles y su acceso efectivo es la suma controlada de ellos.
24. Cada asignación define alcance, fecha de inicio y término opcional.
25. Las asignaciones futuras quedan programadas; las revocadas permanecen en el historial.
26. La pantalla muestra el acceso acumulado antes de confirmar nuevas asignaciones.
27. Sistema incluye una auditoría periódica de accesos con clasificación de riesgo.
28. Los administradores pueden certificar o revocar cada asignación y exportar la revisión en CSV.
29. Los perfiles privilegiados se recertifican según una frecuencia parametrizable; no tienen aprobación permanente.

### Identidad institucional — paleta oficial

La configuración de identidad visual usa la paleta de la Línea Base Maestra LB-PC-2026-09-17: azul institucional `#06148E`, azul complementario `#004AD4`, dorado `#F3C609` y dorado fuerte `#FBAE17`. El archivo oficial del logotipo se referencia sin deformarlo, recolorearlo ni recortarlo, según la Guía de uso del logotipo de la GLMCh. El incremento se integró mediante PR #132 y se publicó en Pages y en el instalable QA desde el SHA `e1eff39fa380144ef69e200010ced86c787e23a4`. Issue #97 mantiene pendiente el despliegue físico y la validación en `srv01`.

## Experiencia QA

La demo presenta un perfil `Administrador del Sistema`, un menú `Sistema` y formularios funcionales en memoria. El instalable utiliza PostgreSQL mediante `InstitutionalRuleSetting` y endpoints autenticados.

La pantalla incluye una matriz consolidada para revisar los permisos vigentes de Venerable Maestro, Secretaría, Tesorería, Hospitalaria, Orador, Primer Vigilante, Segundo Vigilante, Ex Venerable Maestro, grandes dignatarios y administradores del sistema.
