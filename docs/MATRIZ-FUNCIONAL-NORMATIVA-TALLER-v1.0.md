# Proyecto Centenario — Matriz funcional-normativa de cargos de Taller v1.0

Estado: **vigente para desarrollo**  
Fecha: 2026-09-17  
Base: Constitución y Reglamento General GLMCh + formularios/protocolos 2026 + Línea Base LB-PC-2026-09-17.

## Continuidad del desarrollo

Este documento **no define un proyecto nuevo, una aplicación separada ni un desarrollo paralelo**. Es una especificación funcional y normativa interna del **mismo Proyecto Centenario** y debe implementarse sobre el código, arquitectura, modelos, repositorio, base de datos y flujos ya vigentes.

El componente técnico `Centenario.Authorization` es solo una pieza interna reutilizable del backend del Proyecto Centenario. Debe integrarse con la API, autenticación, PostgreSQL, auditoría, frontend y demás módulos de la misma solución. No debe evolucionar como producto independiente.

Toda implementación derivada de esta matriz debe respetar la regla de no regresión, continuar desde `main` y reutilizar lo ya desarrollado.

## Regla de diseño

Una vista o acción queda habilitada únicamente cuando existe una de estas justificaciones:

1. **Normativa directa**: Constitución, Reglamento General o disposición institucional aplicable.
2. **Protocolo institucional**: formulario, protocolo o flujo oficial vigente.
3. **Control operativo**: mecanismo de seguridad, segregación de funciones, trazabilidad o continuidad aprobado para el software.

El control operativo **no crea una atribución masónica nueva**.

## Acciones normalizadas del software

- Ver
- Crear
- Editar
- Registrar
- Revisar
- Validar
- Aprobar
- Autorizar
- Firmar
- Remitir
- Informar
- Inspeccionar

## Venerable Maestro

### Responsabilidad normativa
Art. 11.2 y normas relacionadas.

### Vistas
- Dashboard Taller
- Ficha del Taller
- Gestión Logial
- Tenidas
- Actas
- Consejo de Administración
- Insinuaciones
- Ceremonias
- Retiros/Traslados
- Tesorería
- Hospitalaria
- Ayudas Hospitalarias
- Documentos
- Agenda
- Reportes
- Bandeja de Pendientes

### Reglas
- Firma actas y documentos que correspondan.
- Inspecciona Tesorería y Hospitalaria; **inspeccionar no equivale a editar**.
- El control de autorización de egresos de Tesorería se registra como **control operativo del proyecto**.
- En Hospitalaria, la autorización normativa de socorros puede provenir del **Consejo de Administración o del Venerable Maestro**.
- Los datos sensibles de Hospitalaria se exponen con minimización.

## Secretario/a

### Responsabilidad normativa
Art. 12.11.

### Vistas
- Dashboard Secretaría
- Ficha del Taller
- Hermanos — datos administrativos autorizados
- Tenidas
- Actas
- Consejo de Administración
- Insinuaciones
- Ceremonias
- Retiros/Traslados
- Documentos
- Agenda
- Notificaciones
- Bandeja de Pendientes

### Reglas
- Es el principal operador administrativo.
- Registra Tenidas, asistencia, oficialidad, excusas, proposiciones, correspondencia, trabajos y acuerdos.
- Firma actas aprobadas.
- Remite los antecedentes reglamentarios a Gran Secretaría.
- No modifica Tesorería, Hospitalaria ni decisiones normativas fuera de su competencia.

## Tesorero/a

### Responsabilidad normativa
Art. 12.12.

### Vistas
- Dashboard Tesorería
- Tesorería
- Presupuesto
- Cuotas
- Morosidad
- Cuadro mensual
- Ceremonias — solo antecedentes financieros que correspondan
- Retiros/Traslados — firma CRV/CRF cuando corresponda
- Documentos financieros
- Consejo de Administración
- Bandeja de Pendientes

### Reglas
- Mantiene Tesorería y presupuesto.
- Informa morosidad al Consejo.
- No administra Hospitalaria.
- El Venerable puede inspeccionar, pero no sustituye al Tesorero en el registro financiero.

## Hospitalario/a

### Responsabilidad normativa
Art. 12.13.

### Vistas
- Dashboard Hospitalaria
- Hospitalaria
- Tronco de Beneficencia
- Ayudas Hospitalarias
- Visitas Hospitalarias
- Reposiciones
- Ceremonias — solo requisitos hospitalarios aplicables
- Documentos y rendiciones
- Consejo de Administración
- Bandeja de Pendientes

### Reglas
- Hospitalaria es independiente de Tesorería.
- El Hospitalario registra y ejecuta socorros autorizados.
- La evidencia de autorización debe aceptar:
  - acuerdo del Consejo de Administración; o
  - aprobación del Venerable Maestro.
