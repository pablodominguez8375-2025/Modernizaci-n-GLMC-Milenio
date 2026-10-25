# QA-AUTO-LIGERO — autorización y camino a la automatización (#525)

**Aprobación expresa Sponsor/Product Owner:** 19-10-2026, respuesta «Sí, apruebo» a la propuesta QA-AUTO-LIGERO. **Destino exclusivo:** glmdch, Compose centenario-qa-lab2, datos sintéticos y origen dev. Quedan excluidos main, producción y srv01. No autoriza cambios de claves ajenas, migraciones destructivas, firewall/DNS/SSH, borrado de volúmenes ni comandos remotos arbitrarios. Seguridad proporcional: no exigir segunda VM para QA funcional; sí mantener respaldo cifrado reciente y reversión.

## Evidencia física y bloqueos

- QA operativo con gateway, web, API, Keycloak/PostgreSQL, Silo y ClamAV; HTTP 200 e ingreso qa.admin. Bases pmgm y pmgm_keycloak accesibles. Las imágenes actuales no tienen etiqueta de commit: **no existe todavía baseline SHA confiable para activar el agente**.
- Checkpoint cifrado G6 posterior a contraseña qa.admin: centenario-qa-g6-CIFRADO-20261019T175218Z.tar.gz, SHA256 bed4ee196074a1919234da44c6bbd66c8a7cd16bdce70fba97ed1e34df8f9cae. Dos BD, roles, objetos, restic check y restauración local de archivos PASS. Su **copia fuera del host aún está pendiente**; la copia anterior del 18-10 sí está en Drive y se probó restauración en otro conjunto de contenedores del mismo host.
- La corrección Sistema > Usuarios, PR #559, está en dev pero **no está probada en QA físico** (#558).
- El agente #525 solo selecciona y prepara paquetes firmados, con plantillas de servicio deshabilitadas; NO es todavía un autodesplegador.

## Alcance de este incremento

scripts/qa/qa-auto-lite-policy.py ofrece evaluate(policy, installed, staged, ancestry, backup, runtime, now) y devuelve un estado de **elegibilidad para revisión de un ejecutor restringido**, nunca una orden de desplegar. No llama Docker, no lee secretos ni modifica host. Comprueba que:

1. El contrato solo permita glmdch, centenario-qa-lab2, dev, QA sintético y los servicios web/API. Main, migraciones destructivas y cambios de red se rechazan.
2. Exista baseline verificado y la selección sea fast-forward del mismo SHA que el staging firmado y fijado.
3. La copia Restic esté cifrada, sea reciente (48 horas), incluya ambas BD/roles/objetos y su hash de copia externa verificado; procedimiento de recuperación previamente probado.
4. Inmediatamente antes de una actualización se hayan confirmado identidad del host/imágenes, preservación de volúmenes, rollback de imágenes fijado, migraciones deshabilitadas y compatibilidad de esquema, sin cambio sensible posterior al respaldo.

**Advertencia:** el JSON de entrada nunca constituye prueba de firma ni autorización. El futuro ejecutor debe obtener evidencias de fuentes independientes, verificar modo/owner de sus recibos y revalidar todo en el instante de aplicación.

## Para completar QA-AUTO-LIGERO

- Implementar ejecutor local de operaciones allowlist: acceso acotado al proyecto, sin shell arbitraria de Actions ni acceso de runners públicos a root/Docker.
- Establecer baseline SHA de primera instalación comprobada físicamente, inicializada una sola vez desde Termius.
- Automatizar respaldo cifrado y copia fuera del host; hasta comprobarlo, mantener **modo sin despliegue**.
- Aplicar web/API de forma reversible, con smoke HTTPS/OIDC y prueba de Usuarios, conservando volúmenes y revirtiendo fallos; migración incompatible requiere aprobación manual.
- Probar fallas y rollback en proyecto sintético efímero antes de habilitar una unidad systemd de periodicidad controlada, con pausa local y registro sanitizado en GitHub #525/#97 y Drive.
- No tocar main, srv01, producción ni datos reales.

## Prueba independiente

Ejecutar: python3 -m unittest discover -s tests -p test_qa_auto_lite_policy.py -v

Las pruebas son offline y cubren alcance, firma por recibos independientes, no-fast-forward, ausencia de backup externo o Keycloak, cambios de red, migraciones y rollback insuficiente.