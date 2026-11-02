# QA525 — reversión supervisada del lanzador: sandbox de recuperación
**Inicio:** 2026-11-01; Issue #822; referencia #525.
**Base:** dev@0d045f75e62d283987a0fa5e3d293918e113c811; main@6dfb9546a4873baff15955cf86abfd7d47e3d111.

## Evidencia y propósito
En glmdch se verificó y custodió la release firmada y el bootstrap de dev@0d045f75. No se ejecutó Python root ni reemplazó el lanzador. La inspección G6 halló siete reportes PASS de restauración aislada mismo-host (29–31 Oct) con DR fuera del servidor pendiente, IAM no incluido y snapshot multirrecurso no probado. El runbook de #822 §4 reconoce que NO hay recuperación de lanzador implementada y aprobada.

## Scope del incremento
Implementar un SIMULADOR de rollback reversible sobre rutas de prueba descartables, sin posibilidad de tocar glmdch ni /opt/centenario ni /usr/local/sbin; usar como entradas la evidencia persistida de sustitución, hash de backup, acta independiente con nonce nuevo y caducidad, bloqueo, journal durable, swap atómico y verificación. Simular SIGKILL y drift. Agregar tests herméticos; no introducir CLI operativo root ni atajos para ignorar intenciones incompletas.

**No se ha autorizado el corte físico**; no se cambia main, Docker, Compose, EF, SMTP, Keycloak, API/WEB ni G6. La posterior incorporación de ejecutor privilegiado y su trust anchor deberá revisarse en PR separada con CI y acto físico independiente.

## Implementación en rama
- `scripts/qa/operators/qa525-launcher-recovery-sandbox.py`: simulación protegida dentro de un directorio descartable de /tmp; valida receipt de corte y backup antiguos, hash vivo, rechaza intent pendiente, exige acta separada con alcance launcher-only, nonce nuevo y TTL de hasta 900 s, no reutiliza aprobación de instalación. Exclusión flock, rollback-intent fsync, copia inmutable de launcher nuevo, staging del viejo, os.replace atómico, recibo durable y limpieza del intent solo tras éxito.
- `tests/test_qa525_launcher_recovery_sandbox.py`: éxito, nonce reutilizado/aprobación incorrecta, backup/receipt manipulados, intent heredado, symlink, fallo antes/después de swap, SIGKILL real contra proceso temporal.
- `tests/test_qa525_launcher_supervised_root_install.py`: importa clase adversarial para que el gate QA525 exact-head de CI la ejecute.

**STOP operacional:** sandbox no recibe rutas glmdch, ni utiliza root; evidencia modelada NO es la reversión privilegiada real. El operador real necesitará implementación separada, revisión de cinco locks, firmar nuevo SHA, ensayos y aprobación formal. Conservar QA físico antiguo hasta entonces.

## Endurecimiento de identidad de aprobación
La aprobación de rollback se congela tras su primera validación y se vuelve a contrastar al límite exacto previo al intercambio atómico. Si su nonce, alcance u otros campos cambian durante el proceso, se bloquea y conserva journal sin mutar el lanzador. Prueba adicional de mutación entre respaldo durable y swap; la implementación sigue siendo SOLO un sandbox y NO un mecanismo de recuperación de glmdch.

## Política pura para futuro ejecutor root
Se agregó `assess_reversal()` a `scripts/qa/operators/qa525-launcher-replacement-contract.py`: exige receipt original committed de campos exactos, backup viejo con SHA correspondiente, hash vivo del launcher instalado, cero intent pendiente, acta distinta con scope launcher-only, nonce no usado y plazo <=900 s. Devuelve siempre `may_recover=false`/`may_apply=false`; no toca host ni autentica el acta. Se añadieron tres pruebas en la suite QA525 del instalador: positivo estrictamente de revisión, autorizaciones falsificadas/replay/vencidas y drift/journal pendiente. La futura implementación root deberá comprobar firmas, custodia, cinco locks y transacciones, sin inferir permiso de este contrato. La fuente de esta política es parte de los módulos firmados de QA525, por lo que toda actualización futura requerirá nuevo paquete Sigstore del SHA de dev que la integre; NO reutilizar el corte actual de glmdch.

