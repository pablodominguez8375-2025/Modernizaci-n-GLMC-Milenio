# PMGM-BLG-049 — Gestión Logial: Tenidas, asistencia y actas versionadas

**Estado:** Implementado en `dev`  
**Prioridad:** P1  
**Versión API:** 0.13.0  
**Fecha de cierre técnico:** 2026-09-08

## Objetivo
Entregar a cada Taller una operación básica de Secretaría Logial sobre la base maestra institucional, sin duplicar fichas de miembros y preservando trazabilidad histórica de asistencia y actas.

## Alcance implementado
- Creación y listado de Tenidas por Taller.
- Tipos de Tenida: regular, solemne, instrucción, aniversario, fúnebre y especial.
- Grados: aprendiz, compañero, maestro o todos los grados.
- Cierre de Tenida con bloqueo posterior de nuevas asistencias.
- Selector minimizado de miembros activos del Taller: ID operativo + nombre de presentación.
- Registro de asistencia: presente, justificado y ausente.
- Rectificación de asistencia por eventos append-only: el registro anterior se conserva.
- Consulta de asistencia vigente tomando el último evento de cada miembro.
- Actas por versiones inmutables.
- Aprobación de una nueva versión dejando la aprobación anterior como `superseded`.
- Auditoría persistente de creación/cierre de Tenidas, asistencia y versiones/aprobaciones de actas.
- Interfaz React responsive y cliente de dominio Lodge Management con OIDC Bearer.

## Seguridad y privacidad
- El acceso operativo está limitado a administración de Gran Logia y administración/secretaría del Taller correspondiente.
- Régimen Interior, Gran Secretaría, Gran Tesorería y Gran Hospitalaria no heredan acceso a actas o asistencia sólo por tener alcance de Orden.
- La interfaz no utiliza RUT, correo ni número institucional para operar asistencia.
- `LodgeMeeting.Grade`, estados de asistencia, identificadores de miembro y contenido de actas están clasificados según sensibilidad/finalidad.
- `AuditMetadataSanitizer` elimina de metadata de auditoría identificadores y contenidos sensibles conocidos.
- `Cache-Control: private, no-store` en consultas de miembros, asistencia y actas.

## Persistencia
Migración: `20260908101500_AddLodgeManagement`

Tablas:
- `core.lodge_meetings`
- `core.lodge_attendance_records`
- `core.lodge_minutes`

La migración pertenece a la cadena principal `PmgmDbContext`; `LodgeManagementDbContext` opera las mismas tablas como contexto modular sobre la misma base PostgreSQL.

## Pruebas de aceptación
- Crear una Tenida válida para un Taller.
- Registrar asistencia `present` y luego rectificar a `excused`.
- Ver sólo la asistencia vigente en la API y conservar ambos eventos en PostgreSQL.
- Crear acta v1, aprobarla, crear v2 y aprobarla.
- Conservar el texto original de v1 como versión reemplazada y v2 como aprobada.
- Confirmar AuditEvent para Tenida, asistencia y actas.
- Confirmar que el selector de miembros no contiene correo ni número institucional.
- Confirmar que Régimen Interior no obtiene capacidad de Gestión Logial.
- Superar Privacy Gate, Data Classification Gate, Migration Safety Gate, backend/PostgreSQL, frontend/Nginx e infraestructura.

## Pendientes posteriores
- Planchas vinculadas a Tenidas.
- Agenda/orden del día estructurado.
- Invitados y visitantes con reglas específicas.
- Firma/aprobación colegiada de actas si la normativa interna lo requiere.
- Exportación PDF/Word controlada por finalidad y permisos.
