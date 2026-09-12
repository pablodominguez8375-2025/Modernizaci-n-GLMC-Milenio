# PMGM-ADR-004 — Monolito modular para el MVP

## Estado
Propuesto — v0.1.

## Contexto
Proyecto Milenio reúne numerosos dominios institucionales, pero el MVP necesita velocidad de entrega, consistencia de datos, trazabilidad y operación simple. Adoptar microservicios desde el inicio agregaría complejidad de despliegue, observabilidad, seguridad, mensajería y consistencia distribuida antes de disponer de métricas reales que lo justifiquen.

## Decisión propuesta
1. Construir inicialmente un monolito modular en ASP.NET Core.
2. Definir límites explícitos de módulos/dominios.
3. Evitar dependencias circulares entre módulos.
4. Exponer contratos internos y servicios de aplicación en lugar de acceso indiscriminado entre componentes.
5. Mantener preparada la posibilidad de extraer módulos a servicios independientes si aparecen razones verificables de escala, aislamiento o despliegue autónomo.
6. Mantener el frontend desacoplado mediante API versionada.

## Consecuencias positivas
- Menor complejidad operativa.
- Transacciones consistentes en el núcleo.
- Ciclo de desarrollo y pruebas más rápido.
- Despliegue sencillo para primeras etapas.
- Facilita demostrar valor institucional antes de optimizar distribución.

## Riesgos
- Si no se respetan límites modulares, el sistema puede transformarse en un monolito fuertemente acoplado.
- La extracción futura de servicios requiere disciplina desde el diseño inicial.

## Controles
- estructura de proyectos/namespaces por módulo;
- reglas de dependencia;
- contratos internos explícitos;
- pruebas de arquitectura cuando sea viable;
- revisión mediante ADR antes de introducir microservicios.

## Criterios para reconsiderar
Se evaluará extracción de un módulo cuando exista al menos una de estas razones:
- necesidad de escalarlo independientemente;
- requisitos de disponibilidad distintos;
- ciclo de despliegue realmente independiente;
- aislamiento de seguridad excepcional;
- carga técnica demostrada que afecte al resto de la plataforma.
