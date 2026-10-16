# QA glmdch — diagnóstico de provisioning Silo — 16-10-2026

## Continuidad y alcance

Pablo pidió continuar implementación/automatización desde GitHub, sin su PC y sin promover main. Silo aprobado sólo para QA sintético. Se retoma PR #506, rama `feature/qa-glmdch-silo-20261015-gpt`, sobre candidato `fdf485f9bf37f9f3a3649b8b77b808d38758160b`. Se reconcilió `dev@3a25ab0bfbfe0bb5aa8009906505ccb86559d723` mediante merge sin conflictos; se preservan Calendar y UI/Claude. El SHA de entrega es el commit que contiene este handoff; readback y resultados exactos se registran en #506/#97 y Drive sin otro commit que invalide gates.

Consultados GitHub #97, #470, #479, #501, #503 y #506, instrucciones GOV-003 §16, AGENTS, START-HERE, BASE/NEXT pertinentes y adenda QA vigente de Drive `1I9bcJ7lKH4T2iQvxafY63HbOBr0ZsGZ8lDX3tSe2UfM`. Búsqueda Drive por diferencias en carpeta Proyecto Centenario desde 12:32 UTC: sólo la adenda QA ya leída; sin nueva regla funcional que afecte este cambio. #503 integrado; #479 bloqueado/reservado; #501 sigue PR de revisión y su automatización no acredita conexión a glmdch. No se fusiona a main para habilitar workflow_dispatch.

## Implementación

- `tests/test_silo_real_recovery.py`: mcli con salida JSON; cada intento fallido emite proyección estructurada con paso, intento, duración, código de salida, categoría, código S3 conocido, estado Docker/OOM/exit y healthcheck ready. Sólo campos/códigos allowlist; no argumentos, env, logs crudos, identidades, endpoints ni secretos. Docker inspect restringido a `.State` y después proyectado; sus logs/errores no salen.
- La palabra `lookup` sola ya no declara DNS: se requieren síntomas concretos `no such host` o `temporary failure in name resolution`. Ninguna categoría constituye por sí sola causa raíz.
- Conservar seis intentos, timeout mcli 45s y cinco esperas 2s. Diagnóstico añade como máximo dos consultas de 7s por fallo. Fracaso final conserva todas las proyecciones; recuperación tras fallo se registra. No se tolera error ni se omite IAM/SDK/recuperación. Los consumidores existentes del helper reciben diagnóstico automáticamente.
- `tests/test_silo_provision_diagnostics.py`: seis regresiones offline de no divulgación, JSON desconocido/malformado, inspección ausente, fallo cerrado tras seis intentos y continuación de todos los pasos tras recuperación. Dobles limitados a pruebas del diagnóstico: no sustituyen los laboratorios S3 reales.
- `.github/workflows/qa-silo-validation.yml`: ejecuta nuevas regresiones y reacciona a cambios en `tests/test_silo*`; mantiene laboratorio Compose real, comprobación de procedencia/licencia, escaneo y umbral HIGH/CRITICAL intactos. `ci.yml` y archivos piloto de #479 no editados.

## Validación y bloqueos

Local: seis pruebas de diagnóstico PASS, compilación sintáctica Python PASS. Sin Docker en este entorno: laboratorio real, SDK y recuperación se validan desde GitHub Actions. CI/Showcase/instalable/suministro del NUEVO SHA deben comprobarse; no heredar PASS del SHA anterior. No se declara corregido el provisioning: este incremento permite recoger evidencia saneada del próximo fallo, sin repetir el SHA fallido a ciegas.

El digest Silo actual conserva bloqueo documentado por 11 hallazgos HIGH del escaneo anterior: base PCRE2, cliente Go y triage específico del módulo/fork. No se cambió digest, no se aplicó waiver/VEX/ignore y no se construyó una imagen nueva. Obtener artefacto corregido verificable, resolver triage y repetir escaneo fresco siguen pendientes. Nunca interpretar PASS del runtime como aceptación de suministro.

**Despliegue QA pendiente.** No conexión SSH, instalación, servicios, cuentas, claves/secretos, firewall/NAT ni datos reales en glmdch. Sin UAT, sin promoción a main (observada `6dfb9546a4873baff15955cf86abfd7d47e3d111`), sin merge a dev. Pages y paquete nuevo no certificados en esta entrega hasta readback de sus workflows; un artefacto generado continúa NO DESPLEGABLE mientras falle seguridad.

## Retomar

1. Consultar HEAD remoto de #506 y runs de ese mismo SHA; leer el primer fallo saneado si aparece (step/attempt/code/ready/state/OOM), contrastar código del proveedor y entorno. Corregir causa demostrada; no subir reintentos ni retirar pruebas.
2. Remediar suministro/base/cliente y triage servidor, conservar digest/procedencia/SBOM/licencia y cero HIGH/CRITICAL antes de propuesta de montaje.
3. Reconciliar cualquier dev concurrente sin sobrescribir Calendar/UI; nuevos gates exact-head y mismo instalable.
4. #501: sigue pendiente habilitación/aprovisionamiento efectivo y conectividad del acceso limitado; no promover main sin autorización. Seguridad/identidades BD2, OIDC, respaldo externo cifrado, RPO/RTO y smoke integral no se dan por aceptados por tests lógicos.
5. Registrar evidencias y bloqueos en GitHub + la adenda QA de Drive.
