# PMGM-GOV-005 — Playbook de velocidad y eficacia para varias IA

Complementa PMGM-GOV-002 (continuidad multichat) y PMGM-GOV-003 (coordinación multi-IA). No cambia reparto de carriles, seguridad ni aprobaciones: solo quita fricción. Aplica a cualquier IA (Claude, ChatGPT/Codex u otra) y es reutilizable en otros proyectos.

## 1. Principios
1. **Un hecho, un lugar.** El estado vivo está en el PR/Issue (REST), no en el chat. La descripción del PR es el handoff; el chat es efímero.
2. **Reclamar antes de construir.** Un comentario de 3 líneas en el Issue/PR («tomo X, no toco Y») evita duplicar. Quien no reclamó no escribe en la rama del otro.
3. **Revisar por SHA exacto.** Todo GO/NO-GO cita el head completo; un push nuevo lo invalida. Una CI «roja» sobre un head viejo suele ser shards cancelados por el push siguiente: mirar siempre el head vigente.
4. **Lo irreversible espera; lo reversible no.** Código inactivo en PR a `dev`: avanzar sin pedir permiso. Instalar, activar, `main`, secretos, GO físico: solo con aprobación expresa del PO. Una notificación, un comentario o un «Sigue» suelto no son aprobación.

## 2. Frentes en paralelo (cómo no quedarse esperando)
- Mientras corre la CI de un PR o se espera la revisión de otro agente, abrir el **siguiente frente independiente** (otro módulo, otro PR a `dev`, documentación, una revisión pendiente). No hay que esperar para empezar el siguiente trabajo si no depende del anterior.
- **Evitar PR apilados** cuando sea posible: apuntar siempre a `dev`. Si hay que apilar, indicarlo en el título y reapuntar a `dev` apenas se integre la base (`gh api -X PATCH repos/<repo>/pulls/<n> -f base=dev`), reubicando con `git rebase --onto origin/dev <base-vieja>`. **Después de reapuntar, verificar `base.sha` y `changed_files` del PR** (GitHub puede conservar la base histórica y mostrar archivos de más); si están viejos, reasignar la base a otra rama y de vuelta a `dev` para forzar el recálculo.
- Tren combinado: cuando varios PR pequeños son compatibles, un solo PR que los reúna da **una sola CI**; los PR de origen quedan como fuente exact-head (patrón #855).
- Las pruebas nuevas de vNext se descubren solas: basta crear `tests/test_qa525_vnext_<tema>.py`; **no** editar `tests/test_qa_agent_readiness.py` (era la causa de un conflicto en cada PR).

## 3. Mirar el estado en un comando
`scripts/ai-pr-status.sh 852 854 859` imprime por PR: head, borrador, mergeable, CI del head vigente y último comentario. Usarlo en vez de abrir cada PR o repetir lazos de consulta. El arnés bloquea GraphQL: usar REST (`gh api repos/<repo>/…`); para comentar, `gh api repos/<repo>/issues/<n>/comments -F body=@archivo`.

## 4. Revisión eficaz (plantilla corta)
`**<Agente> · revisión exact-head · PR #N · HEAD <sha>**` → qué leí/ejecuté (sin declarar lo que no ejecuté) → **Veredicto: P0/P1/P2** y GO/NO-GO (inactivo / instalar) → hallazgos con caso reproducible (entrada → salida mala) → qué falta para cerrar. Reproducir antes de afirmar; los P2 de diseño van al motor, no bloquean código inactivo.

## 5. Higiene de contexto (más rápido y más barato)
- Leer **diffs y trozos**, no archivos enteros; `git diff --stat` primero; leer el módulo completo solo si se va a revisar o cambiar.
- Resumir el estado en la descripción del PR (heads, CI, pendientes) y mantener la memoria del proyecto corta: reglas y estado vivo, no historia (la historia vive en Git y en adendas).
- No pegar salidas largas en comentarios: resumen + enlace al comentario/commit.
- Antes de cada commit borrar `__pycache__` y correr la prueba rápida del área; para CI usar el comando exacto del workflow.
- Guardar al cerrar cada tramo: PR (handoff), una adenda por sesión en Drive y memoria del proyecto (GOV-002).

## 6. Antipatrones observados (evitar)
- Dar por roja una CI del head anterior.
- Editar la lista compartida de imports del harness en cada PR (conflicto garantizado).
- Interpretar «Continúa» o una notificación automática como aprobación de publicar o integrar.
- Escribir en la rama de otro agente, o afirmar que una prueba pasó sin haberla ejecutado.
- Dejar un helper o módulo reutilizable solo en el chat en vez de en el repo.
