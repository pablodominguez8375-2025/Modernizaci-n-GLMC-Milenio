# QA525 — Paso 2: vínculo de bytes admisión ↔ registro de aprobación externa

**Estado:** componente INACTIVO y NO AUTORIZADOR, diseñado para integrarse a `dev` sin tocar QA físico.

## Problema
El módulo `qa525-vnext-cache.py` escribe `admission-<nonce>.json` y `qa525-vnext-journal.py.begin(root, path, expected_sha256, now)` compara bytes con un SHA256 que recibe **del caller**. Sin una fuente de confianza independiente, un caller malicioso podría elegir el hash de su propio archivo. Claude lo señaló en PR #846 y lo priorizó como paso 2 tras el plano único #847.

## Contrato propuesto
1. Una entidad externa autenticada por bootstrap firmado (NO definida aquí) aportará un registro inmutable `qa525-approval-anchor-v1` separado de la admisión con `admission_sha256`, `app_source_sha`, `ops_source_sha`, `policy_hash`, identidades de artefacto, `signed_installable_sha256`, `manifest_sha256`, `attestation_bundle_sha256`, nonce, G6 review hash y Sponsor approval receipt hash, ventana 30 minutos y scope `glmdch/QA_SYNTHETIC/API_WEB_QA_ONLY`.
2. `scripts/qa/qa525-vnext-trust-binding.py` implementa **solo la consistencia de bytes**: rechaza JSON duplicado/NaN, recibo de admisión no canónico, campos extra, SHA/identidad/formato inválidos, mezcla de versiones, nonce cambiado, tiempos expirados, digest de admisión alterado. Compara política también con un `trusted_policy_hash` entregado por bootstrap. Usa igualdad en tiempo constante para digests.
3. Devuelve `status=BYTE_BINDING_MATCH_ONLY_EXTERNAL_AUTH_NOT_VERIFIED`, siempre `signature_verified_here=false`, `sponsor_approved_here=false`, `g6_verified_here=false`, `may_apply=false`, `may_install=false`. Nada del resultado da permiso de ejecutar.
4. **Precondiciones externas NO RESUELTAS:** autenticación criptográfica de registro independiente, identidad y custodia de binarios OPS/imports/gh, verificación real G6/restore, ruta y ancestros root, locks unificados + deshabilitar legacy, baseline + hijos + verify_state. El registro de hash de G6 y de Sponsor es *referencia*, no su autenticación. La fase que copia el recibo a root O_EXCL/fsync aún NO se implementa.
5. Pruebas herméticas no usan Docker, red, host, claves ni firmas ficticias con pretensión de autenticidad.

**Riesgo de mal uso:** quien forje *ambos* archivos para que coincidan también obtendrá `BYTE_BINDING_MATCH_ONLY_EXTERNAL_AUTH_NOT_VERIFIED`. Esto es correcto: byte binding ≠ prueba de aprobación. El futuro motor nunca debe confiar en este helper sin verificación de una aprobación externa ya anclada por un bootstrap firmado confiable.

**Relación #847:** solo diseño; no cambia markers ni habilita un segundo launcher. El host físico conserva `6eed4f00...` como último corte real por acta de operador; `dev` contiene código inactivo. Este incremento no escribe estado ni consume nonce.

**Coordinación:** Claude desarrolla paralelamente PR #848 verificador de terminales; ramas se basan en `dev` exacto, cambios en `tests/test_qa_agent_readiness.py` se serializan preservando ambos imports; nunca aplicar merge sin resolver diferencias.