## Ejecutor root propuesto — NO DESPLEGADO
Además del sandbox y la política pura, PR #838 incorpora `--rollback-approved`
a `qa525-launcher-supervised-root-install.py`. El nuevo comando ejecutará
solo en `glmdch` con root + intérprete aislado, previo código root autenticado
por admisión de 900 s, cinco locks, validación de todos los recibos originales
y acta root0600 de rollback **distinta** de la aprobación de instalación.
Exige que NO exista intent original ni journal de reversión residual; solo
reversión de corte previamente comprometido. `RECOVERY_STATE` root0700
preprovisionado, backup original y hash vivo verificados, intent y copia actual
fsync, stage antiguo y swap atómico, recibo posterior. En cualquier interrupción
queda journal protegido y STOP: no intentar otra vez sin reconciliación.
Nuevo test con ficheros temporales para corte exitoso, consentimiento inválido,
intent original pendiente, interrupción antes y después del swap. El rollback
original a medias (sin receipt) sigue sin ejecutor de recuperación, y el
procedimiento de recuperación de BD/IAM no se declara resuelto.
No se ha proporcionado NINGÚN comando privilegiado físico. Las rutas y nueva
opción no existen en el launcher custodiado de SHA 0d045f75. Pendientes:
CI exact-head, revisión independiente de código root, release dev nueva, G6
apropiado y aprobación formal independiente antes de la operación en glmdch.

## Hallazgo de revisión cruzada de versiones — 01-11-2026, STOP para recovery entre cortes
Revisión estática del ejecutor root propuesto identifica una dependencia de procedencia: `rollback_approved()` exige `receipt["source_sha"] == packet["source_sha"]` y `receipt["release_asset_sha256"] == packet["release_asset_sha256"]` antes de escribir el journal de reversión. El paquete de bootstrap firmado recuperador se identifica mediante su propio SHA y digest. Si se instala un lanzador de corte A y el código de recuperación existe solo en una release posterior B, esta igualdad bloquea la reversión A→B, aun con copia anterior legítima y aprobación de rollback. El ZIP root0600 dev@0d045f75 actualmente retenido en glmdch carece de este nuevo modo, por lo que NO es respaldo ejecutable de esta función.

Se añadió la prueba hermética `test_cross_release_recovery_fails_closed_pending_dual_provenance` al gate QA525: prepara receipt committed y backup íntegro del corte A, cambia el origen/digest del bootstrap recuperador a una identidad diferente y verifica rechazo `ROOT_REVERSAL_BOOTSTRAP_NOT_ORIGINAL_CUT` sin escritura ni swap. **La prueba documenta una restricción existente, no soluciona el soporte de reversión entre cortes.**

Diseño pendiente para revisión independiente: si la recuperación desde B de un corte A es requisito, exigir doble vínculo firmado y no ambiguo (ejecutor de recuperación B y corte original A), receipt/backup original con SHA íntegro, firma/manifest correspondientes, autoridad y consentimiento separados que identifiquen ambos cortes, locks, controles anti-replay e interrupción, y pruebas positivas/negativas de recuperación aislada. No eliminar la comparación de origen ni relajarlo a simple JSON no autenticado sin un contrato alternativo revisado. Hasta resolver el diseño y demostrar un camino de recuperación probado, **no fusionar precipitadamente la PR ni sustituir el lanzador físico**. Mantener separadas las etapas de API+WEB; DR externo, IAM y consistencia multirrecurso continúan sin acreditarse.

## Bloqueo adicional: recuperación diferida y admisión de 900 segundos
La implementación `qa525-signed-root-bootstrap-prep.py` de dev rechaza cualquier admisión ya existente (`BOOTSTRAP_ADMISSION_ALREADY_EXISTS`) o target `STORE / source_sha` preexistente (`BOOTSTRAP_TARGET_ALREADY_EXISTS`). El ejecutor `rollback_approved()` llama `bootstrap_trusted_sources()`, que exige admisión vigente y `expires_at-issued_at <= 900`. Por consiguiente, **incluso un corte A cuya release ya contiene este rollback puede quedarse sin mecanismo de reversión autorizable después de 15 minutos**, porque la admisión de A caduca y el bootstrap one-shot no puede reemitirla. Un respaldo/receipt histórico correcto NO renueva la autenticidad del ejecutor.

