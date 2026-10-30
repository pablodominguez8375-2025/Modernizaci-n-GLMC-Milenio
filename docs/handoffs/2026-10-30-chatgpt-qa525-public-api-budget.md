# QA525 — cuota pública agotada, correctivo PR807 — 2026-10-30

## Estado y origen

HTTP403/CUOTA0 confirmado por PO en glmdch, reset18:35:29Z. Cero imágenes candidatas al ae21 y preparación detenida en readiness. No apply ni cambios SQL. Base dev ae21e611b50ef32bb2ebcfa2abc4e489b8ba9367; rama fix/qa525-public-api-budget-20261030-gpt; PR807; issue525 agente:chatgpt. Main sin promoción. PO ya autorizó continuar actualización y modo de operador único; este correctivo conserva ese alcance y sus controles.

## Cambio

Gate con inventario coherente por invocación y pin interno verificado contra HEAD real final; staging con selección de la misma operación y verificación independiente posterior; entrypoint reutiliza comparación exacta y concentra revalidaciones completas en los límites de firma, fin de build, antes/después de recreación. Callbacks de build conservan manifiesto completo y HEAD vivo. Readiness y CLI exponen solo códigos HTTP seguros. No cache persistente, token, fallback, timestamps reescritos ni aceptación de evidencia vencida.

Contrato: docs/qa/QA525-PUBLIC-API-BUDGET.md. Fuente/aplicación y controles de identidad, G6, EF, locks, nonce y rollback se mantienen. PR804/805 separados. Migración y respaldo no se repiten por este fallo.

## Verificación

124 pruebas herméticas locales PASS (antes del ajuste documental). Incluyen presupuesto real de consultas, pin antiguo/deriva de HEAD, revocación CI durante build, actas/flags/capas, firma y selección alteradas, postgate revocado con rollback dual y clasificación HTTP sin exponer datos privados. CI exact-head pendiente de consultar; no afirmar integrado o desplegado.

## Continuidad física

Staging ae21 Sigstore/manifiesto PASS; EF99 exacto y G6 postDB de01:33:39 Drive/restore PASS. Tres registros privados se crearon válidos, pero su vigencia30min puede vencer durante espera de cuota. No existe prepared.json según último diagnóstico. Tras CI y nuevo corte firmado, validar ausencia de deriva y registrar revisión del nuevo SHA/digest; archivar actas previas y reemitirlas solo tras revalidación real. Esperar reset de cuota, no repetir probes a ciegas, no forzar contra 403.

Luego preparar API/WEB por fuente nueva, comprobar recibo y activar con aprobación real de único uso y rollback. Verificar /qa-version.json, HTTPS/OIDC, correo, roles y UAT sintética. Solo glmdch; no main/producción/srv01 ni PR771/737/766.