- Los antecedentes personales sensibles deben tener acceso restringido.

## Orador/a

### Responsabilidad normativa
Art. 12.8 y 12.9.

### Vistas
- Dashboard Orador
- Normativa
- Gestión Logial
- Actas
- Elecciones y Votaciones
- Memoria Anual
- Decretos
- Insinuaciones
- Retiros/Traslados
- Documentos
- Consejo de Administración
- Bandeja de Pendientes

### Reglas
- Revisa cumplimiento normativo.
- Controla elecciones, votaciones y escrutinios.
- Firma actas y extractos aprobados.
- No recibe autorización ejecutiva general.
- No puede presidir la Logia.

## Primer Vigilante

### Responsabilidad normativa
Art. 12.3, 12.5 y 12.6.

### Vistas
- Dashboard Compañeros
- Docencia de Compañeros
- Asistencia de Compañeros
- Trabajos de Compañeros
- Evaluación de Columna
- Tenidas — consulta
- Consejo de Administración
- Biblioteca Virtual
- Bandeja de Pendientes

### Reglas
- Responsable de la instrucción de Compañeros.
- Mantiene registro de cámaras, asistencia, trabajos, méritos y deméritos.
- Puede emitir los informes que reglamentariamente correspondan.
- No administra Secretaría, Tesorería ni Hospitalaria.

## Segundo Vigilante

### Responsabilidad normativa
Art. 12.3, 12.5 y 12.6.

### Vistas
- Dashboard Aprendices
- Docencia de Aprendices
- Asistencia de Aprendices
- Trabajos de Aprendices
- Evaluación de Columna
- Tenidas — consulta
- Consejo de Administración
- Biblioteca Virtual
- Bandeja de Pendientes

### Reglas
- Responsable de la instrucción de Aprendices.
- Mantiene registro de cámaras, asistencia, trabajos, méritos y deméritos.
- Puede emitir los informes que reglamentariamente correspondan.
- No administra Secretaría, Tesorería ni Hospitalaria.

## Inmediato Ex-Venerable Maestro

### Responsabilidad normativa
Art. 12.2 y 12.5.

### Vistas
- Dashboard Maestros
- Docencia de Maestros
- Asistencia de Maestros
- Trabajos de Maestros
- Tenidas — consulta
- Consejo de Administración
- Biblioteca Virtual
- Bandeja de Pendientes

### Reglas
- Responsable de instrucción de Maestros.
- Preside en ausencia del Venerable en el orden reglamentario.
- **No hereda automáticamente todos los permisos ejecutivos del Venerable**.
- Toda ampliación temporal por subrogación se registra como permiso temporal explícito, con vigencia, motivo y referencia de autoridad.

## Consejo de Administración

Integración conforme a Art. 10.1:

- Venerable Maestro
- Inmediato Ex-Venerable Maestro
- Primer Vigilante
- Segundo Vigilante
- Orador
- Secretario
- Tesorero
- Hospitalario

El Consejo se modela además como **órgano colegiado**. Una decisión del Consejo no debe simularse como una acción individual de un cargo.

## Firmas CRV / CRF

Los formularios institucionales 2026 requieren:

1. Venerable Maestro
2. Tesorero/a
3. Orador/a
4. Secretario/a

El workflow no puede pasar a estado completo si falta alguno de estos cuatro firmantes.

## Subrogación

Orden reglamentario:

Venerable Maestro → Inmediato Ex-Venerable Maestro → Primer Vigilante → Segundo Vigilante.

La subrogación se implementa mediante **permisos temporales explícitos**, no mediante cambio permanente del perfil del usuario.

Cada concesión temporal debe registrar:

- usuario;
- Taller;
- vista;
- acción;
- inicio;
- término;
- motivo;
- acta/decreto/referencia de autoridad;
- auditoría de creación y revocación.

## Protección de datos

Las vistas de Hospitalaria, morosidad, ayuda y antecedentes confidenciales se clasifican como sensibles/restringidas.

El frontend deberá ocultar por defecto las vistas sin permiso y el backend deberá volver a validar cada acción. Ocultar botones no constituye seguridad.

## Estado de implementación

Esta matriz se materializa inicialmente, dentro del mismo Proyecto Centenario, en:

- `backend/Centenario.Authorization/`
- `backend/Centenario.Authorization.SmokeTests/`
- `database/001_rbac_taller.sql`
- `adrs/ADR-001-RBAC-NORMATIVO-TALLER.md`

El catálogo de permisos está desacoplado del frontend únicamente como decisión de arquitectura interna para permitir su integración posterior con React o Blazor. **No es una aplicación separada** y debe continuar integrándose con el resto del Proyecto Centenario.