Requisito de ingeniería para resolver antes de GO: vía de **reautenticación privilegiada de recuperación separada y auditada**, capaz de revalidar fuentes y firmas exactas, emitir una admisión de recuperación diferenciada y temporal sin borrar ni modificar admisiones/journals originales y sin saltar controles anti-replay; autorización con nonce distinto, ámbito y SHA firmados, locks y validación de hashes/DR. Pruebas herméticas para recuperación tardía válida, admisión expirada, reuso, duplicados, dos versiones distintas, corte previo interrupted, SIGKILL, rollback pendiente y caducidad antes del swap. **No** sugerir eliminación manual de `/etc/pmgm/qa525-bootstrap-admission.json` ni directorio de fuentes para permitir reintento. No hay soporte de recuperación diferida físico aprobado en la PR actual. Se requiere revisión independiente y nuevo paquete firmado.

## Contrato puro para recuperación diferida con doble procedencia — nuevo avance, 01-11-2026

Implementados `assess_delayed_recovery_plan()`, constantes y conjuntos de campos estrictos en `scripts/qa/operators/qa525-launcher-replacement-contract.py` (commit 92dcedad), con cinco pruebas adversariales en `tests/test_qa525_launcher_supervised_root_install.py` (commit 440f5db1). **No se integró al ejecutor root, no constituye un nuevo bootstrap ni emite credenciales, no lee host, no instala ni revierte.** La rama sigue DRAFT.

El contrato distingue **A**, recibo original de sustitución `LAUNCHER_ATOMIC_SWAP_COMMITTED` con SHA, digest de release, nonce y hashes previo/posterior, de **B**, paquete/código propuesto para recuperación, con SHA, digest de release, manifiesto y hash del ejecutor. El plan revisa que el acta separada haga referencia exacta a A y B; exige copia anterior y launcher vivo que correspondan a los hashes históricos, sin intent pendiente. El campo `cut_receipt_sha256` es el hash SHA-256 de una **representación JSON canónica** calculada con `frozen_sha()`, no el hash de los bytes del archivo original; una implementación root futura DEBE verificar por separado el recibo real y conservar evidencia de sus bytes/autoridad. El plan comprueba que un supuesto paquete de recuperación y su aprobación estén vigentes **ambos** por un máximo de 900 segundos. La admisión histórica A ya vencida no sirve como credencial para ejecutar B.

**Límite de confianza crucial:** `recovery_claim`, el acta y el recibo son objetos aportados por el llamador; el contrato NO verifica autenticidad Sigstore, procedencia del paquete, integridad de archivos del host, identidad del operador, firmas ni locks. Su campo `claims_authenticated=false` y todas las capacidades (`may_install`, `may_apply`, `may_recover`) quedan siempre en falso. Las pruebas confirman un plan de revisión A/B, rechazos por adulteración, scope, vencimiento, booleanos, nonces repetidos y estado pendiente. Una aprobación JSON inventada que pase esta función **NO autoriza operación física**.

**Bloqueos que el nuevo contrato NO resuelve:** (1) no existe aún root verifier/boundary de recuperación diferida capaz de emitir una admisión B nueva sin borrar o reusar la vieja; (2) `rollback_approved()` sigue requiriendo A==B y el bootstrap original one-shot no se modifica; (3) no hay reconciliación automatizada para sustitución original interrumpida; (4) G6 offhost/IAM/consistencia multirrecurso no acreditado. Es requisito futuro: revisión externa del diseño, prueba de seguridad/rollback en escenario aislado, manifiesto y firma exact SHA integrados, y aprobación distinta launcher-only antes de tocar glmdch. CI debe evaluarse **por HEAD exacto**; 7/8 del commit previo 432fb6be (PMGM CI falló al hacer Docker pull en test Silo), no corresponde al nuevo HEAD.

