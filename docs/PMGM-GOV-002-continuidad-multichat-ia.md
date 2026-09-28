## Handoff vigente — Issue #46, estados de asistencia — 28-09-2026 UTC

- Decisión PO registrada en GitHub Issue #46 y Drive Línea Base: Presente = asistió; Ausente = faltó sin aviso; Justificada = faltó, pero avisó, tenía permiso o comunicó una razón.
- Base de implementación: `dev@b252d1e2a1b1b5a2a111037f743b6e57823b6ef7`; `main@6dfb9546a4873baff15955cf86abfd7d47e3d111` sin cambios. Rama `feature/instruction-attendance-status-20260928` creada desde ese SHA.
- Brecha confirmada: el registro de instrucciones aceptaba sólo presente/ausente y la interfaz asignaba Presente silenciosamente a todos. Mi ficha ya representaba “Justificada”; el reporte de Orden agregaba sólo presentes/ausentes.
- Este incremento añade selección obligatoria de los tres estados, conserva los registros de asistencia existentes, agrega el conteo de justificadas al reporte agregado, pruebas backend/frontend y criterios QA. No requiere migración: el estado se persiste en el campo existente.
- Cambios en rama/PR hasta CI exact-head SUCCESS; no declarar integrado/publicado antes de verificarlo. `srv01` sigue pausado por Issue #97: no instalación ni QA física/UAT. No promover `main`.

## Handoff vivo — post-merge PR #210 — 28-09-2026

- Estado verificado: `dev@4087f47a575ba6bf48fead93838de5425a62bb10`; `main@6dfb9546a4873baff15955cf86abfd7d47e3d111` sin cambios. No había clon Git local; lectura remota de ramas, árbol, fuentes y estado completada.
- PR #210 documental quedó integrada por squash en el SHA vigente. Exact-head del PR y post-merge PMGM CI, Showcase/Pages, QA Installable y Pre-UAT: SUCCESS.
- Pages artifact #10984181780 digest `sha256:4cff4268bee16ca3e48cffc9d0f0ddfff0bb1fa107571c741867a54f8b1dd566`. `qa-current.json` confirma `sourceSha=4087f47a575ba6bf48fead93838de5425a62bb10`; ZIP publicado SHA-256 `a43f2b7490b6f3b5e7b44d96c9137ae655ca5aa0aaa2f1f1686b133d7c0d9a6c`.
- QA artifact #10983728727 digest `sha256:353e3808fd25424fe1837e217303cbdc79a3c692ae62331cf04036a9ae456b92`; ZIP instalable interior SHA-256 `cbe8e16c66b4cd0d7d470160fe41102fc4212d7c35bc1f0e9acd85478fac2ceb`; BUILD-INFO verifica el SHA de origen. Paquete generado, no instalado.
- Issue #97 sigue abierto. `srv01` pausado por instrucción del Sponsor: no instalar, desplegar ni ejecutar smoke/regresión física/UAT. No promover `main`.
- Issue #46 / PMGM-BLG-078 sigue abierto. El código vigente de Mi ficha presenta historial propio de instrucciones con fecha, grado, tema, asistencia y encargado, en modo sólo consulta; la demo usa registros ficticios y la prueba frontend actual impide agregar un campo `progress` a los datos demo. La prueba no sustituye aceptación visual institucional. Antes de tocar código, identificar una brecha concreta frente a sus criterios; no duplicar lo integrado por PR #206.
- No se inició un incremento funcional. #190 mantiene pendientes tarifas/monedas; #191 requiere cerrar presentación contable. PR #182 fue cerrada sin merge y sustituida por #204.
- La continuidad se rige por estado persistente: cualquier chat o IA puede continuar leyendo START-HERE, GitHub `dev` y la Línea Base/documentos oficiales de Drive. Ningún chat o memoria conversacional es requisito ni fuente de verdad. GitHub gobierna código/backlog/decisiones técnicas; Drive conserva Línea Base y fuentes oficiales. Esta regla armoniza las instrucciones vigentes y no altera gobierno institucional.

# PMGM-GOV-002 — Continuidad multichat, multi-IA y gobierno de ramas

## HANDOFF VIGENTE PARA CUALQUIER CHAT O IA — 27-09-2026

Este bloque es una fotografía del último estado revisado, no reemplaza la verificación en vivo. Antes de actuar, vuelve a consultar GitHub y Drive.

### Estado del proyecto verificado — 27-09-2026

