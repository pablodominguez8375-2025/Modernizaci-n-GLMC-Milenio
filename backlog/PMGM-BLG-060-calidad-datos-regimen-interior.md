# PMGM-BLG-060 — Calidad de datos de Régimen Interior

**Prioridad:** P1  
**Estado:** Implementado en rama / pendiente de CI y merge  
**Versión objetivo:** v0.27  
**Área responsable:** Régimen Interior

## Objetivo

Permitir que Régimen Interior detecte automáticamente fechas, secuencias y relaciones históricas que requieren corroboración, sin modificar el padrón ni reescribir la historia institucional de forma automática.

## Alcance v0.27

Endpoint read-only:

`GET /api/regimen-interior/data-quality`

Filtros:

- fecha de corte (`asOf`);
- Taller relacionado o toda la Orden;
- severidad (`error` / `warning`);
- código de regla;
- búsqueda por nombre, Nº institucional, Taller, título o código;
- límite de resultados (1–1000; 250 por defecto).

Respuesta:

- errores detectados;
- advertencias detectadas;
- cantidad de miembros afectados;
- distribución por código de regla;
- lista explicable de observaciones;
- fecha observada y fecha relacionada, cuando corresponda;
- acción sugerida para corroboración humana.

## Reglas iniciales

### Afiliaciones

- rango de afiliación inválido;
- más de una afiliación vigente al mismo corte;
- afiliaciones de Taller superpuestas;
- estado activo/reintegrado sin afiliación vigente.

### Grados

- aumento de salario sin iniciación previa;
- exaltación sin aumento de salario previo;
- aumento de salario anterior a la iniciación;
- exaltación anterior al aumento de salario;
- hitos de grado duplicados.

### Estado institucional

- reintegro sin retiro previo;
- retiros consecutivos sin reintegro;
- eventos institucionales posteriores a una defunción.

### Defunción y actividad posterior

- grado posterior a defunción;
- cargo iniciado después de defunción;
- afiliación vigente después de defunción.

### Cargos

- período de cargo con fecha de término anterior al inicio.

### Traslados

- mismo Taller como origen y destino;
- fecha efectiva propuesta anterior a la solicitud;
- fecha aprobada anterior a la solicitud;
- traslado ejecutado sin afiliación destino;
- fecha de afiliación destino distinta de la fecha efectiva;
- traslado fuera del período de afiliación origen.

## Política de corrección

v0.27 **no modifica datos**. Una observación:

1. identifica una inconsistencia o caso dudoso;
2. explica por qué requiere revisión;
3. muestra las fechas mínimas necesarias;
4. propone una acción de corroboración;
5. deja la corrección para un flujo humano con evidencia institucional.

Esto evita que una regla automática elimine, cambie o reconstruya historia masónica sin respaldo.

## Ley 21.719 y minimización

La funcionalidad reutiliza `PMGM-DPA-002 — Régimen Interior`; no crea una finalidad nueva.

Puede proyectar, sólo a roles autorizados:

- nombre;
- Nº institucional;
- Taller relacionado;
- fechas institucionales necesarias para explicar la inconsistencia.

No proyecta:

- correo;
- teléfono;
- dirección;
- notas internas;
- razones de retiro;
- evidencia documental;
- contenido de actas/decretos;
- datos individuales de Hospitalaria.

Controles:

- `CanRunRegimenInteriorReports`;
- `Cache-Control: private, no-store`;
- cliente Bearer-only;
- same-origin;
- read-only;
- sin tablas paralelas ni correcciones automáticas.

## Criterios de aceptación

1. Usuario no autorizado obtiene 403.
2. Taller inexistente obtiene 404.
3. Severidad inválida obtiene 400.
4. El motor detecta secuencias imposibles de grados.
5. El motor detecta actividad posterior a defunción.
6. El motor detecta afiliaciones y traslados incompatibles.
7. Cada observación incluye código, severidad, explicación y acción sugerida.
8. Los filtros no exponen datos adicionales.
9. La UI muestra resumen, filtros, reglas activas y detalle responsive.
10. El showcase incluye observaciones ficticias representativas.
11. Frontend usa Bearer token, `credentials: omit`, `cache: no-store` y `redirect: error`.
12. Backend, frontend, PostgreSQL, infraestructura y gates de Ley 21.719 deben quedar verdes antes del merge.

## Siguiente evolución sugerida

- cola formal de casos por corroborar con responsable y estado;
- vinculación controlada de evidencia/acta/decreto;
- resolución de observación con auditoría;
- reglas estatutarias configurables;
- exportación autorizada del informe de inconsistencias;
- métricas de calidad de padrón por Taller y por Orden.