La continuidad y auditoría deben reflejar únicamente este avance de ingeniería y conservar el estado físico QA anterior.

## Modelo A/B de recuperación tardía con transacción duradera — incremento hermético 01-11-2026
En rama PR #838 se añadió `simulate_delayed_dual_recovery()` a
`scripts/qa/operators/qa525-launcher-recovery-sandbox.py` (commit `45cab925`)
y cinco casos a `tests/test_qa525_launcher_recovery_sandbox.py`
(commit `8e2251e6`). La función **NO verifica firmas ni admite una ruta física**:
aplica el contrato `assess_delayed_recovery_plan()` sobre los datos NO
autenticados de A (receipt original) y B (claim/acta reciente), junto con las
comprobaciones del sandbox dentro del mismo lock de prueba; reutiliza su
journal/fsync/backup/swap sobre archivos descartables bajo /tmp, con revalidación
de A, B y consentimiento antes del swap. El resultado
`SIMULATED_DELAYED_DUAL_PROVENANCE_NO_HOST_CHANGE` lleva
`approved_for_host_recovery=false` y
`recovery_claim_authenticated=false`; no crea admisión privilegiada.

Pruebas: recuperación simulada cuando el recibo de A es histórico y no se
depende de la antigua admisión de A; rechazo de origen/digest/manifiesto/ejecutor
B adulterados y acta A/B disconforme, caducada o sin scope; modificación de B
tras backup durable bloqueada sin swap y journal retenido; error después de swap
retiene journal y prohíbe replay; prohibición explícita de /opt/centenario.
El simulador anterior y la suite de root offline comparten estos tests por
importación, para evitar falsos positivos de una prueba huérfana.

**No interpretar la simulación como recuperación probada de glmdch.** No
comprueba Sigstore, identidad del operador real, ancla de primer root, 5 locks
del host, journals de Docker/DB, protección de fuentes root ni funcionalidad
API/WEB. La implementación privilegiada `rollback_approved()` no consume
este modelo A/B ni permite nueva readmisión, sigue siendo NO-GO. Antes del
cut físico se requiere verificador de firmas exactas A/B, mecanismo seguro
de emisión de admisión B sin borrar A, ensayo de recovery/suspensión, review
independiente, CI exact HEAD y aprobación separada launcher-only. No se
modificó la release root0600 antigua ni glmdch. No ejecutar scripts srv01.

## Auditoría dual Sigstore sin privilegios — continuidad 01-11-2026 (NUEVO, SIN DESPLIEGUE)

Se agregó `scripts/qa/operators/qa525-delayed-recovery-signed-evidence-audit.py`
para vincular, bajo UID **no root**, dos artefactos locales distintos:
A, ZIP firmado del corte original (SHA+digest debe coincidir con su receipt
committed), y B, ZIP firmado del candidato de recuperación (SHA+digest, digest
de manifiesto y SHA del ejecutor root extraído de su MANIFEST). Reutiliza el
auditor previo `qa525-attested-installer-audit.py`, el cual verifica SHA del
sobre, GitHub Sigstore con workflow y repo exactos, source-digest, prohibición
de runners self-hosted y todas las entradas del manifiesto firmado. El contrato
`assess_delayed_recovery_plan()` comprueba en **solo lectura** que una
aprobación de revisión nueva vincule ambos cortes, hashes y nonce, sin
permitir root, API/WEB, autorización real, cambios de estado ni admisión.
Antes de importar el módulo Python de contrato local se exige que B haya
verificado el hash del mismo código en su release firmado y que no haya
cambiado en disco; impide cargar una versión local de contrato no coincidente
con el contenido firmado (no reemplaza un ancla de primer arranque).

