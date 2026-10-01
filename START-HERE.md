Warning: truncated output (original token count: 9263)
Total output lines: 236

## Handoff vigente — integración y publicación de PR #219 — 29-09-2026

- HEAD observado al inicio: `dev@90988f1bb09d86eb017248ce02040ebe47e053a2`; `main@6dfb9546a4873baff15955cf86abfd7d47e3d111`.
- PR #219 se integró por squash desde `feature/cuadro-padron-qa42-20260929`, head exacto `2f31d8b7629ffc08a2e8c6574da63a180ca9afa2`; commit integrado y HEAD de `dev`: `3a695b0b8b8808f02ca7d0e91811010b096f623f`. PR #117 y #119 cerrados sin merge como reemplazados.
- Cambios: terminología institucional para Cuadro, fichas/historial y Padrón de la Gran Asamblea; revisión preliminar con estados pendientes/observados; runbook sincronizado a QA-001..QA-042. UAT institucional histórica de 20 casos preservada.
- Gates exact-head del PR: PMGM CI #1681, Showcase #997 y QA Installable #635 SUCCESS. Post-merge en `3a695b0`: PMGM CI #1682, Showcase/Pages #998 (Deploy testing showcase SUCCESS), QA Installable #636 y Pre-UAT #406 SUCCESS.
- Pages artifact #11032562325, SHA-256 `78e5292b2840009c84533e6224bc4ff28d28dea1af284cbbce7dc86667bbdbf8`. El `qa-current.json` incluido se leyó de vuelta: `sourceSha=3a695b0b8b8808f02ca7d0e91811010b096f623f`; paquete `Proyecto-Centenario-QA-srv01-3a695b0b8b88.zip`, SHA-256 verificado `58d7f8018670959766178b6308cd3601bfdffdc634054997b0363d47ab8e15e6`. URL Pages: https://pablodominguez8375-2025.github.io/Modernizaci-n-GLMC-Milenio/
- Evidencia responsive de post-merge #11032922148, SHA-256 `551df40d23c47728297d32be7151e3f954c7a107ca068e41fce88708f3aa8cc6`. Artefactos post-merge QA #11032197880 SHA-256 `598567c8ae8667c34c7e42d6cae954fef5813113f072a4752bf14dc7371b0fcc`; Pre-UAT #11031963231 SHA-256 `cebc5431283578c5ae607ab9c9d24a816e5f4d12ac626849318678c0f8cad977`.
- Los artefactos están empaquetados/publicados, no instalados. Issue #97 mantiene `srv01` pausado: no instalación, despliegue manual, smoke/regresión física ni UAT. No promover `main`.

---
# Handoff vigente — Proyecto Centenario — 28-09-2026 UTC