- Repositorio: `pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio`.
- HEAD verificado después de integrar PR #193: `dev@435a0a6870a7db645d98256a7b8fbdac411d307d`; `main@6dfb9546a4873baff15955cf86abfd7d47e3d111` sin cambio.
- PR #187, #189, #192 y #193 están fusionadas en `dev`. PR #193 corrigió el rate limit de ECR usando la imagen oficial AWS CLI de Docker Hub fijada en 2.32.25 y formalizó la política de integración sin aprobación redundante de terceros.
- Sobre el HEAD integrado: PMGM CI #1620 SUCCESS; Showcase #911 SUCCESS (artifact Pages y paso Deploy showcase SUCCESS); QA Installable #549 y Pre-UAT #381 SUCCESS. Artefactos no significan instalación/UAT.
- Issue #188 y #191 continúan abiertos para pendientes; Issue #97 abierto y `srv01` en pausa. No hubo instalación ni UAT. No promover a `main`.
- Para una PR a `dev`, la autorización ya otorgada por el Product Owner para el alcance aprobado permite integrar cuando CI exact-head esté SUCCESS y GitHub lo permita técnicamente. No pedir segunda autorización de colaborador. Un check verde aislado no es autorización. Verificar cualquier protección real de GitHub en vivo; promoción a `main` y levantamiento de pausa de `srv01` requieren decisiones separadas.

### Procedimiento obligatorio para continuar

1. Verifica conexión y consulta los SHA vivos de `origin/dev` y `origin/main`. En el checkout, usa `git fetch origin dev main`; no asumas que la rama local o este handoff siguen al día.
2. Lee `AGENTS.md`, `START-HERE.md`, `README.md`, `docs/PMGM-BASE-001-estado-maestro.md`, `docs/PMGM-NEXT-001-siguiente-corte-tecnico.md`, `docs/PMGM-GOV-001-instrucciones-decisiones-consolidadas.md`, `docs/PMGM-GOV-002-continuidad-multichat-ia.md`, ADR, pruebas y workflows relacionados en el HEAD vivo de `dev`.
3. Consulta en Drive la Línea Base Maestra vigente y los documentos oficiales que afecten el tema. Revisa la fecha/versión y resuelve diferencias antes de cambiar reglas, permisos, cargos, firmas o flujos.
4. Comprueba estado de PRs/issues, Actions, Pages y paquete instalable por SHA. Informa por separado: documentado, implementado en rama, integrado en `dev`, publicado en Pages, artefacto generado, instalado en QA física y aceptado en UAT. No deduzcas una etapa de otra.
5. Mantén `srv01` en pausa mientras esa instrucción siga vigente. No empieces un nuevo incremento funcional sin alcance aprobado. Para un alcance autorizado, crea `feature/*` desde el HEAD actual de `dev`, implementa con pruebas/documentación, abre PR a `dev`, verifica gates exact-head y sigue la regla de aprobación vigente. No cambies `main` directamente.
6. Registra decisiones y resultados en GitHub y Drive; actualiza BASE/NEXT/handoff con SHAs, PRs, runs, artefactos, límites y pendientes. Deja el árbol y la rama local identificados al cerrar.

### Instrucción reutilizable para iniciar otro chat o IA

> Continúa el Proyecto Centenario desde las fuentes persistentes. No uses conversaciones ni memoria como fuente de verdad. Consulta el HEAD vivo de `dev` y `main` en `pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio`; lee `START-HERE.md`, `AGENTS.md`, README, BASE/NEXT, GOV-001/GOV-002, ADR y documentación relacionada en ese mismo SHA. Consulta en Drive la Línea Base Maestra y los documentos oficiales aplicables. Compara ambas fuentes antes de cambiar código o reglas. Conserva `main` y respeta la pausa vigente de `srv01`; no declares QA/UAT por CI o artefactos. Continúa sólo el alcance aprobado, por feature branch y PR a `dev`. Registra el avance en GitHub y Drive y termina con un handoff verificable.


**Estado:** vigente al integrarse en `dev`  
**Fecha:** 2026-09-17  
**Alcance:** continuidad técnica/funcional del Proyecto Centenario

## 1. Decisión

El Proyecto Centenario puede ser continuado desde cualquier chat, IA o herramienta de desarrollo. La continuidad no depende de conservar una conversación específica: depende de reconstruir el estado desde las fuentes persistentes del proyecto.

