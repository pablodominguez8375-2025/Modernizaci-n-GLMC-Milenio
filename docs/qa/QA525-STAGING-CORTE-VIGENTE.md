# QA525 — staging del corte vigente con firma autenticada

Fecha 28-10-2026. Seguimiento Issue #525, PR #785, continuación de PR #783.
El PR #771 mantiene en exclusiva sus operadores del host, que NO se modifican aquí.

## Flujo implementado

El programa scripts/qa/qa525-stage-current-release.py usa tres módulos ya
aprobados, con límites separados:

1. qa525-current-release-gate.py: consulta en GitHub el HEAD **actual**
   de dev, cinco gates de CI, artefacto Silo, publicador SUCCESS, release
   no-draft y único asset con target y SHA256 concordantes.
2. qa-public-release-download.py: descarga pública sin credenciales, con
   redirección HTTPS acotada, compara digest byte a byte, limita tamaño
   y elimina un ZIP parcial si falla.
3. qa-signed-staging.py: coteja nuevamente el digest, extrae el ZIP externo
   sin enlaces simbólicos ni traversal, ejecuta
   gh attestation verify con identidad estricta del workflow
   .github/workflows/qa-silo-rebuild.yml, source-digest SHA exacto,
   --deny-self-hosted-runners, y verifica BUILD-INFO, MANIFEST.sha256,
   contenidos esperados y evidencia mínima de Silo.

Después vuelve a consultar el gate de GitHub: **si cambió dev, los
workflows o la release, se descarta todo el staging temporal**. Solo
después publica el directorio de código verificado en la ruta de salida.

El resultado es SIGNATURE_VERIFIED_STAGED_NOT_DEPLOYED. No valida licencia
AGPL, disponibilidad en glmdch, versiones de Docker, TLS/OIDC runtime,
backups/restauración físicos ni UAT. No ejecuta INSTALAR.sh, docker compose,
migradores, servicios systemd, credenciales, base de datos ni cuentas.

## Uso cuando exista un entorno autorizado

Requisitos: Python 3.12+, GitHub CLI gh con capacidad de verificación de
attestations públicas, conectividad saliente a GitHub API / release CDN,
y los tres verificadores del mismo checkout confiable.

La salida debe ser una ruta absoluta nueva bajo un directorio **privado
0700 propiedad del usuario ejecutor**. El comando de ejemplo ejecuta
solo staging local, **no en el host glmdch sin procedimiento aprobado**.

~~~bash
mkdir -m 700 "$HOME/qa525-private"
python3 scripts/qa/qa525-stage-current-release.py \
  --output "$HOME/qa525-private/corte-validado"
~~~

El código bloquea rutas relativas, directorios privados mal configurados,
salidas preexistentes, firmas fallidas, ZIP alterado, metadata incompleta,
cambio HEAD durante la descarga y recibos inconsistentes. No imprime
tokens ni descarga usando uno.

### Cómo se valida el software antes de integrar

~~~bash
python3 -m py_compile scripts/qa/qa525-stage-current-release.py
python3 -m unittest discover -s tests -p test_qa525_stage_current_release.py -v
~~~

El workflow QA525 current signed staging contract ejecuta tests herméticos.
La prueba usa un firmador simulado **solo en tests**: NO acredita que el
ZIP real fue autenticado ni que el host esté preparado. La firma real
se comprueba únicamente cuando se invoca el script con un ZIP público
y el comando gh attestation verify real.

## Siguiente integración y aplicación en QA

No usar automáticamente el contenido de staging para desplegar. El
preparador del PR #771 debe consumir primero el gate dinámico y
el paquete firmado del HEAD, abandonar el SHA fijo 0bd927b, y validar
política de cambios (migraciones, Program.cs, flags de cuenta),
identidad/SMTP, respaldos frescos de pmgm y pmgm_keycloak, rollback,
locks y autorización de operación física. Antes de aplicar debe
recomprobar vigencia del SHA y recibos de backup/imagen.

La activación automática de timers y la promoción a main permanecen
fuera de alcance. Registrar el recibo firmado y la UAT en GitHub/Drive.