Comits: `7debd019` auditor de evidencias; `4c04feb7` obliga a que las
inyecciones de pruebas devuelvan `SYNTHETIC_...` y
`release_envelopes_sigstore_checked_unprivileged=false`, para que jamás
se interpreten stubs como una verificación criptográfica real;
`633e4f0a` tests adversariales de informes falseados A/B, hashes,
aprobación y prohibición de root; `d2ffe51b` los integra obligatoriamente
al gate QA525 offline; `0cf081a1` mueve el import Python local de contrato
después de la verificación del manifiesto B; `d47f6874` añade test de ese
límite de confianza. En ejecución real sin inyecciones, el informe sólo
puede expresar `release_envelopes_sigstore_checked_unprivileged=true`
cuando ambas llamadas al auditor previo terminan satisfactoriamente.
En test sintético permanece false. El objeto receipt y aprobación siguen
siendo datos del llamador no autenticados; `cut_receipt_authenticated_by_audit`
y `approval_authenticated_by_audit` permanecen siempre false.

**PROHIBIDO CONFUNDIR**: verificación de dos releases bajo usuario sin root
no crea una credencial durable ni verifica la custodia física de journal y
backup en glmdch. Todavía NO existe una admisión fresca root autorizada
para un ejecutor B, ni ha cambiado el `rollback_approved()` físico
(continúa A==B y requiere admisión antigua one-shot). No existe recuperación
de un corte original incompleto sin receipt. El verificador tampoco
demuestra restauración de IAM o DR fuera del host; la máquina QA única
sigue siendo glmdch. PR #838 sigue borrador; revisión independiente,
CI exact-HEAD completo, G6 y autorización launcher-only continúan como
gates NO-GO para cualquier operación física.

Trazabilidad: Issues #822/#525, este handoff y Drive Proyecto Centenario.


## Reconciliación de recibo separado sin borrar intent — continuidad 01-11-2026

Este incremento en PR #838 incorpora dos revisiones A/B **sin privilegios**:
\`assess_pending_reconciliation_plan()\` clasifica un corte original A con
\`SWAP_INTENT_NO_DEPLOYMENT\` y sin \`LAUNCHER_ATOMIC_SWAP_COMMITTED\`.
\`review_pending_two_signed_envelopes()\` liga A a un ZIP firmado de origen A,
y la revisión B a un segundo ZIP Sigstore/manifest verificado, sin fingir
que un intent sea un recibo committed. El claim B tiene source SHA/digest
diferentes de A; nonce, hash de intent, approval y vigencia <=900s se comparan
antes de emitir exclusivamente un plan de revisión sin efectos.

\`assess_pending_reconciliation_receipt()\` especifica el futuro formato de
**recibo B separado, append-only**, con identidades de A, hash canónico del
intent original preservado, firma/manifest/ejecutor B por verificar externamente,
nonce distinto, aprobación B vinculada, fase previa/posterior al swap y
SHA antiguo restaurado verificado. Rechaza falso receipt A, historial B pendiente,
drift de backup/live y datos ajenos. La autenticidad de tales campos debe ser
establecida por un ejecutor root independiente *antes* de usar el recibo: el
contrato solo compara datos suministrados y devuelve siempre
\`may_apply=false\`, \`may_install=false\`, \`may_recover=false\`,
\`reconciliation_complete=false\` y
\`root_history_reconciliation_implemented=false\`.

**NO afirmamos recuperación completada:** el código actual
\`scan_history()\` del instalador root todavía bloquea un intent A, incluso
si hay un recibo B no autenticado. No implementar un bypass que acepte el
recibo puramente por su apariencia JSON. Falta un ejecutor B firmado con
cadena de confianza root independiente, cinco locks, verificación duradera
de fuentes/actas y reconciliación verificable de historia original
conservada, ensayos SIGKILL y revisión independiente. La release firmada A
custodiada en glmdch no incluye B ni esta política de PR draft. No cambiar
\`dev\`/main, QA glmdch, EF, Docker o IAM con esta entrega.

Commits de esta etapa: \`f9638732\` contrato del recibo, \`0b714ee9\`
pruebas adversariales. CI exigible del HEAD FINAL de PR, no de los commits
previos. Tras el review independiente y la integración de código revisado
deberá generarse nueva release firmada B; en ventana autorizada, primero
launcher-only, luego API+WEB por acto distinto, G6 vigente y smoke/UAT.
