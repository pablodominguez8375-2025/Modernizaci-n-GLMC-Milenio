# PMGM-GOV-003 — Protocolo de coordinación multi-IA


- Estado: **APROBADO por el Product Owner (Pablo Domínguez) el 29-09-2026**.


- Aplica a: Claude (claude.ai / Claude Code), ChatGPT/Chatito, Codex y cualquier agente que trabaje en este repositorio en paralelo.


- Complementa sin reemplazar: `START-HERE.md`, `AGENTS.md`, PMGM-GOV-001, PMGM-GOV-002 y la Línea Base Maestra (Drive).


- Copia en Drive: «PROTOCOLO DE COORDINACIÓN MULTI-IA (Claude + ChatGPT/Codex)», id `1BCQcpvA2GfGtZ1j-WQAxAO3pk93L2FwkTfiecq5DKts`. Ante diferencias, prevalece este archivo en `dev`.


## 1. Principio


Ningún agente programa sin haber **reclamado** antes el trabajo en GitHub. Si no está reclamado en GitHub, se considera que no está en curso. Los chats y las memorias de las IA no sirven como mecanismo de coordinación.


## 2. GitHub como tablero único


1. Toda tarea debe tener un Issue. Si no existe, el agente lo crea antes de empezar.


2. Reclamar una tarea significa dos cosas:


   - poner en el Issue la etiqueta `agente:claude` o `agente:chatgpt`;


   - abrir de inmediato un PR **draft** titulado `[CLAUDE] …` o `[CHATGPT] …`.


   La descripción del PR debe indicar el Issue, el alcance, la lista de archivos que se van a tocar y la fecha estimada de cierre.


3. Cada Issue lo trabaja un solo agente. Solo el PO puede reasignarlo.


4. Si un PR draft pasa 72 h sin commits, el PO (y nadie más) puede liberarlo.


## 3. Ramas


- Nombre: `feature|fix|docs/<tema>-<AAAAMMDD>-claude` o `…-gpt`.


- Se crean desde el HEAD vivo de `dev` y se rebasan sobre `dev` vivo antes del merge.


- Ningún agente hace commits en la rama de otro. Las sugerencias van como comentario en el PR.


## 4. Chequeo anti-conflicto (antes del primer commit y antes del merge)


1. Ejecutar `git fetch` y revisar el HEAD vivo de `dev`.


2. Listar los PR abiertos y sus archivos (`gh pr list`, `gh pr diff <n> --name-only`).


3. Si un archivo propio aparece en un PR abierto de otro agente:


   - si es un archivo caliente (§5), detenerse y avisar en el Issue;


   - si no lo es, continuar e informarlo en el PR.


## 5. Archivos calientes (solo un PR abierto a la vez)


- `frontend/src/App.tsx`


- `demoProfiles.ts` y su test


- `.github/scripts/capture-showcase-views.mjs` y `.github/workflows/*`


- Hojas de tema e identidad: `institutional-theme.css`, `*ppt-fidelity.css`, `mobile-nav-compact.css`, `member-portal.css`


- Migraciones EF Core (`backend/**/Migrations/*`): un solo agente a la vez


- `docs/qa/PMGM-SRV01-REGRESSION-KIT.md`


- Gobierno: `AGENTS.md`, `docs/PMGM-GOV-*`, ADR


## 6. Handoffs sin conflictos


- Cada agente deja su handoff en un archivo **nuevo**: `docs/handoffs/AAAA-MM-DD-<agente>-<tema>.md`.


- `START-HERE.md` se actualiza solo en el PR documental posterior al merge, a cargo del agente que integró. Ese PR agrega una línea con enlace al handoff y no reescribe bloques.


- Contenido mínimo del handoff:


  - SHA de `dev` y `main` al inicio y al cierre;


  - PR e Issue;


  - archivos tocados;


  - pruebas (tests, lint, tsc);


  - gates exact-head;


  - SHA publicado en Pages;


  - paquete QA con su SHA-256;


  - pendientes y lo que **no** se debe tocar.


## 7. Integración a `dev`: un merge a la vez


1. Se exigen gates exact-head en SUCCESS: CI, Showcase y QA Installable.


2. Antes de fusionar, confirmar que `dev` no cambió desde el último rebase. Si cambió, rebasar de nuevo y esperar los gates.


3. Fusionar con squash. Rige la autorización permanente de `AGENTS.md` §12.


4. Después de cada merge, el otro agente rebasa sus ramas abiertas.