- Ramas comprobadas: `dev@bca36d5d0fb49c224aaa355f4cecefc43b3037df`; `main@6dfb9546a4873baff15955cf86abfd7d47e3d111` sin cambios.
- PR #214, rama `feature/instruction-attendance-status-20260928`, head `921cefab91470dac83e9d2e9a79d5a18b51d8810`, integrada por squash en dev. Estados aprobados por PO para #46: Presente = asistió; Ausente = no asistió ni avisó; Justificada = no asistió, pero avisó, tuvo permiso o comunicó una razón.
- Validaciones exact-head de PR #214: PMGM CI #1669, Showcase #980 y QA Installable #618 SUCCESS. Post-merge: PMGM CI #1670, QA Installable #619, Pre-UAT #401 y Showcase/Pages run 36485694042 SUCCESS; “Deploy testing showcase” SUCCESS.
- Pages artifact #10998698098, SHA-256 `79425532171f8566ae8dd2726ed76622510e5092fb34469bd31c1a3f6781fa4f`. `qa-current.json` del artefacto verifica `sourceSha=bca36d5d0fb49c224aaa355f4cecefc43b3037df`, base URL `https://pablodominguez8375-2025.github.io/Modernizaci-n-GLMC-Milenio`, ZIP publicado `Proyecto-Centenario-QA-srv01-bca36d5d0fb4.zip`, SHA-256 `0f329c7cd3000509cd32c1d6f89e395b987303741d96cee372f3fd5f9d2df6ad`. El job de despliegue terminó SUCCESS; queda verificar acceso público y actualizar la baseline/URL de Issue #63.
- QA Installable #10999940575: artifact SHA-256 `b6a8ccf4ad63df12a960113cd937c923d11a5668d8196574c3ccd56dba5b0235`; ZIP interno `c32b00683a8bcaf8d7c16e016ccee1eaead3cfb73b52a0caca2f99754d75a881`; BUILD-INFO y MANIFEST 784/784 verificados. Está empaquetado, no instalado. `srv01` continúa pausado; no hacer instalación, despliegue manual, smoke/regresión física ni UAT. Issue #97 sigue abierto por instrucción explícita del Sponsor.
- Automatización responsive: 20 perfiles, 483 estados a 360×800; evidencia automatizada, no revisión institucional ni UAT.
- Aprobaciones/definiciones por resolver: Issue #191 requiere fuente o decisión contable sobre presentación de anticipos, recuperaciones, pagos multiperíodo y reversos/correcciones. Issue #190 sigue abierto para completar el alcance general del tarifario; Perú USD se aprobó e integró por PR #213, conforme a Decreto 1759 (cuota ordinaria USD 6), sin fijar otras tarifas USD ni conversión. Revisar propuestas/PR abiertas sobre ramas históricas antes de cualquier merge; no promover `main`.
- Issue #46 registra su aclaración e implementación. La Línea Base Maestra de Drive fue actualizada y leída de vuelta.
- Este bloque supersede handoffs fechados anteriores: contrastar siempre HEAD, Actions, Pages, Issues/PRs y fuentes Drive vigentes antes de actuar.

## Handoff vivo — revisión de aprobaciones — 28-09-2026 UTC

- SHA observado al abrir este handoff: `dev@26deb078a25b62fa863bf4e709e6820a337d67a2`; `main@6dfb9546a4873baff15955cf86abfd7d47e3d111`, sin promoción. PR #211 documental integrada en dev@26deb078. Pages run 36455750108 SUCCESS; `qa-current.json.sourceSha` coincide con 26deb078. QA artifact #10985078472 empaquetado; no instalado. Issue #97 mantiene la pausa de srv01.
- Revisión explícita pendiente: PR #154 solicita al PO 3 decisiones para implementar Perú en USD: (1) sublibros CLP/USD separados y sin conversión, (2) cuota local peruana en USD con mínimo oficial USD 6, (3) circuito de recepción/conciliación USD con Gran Tesorería. Es PR documental/propuesta, base antigua dev@8522b0d, head 698908e, mergeable=false y sin revisiones registradas. No aprobar ni fusionar esa base obsoleta sin comparar/rebasar primero.
- Fuente oficial Drive revisada: Decreto N.º 1.759, emitido 15-12-2025 y vigente desde 01-01-2026, Drive ID `1qsXM3CPAJw9jW1EHk3YjbNinHnexRub2` (archivo actualizado 22-09-2026): Santiago CLP 21.000, otros Orientes CLP 15.000 y Perú USD 6 para cuota ordinaria. No define importe USD para cónyuge/estudiante/tercera edad peruana ni tipo de cambio; no extrapolar. Cuadro Pago Gran Tesorería (11-09-2026) es referencia operativa. Manual Módulo Tesorería (versión 2026, actualizado 21-09-2026) describe recepción real e imputación a cuotas pasadas por mes; no resuelve presentación financiera de anticipos futuros ni política completa de reversos.
- Issue #190 ya registra la regla aprobada de dos componentes (aporte oficial + local) y ubicación en ficha de Taller; el dato oficial USD 6 existe. Antes de implementar su extensión monetaria, resolver las tres decisiones de PR #154 y no inferir categoría/tipo de cambio.
- Issue #191: regla operativa confirmada por PO de separar año de recepción del dinero del período de cuota aplicado. Sigue pendiente definición contable institucional para anticipos/recuperaciones, pagos multiperíodo y reversos/correcciones. No inventar cuentas contables; requiere fuente/decisión antes de cerrar diseño.
- Issue #63 sigue abierto para aceptación visual institucional global. En sus comentarios sólo consta aprobación parcial y ligada a SHA antiguo para Mi ficha → Biblioteca Virtual; la auditoría responsive automatizada no sustituye la revisión/aceptación del PO.
- Issue #46 ya tiene el alcance principal implementado por PR #206. Sigue una discrepancia puntual entre el criterio «justificada» y la línea base/código «presente/ausente»; no modificar ni cerrar hasta reconciliar la regla.
- Operación y releases: Issue #97 continúa pausado por instrucción vigente; levantarlo requiere instrucción explícita del Sponsor. PR #1 main ← dev permanece draft/bloqueada: su candidato UAT 739ba0b es anterior al dev actual; no solicitar aprobación de promoción antes de nuevo candidato y UAT. PR #116 está abierta en base histórica dev@5704400/head 17e1bd7; comentario PO exige autorización de merge separada, pero primero comparar el candidato con dev actual. PR #182 está cerrada sin merge y sustituida por #204.
- No hay cambios funcionales en este corte. No se instaló, no hubo QA física/UAT y main sigue intacta. Los checks/Pages/paquete prueban sólo integración/publicación/empaquetado. Revisión registrada también en Drive Línea Base Maestra.

