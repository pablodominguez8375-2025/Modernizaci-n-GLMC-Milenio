# QA glmdch — ciclo de adquisición verificable, sin despliegue

Estado 17-10-2026 para Issue #525. Código disponible en `scripts/qa/qa-acquire-candidate.py`.

### Qué hace
Ejecuta en secuencia módulos **de código confiable incluido en dev**:

1. `qa-agent-readiness.py`: selecciona el SHA exacto de `dev` con flujos CI/QA/Silo/HTTPS/Showcase aprobados y paquete completo SHA256.
2. `qa-artifact-download.py`: lee un token Actions **read only** de archivo protegido 0600; descarga por ID desde API GitHub y sin reenviar autorización al CDN; verifica SHA-256 ZIP externo.
3. `qa-signed-staging.py`: verifica atestación Sigstore del ZIP interno, identidad workflow y SHA de origen, `BUILD-INFO`, manifiesto completo y presencia del bundle firmado Silo. Rechaza traversal, symlinks, duplicados y tamaños excesivos.
4. Vuelve a consultar `dev`; si cambió SHA, id o digest del artefacto **no publica** la carpeta preparada.
5. Mueve la carpeta validada de manera atómica a `<root-privado>/<SHA>`. Impide sobrescribir un staging anterior y usa lock exclusivo por ruta.

### Límites
**No ejecuta ningún script del instalable, no arranca contenedores y no ejecuta migraciones, backups, despliegues ni un timer.** La salida `VERIFIED_QA_CANDIDATE_PREPARED_NOT_DEPLOYED` no certifica el runtime Silo, aprobación de QA ni certificación de servidor. El programa requiere que el directorio de staging **ya exista** y sea privado 0700 propiedad del servicio. No crea ni imprime credenciales de GitHub.

La PR #529 antirollback debe impedir promoción a una versión que no sea descendiente de la última realmente desplegada y prohibir el primer despliegue automático. El script de adquisición solo prepara archivos: no sustituye esa barrera.

### Próxima fase y límites de seguridad
Antes de ejecutar el ciclo en `glmdch`, finalizar y probar: servicio systemd aislado sin privilegios Docker y primera instalación administrada supervisada; acceso GitHub Actions read-only con expiración; revisión independiente de firma+licencia+CVE y carga Silo en host; backup cifrado/restauración externa; preflight G1–G10; DNS/TLS, OIDC y 502. El sistema debe fallar cerrado ante bloqueos y nunca promover `main` ni usar datos reales o `srv01`.

Los registros sanitizados deben incorporarse a GitHub Issues #525/#97 y Línea Base Drive.
