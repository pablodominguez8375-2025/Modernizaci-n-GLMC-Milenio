# QA525 vNext — invariantes de revisión (autorrevisión antes de abrir PR)

Lista corta para toda pieza vNext **inactiva**. Cada ítem salió de un hallazgo real de revisión (#849–#865). No autoriza nada físico; el permiso de desarrollo no es un GO de `glmdch`.

1. **Gate en TODA mutación.** Intent, switch y rollback pasan por `gate(stage)`; si lo deniega, no se ejecuta nada y se persiste `RECOVERY_REQUIRED` (P1 de #865).
2. **Persistir antes de actuar.** El intent queda durable (NOREPLACE + fsync de archivo y directorio) antes de invocar un adaptador; un intent persistido significa «posiblemente ejecutado».
3. **Nunca inferir desde memoria si algo quedó durable.** Tras una escritura fallida se relee la cadena bajo el lease; si el tope no es exactamente el plan previo → ambiguo → parar sin ejecutar (P1 de #865).
4. **Ambigüedad = parar y conservar evidencia.** Nada de reparar, reintentar la acción ni borrar restos de un crash.
5. **Verificación solo con evidencia del verificador** (`require_ok` con baseline/target propios); nunca con un digest suministrado por el llamador (#851/#861/#864). Un checksum público no es una firma.
6. **Un solo dueño del journal/lease.** Los módulos que reciben un lease usan la misma instancia de `journal` (#863/#865).
7. **Todo efecto inyectado.** Los módulos no importan subprocess, socket, os ni urllib, ni abren archivos propios; el runner, el lector de metadata y la salud los pasa el motor futuro. Cada módulo tiene un test que lo comprueba.
8. **Errores con códigos fijos.** Sin eco de texto externo, rutas ni excepciones; cada código nuevo con test.
9. **Tipos estrictos.** `type(x) is int/str`, nunca `isinstance` (bool/subclases); JSON canónico, sin duplicados ni NaN; rutas sin `.`, `..`, vacíos ni NUL.
10. **Salidas no autoritativas.** Todo resultado lleva `may_apply`, `may_install`, `signature_verified_here` y `physical_runtime_verified` en `False`.
11. **Pruebas de mutación.** Antes de abrir el PR, quitar a propósito cada guarda nueva y confirmar que algún test falla.
12. **Higiene de PR.** Rebasar sobre dev vigente, comprobar `base.sha` y `changed_files`, borrar `__pycache__`, no tocar ramas de otros agentes, DRAFT hasta revisión exact-head.

**Fuera de alcance de estas piezas** (siguen NO-GO hasta su propio gate): bootstrap OPS/APP/Sponsor/G6 verificado, los 5 locks legacy + root, adaptadores reales, `glmdch`, `main`, base de datos e identidad.
