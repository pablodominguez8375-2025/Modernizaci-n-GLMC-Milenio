# PMGM-BLG-056 — Fichas de miembros y padrón de Taller

**Estado:** En implementación  
**Prioridad:** P1  
**Corte:** QA / prototipo institucional v0.23  
**Módulos:** Core/Membresía, Gestión Logial, Régimen Interior, Privacidad

## Objetivo

Convertir la información institucional ya persistida de cada miembro en una ficha operativa y trazable, utilizable desde el QA por los órganos y Talleres autorizados, sin duplicar la fuente de verdad.

## Alcance del corte

- padrón filtrable por Taller;
- búsqueda por nombre o número institucional;
- estado de membresía y grado actual;
- ficha con afiliación vigente, cargos y trayectoria;
- historial de membresías por Taller;
- visualización de traslados preservando el historial del Taller de origen;
- regularidad financiera/hospitalaria sólo para roles autorizados;
- datos de contacto separados de la ficha general y minimizados por rol;
- soporte mock para showcase standalone y consumo real en QA integrado;
- pruebas de frontera Bearer-only y política de privacidad de contacto.

## Reglas de privacidad

La ficha institucional no autoriza por sí sola a consultar correo, teléfono o dirección. El contacto sólo se proyecta para administración del Taller autorizado, Gran Logia Admin, Régimen Interior o Privacidad/DPD según alcance. Gran Secretaría y lectores ordinarios pueden consultar la ficha necesaria para su finalidad sin recibir esos campos.

La funcionalidad queda cubierta por `PMGM-DPA-001 Core/Membresía` del manifiesto Ley 21.719 existente; no se crea un tratamiento nuevo, sino una proyección minimizada sobre datos ya gestionados por dicho tratamiento.

## Criterios de aceptación

1. Un usuario sin alcance del Taller recibe 403.
2. El padrón no devuelve correo, teléfono ni dirección.
3. La ficha devuelve `contactVisible=false` y `contact=null` cuando el rol no necesita contacto.
4. Un rol administrativo autorizado puede ver el contacto.
5. El historial mantiene afiliaciones anteriores después de un cambio de Taller.
6. El showcase contiene datos ficticios para demostrar padrón, ficha y traslado.
7. Frontend y backend pasan CI, incluyendo gates de Ley 21.719.

## Siguiente incremento

- edición controlada de datos personales con auditoría y motivo;
- ficha del Taller con autoridades/cargos vigentes;
- reportes ejecutivos del padrón por Taller y Orden;
- enlace directo desde Gestión Logial, Régimen Interior y ceremonias hacia la ficha autorizada.
