# PMGM-BLG-059 — Control Operativo de Régimen Interior

**Prioridad:** P1  
**Estado:** Implementado en rama / pendiente de CI y merge  
**Versión objetivo:** v0.26  
**Área responsable:** Régimen Interior

## Objetivo

Convertir los indicadores agregados de Régimen Interior y Reportería Ejecutiva en un listado operativo autorizado que permita revisar la trayectoria institucional de cada hermano/a sin perder el historial de Talleres ni exponer datos personales innecesarios.

## Alcance v0.26

Endpoint:

`GET /api/regimen-interior/members`

Filtros:

- fecha de corte (`asOf`);
- Taller relacionado, incluyendo relación histórica;
- estado institucional;
- grado actual;
- estado financiero;
- búsqueda por nombre, Nº institucional o Taller;
- sólo Past Active;
- sólo traslados pendientes;
- límite de filas (1–1000, 250 por defecto; UI 500).

Cada miembro aparece una sola vez e incluye:

- Nº institucional y nombre;
- relación vigente o histórica con el Taller consultado;
- Taller vigente y último Taller conocido;
- estado institucional actual y fecha efectiva;
- grado actual;
- fecha de iniciación;
- fecha de aumento de salario;
- fecha de exaltación;
- último retiro voluntario o forzoso y fecha;
- último reintegro;
- defunción, cuando corresponda;
- fecha del traslado más reciente;
- estado financiero vigente en el Taller actual;
- condición Past Active;
- traslado pendiente;
- cantidad de afiliaciones históricas registradas.

## Reglas de trazabilidad

1. El grado pertenece al hermano/a y se obtiene de su último evento de grado, aunque haya sido registrado en un Taller anterior.
2. Las afiliaciones históricas no se reescriben al trasladar al hermano/a.
3. Si se consulta el Taller de origen después de un traslado, el hermano/a puede aparecer con relación `historical`, conservando al mismo tiempo el Taller vigente actual.
4. Una afiliación `active` se considera vigente al corte si comenzó antes o en la fecha de corte y no ha terminado o termina en/tras esa fecha.
5. Past Active mantiene la definición operativa de v0.25: miembro actualmente relacionado que registra al menos un cargo institucional finalizado antes del corte.

## Ley 21.719 y minimización

La funcionalidad reutiliza `PMGM-DPA-002 — Régimen Interior`; no crea un tratamiento nuevo.

La proyección **no entrega**:

- correo;
- teléfono;
- dirección;
- notas internas;
- razones de retiro;
- evidencia documental;
- contenido de actas/documentos;
- datos de salud u Hospitalaria individual.

Controles:

- sólo `CanRunRegimenInteriorReports`;
- `Cache-Control: private, no-store`;
- cliente Bearer-only;
- same-origin;
- búsqueda y filtros sobre datos mínimos ya autorizados;
- sin tabla paralela ni duplicación de fuente de verdad.

## Criterios de aceptación

1. Usuario sin rol autorizado obtiene 403.
2. El Taller inexistente obtiene 404.
3. Filtros inválidos obtienen 400.
4. El mismo hermano aparece una vez por consulta.
5. Un traslado preserva Taller de origen histórico y Taller destino vigente.
6. Grado y fechas masónicas sobreviven al traslado.
7. Retiros, reintegros, defunción y traslado se muestran como hitos, sin notas/razones/evidencias.
8. La morosidad proviene del último snapshot de Gran Tesorería del Taller vigente.
9. Past Active usa cargos históricos terminados, sin mover esos cargos al Taller nuevo.
10. La UI funciona responsive y en showcase con datos ficticios.
11. Frontend usa Bearer token, `credentials: omit`, `cache: no-store` y `redirect: error`.
12. Backend, frontend, PostgreSQL, infraestructura y gates de Ley 21.719 deben quedar verdes antes del merge.

## Siguiente evolución sugerida

- drill-down desde Reportería Ejecutiva a este listado preservando filtros;
- exportación XLSX/PDF autorizada con marca de clasificación y auditoría;
- vista de inconsistencias de fechas (ej.: exaltación anterior a aumento de salario);
- cola de correcciones con responsable y evidencia;
- reglas estatutarias configurables para categorías institucionales específicas.
