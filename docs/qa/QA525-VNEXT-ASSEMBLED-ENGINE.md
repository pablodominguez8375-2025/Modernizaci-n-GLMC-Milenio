# QA525 vNext — motor ensamblado sintético

`qa525-vnext-engine.py` une, por primera vez, el flujo completo de las
bibliotecas vNext revisadas: cinco locks sintéticos + lease raíz, baseline
único, plan durable, manifiesto durable de scopes, conmutación API/WEB,
observación, verificación, rollback y terminal.

## Orden cerrado

0. ligar los bytes de admisión y de aprobación externa (`admission-bridge` + `trust-binding`) con la política confiable; el digest que fija plan, scopes y lease es el ligado, nunca el del llamador, y la ventana se revalida al `begin`;
1. tomar los cinco locks del fixture privado y después el lease del journal;
2. capturar una sola vez el baseline y preparar el plan ligado al nonce;
3. persistir y hacer `fsync` del conjunto exacto de scopes API/WEB;
4. en cada etapa comprobar locks, gate externo, locks nuevamente, intent
   durable, unidad registrada y recién entonces invocar el runner;
5. verificar cada cambio mediante el observador; ante fallo, rollback con las
   capas del baseline; ante ambigüedad, conservar evidencia y detenerse.

## Límite de seguridad

Este motor **no es todavía un entrypoint físico**. Solo expone
`run_synthetic`; todos los efectos son callbacks y el plano de locks rechaza
rutas reales. `may_apply` y `may_install` permanecen en `false`.

Antes de un instalador host son obligatorios, en una revisión separada:

- bootstrap OPS previamente autenticado y manifiesto Sigstore exacto;
- wrapper root para los cinco locks físicos y lease raíz;
- adaptadores reales de runner, metadata, salud HTTPS/OIDC y cgroup;
- gate autenticado de Sponsor y G6 vigente/restaurado;
- ensayo de crash/recuperación en ambiente aislado y autorización física.

CI o pruebas sintéticas no sustituyen esos controles ni autorizan `glmdch`,
`main`, base de datos, identidad o producción.