Este documento complementa `PMGM-GOV-001`. Cuando una referencia histórica señale que un chat concreto es el único hilo maestro, debe interpretarse como una práctica de coordinación de ese momento, **no como una restricción de continuidad**. La fuente persistente prevalece.

## 2. Ramas

- `dev`: rama activa de desarrollo e integración.
- `main`: rama estable y de promoción controlada.
- `feature/*`: cambios aislados nacidos desde el HEAD vigente de `dev`.
- `release/*`: cortes de estabilización/RC cuando correspondan.

Toda IA/desarrollador debe verificar las ramas antes de trabajar. No debe asumir que `main` contiene el desarrollo más reciente.

Checkpoint conocido al aprobar esta regla: `dev` en `12b37f465220078e3d7472b3c019965c7cce85ae`, merge de PR #87. El checkpoint no sustituye el HEAD vivo.

## 3. Fuentes persistentes

Para retomar:

1. GitHub `dev`: código integrado activo, pruebas, migraciones, ADR, workflows y documentación técnica.
2. Google Drive: Línea Base Maestra y documentación oficial/normativa.
3. GitHub `main`: rama estable/promoción y referencia de releases consolidados.
4. Chats: contexto histórico, nunca sustituto del estado versionado.

## 4. Reconciliación

Si `main` y `dev` divergen, no se debe hacer merge ciego ni escoger una rama por nombre. Se debe identificar qué commits pertenecen a desarrollo activo, cuáles son documentación/estabilidad, revisar conflictos y promover de forma explícita.

Si GitHub y Drive difieren en una regla funcional o normativa, se compara fuente, fecha y aprobación antes de modificar el código afectado.

## 5. Flujo de desarrollo

`Requisito → fuente/norma → feature desde dev → código + pruebas + documentación → PR a dev → CI exact-head → merge dev → Demo Pages + artefacto/QA → UAT → release/main según aprobación`

No se requiere autorización adicional para continuar una tarea ya aprobada dentro de su alcance; sí se requiere decisión cuando el cambio altera arquitectura, normativa, alcance o una decisión previamente aprobada.

## 6. Registro obligatorio al terminar cada tarea

Al cerrar cada tarea del Proyecto Centenario, con o sin cambios de código, deja un registro en **GitHub y Google Drive**:

- GitHub: Issue/PR y documentos versionados pertinentes; fecha, SHA exacto de `dev` y `main`, rama/commit/PR, alcance, archivos, pruebas/gates y artefactos, situación de Pages/QA/UAT, límites y próximos pasos.
- Drive: Línea Base Maestra sincronizada con el mismo resultado, enlaces y evidencias; si no cambió el estado, registra brevemente la revisión y el próximo paso.
- Continuidad: instrucciones claras, fuentes persistentes a consultar y un handoff copiable para cualquier chat o IA.
- Verifica mediante lectura de retorno que ambos registros quedaron escritos. No cierres la tarea antes de esa comprobación.

En cada continuación, vuelve a verificar el HEAD y el estado de PRs/issues; este documento, los comentarios y los chats son checkpoints y no reemplazan el estado vivo.

## 7. Regla de no regresión

Nunca sustituir el código activo de `dev` por prototipos, código simplificado o implementaciones paralelas provenientes de chats o ramas desactualizadas. Reutilizar y extender el sistema existente.


## 8. Aprobación de PR y autoridad del Product Owner

Para fusionar una PR a `dev`, el proyecto no requiere por defecto una aprobación adicional de un tercero o colaborador. La autorización expresa del Product Owner para continuar e integrar un alcance aprobado es suficiente, siempre que los gates exact-head requeridos estén en SUCCESS y GitHub permita el merge. Cuando el Product Owner pide “continuar” y fusionar los cambios “si están listos”, esa instrucción autoriza la integración al cumplirse esas condiciones; no se vuelve a preguntar por una segunda aprobación.

Los checks verdes prueban gates técnicos y no sustituyen la autorización del Product Owner. A la vez, no se debe inventar un requisito de revisión externa porque una PR no muestre reviews. Comprueba la protección/regla de la rama en vivo: si hay un bloqueo real de GitHub, describe exactamente ese bloqueo y aplica la configuración vigente sin atribuirlo a una regla de proyecto no documentada. La promoción a `main` sigue requiriendo autorización explícita y el flujo de estabilidad; esta regla no levanta la pausa de `srv01` ni autoriza UAT.
