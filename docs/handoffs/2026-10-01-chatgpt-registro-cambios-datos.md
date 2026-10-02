# Registro de salida de cambios de datos — ChatGPT

Issue #259; pedido explícito del Sponsor Pablo. Base viva dev 08a8c363f9df39e6db6ad4be577cc1aab7656adf; main 6dfb9546a4873baff15955cf86abfd7d47e3d111 congelado. Fuentes GitHub/Drive, PR abiertos, reservas y modelos conceptuales consultados.

Reserva GOV-003: docs/PMGM-GOV-004-registro-cambios-datos.md, docs/modelo-datos/PMGM-DB-004-diccionario-y-estructuras.md, docs/modelo-datos/plantillas/registro-cambio-datos.md, .github/PULL_REQUEST_TEMPLATE.md y este handoff. Estimación documental en esta intervención, sujeta a gates. Draft previo a implementación. No tocar archivos de Claude ni AGENTS/GOV previos presentes en #1 congelado; archivos nuevos sin cruce.

Alcance: definir salida obligatoria para todo cambio al modelo físico/lógico, contrato de datos, catálogo/regla/clasificación relevante; diccionario del alcance cambiado, estructura/relaciones y registro antes/después con migración, impacto, pruebas y SHA. Registros operacionales individuales conservan su auditoría: no son disparadores de un nuevo diccionario.

No cambios de aplicación, schema o datos; no se certifica diccionario físico exhaustivo ni base instalada. Modelos conceptuales existentes preservados con sus estados. Main/srv01/QA física/UAT excluidos. Implementación/gates/integración pendientes; el recibo final GitHub/Drive completará SHA/estado/evidencias. START-HERE exactamente una línea en PR documental posterior al merge.
