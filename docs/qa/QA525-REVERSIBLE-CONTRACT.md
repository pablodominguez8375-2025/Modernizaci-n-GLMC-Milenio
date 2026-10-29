# QA525 — contrato de recibos para transacción API+WEB reversible

**Estado:** contrato puramente sintético/en memoria, PR de desarrollo hacia `dev`. No hay instalación en glmdch ni permiso para operar bases o servicios.

## Para qué sirve

El PR #771 contenía un aplicador fijado a un SHA antiguo. No se puede transformar ese código en actualización automática sin un contrato claro para detenerse ante evidencias incompletas. Este componente define condiciones mínimas de coherencia de los recibos de un futuro operador **dinámico**:

1. Fuente `dev` exacta y release `qa-signed-<SHA>`, recibos de comparación sin cambios sensibles.
2. Preflight físico de solo lectura con línea base API y WEB y staging firmado con doble comprobación del host.
3. Recibo futuro de **ambas imágenes nuevas** construidas y sin cambiar ningún servicio.
4. Fotografía consistente de **los siete servicios** y conservación de imágenes anteriores para reversión.
5. Dumps **frescos de pmgm y pmgm_keycloak**, hashes completos, `pg_restore --list` exitoso, custodia externa y simulación de restauración demostrada.
6. Ensayo documentado de reversión conjunta de API/WEB, conservación de Keycloak/MinIO/PostgreSQL/gateway/ClamAV, entorno de identidad y conjunto de migraciones.

Si falta cualquier elemento, el validador **rechaza**. Su resultado positivo es exclusivamente `STRUCTURAL_CONTRACT_PASS_PHYSICAL_REVIEW_REQUIRED`; **siempre** devuelve `may_apply:false`, `host_attested:false` y `backup_verified_by_this_tool:false`, porque JSON externo no acredita hechos en un servidor.

## Ejecución de ejemplo (solo recibos sintéticos)

```bash
python3 scripts/qa/operators/qa525-reversible-transaction-contract.py < recibo-sintetico.json
```

El comando solo lee stdin (64 KiB máximo) y emite estado o rechazo redactado; no llama a Docker ni solicita credenciales. **No ejecutar con recibos que incluyan secretos**. Las pruebas se encuentran en `tests/test_qa525_reversible_transaction_contract.py` y el workflow `qa525-reversible-contract.yml`.

## Qué NO implementa

- No verifica la firma real Sigstore ni las imágenes de contenedores.
- No genera respaldos ni certifica restauraciones.
- No construye imágenes, reinicia ni instala API/WEB.
- No ejecuta `docker compose`, `systemctl`, migrador o modificación de Keycloak.
- No autoriza `apply` manual ni automático, tampoco con recibos sintéticos perfectos.

Un operador físico futuro deberá reproducir de forma independiente las afirmaciones de los recibos con raíces de confianza auditadas, locks exclusivos, SHA exacto, identidades del runtime, respaldos off-host reales y un plan probado de rollback. La actualización actual con cambios de `Program.cs` y `UserAccounts` mantiene `BLOCKED_REVIEW_REQUIRED` hasta revisión supervisada. No fusionar PR #771 en bloque ni usar los `TARGET` fijos de 88fb/e3e90/0bd927b.

## Handoff

Issue #525, Issue #97, PR #787 (guardias ya integradas), PR #788 (confianza previa a imports en revisión), PR #771 (operador antiguo en borrador). No promover `main`. El hilo «Montaje QA operativo» corresponde a la primera inspección/backup/despliegue supervisado **después** de disponer de operador actual y runbook aprobado; sus pruebas físicas no pueden ser suplidas por esta CI.
