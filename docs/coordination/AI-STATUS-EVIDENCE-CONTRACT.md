# Contrato mínimo de estados y evidencia para agentes

> Contrato documental **no ejecutable**. Ayuda a registrar decisiones sin falsos positivos. No añade puertas al GO ni sustituye `PMGM-GOV-003`. La evidencia debe provenir del sistema indicado, con lectura actual, no de memoria ni adendas.

## Registro de estado: una fila por etapa, no por comentario

| Campo | Regla |
|---|---|
| `as_of_utc` | Fecha/hora de la **observación**, nunca asumir que sigue vigente. |
| `stage` | Uno de `EN_CODIGO`, `CI`, `RELEASE`, `QA_FISICO`, `UAT`, `MAIN`. |
| `result` | `PASS`, `FAIL`, `NO_GO`, `PENDIENTE`, `UNKNOWN`. |
| `subject` | Issue/PR/artefacto/servicio exacto. No mezclar un launcher PASS con G6 backup. |
| `source_link` | GitHub, Action, recibo o acta controlada verificada; si no existe, `UNKNOWN`. |
| `source_sha` | Commit real asociado a esa etapa, si aplica; **nunca** decir HEAD vivo salvo tras leer refs. |
| `authorization` | Tipo, alcance y referencia del GO específico; `NONE` si no existe. |
| `next_owner_action` | Un responsable, siguiente acción realizable y decisión requerida. |

### Reglas de interpretación

- **EN CÓDIGO:** rama + PR + archivos + HEAD del PR. PR draft o merged no certifica release.
- **CI:** nombres exactos de gates sobre el **HEAD exacto** y sus conclusiones. `in_progress`, `queued`, `skipped` ≠ `SUCCESS`; distinguir un skipped esperado y documentado de un gate obligatorio.
- **RELEASE:** SHA fuente, ID de Actions/release, digest SHA-256 del paquete y cadena de firma. Si no se recuperaron del proveedor: `UNKNOWN`. No usar ZIP parcial o Pages como instalable.
- **QA FÍSICO:** solo `glmdch`. Exigir lectura física específica, servicio/revisión realmente observados, fecha, redacción sin secretos y recibo. CI sintética y prueba local no cuentan.
- **UAT:** aceptación trazable de usuarios/casos; `UNKNOWN` hasta evidencia real.
- **MAIN:** lectura de HEAD de `main` y aprobación expresa; un merge a `dev` no equivale a `main`.
- **GO:** `NO_GO` es **por operación**, no global. Por ejemplo `LAUNCHER_ONLY` y `G6_BACKUP` son decisiones diferentes. El estado `UNKNOWN` nunca se transforma por inferencia en `PASS`.
- **No hay despliegue nuevo:** escribir exactamente **«despliegue QA pendiente»** para cualquier corte no instalado, aunque CI sea verde.

## Formato breve para cierres (máximo diez líneas)

```text
FECHA/HORA OBSERVADA (UTC): ...
DEV HEAD (lectura viva): ... | MAIN HEAD (lectura viva): ...
ISSUE/AGENTE/PR: ... | RAMA/HEAD PR: ...
ARCHIVOS (delta): ...
EN CÓDIGO: ... | CI exact-head: ...
RELEASE / PAQUETE SHA-256: ... | PAGES sourceSha: ...
QA FÍSICO glmdch: ... (evidencia o UNKNOWN)
UAT: ... | MAIN: ...
NO TOCAR / AUTORIZACIÓN FALTANTE: ...
SIGUIENTE ACCIÓN + RESPONSABLE: ...
```

Escribir enlaces a evidencia en el Issue/PR y una adenda Drive por lote. Detalles, logs, pruebas y antecedentes permanecen detrás de esos enlaces, **no repetirlos** en cada respuesta.

## Prevención de errores de coordinación

1. Consultar HEAD vivo `dev`/`main`; si no hay acceso, **STOP** con la fuente no verificada.
2. Issue reclamado con una sola etiqueta `agente:*` y PR draft del mismo agente antes de programación. Rama desde HEAD dev con patrón `feature|fix|docs/<tema>-AAAAMMDD-claude|gpt`.
3. Listar todos los PR abiertos y archivos antes del primer commit y antes del merge. Si hay archivo caliente compartido: **STOP y avisar**.
4. No editar ramas de otro agente; sugerir por comentario en el PR. El Sponsor puede reasignar.
5. No fusionar hasta gates CI/Showcase/QA Installable `SUCCESS` exact-head y dev sin drift; squash solo a `dev` cuando autorizado. No tocar `main` ni host por inferencia.
6. Handoff nuevo solo si otra sesión debe retomar; registro Drive fechado, sin borrar documentos oficiales ni reescribir `START-HERE.md`.
7. Para trabajo de normativa, abrir Constitución/Reglamentos y formularios; permisos de aplicación no crean atribuciones institucionales.

## Qué NO se debe hacer para ahorrar tokens

- No borrar journals, backups, adendas, historial Git, prepared o actas.
- No mantener copias completas de `START-HERE`, GOV, el Estado Maestro o una transcripción de 1.000 comentarios.
- No refrescar todos los SHA conocidos al final de cada párrafo: solo una lectura al inicio y al cierre, y ante un merge.
- No hacer sondeo constante de Actions; verificar una vez tras el evento útil y registrar resultados.
- No publicar credenciales, PIN, tokens, claves, líneas firmadas, huellas o IP.
