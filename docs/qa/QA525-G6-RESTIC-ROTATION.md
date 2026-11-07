# QA525 G6 — Rotación de la contraseña de Restic (PROPUESTA, sin ejecutar)

Estado: **borrador para revisión de ChatGPT. No ejecutado. Ningún comando de este
documento se corre en glmdch sin que el Sponsor lo autorice en ese momento y
ChatGPT haya dado 0 P0/P1 sobre este texto.**

Alcance: alternativa A de G6 (QA sintético únicamente). No cubre datos reales
(eso exige la alternativa B, #694, Ley 21.719).

## Por qué

La contraseña del repositorio Restic `/opt/centenario/qa-g6` quedó expuesta en un
chat. Debe considerarse comprometida. El valor no se repite ni se guarda en este
repositorio, en Drive ni en chats. La copia legítima vive solo en el Bitwarden
del Sponsor.

## Hecho técnico que decide el método

`restic key add/passwd` cambia la contraseña que abre la *clave maestra* del
repositorio, pero **no recifra los datos**. Quien conserve la contraseña vieja y
una copia anterior del repositorio (por ejemplo los cifrados subidos a la
carpeta Drive de respaldos) sigue pudiendo abrir esa copia. Quitar la clave vieja
solo protege el repositorio vivo.

Como el contenido es sintético y la carpeta Drive es privada, el riesgo es bajo,
pero la opción B cierra el hueco por completo.

| Opción | Qué hace | Cierra copias antiguas | Costo |
|---|---|---|---|
| A. Rotar clave | `key add` nueva, verificar, `key remove` vieja | No | Bajo |
| B. Repo nuevo (recomendada) | `restic init` nuevo con contraseña nueva, respaldo completo nuevo, retirar el repo y los cifrados viejos de Drive | Sí | Un respaldo completo, que de todos modos se necesita antes del GO |

Recomendación: **B**. Evita razonar sobre claves residuales y deja el acta de
restauración sobre un repositorio limpio.

## Reglas para ambas opciones

1. La contraseña nueva la genera el generador de Bitwarden (32+ caracteres) y se
   guarda en Bitwarden **antes** de usarla en glmdch.
2. Nunca se escribe en la línea de comandos ni se pega en el chat. Entra por
   entrada estándar a un archivo `0600 root:root`, con `read -s`, para que no
   quede en el historial del shell.
3. Un comando por vez desde Termius. El Sponsor pega la salida solo después de
   revisar que no contenga la contraseña.
4. Los scripts de `/opt/centenario/qa-tools` (hashes fijados 5bbf092e… y
   514a482e…) no se modifican. La contraseña no forma parte de ellos.
5. Antes de empezar: confirmar sin modificar nada que el archivo actual existe,
   su modo y su propietario (`stat`), sin leer su contenido.

## Esqueleto de pasos (a afinar por ChatGPT con las rutas y flags reales)

Opción B:

1. Solo lectura: `stat` del archivo de contraseña actual; espacio libre.
2. Crear `/etc/pmgm/qa-g6-restic-password.new` (modo 0600) leyendo de stdin.
3. `restic init` sobre un directorio nuevo (por ejemplo `/opt/centenario/qa-g6-v2`)
   con `--password-file` el `.new`.
4. Respaldo completo con el script fijado, apuntado al repo nuevo.
5. `restic check` y restauración aislada en el mismo host, con el acta existente
   (<48 h y que cubra toda la ventana).
6. Cuando el acta esté verde: reemplazar el archivo de contraseña vigente por el
   `.new` y apuntar la configuración al repo nuevo. Decisión de ChatGPT: renombrar
   directorios o cambiar la variable de ruta.
7. Retirar el repo viejo y su archivo de contraseña (`shred`, luego borrar). Pedir
   al Sponsor borrar de Drive los cifrados antiguos y la carpeta duplicada vacía.
8. Registrar en #525 el incidente cerrado, **sin ningún valor secreto**.

Opción A (si ChatGPT la prefiere): pasos 1–2 iguales, luego
`restic key add --new-password-file`, verificar con la clave nueva
(`key list`, `check`, `snapshots`), mover el archivo, `key remove` de la clave
vieja autenticando con la nueva, `shred` del archivo viejo.

## Lo que este documento no autoriza

- No instala nada, no toca glmdch, no da GO.
- No sustituye el respaldo + restauración + acta del día del GO.
- La acta de GO la firma el Sponsor con su clave OPS.

## Preguntas abiertas para ChatGPT

1. ¿A o B? (recomiendo B).
2. Flags exactos y rutas de los scripts fijados para el respaldo y la restauración.
3. ¿Cómo apunta hoy la configuración al repo y a la contraseña (variable, archivo)?
