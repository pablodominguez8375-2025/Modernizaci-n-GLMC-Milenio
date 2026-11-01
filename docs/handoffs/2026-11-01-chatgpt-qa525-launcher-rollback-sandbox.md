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
