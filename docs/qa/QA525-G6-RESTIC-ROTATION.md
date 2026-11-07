# QA525 G6 — Rotación de la contraseña de Restic (PROPUESTA, sin ejecutar)

Estado: **decisión de diseño tomada por ChatGPT (alternativa B, repositorio nuevo; #931 comentario 6714009993). Borrador documental, sin verificar contra los scripts reales del host. No ejecutado. Este documento NO contiene comandos ejecutables: son pasos de diseño. Cada comando real se redacta después del preflight de solo lectura, lo revisa ChatGPT y el Sponsor lo autoriza en ese momento.**

Alcance: alternativa A de G6 (QA sintético únicamente). No cubre datos reales
(eso exige la alternativa B, #694, Ley 21.719).

## Por qué

La contraseña del repositorio Restic `/opt/centenario/qa-g6` quedó expuesta en un
chat. Debe considerarse comprometida. El valor no se repite ni se guarda en este
repositorio, en Drive ni en chats. La copia legítima vive solo en el Bitwarden
del Sponsor.

## Decisión: B, repositorio Restic nuevo

`restic key add/passwd` (alternativa A) cambia la contraseña que abre la clave maestra pero **no recifra los datos**. Quien tenga la contraseña expuesta y una copia anterior (por ejemplo las ya exportadas a Drive) las sigue abriendo. B (repositorio nuevo, contraseña nueva, respaldo completo nuevo) evita que la contraseña comprometida abra copias nuevas. Es una decisión de diseño, no un GO ni una autorización para rotar, borrar o copiar datos físicos.

## Reglas

1. La contraseña nueva se genera en Bitwarden (32+ caracteres) y se guarda ahí **antes** de usarla en glmdch. Nunca pasa por el chat, Git, Drive ni la línea de comandos. El Sponsor la introduce por Termius sin eco en pantalla y sin historial (el método exacto lo fija el SOP revisado).
2. Un comando por vez desde Termius; el Sponsor pega la salida solo después de comprobar que no contiene la contraseña. STOP ante cualquier discrepancia.
3. **No se inventan rutas, flags ni variables.** Los scripts `g6-local.py` y `g6-restore.py` (root, pines 5bbf092e… / 514a482e…) viven en glmdch. Hasta leer cómo obtienen `RESTIC_REPOSITORY` y `RESTIC_PASSWORD_FILE` no se redacta ningún comando de creación.
4. Está **prohibido** modificar los scripts pineados para apuntar al repositorio nuevo sin revisión de ChatGPT y de Claude y un pin nuevo.
5. `shred` no es garantía de sanitización en SSD ni en nube. No se retira el repositorio, el archivo de contraseña ni las copias antiguas en esta fase.

## Secuencia propuesta para el SOP

1. **Preflight solo lectura** (autorizado en principio; cada comando se revisa antes): propietario, modo y hash de los dos scripts; espacio libre; cómo obtienen el repositorio y el archivo de contraseña (inspección de las líneas relevantes sin mostrar secretos; `--help` solo si es seguro).
2. El Sponsor genera la clave nueva en Bitwarden y la deja en un archivo `0600 root:root` nuevo, distinto del actual.
3. Crear un repositorio distinto, en una carpeta nueva, única y propiedad de root, sin afectar el actual.
4. Respaldo completo al repositorio nuevo; `restic check` y revisión de snapshots.
5. Exportar la copia cifrada fuera del host (carpeta Drive de respaldos), descargarla y comprobar que su SHA es idéntico.
6. Restauración aislada en el mismo glmdch, sin Compose ni base de datos viva, y acta con SHA, fecha y estado, sin secretos. Debe cubrir toda la ventana del GO (<48 h).
7. Solo después: el Sponsor decide con ChatGPT una conmutación atómica y reversible hacia el repositorio nuevo, conservando el antiguo.
8. **Fase independiente y con aprobación separada**: retención y purga del material antiguo (repositorio, archivo de contraseña, copias en Drive). Mientras tanto se aísla y se restringe el acceso, y se documenta el riesgo residual: la papelera, las versiones y los cachés de Drive pueden conservar copias.

## Lo que este documento no autoriza

- No instala nada, no toca glmdch, no da GO y no autoriza borrar nada.
- No sustituye el respaldo, la restauración y el acta del día del GO.
- El acta de GO la firma el Sponsor con su clave OPS.

## Riesgo residual aceptado (alternativa A de G6, datos sintéticos)

No hay recuperación ante pérdida del host (el Sponsor no tiene segundo servidor). El contenido es sintético. Antes de datos reales (#694, Ley 21.719) se exige la alternativa B de G6 (age + laboratorio externo).
