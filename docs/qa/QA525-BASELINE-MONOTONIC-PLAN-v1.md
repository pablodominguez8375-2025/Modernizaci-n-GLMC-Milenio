# QA525 — Baseline exacto y plan monótono (paso 3, INACTIVO)

**Ámbito:** pruebas herméticas exclusivamente; NO es un motor APPLY, no captura el host, no persiste journal, no escribe APPLIED o ROLLED_BACK y no sustituye supervisión del Sponsor.

Fuente del diseño: Claude [PR #846 comentario 6667365120](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/pull/846#issuecomment-6667365120); plan de aceleración Sponsor 05-11-2026. Contrato de 5 locks y una sola ruta física: PR #847 integrado dev@02372aa8. Vínculo de admisión externo: PR #849 DRAFT, en paralelo. Verificador terminal read-only: Claude PR #848 DRAFT, en paralelo. Las ramas no comparten módulos; el merge de `tests/test_qa_agent_readiness.py` es serializado y debe preservar todos los imports.

## Qué sí hace el código

- `scripts/qa/qa525-vnext-baseline-plan.py` recibe solo objetos Python proporcionados por el caller (NO adapta Docker, no lee disco). Acepta **exactamente** api/web, 5 dependencias `keycloak,postgres,minio,gateway,clamav`, 3 oneshots `db-grants,migrator,minio-init` con `exit_code=0`; imágenes SHA256, IDs de contenedor hex, capas Compose absolutas sin traversal/duplicado y `web_sha` exacto. La estructura `images,dependencies,oneshots,layers,web_sha` coincide con la que consume el verificador Claude #848, pero el módulo no depende de él.
- `prepare(base,target)` valida y copia profundamente ambos, fija SHA256 del JSON canónico para detectar alteraciones accidentales, y devuelve `stage=APPLYING`, `may_apply=false`, `external_runtime_verified=false`; **no** significa que el journal APPLYING esté escrito a disco.
- `advance(plan,stage,evidence_sha256)` valida etapa anterior y obliga la secuencia `APPLYING → API_SWITCHING → API_VERIFIED → WEB_SWITCHING → WEB_VERIFIED → VERIFYING`. `RECOVERY_REQUIRED` puede interrumpir cualquier etapa y es terminal/bloqueante. No existen etapas de escritura APPLIED ni ROLLED_BACK. El proof SHA es **una referencia no autenticada**, no autoriza transición física.
- `tests/test_qa525_vnext_baseline_plan.py` cubre baseline legítimo y variaciones adversariales, ausencia de dependencias/oneshots, capas inválidas, selección de imagen equivocada, replay, pasos saltados y tamper de checksum. Clase importada en readiness protegido. No se usa file/network/subprocess/root/SSH/Docker/host.

## Obstáculos deliberados a instalación

1. Antes de cualquier mutación real, un adaptador revisado bajo locks de host debe capturar el baseline **del mismo host** y una ruta de journal root0600 debe almacenarlo por O_EXCL/fsync fd+dir; una copia en memoria no da crash safety ni protege contra fallo de energía. `qa525-vnext-journal.py` aún no acepta este baseline como parte del registro APPLYING; integration es trabajo futuro.
2. La firma de OPS/artefacto, la decisión Sponsor y G6/restore real del mismo corte se autentican de modo independiente del SHA que el caller suministre. Los módulos #849/#850 no prueban esos actos.
3. Los 5 locks heredados del instalador no están demostrados como dominio de exclusión del ejecutor legacy; se debe deshabilitar legacy, reparar `approval-consumed-*`, validar `dual-<sha>.json` histórico, fijar ambos ancestros confiables, y retener lock ante hijos/cgroups.
4. Claude #848 verifica terminales solo observando adapter. Resultado SHA del verificador tampoco autentica un adapter o un reporte autocreado; no conectar a terminal writer sin target exacto, fuentes confiables, sesiones bajo lock, salud/OIDC y oneshots reales.
5. El código es `prepare/advance` PURO, **NO durable**. No instalar en `glmdch`, no ejecutar Docker ni cambiar main/prod/identidad/DB. QA físico permanece versión `6eed4f00...` último estado operador APPLIED una vez.

**Gate de este PR**: revisión Claude exact-head + CI verde para integrar solamente código inactivo a dev; ninguna activación física. La instrucción Sponsor de máxima aceleración promueve carriles paralelos y revisiones tempranas, no elimina preflight firmado ni autorización de instalación.