## Handoff vivo — post-merge PR #210 — 28-09-2026

- Estado verificado: `dev@4087f47a575ba6bf48fead93838de5425a62bb10`; `main@6dfb9546a4873baff15955cf86abfd7d47e3d111` sin cambios. No había clon Git local; lectura remota de ramas, árbol, fuentes y estado completada.
- PR #210 documental quedó integrada por squash en el SHA vigente. Exact-head del PR y post-merge PMGM CI, Showcase/Pages, QA Installable y Pre-UAT: SUCCESS.
- Pages artifact #10984181780 digest `sha256:4cff4268bee16ca3e48cffc9d0f0ddfff0bb1fa107571c741867a54f8b1dd566`. `qa-current.json` confirma `sourceSha=4087f47a575ba6bf48fead93838de5425a62bb10`; ZIP publicado SHA-256 `a43f2b7490b6f3b5e7b44d96c9137ae655ca5aa0aaa2f1f1686b133d7c0d9a6c`.
- QA artifact #10983728727 digest `sha256:353e3808fd25424fe1837e217303cbdc79a3c692ae62331cf04036a9ae456b92`; ZIP instalable interior SHA-256 `cbe8e16c66b4cd0d7d470160fe41102fc4212d7c35bc1f0e9acd85478fac2ceb`; BUILD-INFO verifica el SHA de origen. Paquete generado, no instalado.
- Issue #97 sigue abierto. `srv01` pausado por instrucción del Sponsor: no instalar, desplegar ni ejecutar smoke/regresión física/UAT. No promover `main`.
- Issue #46 / PMGM-BLG-078 sigue abierto. El código vigente de Mi ficha presenta historial propio de instrucciones con fecha, grado, tema, asistencia y encargado, en modo sólo consulta; la demo usa registros ficticios y la prueba frontend actual impide agregar un campo `progress` a los datos demo. La prueba no sustituye aceptación visual institucional. Antes de tocar código, identificar una brecha concreta frente a sus criterios; no duplicar lo integrado por PR #206.
- No se inició un incremento funcional. #190 mantiene pendientes tarifas/monedas; #191 requiere cerrar presentación contable. PR #182 fue cerrada sin merge y sustituida por #204.
- La continuidad se rige por estado persistente: cualquier chat o IA puede continuar leyendo START-HERE, GitHub `dev` y la Línea Base/documentos oficiales de Drive. Ningún chat o memoria conversacional es requisito ni fuente de verdad. GitHub gobierna código/backlog/decisiones técnicas; Drive conserva Línea Base y fuentes oficiales. Esta regla armoniza las instrucciones vigentes y no altera gobierno institucional.

