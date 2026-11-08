# QA525 G6 — Rotación de la contraseña de Restic (PROPUESTA, sin ejecutar)

Estado: **decisión de diseño tomada por ChatGPT (alternativa B, repositorio nuevo; #931 comentario 6714009993). Borrador documental, sin verificar contra los scripts reales del host. No ejecutado. Este documento NO contiene comandos ejecutables: son pasos de diseño. Cada comando real se redacta después del preflight de solo lectura, lo revisa ChatGPT y el Sponsor lo autoriza en ese momento.**

Alcance: alternativa A de G6 (QA sintético únicamente). No cubre datos reales
(eso exige la alternativa B, #694, Ley 21.719).

## Por qué

La contraseña del repositorio Restic `/opt/centenario/qa-g6` quedó expuesta en un
chat. Debe considerarse comprometida. El valor no se repite ni se guarda en este
repositorio, en Drive ni en chats. La copia legítima vive solo en el Bitwarden
del Sponsor.

## Decisión de rotación: B, repositorio Restic nuevo (no confundir con las alternativas A/B de G6)

`restic key add/passwd` (alternativa A) cambia la contraseña que abre la clave maestra pero **no recifra los datos**. Quien tenga la contraseña expuesta y una copia anterior (por ejemplo las ya exportadas a Drive) las sigue abriendo. B (repositorio nuevo, contraseña nueva, respaldo completo nuevo) evita que la contraseña comprometida abra copias nuevas. Es una decisión de diseño, no un GO ni una autorización para rotar, borrar o copiar datos físicos.

## Reglas

1. La contraseña nueva se genera en Bitwarden (32+ caracteres) y se guarda ahí **antes** de usarla en glmdch. Nunca pasa por el chat, Git, Drive ni la línea de comandos. El Sponsor la introduce por Termius sin eco en pantalla y sin historial (el método exacto lo fija el SOP revisado).
2. Un comando por vez desde Termius; el Sponsor pega la salida solo después de comprobar que no contiene la contraseña. STOP ante cualquier discrepancia.
3. **No se inventan rutas, flags ni variables, y NO existe override por entorno.** Evidencia (AST, 07-11, #931 comentario 6715074055): `g6-local.py` (L213–215) hace `env = os.environ.copy()` y luego `env.update({'RESTIC_PASSWORD_FILE': str(KEY), 'RESTIC_REPOSITORY': str(ROOT / <constante>)})`; `g6-restore.py` (L177–183) usa `repo = stage / <constante>` y el mismo `env.update`. Como el `update` sobrescribe lo heredado, **exportar `RESTIC_REPOSITORY` o `RESTIC_PASSWORD_FILE` desde Termius no tiene efecto y es peligroso presumirlo efectivo**: el respaldo seguiría escribiéndose en el repositorio viejo con la contraseña comprometida y parecería haber funcionado. Este documento no sugiere ningún override.
4. Está **prohibido** modificar los scripts pineados (5bbf092e… / 514a482e…) sin PR, revisión de ChatGPT y de Claude, pin nuevo y GO específico. El diseño es **aditivo**: se agregan scripts nuevos con rutas y clave distintas; los viejos quedan intactos.
5. `shred` no es garantía de sanitización en SSD ni en nube. No se retira el repositorio, el archivo de contraseña ni las copias antiguas en esta fase.

## Diseño aditivo propuesto (sin tocar los scripts pineados)

Contrato, no implementación; nada de esto se ejecuta ni se instala sin PR, CI, revisión de ambos agentes y GO específico del Sponsor.

1. **Scripts nuevos, no editados:** un par `g6-local-v2` / `g6-restore-v2`, con nombre y pin propios, instalados `root:root` en el mismo directorio de herramientas. Solo cambian las constantes de ubicación: repositorio nuevo en carpeta nueva y exclusiva (por ejemplo `qa-g6-v2`), y archivo de clave nuevo distinto del actual. El resto de la lógica debe ser **idéntico**; la revisión exige un `diff` mecánico contra la fuente pineada que demuestre que solo cambian esas constantes.
2. **Retrocompatibilidad:** los scripts y el repositorio G6 antiguos no se modifican ni se mueven. El validador de G6 debe aceptar el acta nueva sin invalidar las anteriores (formato `archive.receipt.json` y estado de restauración idénticos). Si el validador o el ancla G6 fijan los hashes de los scripts, deben aceptar explícitamente el pin nuevo como segundo valor permitido, con prueba de que un script no listado sigue rechazado.
3. **Evidencia de recuperación aislada:** la restauración v2 extrae a un directorio temporal exclusivo, sin Compose ni base de datos viva, y deja un acta con SHA, fecha y estado, sin secretos. El acta debe distinguir el repositorio v2 del antiguo para que nadie confunda una restauración del repositorio comprometido con la nueva.
4. **Sin sobrescritura:** los scripts v2 se niegan a operar si el repositorio o el archivo de clave ya existen con otro contenido, y no leen `RESTIC_*` del entorno heredado (lo ignoran o lo rechazan, nunca lo respetan en silencio).

**Pregunta abierta para ChatGPT:** la fuente de `g6-local.py` y `g6-restore.py` no está en el repositorio (viven solo en el host). Para el `diff` mecánico se necesita su texto exacto: o ya existe en un PR/archivo que yo no veo, o hay que traerlo con una lectura de solo lectura verificada por hash. Falta también inspeccionar sin revelar valores qué son `ROOT`, `KEY` y `ARCHIVE`.

## Secuencia propuesta para el SOP

1. **Preflight solo lectura** (cada comando revisado antes): propietario, modo y hash de los dos scripts; espacio libre; definiciones de `ROOT`, `KEY` y `ARCHIVE` sin mostrar valores sensibles. Hecho parcialmente: ubicación de las asignaciones de entorno.
2. PR con los scripts v2 y su `diff` contra la fuente pineada; CI; revisión de ChatGPT y de Claude; pin nuevo.
3. GO específico del Sponsor para instalar los scripts v2. Recién ahí se genera la clave nueva en Bitwarden y se deja en un archivo `0600 root:root` nuevo, introducida por Termius sin eco ni historial.
4. Respaldo completo con `g6-local-v2` al repositorio nuevo; `restic check` y revisión de snapshots.
5. Exportar la copia cifrada fuera del host (carpeta Drive de respaldos), descargarla y comprobar que su SHA es idéntico.
6. Restauración aislada con `g6-restore-v2` en el mismo glmdch y acta (<48 h, que cubra toda la ventana del GO).
7. Solo después: el Sponsor decide con ChatGPT cuándo el GO usa el acta v2. El material antiguo se conserva intacto y aislado.
8. **Fase independiente y con aprobación separada**: retención y purga del material antiguo (repositorio, archivo de contraseña, copias en Drive). Mientras tanto se restringe el acceso y se documenta el riesgo residual: la papelera, las versiones y los cachés de Drive pueden conservar copias; `shred` no garantiza sanitización en SSD ni en nube.

## Lo que este documento no autoriza

- No instala nada, no toca glmdch, no da GO y no autoriza borrar nada.
- No sustituye el respaldo, la restauración y el acta del día del GO.
- El acta de GO la firma el Sponsor con su clave OPS.

## Riesgo residual aceptado (alternativa A de G6, datos sintéticos)

No hay recuperación ante pérdida del host (el Sponsor no tiene segundo servidor). El contenido es sintético. Antes de datos reales (#694, Ley 21.719) se exige la alternativa B de G6 (age + laboratorio externo).
