# #533 — Etapa C18: serialización CSV segura de los dos listados de Asamblea

Fecha 2026-10-20. Baseline dev@3059657c8033da13a13d0195cc19aedab81ccf0a; main@6dfb9546a4873baff15955cf86abfd7d47e3d111 no modificar. QA físico en otro chat.

## Contrato de datos y privacidad
Dos proyecciones puras basadas en `AssemblyRosterPolicy` existente:
1. **Listado general de asambleístas**: N°, apellidos y nombres, RUT, Taller, Firma vacía, marca genérica de habilitación sin divulgar causas reservadas.
2. **Listado de sufragantes**: mismos campos, sin columna causal; únicamente personas con `MayVote` y estado `habilitado` concordantes.

La serialización CSV usa UTF-8 (con BOM para planillas), separador punto y coma y comillas dobles escapadas; rechaza caracteres de control, saltos de línea en campos y protege contra interpretación como fórmula (= + - @, incluso espacios invisibles iniciales). Conserva acentos y Ñ, orden es-CL con tildes ignoradas (política vigente), desempate RUT y firma vacía. No incluye sentido ni dato de voto, monto bancario, sanciones, nombre del funcionario ni expediente.

**Límite no negociable:** este componente no acredita un cierre oficial, no consulta identidades ni bases y no publica endpoint de descarga. Los futuros handlers PDF/CSV deben exigir asamblea efectivamente cerrada por RI con digest/instantánea inmutable, roles RI/GS y auditoría de lectura antes de suministrar datos personales reales. Jamás llamar a la vista preliminar «Padrón de la Gran Asamblea» ni exportar una lista como oficial desde draft. El PDF requiere implementación aparte, y la certificación GT histórica/sello RI oficial aún bloquean el cierre.

No modificar rutas de otros agentes (C16/C17), migrations, main, QA, servidores. PR draft hasta tres gates CI/Showcase/Instalable PASS exact SHA. Handoff para Claude después de contrato validado.
