# PMGM-REQ-033 — Parametrización administrable del sistema

**Prioridad:** P0  
**Estado:** Implementado en demo y API; pendiente CI/merge  

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

## Experiencia QA

La demo presenta un perfil `Administrador del Sistema`, un menú `Sistema` y formularios funcionales en memoria. El instalable utiliza PostgreSQL mediante `InstitutionalRuleSetting` y endpoints autenticados.

La pantalla incluye una matriz consolidada para revisar los permisos vigentes de Venerable Maestro, Secretaría, Tesorería, Hospitalaria, Orador, Primer Vigilante, Segundo Vigilante, Ex Venerable Maestro, grandes dignatarios y administradores del sistema.
