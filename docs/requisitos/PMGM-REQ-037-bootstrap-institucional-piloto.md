# PMGM-REQ-037 — Bootstrap institucional del piloto

## Objetivo
Permitir que el Superadmin configure la estructura institucional mínima de Proyecto Milenio sin SQL manual, sin datos demo y sin almacenar identidades personales reales en Git.

## Alcance v0.33
- Institución matriz: **Gran Logia Mixta de Chile**.
- Taller inicial: **Respetable Logia Libertad Nº 23**.
- Rol superior de plataforma: `platform_superadmin`.
- Roles administrativos base: Administrador de Gran Logia y Administrador de Taller.
- Catálogo de cargos de Taller separado de los permisos de seguridad.
- Validación previa (`dry-run`) antes de cualquier escritura.
- Aplicación transaccional, auditable e idempotente.
- Una combinación `packageKey` + `packageVersion` no puede reutilizarse con un payload distinto.

## Cargos principales de Taller
Venerable Maestro; Inmediato Ex Venerable Maestro; Primer Vigilante; Segundo Vigilante; Orador; Secretario/a; Hospitalaria; Tesorero/a.

## Cargos ritualísticos iniciales
Primer Diácono; Segundo Diácono; Maestro de Ceremonias; Guarda Templo Interno; Guarda Templo Externo (alias Retejador); Maestro de Armonía.

## Seguridad
- `apply` sólo puede ejecutarlo un `platform_superadmin` con `pmgm_scope=order`.
- El rol Superadmin no reemplaza al Administrador institucional; es una capa superior de plataforma.
- Los cargos institucionales no conceden permisos técnicos por sí mismos.
- Perfiles de soporte configurables se modelarán sobre permisos granulares sin heredar propiedad completa de la plataforma.

## Privacidad — Ley 21.719
El paquete v0.33 no acepta datos de personas, miembros, RUT, email, teléfono ni credenciales. La tabla de bootstrap persiste sólo metadatos técnicos del paquete. La identidad de quien ejecuta una aplicación queda exclusivamente en el subsistema de auditoría ya clasificado.

## Criterios de aceptación
1. Dry-run detecta conflictos sin modificar datos.
2. Apply crea o verifica una única institución matriz y Talleres hijos.
3. Reejecutar el mismo paquete no duplica registros.
4. Mismo key/version con contenido diferente es rechazado.
5. Se crean catálogos base de seguridad y cargos.
6. Toda aplicación exitosa genera evento de auditoría.
7. PostgreSQL y frontend pasan CI junto con los gates de privacidad/migración.
