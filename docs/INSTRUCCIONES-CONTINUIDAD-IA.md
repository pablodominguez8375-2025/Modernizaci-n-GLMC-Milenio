# Instrucciones de continuidad para IA y desarrollo — Proyecto Centenario

Estado: **vigente y obligatorio**  
Fecha de emisión: 2026-09-17

Este documento permite retomar el Proyecto Centenario desde un chat nuevo, una nueva sesión o una IA distinta sin depender de memoria conversacional.

## Objetivo

Garantizar que cualquier herramienta de desarrollo continúe el mismo Proyecto Centenario desde el estado real y versionado, preservando acuerdos, código, documentación, demo GitHub Pages y QA operacional.

## Fuente de verdad

- Repositorio: `pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio`
- Rama: `main`
- Línea Base: `LB-PC-2026-09-17`
- Drive carpeta Proyecto Centenario: `1P74Q8lhNPu6lHZ5zZR9ZD3AJFr_EtZyO`
- Drive Línea Base Maestra: `1ncl0d--Bny8PzvtP38muPo5H2-JM5QUDGqH3lJ5ZtPM`

## Protocolo de entrada para cualquier IA

La primera tarea de una IA que recibe el proyecto es reconstruir contexto desde fuentes persistentes, no desde conversaciones.

Secuencia obligatoria:

1. consultar HEAD actual de `main`;
2. listar árbol del repositorio y detectar código existente;
3. leer `AGENTS.md` y `README.md`;
4. leer Línea Base, Control de Cambios, reglas de continuidad y estrategia Demo+QA;
5. leer ADR, migraciones, pruebas y configuración del módulo afectado;
6. consultar la Línea Base Maestra y documentos oficiales relacionados en Google Drive;
7. comparar fuentes y detectar discrepancias;
8. confirmar qué está realmente implementado, qué solo está documentado y qué solo está simulado;
9. continuar desde ese punto, sin reconstruir desde cero.

## Prohibiciones

No se permite:

- crear otro proyecto para resolver una función del Proyecto Centenario;
- crear un repositorio paralelo salvo decisión explícita del Sponsor;
- reescribir módulos existentes sin revisar primero su código vigente;
- sustituir código real por mocks o ejemplos sin una decisión registrada;
- considerar un chat antiguo como fuente superior a GitHub/Drive;
- separar la demo GitHub Pages del sistema QA en una línea funcional distinta;
- declarar operacional una función solo visible en Pages;
- omitir la actualización de Drive cuando un acuerdo funcional/normativo cambia;
- inventar atribuciones de cargos que no estén soportadas por Constitución/Reglamento, protocolos o controles operativos aprobados.

## Continuidad de programación

El proyecto debe evolucionar en una línea única:

`Requisito → diseño → aprobación → código main → pruebas → GitHub Pages → QA instalable → QA/UAT → documentación`

Para todo incremento relevante se debe revisar impacto en:

- backend;
- frontend;
- PostgreSQL/migraciones;
- autenticación;
- perfiles y permisos;
- auditoría;
- documentos/archivos;
- demo GitHub Pages;
- Docker/infraestructura QA;
- pruebas;
- documentación GitHub;
- Línea Base/Drive.

## Doble salida permanente

### GitHub Pages

Objetivo: validación visual y funcional con datos ficticios/controlados.

Debe mostrar el avance real aprobado. Si una función está simulada por limitaciones de publicación estática, debe indicarse claramente.

### QA `srv01`

Objetivo: validación operacional real.

Entorno confirmado:

- Ubuntu 26.04.1 LTS
- 8 vCPU
- ~8 GB RAM
- Docker 29.1.3
- Docker Compose 2.40.3
- repo en `/opt/centenario/app`
- Deploy Key GitHub operativa

QA es la referencia para afirmar que una función es operacional.

## Arquitectura

Base vigente:

- ASP.NET Core
- PostgreSQL
- OpenID Connect / SSO
- Docker
- Nginx o equivalente
- TLS
- GitHub Actions
- Frontend React o Blazor hasta que un ADR vigente cierre la decisión

No modificar arquitectura por preferencia de una IA. Toda variación estructural requiere ADR y aprobación.

## Estado técnico que debe reconocerse

Existe implementación inicial de RBAC normativo de Taller:

- `backend/Centenario.Authorization/`
- `backend/Centenario.Authorization.SmokeTests/`
- `database/001_rbac_taller.sql`
- `adrs/ADR-001-RBAC-NORMATIVO-TALLER.md`

La IA debe inspeccionar estos archivos y extenderlos; no recrearlos desde un resumen.

## Marco funcional no regresable

El proyecto mantiene, entre otras, estas decisiones:

- aplicación multi-Taller;
- historia longitudinal de miembros;
- movilidad entre Talleres con historial preservado;
- Secretaría, Tenidas, Tesorería, Hospitalaria, Docencia, Insinuaciones, Ceremonias, Gestión Documental, Gran Archivero, Biblioteca Virtual, Régimen Interior y módulos superiores;
- perfiles de Taller reglamentarios;
- Consejo de Administración;
- reglas de firmas CRV/CRF;
- Tesorería y Hospitalaria independientes;
- flujo de ceremonias con validaciones superiores;
- publicación de insinuados y entrevistas parametrizables;
- auditoría transversal;
- administración de usuarios, perfiles, vistas, SMTP, notificaciones, backup/restauración;
- cumplimiento de Ley 21.719 y minimización de datos;
- CENDOC fuera del alcance como módulo único.

La lista no reemplaza la Línea Base; sirve como alerta anti-regresión.

## Regla normativa

La Constitución y Reglamento General vigente es la fuente superior para responsabilidades y límites de los cargos del Taller.

Cada permiso debe justificarse como:

- Normativa Directa;
- Protocolo Institucional;
- Control Operativo aprobado.

Si una acción no tiene una de esas bases, no debe habilitarse por defecto.

## Manejo de discrepancias

Si GitHub y Drive difieren en un punto material:

1. no programar silenciosamente sobre una de las dos versiones;
2. identificar cuál documento es posterior y cuál está aprobado;
3. validar contra normativa oficial si corresponde;
4. registrar la resolución;
5. actualizar ambas fuentes;
6. recién después tocar el código afectado.

## Handoff entre IAs o sesiones

Al terminar una intervención, dejar explícito:

- commit/HEAD final;
- archivos modificados;
- cambios funcionales;
- cambios de datos/migraciones;
- pruebas y resultados;
- estado GitHub Pages;
- estado QA;
- documentación GitHub actualizada;
- documentación Drive actualizada;
- pendientes y riesgos.

Una IA nueva debe usar este handoff solo como índice; debe verificar todo contra `main` y Drive antes de continuar.

## Prompt mínimo para iniciar desde cualquier chat o IA

> Continuar Proyecto Centenario. Antes de hacer cambios, consulta el HEAD actual de `main`, `AGENTS.md`, la Línea Base, Control de Cambios, reglas de continuidad, estrategia GitHub Pages + QA, ADR/código/migraciones/pruebas existentes y la documentación oficial vigente en Google Drive. No reconstruyas desde chats antiguos. Preserva lo ya programado, mantén una sola línea de desarrollo y actualiza tanto la demo GitHub Pages como la versión instalable QA cuando corresponda.

## Estado de la instrucción

Permanente hasta que el Sponsor/Product Owner ordene expresamente su modificación y dicha modificación quede consolidada en GitHub y Google Drive.
