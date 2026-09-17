# ADR-001 — RBAC normativo para cargos de Taller

Fecha: 2026-09-17  
Estado: **Aceptado para implementación inicial**

## Contexto

El sistema requiere vistas y acciones diferenciadas para los cargos del Taller, pero dichas acciones deben respetar la Constitución y Reglamento General de la Gran Logia Mixta de Chile, formularios institucionales y reglas operativas del Proyecto Centenario.

## Decisión

Se implementará un catálogo RBAC basado en:

`Perfil + Vista + Acción + Base + Fuente + Sensibilidad`

Donde `Base` distingue:

- NormativaDirecta
- ProtocoloInstitucional
- ControlOperativo

No se implementará herencia automática de permisos entre cargos.

## Subrogación

La subrogación no cambia el perfil permanente del usuario y no copia automáticamente todos los permisos del cargo subrogado.

Se usarán permisos temporales explícitos con:

- rango de vigencia;
- motivo;
- referencia de autoridad;
- auditoría.

## Consejo de Administración

Se considera órgano colegiado. Las decisiones colegiadas se registrarán como evidencia del Consejo, no como privilegio individual de uno de sus integrantes.

## Hospitalaria

La ejecución de una ayuda requiere evidencia de:

- acuerdo del Consejo de Administración; **o**
- aprobación del Venerable Maestro.

Hospitalaria permanece separada de Tesorería.

## Seguridad

El frontend solo usa el catálogo para construir navegación y botones. La autorización efectiva se valida en backend.

## Consecuencias

- Evita que una vista otorgue atribuciones no reglamentarias.
- Permite auditoría del fundamento de cada permiso.
- Facilita parametrización futura.
- Mantiene el desarrollo independiente de React/Blazor.
