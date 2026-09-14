# PMGM-BLG-057 — Ficha de Taller y autoridades

**Estado:** En implementación  
**Prioridad:** P1  
**Corte:** QA / prototipo institucional v0.24  
**Módulos:** Core, Membresía, Gestión Logial, Tesorería, Hospitalaria, Privacidad

## Objetivo

Entregar una vista institucional única del Taller sin crear una nueva fuente de verdad: identidad, padrón agregado, grados, autoridades vigentes, regularidad autorizada y actividad reciente se proyectan desde los módulos ya existentes.

## Alcance

- `GET /api/institutional/organizations/{id}/profile`;
- control de alcance con `CanReadOrganization`;
- total de miembros con membresía activa;
- distribución por último grado registrado en el Taller;
- autoridades/cargos vigentes según fecha y período;
- estado mínimo de Gran Tesorería y Gran Hospitalaria sólo cuando el rol tiene finalidad;
- últimas Tenidas y sesiones de docencia;
- traslados entrantes/salientes recientes;
- UI responsive `Ficha de Taller`;
- selector de Taller autorizado;
- mocks completos para showcase standalone;
- pruebas de frontera Bearer-only y de política de regularidad.

## Privacidad y Ley 21.719

La ficha no expone correo, teléfono, dirección ni otros datos de contacto de los miembros. Los nombres de autoridades se muestran por su función institucional. La regularidad financiera/hospitalaria se omite completamente cuando el usuario puede leer el Taller pero no posee una finalidad autorizada para consultar dicha información.

La proyección reutiliza los tratamientos existentes de Core/Membresía, Gestión Logial, Gran Tesorería y Gran Hospitalaria; no crea un tratamiento independiente.

## Criterios de aceptación

1. Un usuario sin acceso al Taller recibe 403.
2. Un lector ordinario puede recibir identidad, autoridades y agregados, pero `regularity=null`.
3. Secretaría de Taller y roles de regularidad autorizados pueden consultar los estados mínimos de regularidad.
4. La ficha no contiene información de contacto personal.
5. El showcase muestra dos Talleres ficticios con autoridades, composición, Tenidas, docencia y traslados.
6. Frontend y backend pasan CI completo y gates de Ley 21.719.

## Siguiente incremento

- reportes ejecutivos comparativos por Taller y Orden;
- indicadores de actividad/asistencia en períodos configurables;
- enlaces contextuales entre Ficha de Taller, padrón, Régimen Interior y ceremonias;
- panel de alertas institucionales por regularidad y documentación pendiente.
