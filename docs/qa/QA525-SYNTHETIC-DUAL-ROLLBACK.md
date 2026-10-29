# QA525 — simulación hermética de transacción API+WEB

**Estado:** propuesta exclusivamente sintética, sin adaptador a Docker/glmdch. No es un despliegue, ni un operador real de `apply`.

El simulador `scripts/qa/operators/qa525-synthetic-reversible-transaction.py` comprueba con un adaptador **en memoria** que la transacción obliga a ejecutar, en ese orden:

1. Preflight físico simulado, identidad de fuente firmada y snapshot de siete servicios.
2. Respaldo sintético de `pmgm` y `pmgm_keycloak`.
3. Custodia externa simulada de respaldos y ensayo de restauración sintético.
4. Revalidación de baseline justo antes de mutar.
5. Aplicación sintética primero de API y luego de WEB, con pruebas de salud, integridad de dependencias y migraciones.
6. En cualquier fallo desde la primera mutación, **intentar revertir AMBAS**, WEB y API, incluso si una reversión falla. Dejar `SYNTHETIC_ROLLBACK_FAILED_MANUAL_REVIEW` si no se puede comprobar ambas.

No hay salida `may_apply:true`, solo `host_updated:false`. El simulador **rechaza cualquier clase distinta del adaptador en memoria incorporado**. No utiliza Docker, PostgreSQL, SSH, GitHub API, archivos de configuración del host, contraseñas ni servicios. Demuestra orden y manejo de fallos, NO compatibilidad con un host real.

Este código complementa los contratos #789 (coherencia de recibos), #790 (preparación de imágenes firmadas) y #791 (lectura de preparación real sin aplicar). **Aún no existe el aplicador físico de API+WEB revisado**. El PR #771 sigue antiguo y bloqueado. Para el primer despliegue supervisado hacen falta, además, revisión efectiva de cambios sensibles, firma Sigstore real, respaldo fresco con custodia externa/restauración real, operador físico seguro con reversión, smoke autenticado, UAT y autorización de glmdch. Nada de esto se autoriza mediante CI sintética.

Requisitos de pruebas: `tests/test_qa525_synthetic_reversible_transaction.py` y el workflow `qa525-synthetic-reversible-transaction.yml`. Probar interrupciones antes y después de cada paso, fallos parciales en API/WEB, reversión de uno o ambos servicios fallida y rechazo de un adaptador que no sea sintético.

**Handoff:** GitHub issue #525 y #97, Estado ejecutivo Drive, `dev` como rama integradora, `main` inalterada, primer QA desde hilo «Montaje QA operativo» solo cuando el operador físico y runbook estén preparados. 
