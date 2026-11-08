# QA525 vNext — contrato de la frontera PRE-ROOT (#936 / #822)

Estado: **contrato de diseño, sin código nuevo**. Inactivo. NO_GO_FISICO. Escrito por Claude, 08-11-2026,
a pedido de ChatGPT (#525 comentarios 6726386978/6726390963). Nada aquí autoriza sudo, instalar ni GO.

## 1. Problema (P1-1 de #958, abierto)
Hoy un punto de entrada root (p. ej. `qa525-vnext-g6-v2-run.py`) hace `exec_module` de módulos hermanos
y confía en el árbol que tiene delante. Un hash calculado por ese mismo código bajo root no prueba nada.
Falta una cadena que establezca **qué bytes se van a ejecutar con privilegios ANTES del primer byte
privilegiado**, y que gaste el GO una sola vez bajo los 5 locks.

## 2. Cadena propuesta (un único camino; no hay receptores alternativos)
| # | Actor / privilegio | Qué hace | Qué verifica (ancla) | Módulo en dev |
|---|---|---|---|---|
| 0 | PO en Termux (móvil, sin privilegios) | Genera/guarda clave OPS ed25519 (hecho: huella `SHA256:DN64Bjj/7SMjP7LX9Nwrcp3lPRBpT2cKPs1NmaL1Y5k`) | La huella la fija el PO desde Bitwarden, nunca desde el host | #947 hoja de firma |
| 1 | Usuario `soporte` SIN sudo | Descarga release por SHA exacto; `gh attestation verify` (identidad Sigstore del workflow + `source-digest`) | Identidad del workflow y commit; sujeto DSSE = `app_archive_sha256` | `qa525-vnext-cache.py`, #932 |
| 2 | `soporte` SIN sudo | Verifica recibo OPS v3 (ed25519, namespace `qa525-ops-v2@pmgm`, huella fijada por el PO) y que `app_archive_sha256` coincide | Firma del Sponsor sobre el digest atestado | #902 |
| 3 | `soporte` SIN sudo | Crea memfd sellado con los bytes ya verificados; calcula pin SHA | Bytes inmutables (F_SEAL_*) | #949 puerto app |
| 4 | Frontera | Entrega el FD al proceso root: un emisor, un receptor | uid/pid del emisor por mensaje (`SCM_CREDENTIALS`), pin SHA independiente, timeout, FD privado RDONLY | #966 (receptor), #963 (ferry) |
| 5 | root, bajo 5 locks + lease vivo | Verifica acta GO (ed25519, namespace `qa525-go-cut-v1@pmgm`) y **consume el nonce** (`.spent` durable) ANTES del primer hijo | Firma GO + nonce de un solo uso + binding APP/OPS/policy/plan/admission | #967 + root-locks |
| 6 | root | Ejecuta SOLO desde los bytes del FD (no desde el árbol del disco) | — | pendiente (carga desde FD) |

Regla de oro: **ningún `exec_module`/import como root de código que no venga del FD verificado**. Lo que
queda en disco como "hermano" no es ancla.

## 2b. PRE-4 — primer ejecutable privilegiado (P1 de ChatGPT sobre #973, ef1d5de1)

Los pasos 4–6 ejecutan bytes root (crear/aceptar el socket, `SCM_CREDENTIALS`, pines, GO, 5 locks, journal)
ANTES de que el paso 6 cargue nada desde el FD. «No usar `exec_module` del disco» es necesario pero NO
suficiente: el receptor root, el intérprete y sus bibliotecas son TCB y deben autenticarse antes.

**PRE-4 (anterior a sudo y a todo byte root):**
1. Existe un *bootstrap root* mínimo, instalado una sola vez por el PO, propiedad de root, en un directorio
   root-owned no escribible por `soporte` (ni por grupo/otros) y sin symlinks en la ruta. Contiene el receptor,
   el verificador de GO y el consumidor del nonce. Es la ÚNICA fuente de código root; el árbol de trabajo, el
   ZIP/APP y los hermanos no se importan nunca como root en las fases 4, 5 ni 6.
2. Su autenticidad NO se prueba con un auto-hash ni con `--trusted-bootstrap-sha256` tomado del mismo paquete.
   La referencia es un pin (o manifiesto firmado con clave OPS del PO) entregado FUERA de GitHub/Drive: huella
   leída por el PO desde Bitwarden y comparada a mano contra la salida de `sha256sum` de cada archivo del
   bootstrap, del intérprete exacto (`/usr/bin/python3.14`, resuelto a ruta real) y de cada helper ejecutable.
   Sin `PATH`, sin `PYTHONPATH`, sin `site`/`usercustomize` de usuario (`python3 -I -S`), entorno limpio.
3. El proceso emisor (`soporte`) se liga al receptor con uid + pid por mensaje (`SCM_CREDENTIALS`); además el
   receptor lo lanza o lo conoce el propio bootstrap (no se confía solo en un PID declarado). El FD se entrega
   por el socket del bootstrap, se reabre `O_RDONLY` por `/proc/self/fd`, se recalcula SHA independiente y se
   compara contra el pin del paso 3 antes de usarlo; no se heredan FDs ajenos (`close_range`/`CLOEXEC`).
4. Evidencia: el bootstrap registra, ANTES de consumir el nonce, un registro `pre-root-trust` con los SHA del
   bootstrap, del intérprete y de los helpers verificados. Sin ese registro no hay nonce ni GO.

**Negativos adicionales exigidos (STOP antes de ejecutar un solo byte Python/root, aun con ZIP/APP/OPS/GO válidos):**
- cambiar solo el código del receptor root entre verificación y ejecución;
- cambiar el intérprete, un helper o una biblioteca del bootstrap;
- un bootstrap en directorio escribible por `soporte` o con symlink en la ruta;
- pin tomado del mismo paquete;
- el mismo criterio sobre `qa525-vnext-g6-v2-run.py` y su `exec_module` (que hoy NO lo cumple: #958 no es
  apto para sudo mientras no corra desde este bootstrap).

Estado: contrato inactivo; no se instala ni ejecuta nada root. Mientras PRE-4 no exista, no hay GO.

## 3. Anclas de confianza que NO se pueden fabricar desde el repo (condición del PO/OPS)
- Binarios del SO (`python3`, `sudo`, `sha256sum`, `gh`) y su integridad: se asumen del sistema operativo
  del host; el contrato debe NOMBRAR cuáles y fijar rutas absolutas (`/usr/bin/...`), sin `PATH` heredado.
- Huella OPS fijada por el PO (Bitwarden) y entregada fuera de banda en cada ejecución.
- Raíz de confianza Sigstore (TUF) accesible al usuario sin privilegios; si el host no la alcanza, el
  paso 1 debe correr en otra máquina del PO y llevar el bundle, nunca bajar la verificación.
- Observado en glmdch (solo lectura, 08-11): `/usr/bin/gh` 2.102.0, Python 3.14.4 con memfd/sellos/SCM_*, `sudo-rs` 0.2.13; `soporte` alcanza GitHub y las raíces TUF de Sigstore/GitHub (HTTP 200). Son hechos de entorno, no anclas: su integridad se fija en PRE-4.

## 4. Pruebas negativas exigidas antes de cualquier consumidor real (por etapa)
Par ajeno / endpoint heredado, pin falso, ZIP sustituido, digest o nonce de la trama usado como autoridad,
GO sin firma, GO con nonce repetido (mismo proceso y entre procesos), recibo OPS con huella distinta,
namespace OPS usado como GO, bytes sin sellar, mensaje sin credenciales, receptor sin timeout,
caída tras crear `.spent` (el nonce queda gastado), módulo hermano alterado en disco (debe ser ignorado),
fallo de lease/locks (no se consume el nonce).
Ya cubiertas en dev (sintéticas): #966, #967, #972 (cadena emisor→receptor→pin→nonce).

## 5. Lo que este contrato NO resuelve
Identidad de ejecutable del emisor más allá de uid/pid; confianza del propio `python3` y del kernel;
`/proc` confiable; durabilidad ante pérdida de energía; clave OPS comprometida; un PO que firme sin leer
la tarjeta (de ahí el código corto de #947). Un `consumed=true` nunca es GO.

## 6. Orden de implementación sugerido (cada paso, un PR inactivo con prueba de proceso real)
1. Paso 1–3 como programa de usuario sin privilegios (`prepare`): verificación Sigstore (herramienta
   por confirmar), recibo OPS, memfd sellado + pin. Prueba con bundles sintéticos y negativos.
2. Paso 5: consumidor que verifica GO + consume bajo `root_flight` (ChatGPT lleva el ensayo locks+nonce).
3. Paso 6: carga de código privilegiado desde el FD (ruta de import desde memoria/memfd), reemplazando el
   `exec_module` del árbol; cierra P1-1 de #958.
4. Ensayo físico solo-lanzador con artefacto firmado real y rollback; GO separado del Sponsor.
