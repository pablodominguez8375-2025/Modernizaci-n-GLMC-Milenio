# Control de cambios — Proyecto Centenario

Regla operativa: todo cambio funcional, normativo o técnico aprobado debe quedar reflejado tanto en GitHub como en la documentación maestra de Google Drive.

## Formato mínimo de cada cambio

- Identificador.
- Fecha de incorporación.
- Descripción.
- Motivo.
- Fuente o antecedente.
- Módulos afectados.
- Perfiles afectados.
- Datos o entidades afectadas.
- Flujos/aprobaciones afectados.
- Impacto técnico.
- Estado: propuesto / aprobado / implementado / verificado.

## Línea base vigente

La referencia vigente es `LB-PC-2026-09-17`, documentada en `docs/LINEA-BASE-PROYECTO-CENTENARIO-2026-09-17.md`.

## Regla de prevalencia

Ante contradicción entre una conversación antigua y la línea base versionada, prevalece la línea base versionada más reciente, siempre que haya sido aprobada e incorporada a GitHub y Drive.

## Continuidad obligatoria

Antes de retomar trabajo en un chat nuevo, una sesión nueva o después de una pausa, se debe consultar:

1. último estado de `main`;
2. `AGENTS.md`;
3. `docs/INSTRUCCIONES-CONTINUIDAD-IA.md`;
4. Línea Base Maestra vigente;
5. este Control de Cambios;
6. documentos oficiales vigentes de Drive relacionados con el cambio;
7. código, ADR, migraciones, pruebas y configuraciones ya existentes.

Las conversaciones anteriores se usan solo como antecedente histórico y no como punto de partida técnico cuando existe información versionada posterior.

## Regla de no regresión

No se puede eliminar, degradar, sobrescribir o reemplazar una funcionalidad, acuerdo o código ya aprobado/versionado sin registrar expresamente el cambio, su motivo, impacto y versión de reemplazo.

El documento normativo permanente es `docs/REGLAS-CONTINUIDAD-Y-NO-REGRESION.md` y forma parte obligatoria del gobierno del proyecto.

## CC-PC-2026-09-17-IA-001 — Protocolo de continuidad para IA y herramientas de desarrollo

- Fecha de incorporación: 2026-09-17.
- Descripción: se crea un protocolo explícito para que cualquier chat, IA, agente de código o desarrollador retome el Proyecto Centenario desde el estado versionado y no desde memoria conversacional.
- Motivo: garantizar continuidad, evitar regresiones, preservar lo ya programado y permitir handoff entre herramientas de desarrollo.
- Fuente: instrucción expresa del Sponsor/Product Owner.
- Módulos afectados: gobierno del proyecto, documentación, flujo de desarrollo, demo GitHub Pages y QA.
- Impacto técnico: incorporación de `AGENTS.md`, `docs/INSTRUCCIONES-CONTINUIDAD-IA.md`, actualización del índice documental y referencia obligatoria desde README/Control de Cambios.
- Estado: aprobado e implementado.
