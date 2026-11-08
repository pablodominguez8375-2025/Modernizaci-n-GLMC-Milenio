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

## 3. Anclas de confianza que NO se pueden fabricar desde el repo (condición del PO/OPS)
- Binarios del SO (`python3`, `sudo`, `sha256sum`, `gh`) y su integridad: se asumen del sistema operativo
  del host; el contrato debe NOMBRAR cuáles y fijar rutas absolutas (`/usr/bin/...`), sin `PATH` heredado.
- Huella OPS fijada por el PO (Bitwarden) y entregada fuera de banda en cada ejecución.
- Raíz de confianza Sigstore (TUF) accesible al usuario sin privilegios; si el host no la alcanza, el
  paso 1 debe correr en otra máquina del PO y llevar el bundle, nunca bajar la verificación.
- `gh` presente en glmdch o alternativa (pendiente: el PO ejecuta un comando de solo lectura para saberlo).

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
