# Handoff — Claude — revisión transversal de legibilidad en todas las vistas — 03-10-2026

- **Issue:** #294 (`agente:claude`). Instrucción del PO del 03-10-2026: continuar con mejoras en todos los menús y vistas.
- **Revisión previa de Drive:** no hay archivos nuevos.
- **Base:** `dev@32e02ab`.
- **Versión:** UI QA v0.81.
- **Alcance:** solo CSS (`common-views.css`).

## Auditoría automática

Se revisaron 27 vistas de 10 perfiles a 1440 px, contando:

- textos visibles de menos de 13,5 px;
- textos en mayúsculas forzadas;
- formularios abiertos;
- glifos;
- textos en inglés.

| Indicador | Antes | Después |
|---|---|---|
| Textos en mayúsculas (Biblioteca) | 51 | 0 |
| Textos en mayúsculas (Cola de corroboración) | 16 | 0 |
| Textos en mayúsculas (Mi ficha) | 14 | 0 |
| Textos en mayúsculas (Calidad de datos) | 13 | 0 |
| Textos de menos de 14 px (Control de miembros) | 54 | 0 |
| Textos de menos de 14 px (Fichas de miembros) | 26 | 0 |
| Textos de menos de 14 px (Reportería) | 23 | 0 |
| Textos de menos de 14 px (Régimen Interior) | 21 | 0 |
| Textos de menos de 14 px (Secretaría y Gestión Logial) | 19 | 0 |

## Regla aplicada

- En todo el contenido (`main.content`) se quitan las mayúsculas forzadas y el espaciado entre letras. Se usa `!important` porque es regla del PO y debe prevalecer sobre los estilos de cada módulo. Se excluyen `code` y `kbd`.
- Estos elementos quedan con letra de 14 px:
  - etiquetas de campo;
  - encabezados de tabla;
  - textos secundarios (`small`, `time`, `dt`);
  - insignias, «pills» y contadores;
  - iniciales de avatar.

## Verificación

- 313 tests en verde. tsc y oxlint OK.
- Segunda pasada de la auditoría: **0 textos de menos de 14 px y 0 mayúsculas en las 27 vistas**.
- Auditoría móvil completa en local.

## Impacto en datos (PMGM-GOV-004)

Ninguno.
