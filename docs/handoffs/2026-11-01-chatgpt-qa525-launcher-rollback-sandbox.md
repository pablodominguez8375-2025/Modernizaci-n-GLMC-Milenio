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
