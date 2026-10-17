# QA glmdch — protección contra retroceso y primer arranque supervisado

Estado 17-10-2026: desarrollo para Issue #525, **no habilita despliegue ni automatización en el host**.

## Contrato
`scripts/qa/qa-antirollback.py` es un control adicional previo a cualquier futuro despliegue:
- No crea ni actualiza archivos; no llama Docker ni ejecuta instalables.
- Exige un registro de última versión realmente desplegada: por defecto `/var/lib/pmgm-qa-agent/last-deployed.json`, archivo regular, propietario del proceso, modo 0600, máximo 2048 bytes, sin enlaces y con un único vínculo físico. Contiene exactamente `{"version":1,"last_deployed_sha":"<40-hex>","qa_only":true}`.
- Si falta, **bloquea el primer arranque automático** (`FIRST_DEPLOYMENT_REQUIRES_SUPERVISED_APPROVAL`), porque solo un procedimiento manual aprobado tras smoke físico debe crear el registro.
- Si el SHA de candidato coincide con último instalado, devuelve `ALREADY_DEPLOYED_NO_ACTION` y evita reejecutar.
- Si es distinto, consulta el endpoint público GitHub compare de SHA anterior a nuevo y acepta **solamente** fast-forward con ancestro verificado por `base_commit`, `merge_base_commit`, `ahead_by` y último commit del diff. Divergencias, forzados, respuestas truncadas o inconsistentes son bloqueo seguro.
- No supone que un SHA aparecido en GitHub sea automáticamente desplegable. Requiere además verificador exact HEAD/gates (#526), descarga/ZIP digest (#528), firma Sigstore/manifest (#527), preflight, backups y smoke.

## Secuencia futura autorizada
1. Construir y revisar el servicio orquestador de los módulos de lectura, descarga, firma y política.
2. Primer despliegue sintético **supervisado** por Sponsor desde SSH móvil, con aprobación del SHA exacto, DNS/TLS, storage, OIDC, BD2, G8, backup/restore y monitoreo de puertos.
3. Solo tras aceptación física, escribir por una utilidad separada y auditada el SHA desplegado al archivo privado de baseline; proteger integridad, owner, modo y cambios.
4. Activar timer para posteriores versiones de `dev` comprobadas. Si falla preflight, migración, smoke o recuperación, no actualizar baseline y detener; prohibida rotación de secretos o promoción `main`.
5. Registrar resultados sanitizados en GitHub Issue #525/#97 y Línea Base Drive.

Ningún código de esta PR modifica `main`, `srv01`, host `glmdch` ni datos institucionales. No confiar la autoridad de despliegue a un actor con permiso para fusionar código en `dev` sin controles independientes de revisión y firma.