## Verificación viva y handoff — 28-09-2026

- HEAD comprobado: `dev@57412f9afd90586899edd4885f371b96cceb36ac`; `main@6dfb9546a4873baff15955cf86abfd7d47e3d111` sin cambios. No hay clone local en el workspace; inspección remota de raíz/árbol completada, sin modificaciones de código.
- PR #209 integrada por squash (head exacto `bee0f62d0da9a56eee4de3e5cb6d58b4f875a21d`). Sólo modificó la auditoría responsive y documentación para incluir SVG visibles.
- CI exact-head de #209: PMGM CI #1659, Showcase #965, QA Installable #603 SUCCESS. Auditoría Chromium: 20 perfiles, 483 estados a 360×800, 563 capturas, sin overflow detectado. Esto es evidencia automatizada; no equivale a revisión institucional, dispositivos reales ni UAT.
- Post-merge del SHA vigente: PMGM CI run 36430246829 SUCCESS; Showcase/Pages run 36430246786 SUCCESS y Deploy testing showcase SUCCESS; QA Installable run 36430246937 SUCCESS; Pre-UAT run 36430247019 SUCCESS.
- Pages artifact #10973542145, digest `sha256:f107ec8feaff05c588b3a2611e7dc9a259ebf0d1e3377e661474b61e8c469555`. Se leyó `downloads/qa-current.json`: `sourceSha=57412f9afd90586899edd4885f371b96cceb36ac`. ZIP publicado `Proyecto-Centenario-QA-srv01-57412f9afd90.zip`, SHA-256 `0e8704690a0b067ceca5734169f48cf3b9b7031bf16e0d7c3e9546b85b52f293`.
- QA artifact #10971934945, digest/SHA-256 del artefacto `8247265d7241579e9e6374356f8794e4ccfea0458239cf204481df961c2bb5b6`. ZIP interior `Proyecto-Centenario-QA-srv01-57412f9afd90.zip`, SHA-256 `b223222c8cf895f668e8489ed9ee2820938a781bd023caa67e882962a2e8ef89`; `BUILD-INFO` confirma SOURCE_SHA y run 36430246937. Paquetes de Pages y QA se construyeron separadamente y sus checksums difieren.
- Estados: funcionalidad de docencia por grado integrada en dev; auditoría SVG integrada; Pages publicado en el SHA actual; artefactos QA/Pre-UAT generados. Nada instalado, sin QA física/UAT aceptada. Issue #97 mantiene `srv01` pausado por el Sponsor; no desplegar/instalar/probar físicamente ni promover `main`.
- No iniciar otro incremento funcional sin alcance aprobado por el Product Owner. Issue #190 conserva decisiones de tarifas/monedas pendientes; #191 requiere definición de presentación contable; PR #182 está cerrada sin merge y sustituida por #204. El filtro de fecha/grado expuesto por API pero no por vista, señalado en el cruce documental, requiere alcance aprobado antes de ampliarlo.
- Próximo paso: esperar un alcance aprobado o instrucción del Sponsor para reanudar QA física/UAT. Revisar la discrepancia documental: GOV-001 conserva una frase histórica de “hilo maestro único”; START-HERE/GOV-002 y la Línea Base vigente establecen continuidad desde GitHub + Drive y verificable por cualquier IA. No cambiar reglas institucionales a partir de esa diferencia.

# START HERE — Proyecto Centenario

## Handoff vigente — Issue #46, estados de asistencia — 28-09-2026 UTC

