# Cierre funcional — Tarifario por decreto v2 — 04-10-2026

Issue #190; PR funcional [#318](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/pull/318), integrado por squash en dev `17b719d3ed910b91737e9fa9792775b3f545c528`. Inicio dev `c81f68dabf52f03875f947a23964c91d30994d56`; main inicial/final `6dfb9546a4873baff15955cf86abfd7d47e3d111`, congelado. Este PR documental parte del cierre funcional, sin cambios de comportamiento.

Fuente: [INSTRUCCIONES PARA CHATGPT v2](https://docs.google.com/document/d/10WnK7fLSomXKAKWGlU1y45DY9JarQLuBubGObdACKr8) y PDF oficial Decreto 1.759. [Handoff de ejecución y reservas](2026-10-04-chatgpt-tariff-decree-v2.md). [DB-007](../modelo-datos/PMGM-DB-007-tarifario-decretos.md) y [GOV-004](../modelo-datos/cambios/2026-10-04-issue-190-tarifario.md) documentan contrato, diccionario, relaciones, anterior/nuevo y migración/recuperación.

## Resultado

Catálogo de decretos persistido, inmutable y auditado; encabezado/vigencia/respaldo/estado, cuotas por zona y categoría, derechos por zona y ceremonia, tabla de cesantía y carga inicial del Decreto 1.759. Los cálculos operativos consultan fecha y geografía de Ficha; no usan importes monetarios codificados. Versiones nuevas sólo con comienzo mensual futuro; faltantes/expiración bloquean sin fallback.

Ficha con Oriente estructurado y ciudad real; país/zona derivados. La migración no inventa ciudades regionales/peruanas y no modifica registros financieros. Consulta territorial de solo lectura. Gran Tesorería: vigentes/históricos y asistente de cuatro pasos bajo demanda. Taller: valores vigentes de su Oriente; aporte fijo + sobrepago editable = total. CLP y USD separados, sin conversiones/indexación. Past Activo permanece exento de mensualidad ordinaria, con Hospitalaria independiente.

Cargos y primeros pagos nuevos guardan importe/moneda/versión; histórico existente no se recalcula. Derechos legacy sin snapshot usan la primera fecha registrada y Ficha disponible sin afirmar geografía histórica desconocida. Expedientes sin tarifa aparecen como pendientes sin importe artificial ni permiso para cobrar. Perú sin tipos/rebajas definidos permanece pendiente de decreto explícito.

Archivos del incremento: servicios/endpoints/DTO y entidades Treasury; CeremonyEndpoints y proyección ceremonial; OrganizationEndpoints/Ficha; PmgmDbContext y migración 20261004205000; frontend Tarifario/Ficha/Tesorerías/api/demo y estilos; tests; script de capturas; DB-007, GOV-004 y handoffs. Sin cambio de workflows, identidad, App, main ni instalación.

## Evidencia exact-head

Head del PR `5e2d7b640aa0de0f90c4d68cff9a3651631229a1`: [CI #37237172073](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/actions/runs/37237172073), [Showcase #37237172059](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/actions/runs/37237172059) y [QA Installable #37237172070](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/actions/runs/37237172070) SUCCESS; base dev confirmada sin cambios antes de merge.

Backend PostgreSQL 483 PASS (sin fallos ni saltos); frontend 410 PASS, lint y build PASS; gate 61 migraciones y paridad byte a byte del seed PASS. El contexto Docker frontend no contiene backend: su prueba de comparación cruzada se omite allí; el CI de repositorio y el gate Python verifican ambas copias.

Capturas: asistente 4 pasos × 8 viewports de 360 a 1920 px; lista × 8. Comprobación estricta de viewport y límites de formulario, más checks de navegación/overflow existentes. Corrección de ancho intrínseco de panel/tabla y contención del drawer; no se rebajó el gate. Artefacto responsive #11316157802, SHA-256 `a437f0643b60d95f15450e87ca47af9abe638b1c3dd186af3fb8c06738a4b4e6`.

## Publicación y continuidad

[Pages](https://pablodominguez8375-2025.github.io/Modernizaci-n-GLMC-Milenio/). La publicación de este PR documental exige los mismos tres gates; tras su merge se verifican CI/Showcase Deploy/QA/Pre-UAT y el paquete público (SOURCE_SHA, checksum, CRC y MANIFEST). El SHA definitivo y recibo público se agregan al Issue/PR y a la adenda de Drive, sin autorreferencia circular en este commit.

Adenda en Proyecto Centenario: [Tarifario v2](https://docs.google.com/document/d/13b97ZT1xjU3vx0j32IXfZgJvCb9IcTfjusEUuw-V22c). Al acreditar publicación, consolidar cierre en Línea Base y marcar las instrucciones « — EJECUTADA».

Siguiente pendiente institucional: srv01 sigue pausado por #97; sin instalación, smoke/regresión física ni UAT. No promover main. #266 conserva pendientes de permisos de otros módulos; no se cierra por este incremento. Reservas del tarifario se liberan al entregar el recibo final.

