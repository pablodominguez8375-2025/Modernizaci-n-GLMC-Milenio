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

Contrato, no implementación; nada de esto se ejecuta ni se instala sin PR, CI hermética, revisión de ChatGPT y de Claude y GO específico del Sponsor.

### Procedencia de la fuente (respuesta al P1-1 de ChatGPT)
`g6-local.py` y `g6-restore.py` son artefactos root del host. **No se trasladan al repositorio ni al chat.** No hay forma segura de «clonarlos con otras constantes» sin antes extraer código root, auditarlo en busca de secretos, rutas e identidades y probar la cadena de custodia (el SHA-256 del archivo local no equivale al SHA de blob de Git). Por eso el par v2 **no es un clon**: es una **implementación nueva**, escrita desde un contrato mínimo aprobado, con CI hermética, que se rechaza si se intenta construir a partir de una fuente no verificada. La fuente v1 queda en el host, intacta y pineada.

### Dos cosas distintas (respuesta al P1-2)
El borrador anterior exigía a la vez «un `diff` que pruebe que solo cambian constantes» y cambiar comportamiento (sobrescritura, entorno, validador). Eso es contradictorio y se retira: **ya no se afirma «solo constantes»**. Se separan:
- **(a) Equivalencia funcional con v1**: no se prueba por `diff` de fuente, sino por contrato observable: mismo formato de `archive.receipt.json`, mismos estados (`ENCRYPTED_LOCAL_ARCHIVE_READY_OFFHOST_PENDING`, `G6_SAME_HOST_ISOLATED_RESTORE_PASS_NOT_DISASTER_RECOVERY`), mismas bases verificadas (pmgm y pmgm_keycloak) y mismo cálculo de edad (<48 h), con pruebas que consumen ese formato.
- **(b) Protecciones de seguridad nuevas, explícitas y con prueba propia** (ver abajo).

### Contrato mínimo del par v2
1. **Ubicaciones propias:** repositorio nuevo en una ruta que no exista previamente; archivo de clave nuevo y distinto del actual (`0600 root:root`); pin propio. No se usa ni se modifica el repositorio antiguo.
2. **Entorno:** el script construye su entorno desde cero y no hereda `RESTIC_*`; si el entorno heredado trae esas variables, se **rechaza** (no se ignoran en silencio).
3. **No sobrescritura:** si el repositorio o la clave nuevos ya existen, STOP. Nunca se crea sobre algo existente ni se destruye el repositorio antiguo.
4. **Archivo realmente exportado = archivo restaurado:** el SHA-256 del archivo cifrado que se copia fuera del host es el mismo que `restore` abre, con la clave nueva; sin ese vínculo no hay acta.
5. **Sin falso PASS ni acta v1 para un GO v2:** el acta v2 lleva una marca de versión y el identificador del repositorio; el validador del GO v2 rechaza un acta v1 y viceversa.
6. **Logs saneados:** nunca imprimen clave, contraseña ni contenido de la base; hay prueba de ello.
7. **Prueba de error:** un caso de fallo deliberado (clave incorrecta o archivo alterado) debe producir STOP y no un acta.

### Hallazgo que afecta el diseño: ruta fija del acta
Tanto el operador legacy (`ARCHIVE_RECEIPT = /opt/centenario/qa-g6/archive.receipt.json`) como `qa525-vnext-cache.py` (`G6_RECEIPT_PATH`) leen el recibo **desde una ruta fija** dentro de `/opt/centenario/qa-g6`. Un repositorio v2 en otra carpeta raíz **no sería visto por ningún validador existente**. Opciones, a decidir por ChatGPT (no puedo ver las constantes ocultas):
- **(i)** Dejar el recibo en la misma ruta fija y poner solo el repositorio Restic nuevo bajo un subdirectorio nuevo de esa raíz, conservando una copia fechada del recibo anterior antes de reemplazarlo (el recibo viejo está vencido desde el 04-11).
- **(ii)** Parametrizar la ruta del recibo en los validadores con un PR aparte (cambio en código ya integrado y en el operador legacy, que se mantiene).
La opción (i) evita tocar los validadores, pero hay que probar que el reemplazo del recibo es atómico y que no destruye evidencia.

### Alcance de la CI
Pruebas herméticas con un Restic simulado o un directorio temporal; **ni la CI ni una aprobación implícita instalan binarios en el host**.

## Secuencia propuesta para el SOP

1. **Preflight solo lectura** (cada comando revisado antes): propietario, modo y hash de los dos scripts; espacio libre; definiciones de `ROOT`, `KEY` y `ARCHIVE` sin mostrar valores sensibles. Hecho parcialmente: ubicación de las asignaciones de entorno.
2. PR con la implementación v2 desde el contrato (sin copiar la fuente del host), CI hermética, revisión de ChatGPT y de Claude y pin nuevo.
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
