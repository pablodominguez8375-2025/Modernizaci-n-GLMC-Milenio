# QA525 — cuota de API pública — 2026-10-30

Estado: correctivo reservado, no implementado ni desplegado.

Evidencia física: HTTP403 CUOTA0 en branches/dev, reset2026-10-30T18:35:29Z. Preparación ae21 bloqueada antes de construir imágenes. EF99/G6 y staging firmado conservados; actas temporales no se renuevan sin revalidación.

Base dev ae21e611b50ef32bb2ebcfa2abc4e489b8ba9367; rama fix/qa525-public-api-budget-20261030-gpt, #525 agente:chatgpt. Main intacta.

Alcance: consolidar inventarios dentro de cada gate, reutilizar fuente validada de la misma operación y comparación exacta; mantener verificación total en límites de firma/build/activación, con manifest y HEAD frescos durante construcción y gates completos antes/después de mutaciones. Añadir diagnóstico HTTP seguro y tests de consumo con deriva/errores. Sin tokens, sin cache persistente ni aceptación de evidencia vencida.

Archivos previstos: gate de release/staging, entrypoint, readiness(error HTTP), tests asociados, contrato y este handoff. No workflows, aplicación, migración ni servidor. Cierre de código/pruebas esta intervención, CI/release y ejecución física después. PR804/805 separados.
