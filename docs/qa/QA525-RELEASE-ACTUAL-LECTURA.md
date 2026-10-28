# QA525 — comprobador dinámico de release firmada para el HEAD vigente

**Alcance:** herramienta exclusivamente de lectura, aplicable al proyecto
Centenario sobre datos sintéticos. NO instala, NO descarga artefactos, NO
ejecuta comandos de host, NO verifica por sí sola firmas Sigstore, NO activa
timers ni toca usuarios o base de datos. Ref. PR #771 / Issue #525.

## Por qué

Los operadores históricos e3e90/88fb tienen el SHA objetivo embebido y
no deben ejecutarse después de nuevos commits. La continuación segura
separa el descubrimiento del corte de la aplicación física.

scripts/qa/qa525-current-release-gate.py consulta el HEAD *actual*
de dev sin argumentos ni secretos. Reutiliza los cinco gates
institucionales y la selección exacta del artefacto Silo de
qa-agent-readiness.py. Además exige:

- Workflow Publish signed synthetic QA envelope de un push a
  dev finalizado en SUCCESS para ese SHA
- Prerelease pública qa-signed-<SHA> con target_commitish igual al
  SHA exacto, sin estado draft
- Un único asset ZIP nombrado qa-signed-<SHA>.zip, de tamaño mayor
  que cero, con digest idéntico al artefacto firmado generado por el
  workflow Silo para el **mismo SHA**
- Que el HEAD de dev siga siendo el mismo al concluir

Una falta de evidencia, respuesta de API anómala, estado incompleto,
hash diferente, asset duplicado o nuevo dev devuelve BLOCKED con
código fijo y salida JSON. No se permite elegir un ancestro anterior.

## Uso read-only

~~~bash
python3 scripts/qa/qa525-current-release-gate.py
python3 -m unittest discover -s tests -p test_qa525_current_release_gate.py -v
~~~

La salida satisfactoria es **exclusivamente**
READY_FOR_SIGNATURE_VERIFICATION_ONLY, junto a SHA, ID de asset
y digest esperado. No equivale a PREPARED, APPLIED ni UAT_PASS.
No es permiso para descargar e instalar en glmdch.

El paso siguiente (fuera de este script) debe obtener el ZIP por canal
autorizado, cotejar SHA-256 y verificar **su firma real** usando
qa-signed-staging.py (provenance, identidad del workflow, source-digest,
manifiesto e integridad). Antes de cualquier escritura, repetir la
verificación HEAD y comparar el recibo resultante con los contenedores
actuales, migraciones, identidad, SMTP, backup, UAT y rollback previstos.

## Primer caso confirmado — 28-10-2026

GitHub dev@0bd927bc863d4c3321236b3a23eb15b748ed72dc
contiene PR #777 (seguridad de Silo) y PR #782 (UI v0.181).
Los cinco gates de publicación de ese SHA se encuentran SUCCESS,
y la release qa-signed-0bd927bc863d4c3321236b3a23eb15b748ed72dc
tiene asset digest
sha256:d095a429f720fa952f78b654808655f9e5d9e35d7911fd53096e95e4ab74f5fd,
coincidente con artefacto de suministro Silo del mismo corte.
Publicador firmado del corte terminó SUCCESS.

Estos son **hechos de GitHub**, no prueba de que glmdch haya recibido
ese paquete. La aplicación física de QA sigue separada y no se ha
autorizado ni ejecutado desde este incremento.

## Continuidad

No fusionar el antiguo PR #771 en bloque: contiene operadores
enlazados a cortes previos y requieren migración/revisión propia.
Este control solo prepara un componente verificable del flujo rápido.
El paquete Silo tiene VEX acotado a un binario reparado; no sustituye
la revisión de licencia AGPL ni otras puertas de seguridad.

main continúa protegida. El primer arranque de agentes de host,
la modificación de unidades pmgm-qa-update-*.service y la automatización
de APPLIED siguen fuera de alcance.