5. Ningún agente promueve `main`, despliega en `srv01` ni declara UAT sin instrucción explícita del Sponsor (Issue #97, PR #1).


## 8. GitHub Pages


- Pages publica el HEAD de `dev`, así que la última integración reemplaza a la anterior.


- Cada aviso de versión QA al PO debe incluir el **enlace, el SHA y los PR incluidos**, verificados en `downloads/qa-current.json`.


- Enlace: https://pablodominguez8375-2025.github.io/Modernizaci-n-GLMC-Milenio/


- No se fusiona otro PR funcional mientras el PO revisa una versión, salvo que él lo autorice.


## 9. Drive


- La Línea Base Maestra tiene un único consolidador: el agente con edición de Google Docs o el PO.


- Un agente que no puede editar documentos existentes deja **adendas fechadas** en la carpeta Proyecto Centenario con el título `Línea Base — Adenda AAAA-MM-DD — <agente> — <tema>`. El consolidador las incorpora y agrega «Consolidada» al título.


- Ningún agente borra ni reemplaza documentos oficiales.


## 10. Reparto por dominio (confirmado por el PO el 29-09-2026)


| Dominio | Agente | Pendientes conocidos al 29-09-2026 |


|---|---|---|


| UI, identidad, responsive, accesibilidad (SENADIS), tests frontend | Claude | Escala tipográfica ≥ 12 px y tipografía de UI (requieren aprobación de identidad); code-splitting; navegación móvil |


| Tesorería y reglas contables | ChatGPT/Codex | PR #221, Issues #190 y #191 |


| Admisiones / Afiliación-Incorporación | ChatGPT/Codex | PR #116 |


| Auditorías de brechas y documentación | Según asigne el PO | PR #131, GAP-005/009/013 |


| Backend y migraciones | Un agente a la vez (§5) | — |


Cuando un trabajo cruza dominios, lo lleva el agente dueño del dominio principal, que lo declara en su PR draft.


## 11. Arranque de sesión (resumen para cualquier IA)


1. Revisar el HEAD vivo de `dev` y `main`.


2. Revisar los PR abiertos con sus archivos y la etiqueta `agente:*`.


3. Leer START-HERE, AGENTS, este GOV-003 y el último `docs/handoffs/*`.


4. Revisar la Línea Base y las adendas de Drive sin consolidar.


5. No tomar Issues ni archivos calientes reclamados por otro agente.


6. Reclamar la tarea (etiqueta + PR draft) antes de programar.


7. Al cerrar: dejar el handoff en un archivo nuevo, la adenda de Drive si corresponde, y enviar al PO el enlace de Pages con SHA y PR.


## 12. Instrucción permanente del PO (29-09-2026)


1. **Registro obligatorio.** Todo trabajo de cualquier agente, ya sea código, revisión, análisis o decisión, debe cerrar con registro de seguimiento y control. El registro consiste en:


   - un handoff en `docs/handoffs/`;


   - una actualización o adenda en Drive de la Línea Base.


   Así cualquier chat o IA puede continuar desde el mismo punto sin depender de la memoria de una conversación. Un trabajo que no tiene registro se considera no entregado.


2. **Las reglas registradas obligan a todos los agentes.** Las reglas que cualquier agente (ChatGPT/Codex, Claude u otro) deja registradas en GitHub (`AGENTS.md`, `START-HERE.md`, `PMGM-GOV-*`, ADR, handoffs) o en Drive (Línea Base Maestra, adendas) se mantienen y se respetan. Si un agente discrepa, lo informa al PO y no las cambia unilateralmente.


## 13. Delegación cuando un agente no puede ejecutar (instrucción del PO, 29-09-2026)


Si un agente no puede ejecutar algo, por ejemplo cuando Claude en claude.ai no tiene permiso de escritura en GitHub ni puede editar Docs existentes, deja la tarea como instrucción para ChatGPT/Codex y ChatGPT/Codex la ejecuta. El procedimiento es el siguiente:


1. **El agente que delega crea un documento en la carpeta Proyecto Centenario.** El título sigue el formato `INSTRUCCIONES PARA CHATGPT — AAAA-MM-DD — <tema>` y el documento incluye:


   - tareas numeradas;


   - archivos y contenido exacto;


   - criterios de verificación;


   - lo que **no** se debe hacer.


2. **ChatGPT/Codex ejecuta las tareas.** Aplica el protocolo completo: reclamo, gates, un merge a la vez, handoff y enlace de Pages al PO. Al terminar, agrega « — EJECUTADA» al título del documento, o « — EJECUTADA PARCIAL» indicando qué quedó pendiente. Además, deja su handoff con referencia al documento.


3. **Límites de lo que se puede delegar.** Solo se delega lo que ya está aprobado. Si una tarea cae en el dominio del agente que delega, esa tarea se transfiere mientras dura la delegación, y el agente que delega revisa el resultado en su siguiente sesión.
