# PMGM-GOV-002 — Continuidad multichat, multi-IA y gobierno de ramas

## HANDOFF VIGENTE PARA CUALQUIER CHAT O IA — 27-09-2026

Este bloque es una fotografía del último estado revisado, no reemplaza la verificación en vivo. Antes de actuar, vuelve a consultar GitHub y Drive.

### Estado del proyecto al preparar este handoff

- Repositorio: `pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio`.
- `dev@49744d10472e739580f58e0752e211ce25f22655`; `main@6dfb9546a4873baff15955cf86abfd7d47e3d111`.
- PR #185 está integrada. `Mis planchas` está en `Mi ficha`; gates exact-head post-merge y Pages/paquete QA fueron verificados para ese SHA.
- Demo: https://pablodominguez8375-2025.github.io/Modernizaci-n-GLMC-Milenio/ . El `qa-current.json` de Pages indicó el SHA de `dev`; checksum publicado y descargado coincidieron y MANIFEST verificó 768/768 archivos.
- Issue #97 sigue abierto para la QA operacional. Por decisión del Sponsor, `srv01` continúa en pausa; no instalar, desplegar, ejecutar smoke de servidor, regresión física ni UAT hasta recibir instrucción explícita.
- PR #186 es documental y permanece abierta al momento de este registro. Consulta su HEAD y checks actuales antes de revisarla. No infieras autorización de fusión a partir de su estado verde.
- Los checks de CI y los paquetes no son aceptación física de QA/UAT ni promoción a `main`.

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
