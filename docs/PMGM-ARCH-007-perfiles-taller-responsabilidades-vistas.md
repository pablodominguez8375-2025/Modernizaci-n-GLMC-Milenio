# PMGM-ARCH-007 — Perfiles de Taller, responsabilidades y vistas normativas

**Estado:** Implementación en curso  
**Fecha:** 2026-09-17  
**Rama objetivo:** `dev`  
**Fuente superior:** Constitución y Reglamento General vigente de la Gran Logia Mixta de Chile  
**Fuentes complementarias:** Línea Base Maestra Proyecto Centenario; matriz funcional normativa de cargos de Taller; formularios y protocolos institucionales vigentes.

## 1. Principio rector

Los cargos institucionales del Taller y los perfiles técnicos de seguridad son conceptos relacionados pero distintos. El software no puede crear atribuciones masónicas nuevas por el solo hecho de habilitar una vista o una acción.

Cada permiso debe quedar respaldado por al menos una de estas categorías:

1. responsabilidad normativa directa;
2. intervención prevista en formulario o protocolo institucional vigente;
3. control operativo aprobado para seguridad, trazabilidad o segregación de funciones.

Los perfiles de Taller nunca deben adquirir permisos de administración global del sistema por defecto.

## 2. Cargos institucionales base

El sistema reconoce como cargos de Taller base:

- Venerable Maestro;
- Inmediato Ex-Venerable Maestro;
- Primer Vigilante;
- Segundo Vigilante;
- Orador/a;
- Secretario/a;
- Tesorero/a;
- Hospitalario/a.

El perfil técnico `lodge_admin` continúa existiendo como perfil de seguridad/administración local y **no equivale a un cargo institucional ni integra por sí solo el Consejo de Administración**.

## 3. Responsabilidades y vistas base

### Venerable Maestro

Base principal: Art. 11.2 y reglas institucionales aplicables.

Vistas base: Dashboard; Miembros; Gestión del Taller; Tenidas; Actas; Consejo de Administración; Insinuados; Circuito de iniciación; Retiros y traslados; Tesorería; Hospitalaria; Documentos; Calendario; Bandeja de pendientes.

Acciones base: consultar, revisar, aprobar/autorizar cuando el flujo lo determine, firmar, inspeccionar y exportar. La supervisión de Tesorería/Hospitalaria no otorga edición general de sus registros.

### Secretario/a

Base principal: Art. 12.11.

Vistas base: Mi ficha; Dashboard; Miembros; Gestión del Taller; Tenidas; Actas; Consejo de Administración; Insinuados; Circuito de iniciación; Retiros y traslados; Documentos; Calendario; Notificaciones; Bandeja de pendientes.

Acciones base: consultar, crear, modificar datos administrativos autorizados, registrar, cargar/descargar archivos, firmar cuando corresponda, remitir y exportar.

### Tesorero/a

Base principal: Art. 12.12.

Vistas base: Dashboard financiero; Consejo de Administración; Circuito de iniciación; Tesorería; Documentos financieros; Bandeja de pendientes.

Acciones base: consultar, crear, modificar y registrar movimientos dentro de su competencia; emitir/gestionar comprobantes y rendiciones; remitir y firmar documentos aplicables. No administra Hospitalaria.

### Hospitalario/a

Base principal: Art. 12.13.

Vistas base: Dashboard hospitalario; Consejo de Administración; Circuito de iniciación; Hospitalaria; Documentos/rendiciones; Bandeja de pendientes.

Acciones base: consultar, crear, modificar y registrar aportes, ayudas, socorros, reposiciones, visitas y rendiciones dentro de su competencia. No administra Tesorería. La información sensible se limita al mínimo necesario.

### Orador/a

Base principal: Arts. 12.8 y 12.9.

Vistas base: Gestión del Taller; Actas; Consejo de Administración; Insinuados; Circuito de iniciación; Retiros y traslados; Elecciones y votaciones; Memoria Anual; Normativa; Documentos.