- Decisión PO registrada en GitHub Issue #46 y Drive Línea Base: Presente = asistió; Ausente = faltó sin aviso; Justificada = faltó, pero avisó, tenía permiso o comunicó una razón.
- Base de implementación: `dev@b252d1e2a1b1b5a2a111037f743b6e57823b6ef7`; `main@6dfb9546a4873baff15955cf86abfd7d47e3d111` sin cambios. Rama `feature/instruction-attendance-status-20260928` creada desde ese SHA.
- Brecha confirmada: el registro de instrucciones aceptaba sólo presente/ausente y la interfaz asignaba Presente silenciosamente a todos. Mi ficha ya representaba “Justificada”; el reporte de Orden agregaba sólo presentes/ausentes.
- Este incremento añade selección obligatoria de los tres estados, conserva los registros de asistencia existentes, agrega el conteo de justificadas al reporte agregado, pruebas backend/frontend y criterios QA. No requiere migración: el estado se persiste en el camp…2263 tokens truncated…aa405bf0204f7be8df0a`. QA Actions artifact #10943455063: digest `sha256:ed08c7ce2d3c17058f19be810814007bbe7fc4ea30da591fbb5b0e0ba169793a`.
- Demo publicada: https://pablodominguez8375-2025.github.io/Modernizaci-n-GLMC-Milenio/
- La mejora de cobranza PR #195 está integrada: la fecha real de recepción del pago se conserva para contabilidad/caja y el año/mes elegido determina la cuota abonada en historial/cartola; no duplica ingresos. Los artefactos son evidencia de publicación/paquete, no de instalación.
- `srv01` está expresamente pausado por el Sponsor. Issue #97 sigue abierto. No instalar, desplegar manualmente, ejecutar smoke en servidor, regresión física ni UAT hasta que el Sponsor levante la pausa. No promover a `main`.
- Issues #190 y #191 permanecen pendientes de definiciones contables/de tarifas; no inventar montos, moneda, conversiones ni tratamiento de anticipos. PR #182 (revisión de menús del Sistema Logial) está abierta con base antigua `14019d2`; no fusionar su rama obsoleta. Comparar su contenido con `dev` y Drive antes de decidir si rebase, sustituir o cerrar.
- La mejora de ficha #199 se autorizó de forma explícita y ya está integrada. Esa aprobación no autoriza automáticamente otro incremento funcional; si Issue #97 sigue bloqueante, se requiere alcance definido por el Product Owner o decisión vigente para la siguiente función.

### Protocolo obligatorio de continuidad

1. Consulta GitHub en vivo: ramas `dev` y `main`, árbol, PRs/issues abiertos, checks y Pages. En un clone ejecuta `git fetch origin dev main`; crea toda rama nueva desde el HEAD vivo de `dev`, nunca desde esta fotografía.
2. Lee en ese mismo HEAD: `START-HERE.md`, `AGENTS.md`, `README.md`, Estado Maestro (`docs/PMGM-BASE-001-estado-maestro.md`), `docs/PMGM-NEXT-001-siguiente-corte-tecnico.md`, GOV-001/GOV-002, ADR, QA, pruebas y workflows pertinentes.
3. Consulta en Drive la Línea Base Maestra y los documentos oficiales vigentes de Proyecto Centenario. Verifica versión/fecha y reconcilia discrepancias. Para normas, cargos, atribuciones, firmas, permisos y aprobaciones, verifica Constitución, Reglamento, protocolos y formularios aplicables.
4. Separa claramente estados: requerido/aprobado, documentado, implementado en rama, integrado en `dev`, publicado en Pages, artefacto QA generado, instalado en servidor, regresión física aprobada, UAT aceptada y promovido a `main`. Un check verde o paquete nunca prueba una etapa posterior.
5. No regresión: inspecciona implementación, modelos, permisos, auditoría, migraciones, pruebas y demo existentes; extiende el producto real, no lo reemplaces con mocks, esqueletos ni sistemas paralelos. Conserva terminología institucional: “Cuadro del Taller/Orden”; “Padrón” se reserva a electores de Gran Asamblea.
6. Para alcance funcional autorizado: rama `feature/*` desde `dev`; cambios mínimos y pruebas necesarias; PR a `dev`; valida CI sobre el SHA exacto; integra sólo si está autorizado y GitHub lo permite. No solicites aprobación redundante del Product Owner para un alcance ya autorizado ni inventes revisores obligatorios. `main` requiere promoción explícita separada.
7. Mantén en sincronía código, Demo GitHub Pages con datos ficticios y paquete instalable QA provenientes del mismo SHA cuando el cambio funcional lo requiera. Cumple la pausa de `srv01`.
8. Al terminar, actualiza documentación de GitHub y Drive; registra SHAs de inicio/cierre, rama/PR, archivos, cambios, pruebas, runs, Pages, checksum del instalable, riesgos, pendientes y decisión requerida. Verifica lectura de retorno y deja handoff apto para otra IA.

### Prompt universal para copiar en un nuevo chat

> Continúa el Proyecto Centenario / Modernización Gran Logia Mixta de Chile como parte del mismo proyecto, sin reiniciar ni reconstruirlo desde conversaciones. Tu primera tarea es verificar en vivo el HEAD de `dev` y `main` en `pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio`, los PRs/issues/checks recientes, la Demo Pages y el artefacto QA. Lee `START-HERE.md`, `AGENTS.md`, `README.md`, Estado Maestro, NEXT, GOV-001/GOV-002, ADR y documentación relacionada en el HEAD actual de `dev`; consulta además en Google Drive la Línea Base Maestra y fuentes institucionales aplicables. Las fuentes persistentes y sus versiones prevalecen sobre memoria o chats. Conserva todo lo aprobado y sigue el flujo `feature/* → pruebas/documentación → PR → CI exact-head → dev`; no cambies `main` sin promoción expresa. `srv01` permanece pausado hasta instrucción del Sponsor: no instales, despliegues ni declares QA/UAT aceptada por CI, Pages o paquetes. No inicies alcance funcional ajeno a autorizaciones vigentes; reconcilia los pendientes de Issue #97, #190, #191 y PR #182 con las decisiones/documentos actuales antes de elegir el siguiente paso. Mantén terminología y reglas institucionales, datos de demo ficticios, no regresión y código/demo/QA trazables al mismo SHA. Actualiza GitHub y Drive al cerrar y deja un handoff verificable. Si no puedes acceder a una fuente necesaria, dilo y no supongas su contenido.

**Este archivo es el punto de entrada obligatorio para cualquier chat nuevo, ChatGPT Work, Codex, IA de desarrollo, agente o desarrollador humano.**

No continúes el proyecto desde memoria conversacional, desde un resumen antiguo ni desde una rama elegida por costumbre. Reconstruye siempre el estado vivo desde las fuentes persistentes.

## 1. Identidad

- Proyecto: **Proyecto Centenario / Modernización Gran Logia Mixta de Chile**.
- Repositorio oficial: `pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio`.
- Rama activa de desarrollo e integración: **`dev`**.
- Rama estable/promoción: **`main`**.
- Carpeta oficial de Google Drive: `Proyecto Centenario`.
- Existe **una sola línea de desarrollo**. Demo GitHub Pages y QA instalable son dos salidas del mismo producto, no proyectos separados.

## 2. Regla de arranque

Antes de responder sobre estado, diseñar, programar, corregir, hacer un PR, desplegar o modificar documentación:

1. consulta el **HEAD vivo de `dev`** y el estado de `main`;
2. inspecciona el árbol y el código realmente existente en `dev`;
3. lee `AGENTS.md`, `README.md`, `docs/PMGM-BASE-001-estado-maestro.md` y los `docs/PMGM-GOV-*` vigentes;
4. lee ADR, migraciones, pruebas, workflows y documentación específica de la tarea;
5. consulta en Google Drive la **Línea Base Maestra** y documentos oficiales vigentes aplicables;
6. si la tarea toca cargos, firmas, permisos, aprobaciones o procedimientos institucionales, verifica Constitución/Reglamento, protocolos y formularios oficiales;
7. compara GitHub y Drive y resuelve cualquier discrepancia material antes de cambiar el código;
8. distingue siempre entre: documentado, implementado en rama, integrado en `dev`, visible en demo, desplegado en QA, probado en UAT y promovido a `main`.

## 3. Prevalencia

Cuando dos fuentes difieran:

1. normativa institucional vigente y documentos oficiales aplicables;
2. Línea Base Maestra y cambios formalmente aprobados;
3. código vigente de `dev`, Estado Maestro, ADR y documentación técnica asociada;
4. `main` como rama estable/promoción y otros documentos vigentes de Drive;
5. chats, resúmenes y memoria de IA, solo como contexto histórico.

## 4. No regresión

No elimines, simplifiques, reemplaces ni reconstruyas funcionalidades, modelos, perfiles, permisos, flujos, migraciones, pruebas, documentación o arquitectura ya vigentes sin una decisión explícita y trazable.

No crees una segunda API, un frontend paralelo, un nuevo esquema de autorización o un mock sustitutivo cuando el sistema real ya contiene esa capacidad.

## 5. Flujo de desarrollo

`Requisito/fuente → feature/* desde HEAD de dev → implementación sobre código existente → pruebas → documentación → PR a dev → CI exact-head → merge a dev → Demo Pages + QA instalable → QA/UAT → promoción controlada a main`

No mezcles una corrección documental/gobierno con cambios funcionales si pueden revisarse por separado.

## 6. Definición permanente de terminado: triple salida

Todo incremento funcional del Proyecto Centenario debe producir y mantener **tres salidas sincronizadas del mismo desarrollo y del mismo SHA**:

1. **Programación real:** funcionalidad implementada sobre el código vigente de `dev`, con backend/frontend/datos/permisos/auditoría/migraciones/pruebas/documentación según corresponda.
2. **Demo funcional GitHub Pages:** misma experiencia y mismos flujos funcionales, usando exclusivamente datos ficticios/sintéticos y adaptadores mock equivalentes a los contratos reales. No puede ser una maqueta estática que omita el comportamiento implementado. Debe publicarse desde `dev` y permitir verificar el SHA visible.
3. **Instalable QA para `srv01`:** paquete reproducible generado desde el mismo SHA, con aplicación, infraestructura, configuración de ejemplo segura, migraciones, scripts/runbook, manifiesto/checksums y controles necesarios para montar el stack operacional de QA.

Un incremento **no se considera terminado** solo porque el PR esté fusionado o el CI compile. Debe quedar, como mínimo, código integrado, demo actualizada/publicable y artefacto instalable generado/actualizado. Cuando exista acceso operativo a `srv01`, el ciclo continúa con despliegue, smoke y QA/UAT. Si el despliegue no pudo realizarse, debe declararse expresamente como pendiente y nunca confundirse con un estado operacional.

La demo pública nunca usa datos personales reales, secretos ni servicios institucionales reales. El instalable QA sí valida la arquitectura operacional con API, PostgreSQL, OIDC/Keycloak, almacenamiento S3/MinIO, ClamAV y demás componentes vigentes.

## 7. Registro obligatorio al terminar cada tarea

Al terminar **cada tarea del Proyecto Centenario**, actualiza ambos registros persistentes, aunque la tarea no cambie código:

1. **GitHub:** deja el resultado en el Issue/PR pertinente y actualiza documentación versionada cuando cambie el estado, decisión, código, validación o próximo paso. Registra fecha, HEAD exacto de `dev` y `main`, rama/commit/PR, archivos afectados, pruebas/gates/artefactos, límites operacionales y pendientes. Si no hubo cambios técnicos, indícalo expresamente.
2. **Google Drive:** actualiza la Línea Base Maestra con el mismo estado y enlaces/evidencias. Si no cambió el estado maestro, deja constancia breve de la revisión y del próximo paso.
3. **Handoff transferible:** deja instrucciones suficientes para que cualquier chat/IA continúe desde GitHub y Drive, indicando fuentes a consultar y qué no se debe asumir desde conversaciones.

La intervención se cierra sólo después de verificar la lectura de retorno de ambos registros. Luego deja, como mínimo:

- HEAD de `dev` observado al inicio y al cierre;
- rama/commit/PR trabajados y estado del PR;
- archivos y funciones modificadas;
- migraciones, si existen;
- pruebas/CI y resultado;
- estado de Demo GitHub Pages;
- estado de QA/artefacto instalable;
- documentación GitHub y Drive actualizada;
- pendientes y riesgos abiertos.

## 8. Prompt universal de arranque

Copia este texto al iniciar un chat, Work o IA que no tenga aún el contexto del proyecto:

> **Continuar Proyecto Centenario. No uses memoria de chats como fuente de verdad. Primero consulta el HEAD vivo de `dev` del repositorio `pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio`, verifica `main` como rama estable, lee `START-HERE.md`, `AGENTS.md`, Estado Maestro, documentos PMGM-GOV/ADR/pruebas/workflows relacionados y consulta la Línea Base Maestra y documentos oficiales vigentes de Google Drive. Preserva todo lo ya programado y aprobado, resuelve discrepancias GitHub/Drive antes de modificar código, trabaja mediante `feature/* → PR/CI exact-head → dev`, mantén paridad Demo GitHub Pages + QA instalable y no promociones a `main` sin el flujo aprobado. Al terminar deja un handoff verificable.**

## 9. Regla final

**Si no puedes consultar `dev` y las fuentes oficiales necesarias, no reconstruyas ni continúes a ciegas: declara qué fuente no pudiste verificar.**

Los SHA escritos en conversaciones o documentos son checkpoints históricos. **El único punto técnico actual es el HEAD vivo de `dev` al comienzo de cada intervención.**

- Continuidad de delegación Claude y consolidación de adendas: [handoff ChatGPT/Codex](docs/handoffs/2026-09-29-gpt-consolidacion-adendas-claude.md).

- Continuidad de UI QA v0.63, encabezado móvil y SHA visible: [handoff ChatGPT](docs/handoffs/2026-09-30-chatgpt-cierre-mobile-v063.md).
- Continuidad de concurrencia e inmutabilidad de ajustes de recibos (#191, PR #228): [handoff ChatGPT](docs/handoffs/2026-09-30-chatgpt-cierre-treasury-concurrency.md).
- Auditoría de históricos #230 / PR #231: [informe](docs/handoffs/2026-09-30-chatgpt-auditoria-antiguos.md) y [cierre documental](docs/handoffs/2026-09-30-chatgpt-cierre-auditoria-antiguos.md); 8 PR/24 issues revisados, #43/#67 reemplazados y #46 completado; remanentes y pausa srv01 preservados.
- Continuidad de pruebas HTTP de insinuados (#44, PR #233): [implementación](docs/handoffs/2026-10-01-chatgpt-candidate-publication-http.md) y [cierre](docs/handoffs/2026-10-01-chatgpt-cierre-candidate-publication-http.md); 354/354, aislamiento por Taller y fecha institucional en fixture, con srv01 pausado.
- Continuidad de sincronización REQ-025 (#235, PR #236): [trabajo](docs/handoffs/2026-10-01-chatgpt-req025-publicaciones.md) y [cierre](docs/handoffs/2026-10-01-chatgpt-cierre-req025-publicaciones.md); propuesta #45 preservada con derechos de ceremonia y remanente técnico #237.
- Continuidad de evidencia histórica de publicación (#237, PR #239): [trabajo](docs/handoffs/2026-10-01-chatgpt-publication-evidence.md) y [cierre](docs/handoffs/2026-10-01-chatgpt-cierre-publication-evidence.md); 368/368, foto/política originales y legado explícito, con srv01 pausado.
- Continuidad de revisión residual #245 / PR #246: [cierre ChatGPT](docs/handoffs/2026-10-01-chatgpt-cierre-gap131-revision-residual.md); GAP-001 corregido, cartola vigente reconocida, #116 restringido y main/srv01 preservados.
