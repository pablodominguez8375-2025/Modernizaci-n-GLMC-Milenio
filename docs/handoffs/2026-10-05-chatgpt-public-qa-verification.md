# Verificación pública completada — Hospitalaria del Taller

Fecha: 05-10-2026 (America/Santiago). Issue #266. Inicio dev/SOURCE_SHA fc3d57e9f3ce6e3579622a452126480b41754862; main 6dfb9546a4873baff15955cf86abfd7d47e3d111.
Complementa [el cierre #322/#323](2026-10-04-chatgpt-hospitalaria-access-postmerge.md), sin reescribir su evidencia histórica.

## Pendiente resuelto
El entorno volvió a responder. Se descargó el ZIP público Proyecto-Centenario-QA-srv01-fc3d57e9f3ce.zip: SHA-256 f05045738e7bcaf72dd84d06629721423ae6424297cc8b0b95d4e020b5654ce7; CRC correcto; 1025/1025 entradas MANIFEST.sha256 comprobadas. BUILD-INFO conserva SOURCE_SHA fc3d57e9f3ce6e3579622a452126480b41754862, SOURCE_REF docs/hospitalaria-access-close-20261004-gpt y BUILD_RUN_ID 37247991353. No confundir con los paquetes independientes de Actions.

Se clonó el bundle Git incluido y se comprobó HEAD fc3d57e9... contra GitHub vivo. Nueve archivos críticos del ZIP coinciden byte a byte con ese checkout: DynamicHospitalariaAccess.cs, LodgeHospitalariaEndpoints.cs, HospitalariaEndpoints.cs, HospitalariaPage.tsx, useHospitalariaAccess.ts, api/pmgmApi.ts, DB-009, handoff postmerge y START-HERE.md.

El índice público sirve /Modernizaci-n-GLMC-Milenio/assets/index-j90zbGsu.js: SHA-256 d970a522b5c4dfcc75f257f716cffd05c321471179de20adbd05ad3f57607d98; contiene el SHA esperado. Se reabrió la demo en navegador: cabecera FC3D57E y Hospitalaria del Taller cargan; datos de demostración. Esto cierra la descarga/comparación y reapertura pendientes tras environment_offline; no certifica dispositivos reales, aceptación visual integral ni UAT.

Gates del corte comprobados: CI37247991538, Showcase37247991547, QA37247991499 y Pre-UAT37247991527 SUCCESS. Evidencia previa del mismo código: backend487/frontend416 PASS. Este PR documental tiene gates propios y los recibos de Issue/PR y Drive registran su SHA final/publicación posterior.

## Revisión y continuación
Drive raíz Proyecto Centenario y sus dos subcarpetas sin archivos nuevos/modificados desde el último cierre. Línea Base leída con historial preservado. PR abiertos #116/#60/#58/#1 sin cambio de HEAD funcional; archivos calientes se mantienen excluidos.

Revisión de código: HospitalariaPage.refreshGrand llama syncDeathReplenishmentCases al cargar/refrescar la bandeja, antes de consultar rendiciones/casos/tarifa. La operación POST genera casos y obligaciones; no debe confundirse con una consulta view al extender #266. Próximo corte: Gran Hospitalaria, grants del ámbito Orden intersectados con autoridad institucional, separar carga de lectura y sincronización (create), controles de tarifas y revisiones según acción, respuestas tardías/revocación sin reexposición. Primero verificar el ámbito real de asignaciones y fuentes normativas, reclamar archivos y abrir draft. No cambiar tarifas ni autoridad masónica.

#266 sigue abierta para ese corte, regularidad, navegación global y otros módulos. #191 conserva definiciones contables; #66 conserva aceptación UAT. #116 NO FUSIONAR TODAVÍA. main/srv01/UAT pausados; **despliegue QA pendiente**.

Sin cambios de implementación, migración, modelo, contratos o reglas de datos: se registra evidencia de integridad y próximo alcance. Sólo handoff nuevo y una línea START-HERE, sin conflictos de gobierno/identidad. SHA final, PR, gates y nueva publicación documental se completan en los recibos GitHub/Drive; consultar siempre dev vivo.

Demo: https://pablodominguez8375-2025.github.io/Modernizaci-n-GLMC-Milenio/
Paquete comprobado: https://pablodominguez8375-2025.github.io/Modernizaci-n-GLMC-Milenio/downloads/Proyecto-Centenario-QA-srv01-fc3d57e9f3ce.zip