Acciones base: consultar, revisar, validar desde el ámbito normativo, firmar documentos aplicables y exportar. No recibe una facultad ejecutiva genérica ni puede presidir la Logia.

### Primer Vigilante

Base principal: Arts. 12.3, 12.5 y 12.6.

Vistas base: Dashboard; Gestión del Taller; Tenidas; Consejo de Administración; Docencia de Compañeros; Biblioteca autorizada.

Acciones base: consultar, crear/modificar/registrar planificación e instrucción de Compañeros, asistencia, trabajos y seguimiento; cargar/descargar material y exportar reportes docentes.

### Segundo Vigilante

Base principal: Arts. 12.3, 12.5 y 12.6.

Vistas base: Dashboard; Gestión del Taller; Tenidas; Consejo de Administración; Docencia de Aprendices; Biblioteca autorizada.

Acciones base: consultar, crear/modificar/registrar planificación e instrucción de Aprendices, asistencia, trabajos y seguimiento; cargar/descargar material y exportar reportes docentes.

### Inmediato Ex-Venerable Maestro

Base principal: Arts. 12.2 y 12.5.

Vistas base: Dashboard; Gestión del Taller; Consejo de Administración; Docencia de Maestros; Biblioteca autorizada.

Acciones base: consultar, crear/modificar/registrar planificación e instrucción de Maestros, asistencia, trabajos y seguimiento; cargar/descargar material y exportar reportes docentes.

La subrogación del Venerable no se interpreta como una ampliación permanente de permisos. Cuando corresponda, debe existir un contexto temporal, trazable y verificable.

## 4. Consejo de Administración

Los ocho cargos institucionales anteriores son reconocidos por el modelo como integrantes funcionales del Consejo de Administración. El sistema debe conservar asistencia, acuerdos, documentos y decisiones con trazabilidad.

Un perfil técnico de administración local (`lodge_admin`) no se transforma automáticamente en miembro del Consejo.

## 5. Docencia por grado

La autorización operativa queda alineada con la responsabilidad reglamentaria:

- grado 1 / Aprendices → Segundo Vigilante;
- grado 2 / Compañeros → Primer Vigilante;
- grado 3 / Maestros → Inmediato Ex-Venerable Maestro.

El backend debe aplicar también el límite de Taller: un oficial no puede administrar docencia de otro Taller solo por poseer el mismo rol.

## 6. Compatibilidad técnica

Se conserva temporalmente el claim histórico `lodge_past_master` para no romper configuraciones existentes, pero su significado funcional vigente es **Inmediato Ex-Venerable Maestro**.

Los roles incorporados/normalizados son:

- `lodge_venerable`;
- `lodge_secretariat`;
- `lodge_treasury`;
- `lodge_hospitalaria`;
- `lodge_orator`;
- `lodge_first_warden`;
- `lodge_second_warden`;
- `lodge_past_master` (Inmediato Ex-Venerable Maestro).

## 7. Pruebas mínimas obligatorias

- Hospitalaria sólo administra Hospitalaria de su propio Taller.
- Primer Vigilante administra docencia de Compañeros, no de Aprendices.
- Segundo Vigilante administra docencia de Aprendices, no de Compañeros.
- Inmediato Ex-Venerable administra docencia de Maestros.
- Ningún cargo cruza el límite de Taller por defecto.
- Los ocho cargos institucionales son reconocidos como miembros del Consejo.
- `lodge_admin` no se trata como cargo institucional del Consejo.
- Un perfil de Taller no puede escalar a administración global del sistema.

## 8. Aplicación en Demo y QA

GitHub Pages debe mostrar los ocho perfiles base, sus vistas y acciones con datos ficticios. La configuración visual no reemplaza el control real de backend.

QA debe validar los claims OIDC, límites de organización/Taller y autorización efectiva en API. Una función no se considera operacional sólo porque esté visible en la demo.
