# Continuidad — Tarifario por decreto v2

Issue #190. Base dev c81f68dabf52f03875f947a23964c91d30994d56; main 6dfb9546a4873baff15955cf86abfd7d47e3d111. Fuente: INSTRUCCIONES PARA CHATGPT v2 del 04-10, Drive 10WnK7fLSomXKAKWGlU1y45DY9JarQLuBubGObdACKr8. Reemplaza v1. Ejecución ordenada expresamente por el PO.

Revisión Drive: desde el último cierre no hay fuentes nuevas en Proyecto Centenario salvo la actualización de Línea Base. V2 y PDF Decreto 1.759 revisados; las instrucciones son anteriores pero su ejecución es el alcance actual. Decreto: cesantía porcentajes de rebaja con pagos 0/5250/10500/15750 Santiago y 0/3750/7500/11250 Regiones; no extrapolar Perú. Past Activo no paga mensualidad ordinaria y mantiene reposición Hospitalaria separada. v2 permite modelar su monto cero sin modificar esa exención.

Reservas: Treasury/GrandTreasuryFeeSchedule, CeremonyEndpoints (cálculo de derechos), Organization/OrganizationEndpoints, PmgmDbContext y nueva migración/snapshot; frontend OrganizationDetailPage, GrandTreasuryPage/TreasuryTerritoryPage, LodgeTreasuryPanel, api/pmgmApi y pruebas; nuevos servicios/DTO/UI del tarifario, DB-007 y este handoff. No App, identidad, workflows, START-HERE ni reservas ajenas. PR histórico #116/#60/#58/#1 conservado, sin intervención activa Claude nueva. Cierre estimado en esta intervención sujeto a gates exact-head.

Alcance: zona desde Ficha, migración de datos existentes, tarifario por decreto con vigencia y respaldo, versiones inmutables/auditadas sin retroactividad, tabla de cesantía, resoluciones por fecha/zona, asistente Gran Tesorería y lectura local con componente oficial/sobrepago/total. No instalación, QA física/UAT ni promoción main. Despliegue QA pendiente.

Estado inicial: reclamo/análisis; no funcionalidad nueva integrada. SHA y resultados se registran en recibos de PR/Issue y Línea Base al cierre.
