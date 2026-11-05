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
## Revisión adversarial P1/P2 Claude (2026-11-05)

Claude detectó una inversión temporal real en el primer HEAD: el registro externo que fija el SHA de los bytes de admisión **debe firmarse después de producir esos bytes**; pedir `issued_at <= admitted_at` bloqueaba el flujo legítimo aunque los 9 tests anteriores pasaran. El módulo exige ahora `admitted_at <= issued_at <= now` con ambas ventanas ≤30 min y `now` dentro del mínimo de vencimientos. El fixture positivo firma a las 18:03 una admisión creada a las 18:02; otro caso rechaza firma anticipada.

Se agregaron además vínculos exactos `silo_workflow_run` y `g6_expires_at` al registro externo, se valida el tipo del lado del ancla (no aceptar 1.0/true por un entero ni 12.0 por identidad física), y se devuelve `external_anchor_sha256` para futura evidencia del marcador. El status textual del registro se denomina `BYTE_BINDING_INPUT_ONLY_NOT_VERIFIED_HERE` para evitar insinuar que el propio módulo validó una firma. Los hashes de G6 y Sponsor siguen siendo referencias sin autenticación local, nunca se infiere autorización por el módulo. El SHA de ancla tampoco es prueba criptográfica de procedencia por sí solo.

**Dependencia externa:** un bootstrap OPS previamente confiable debe verificar firma real del registro/ancla, autoridad de emisión del Sponsor, G6 revisado, tiempos, filesystem root y cadena de ancestros para ambas rutas, y pasar el policy_hash independiente del propio admisor. Solo después un motor bajo locks podría copiar bytes+SHA O_EXCL/fsync y consumirse un nonce; no implementado aquí. No confundir la prueba de binding con autenticación.
