# QA525 — B7, inspección bloqueada y lease de diario (INACTIVO)

## Motivación

El motor vNext de `dev` sigue siendo sintético. Los cinco locks históricos
ya tienen wrapper/puerto POSIX revisados, pero la inspección de línea base
del PR #904 necesita ocurrir **dentro de la misma ventana exclusiva** que
el futuro movimiento API/WEB. No es seguro comprobar una pila Compose de
10 servicios y luego asumir que continúa intacta después de soltar locks.

Este módulo nuevo `qa525-vnext-b7-held-readonly.py::held_readonly` cierra
solo la **primera mitad de esa frontera**.

**Advertencia de efectos físicos:** Aunque este código no ejecuta cambios
API/WEB ni escribe directamente archivos, `root_flight` llama a
`journal.begin` y `lease.close` del journal inyectado. Si se inyecta el
journal físico real, esas llamadas **pueden escribir estado persistente en disco**
(incluso sin APPLY). Por ello NO es un «preflight de solo lectura» autorizable
en `glmdch` sin GO explícito de mantenimiento/ejecución, y NO debe ser
ejecutado por un diagnóstico READ_ONLY. Todas las pruebas de este PR usan
únicamente journales y puertos sintéticos, sin recursos de `glmdch`.

Contrato de la biblioteca:

1. **Exige inyección explícita** de puerto root, journal, revisión, pin SHA y
   proveedor de inspección. No construye defaults, no tiene CLI/root/apply.
2. Reutiliza `qa525-vnext-root-locks.root_flight` para adquirir los
   cinco locks exactamente en orden y una concesión de diario *después*.
3. Afirma cinco locks y lease **antes y después** de llamar una vez a
   `inspector()`; rechaza cambio de inode/FD/lease.
4. Exige el contrato exacto de resultado de `qa525-vnext-readonly-bridge`
   (#904), incluido SHA de baseline idéntico al pin, 7 running,
   3 oneshots exited/0, mínimo histórico de capas 6 API y 5 WEB y
   2–5 muestras. Una versión futura puede añadir capas, nunca perderlas
   ni inventar equivalencia con un simple conteo.
5. Rechaza que un resultado de inspección alegue
   `may_apply=true`, `may_install=true`, `g6_verified_here=true`,
   autenticación de host o aprobación Sponsor. Devuelve únicamente
   digests no firmados y `may_apply=false` para control de código.
6. Sale mediante el `finally` del wrapper de locks, liberando lease
   y locks en orden seguro. Si falla la limpieza, STOP fijo y **nunca**
   entrega resultado de éxito.

## Limitaciones obligatorias

No prueba identidades OPS/APP, firma Sigstore, custodia G6, autenticidad
del pin de capas o de quien suministra los puertos. El valor de `expected_
baseline_sha256` es una comprobación de coherencia; **un atacante que elige
el pin y la observación puede fabricar coherencia**. El bootstrap PRE-root
deberá autenticar esas fuentes, el historial del journal, la aprobación
separada del Sponsor y los hash de todos los Compose JSON antes de usar
cualquier efecto real.

`held_readonly` **no** es `external_gate` del vuelo. Su reporte
no habilita `run_apply`, ni siquiera cuando la inspección resulta estable.
El siguiente ensamblaje B7 deberá mantener el MISMO plano held durante
G6/OPS/APP, captura de baseline, scopes cgroup y ejecución protegida,
revalidando firmas y vigencias antes de cada intención de cambio, y
con rollback de API+WEB ante fallo. No deberá capturar y liberar locks,
para luego usarlos en una actualización diferente.

El código no se ha probado en `glmdch`; sus tests usan puertos en memoria.
Los 3 oneshots siguen pendientes de prueba física. Claude conserva
PR #902 OPS, #905 bounded DNS y #906 runbook/G6 opción A. Sin main,
producción, instalador físico ni activación automática.

## QA automatizada

`python3 -m unittest -v tests/test_qa525_vnext_b7_held_readonly.py`
o descubrimiento de `test_qa_agent_readiness.py` (dinámico) cubren:
locks ocupados, no root, lease rechazado, inode modificado durante
inspección, resultado falsificado, SHA distinto, cierre defectuoso,
y recuperación de todos los locks incluso tras rechazo. Las excepciones
no incorporan texto externo, rutas privadas ni credenciales.
