# QA operativo — procedimiento rápido y reversible de actualización

## Objetivo

Actualizar API y WEB del QA sintético de glmdch con un único corte firmado, manteniendo identidad, cuenta Master, SMTP, datos y servicios auxiliares. El procedimiento no promueve `main`.

## Regla de inicio

No se prepara nada en glmdch mientras `dev` tenga workflows en curso o no exista una release `qa-signed-<SHA>` para su HEAD exacto.

Esto evita construir imágenes que queden obsoletas por una fusión posterior. La release firmada es el punto de congelación del corte.

## Flujo normal

| Etapa | Responsable | Resultado exigido | Escrituras en servicios |
|---|---|---|---|
| 1. Descubrir | GitHub | HEAD exacto de `dev` | No |
| 2. Acreditar | GitHub Actions | CI, Silo, QA instalable, HTTPS/OIDC y Showcase aprobados | No |
| 3. Congelar | Publicador firmado | `qa-signed-<HEAD>` con digest y attestation | No |
| 4. Preflight | glmdch | Línea base, capas Compose, identidad, Master, SMTP, BD y salud coherentes | No |
| 5. Preparar | glmdch | Imágenes API/WEB construidas y fijadas; contenedores intactos | Solo caché de imágenes |
| 6. Aplicar | glmdch | Respaldo fresco, API/WEB al mismo SHA, salud correcta | Recrea solo API y WEB |
| 7. UAT | Sponsor | Login Master y recorridos funcionales | Uso normal |
| 8. Registrar | GitHub/Drive | SHA, imágenes, respaldo, resultado y UAT | No |

## Gates exactos antes de tocar glmdch

1. Leer `dev` y guardar el SHA completo.
2. Confirmar que no cambió durante la revisión.
3. Exigir éxito del mismo SHA en:
   - PMGM CI;
   - QA Silo supply and recovery;
   - Proyecto Centenario QA srv01 Installable;
   - QA canonical HTTPS and OIDC;
   - PMGM Showcase Demo.
4. Exigir la release `qa-signed-<SHA>` y su asset con digest SHA-256.
5. Si cualquier gate falla o queda pendiente, finalizar como `BLOCKED`. No descargar ni compilar en glmdch.

## Ejecución rápida en glmdch

La actualización debe entregarse como un único bloque verificado que:

1. descarga los operadores desde un commit inmutable;
2. comprueba sus SHA-256 antes de instalar;
3. instala con propietario root y modo 0700;
4. ejecuta una unidad transitoria systemd;
5. deja el proceso trabajando aunque se cierre Termius;
6. muestra el progreso mediante `journalctl`.

No se copian mensajes de estado como comandos. Las líneas `QA_...` son resultados del journal.

## Preflight reutilizable

El preflight debe tardar segundos y validar sin modificar:

- host `glmdch`, proyecto `centenario-qa-lab2` y siete servicios activos;
- imágenes y revisiones actuales de API y WEB;
- archivos Compose efectivos y coincidencia con el entorno de los contenedores;
- capa de identidad, subject/issuer/authority del Master y cifrado SMTP;
- flags de migración desactivados;
- historial de migraciones exacto;
- temporizador WEB inactivo;
- salud pública y `qa-version.json`;
- ausencia de otra actualización bajo los locks compartidos.

Las comprobaciones acreditadas se guardan en un recibo privado y no se repiten si el SHA, los contenedores, las capas y los archivos no cambiaron.

## Preparación

La preparación se ejecuta solamente después de existir la release firmada:

- valida digest, attestation, manifiesto completo y HEAD exacto;
- rechaza cambios de migraciones o `Program.cs` salvo plan específico revisado;
- construye API y WEB una vez;
- etiqueta ambas imágenes con el SHA objetivo;
- conserva las imágenes anteriores;
- escribe `prepared.json` root:root 0600 con línea base e identificadores;
- no ejecuta `docker compose up`, migrador ni cambios de cuentas.

Si `dev` cambia antes de aplicar, el recibo se invalida. Se elimina del flujo operativo y se espera una nueva release; las imágenes antiguas pueden limpiarse después.

## Aplicación conjunta

Antes del primer cambio:

1. volver a validar HEAD, gates, release, firma y recibo;
2. crear dumps nuevos de `pmgm` y `pmgm_keycloak`;
3. verificar ambos con `pg_restore --list`;
4. guardar hashes, tamaños y rutas privadas;
5. mantener `pmgm-qa-lite.timer` inactivo.

Luego:

1. recrear solo API con su pila Compose vigente más una capa de imagen;
2. recrear solo WEB con su pila Compose vigente más una capa de imagen;
3. no ejecutar migrador cuando el conjunto de 98 migraciones no cambió;
4. comprobar que Keycloak, PostgreSQL, MinIO, gateway y ClamAV conservaron sus IDs;
5. comprobar entorno API idéntico, revisión OCI objetivo, salud pública y `qa-version.json`;
6. actualizar atómicamente los recibos privados.

## Reversión automática

Ante cualquier fallo después de comenzar el cambio:

1. recrear API con sus capas e imagen anteriores;
2. recrear WEB con sus capas e imagen anteriores;
3. verificar la revisión WEB anterior, salud pública y entorno API;
4. restaurar el recibo anterior;
5. conservar los dumps para diagnóstico;
6. registrar `REVERTED` sin mostrar secretos.

El despliegue no restaura las bases automáticamente porque el cambio normal no escribe esquema. Los dumps son la recuperación supervisada si una revisión posterior demuestra escrituras incompatibles.

## UAT mínima

Después de `APPLIED`:

- ingresar como Master;
- confirmar menús administrativos;
- abrir inventario de cuentas y comprobar que las protegidas aparecen sin mutaciones indebidas;
- verificar Centro de ayuda;
- probar creación de cuenta con correo;
- comprobar postulantes en móvil e impresión de ficha;
- validar una función institucional crítica.

Solo después se registra `UAT_PASS`.

## Estados oficiales

- `WAITING_GATES`: pruebas en curso.
- `BLOCKED`: gate, firma, vulnerabilidad o línea base inválida.
- `READY`: release firmada del HEAD exacto.
- `PREPARED`: imágenes construidas, servicios intactos.
- `APPLIED`: API y WEB sincronizadas.
- `UAT_PASS`: validación funcional completada.
- `REVERTED`: imágenes anteriores restauradas.

## Qué reduce el tiempo

- Esperar la release firmada antes de usar CPU del servidor.
- Congelar un SHA y cancelar automáticamente si cambia `dev`.
- Reutilizar el preflight cuando su huella no cambió.
- Conservar imágenes anteriores para reversión inmediata.
- Construir cada imagen una sola vez y registrar su ID.
- Ejecutar en systemd y consultar un solo journal.
- Usar un solo runbook y un solo comentario final en GitHub/Drive.
- No repetir V8, V9, V10, hotfixes o diagnósticos ya acreditados.


## Modalidad de mínima intervención

La automatización se divide en dos unidades locales y mantiene la política de fallo cerrado:

1. `pmgm-qa-update-check.timer` consulta periódicamente GitHub con lectura limitada. Si no existe un corte firmado y aprobado, registra `WAITING_GATES` o `BLOCKED` y termina sin descargar ni usar Docker.
2. `pmgm-qa-update-prepare.service` descarga una sola vez el artefacto exacto, verifica digest, firma, identidad del workflow y manifiesto, y prepara las imágenes sin cambiar servicios.
3. `pmgm-qa-update-apply.service` solo puede consumir un recibo `PREPARED` vigente. Crea respaldos, aplica API y WEB, verifica salud y revierte automáticamente si falla.
4. Un lock común impide ejecuciones simultáneas. Los recibos por SHA hacen idempotente cada etapa.
5. Los cortes con migraciones, cambios de identidad, Keycloak, PostgreSQL, Silo, secretos o capas base quedan en `BLOCKED_REVIEW_REQUIRED`; nunca se aplican automáticamente.
6. Los cortes ordinarios de API/WEB, sin cambios de esquema ni infraestructura, pueden avanzar de `READY` a `APPLIED` sin intervención.
7. La participación normal del Sponsor se limita a la UAT posterior. Solo los estados `BLOCKED_REVIEW_REQUIRED`, `REVERTED` o `UAT_FAILED` requieren acción.

### Instalación inicial única

La activación exige una única intervención administrativa en glmdch para instalar y habilitar las unidades, crear directorios privados, configurar una credencial de solo lectura y ejecutar una simulación sin despliegue. Después de esa ceremonia, no se vuelven a copiar scripts por Termius ni se solicitan contraseñas en cada actualización.

La primera activación debe comprobar:

- usuario y permisos mínimos de cada unidad;
- credencial de GitHub solo lectura, root-only y reemplazable;
- límites de CPU, memoria, disco y tiempo;
- rutas privadas para staging, recibos y respaldos;
- `NoNewPrivileges`, protección del sistema y restricciones de red;
- simulación de bloqueo, preparación, aplicación, fallo de salud y reversión;
- conservación de Keycloak, Master, SMTP, bases y servicios auxiliares.

### Política recomendada

- Consulta automática cada 30 minutos con desfase aleatorio.
- Preparación automática solo cuando el corte firmado lleva al menos 10 minutos estable.
- Aplicación automática en la ventana 02:00–05:00 de Chile.
- Máximo un intento por SHA; un fallo exige un SHA nuevo o desbloqueo documentado.
- UAT y registro al inicio de la jornada siguiente.


## Control dinámico de diferencias — revisión sin instalar (28-10-2026)

A partir de los PR #783 y #785 en dev, el nuevo
`scripts/qa/operators/qa525-current-change-policy.py` (PR #771) sustituye
el criterio de «cambiar solo TARGET» por una revisión read-only del candidato:

- Comprueba HEAD vivo y firmado, con cinco gates, publisher y digest de la
  release a través del verificador del PR #783.
- Compara el SHA **analítico** de la API base con el HEAD vivo mediante
  GitHub Compare; exige que sea ancestro y que la lista de cambios no esté
  truncada (máximo 299 archivos y menos de 250 commits).
- Marca `BLOCKED_REVIEW_REQUIRED` si cambiaron migraciones, modelos/DbContext,
  arranque `Program.cs`, cuentas/identidad, Master, Docker/Compose, configuración
  de compilación o archivos runtime desconocidos; los renombres se revisan.
- Si el corte solo cambia módulos y frontend sin esos riesgos, devuelve
  `ELIGIBLE_FOR_HOST_PREFLIGHT_ONLY`. **No autoriza compilación automática
  ni despliegue**, y sigue exigiendo comprobación del baseline físico, staging
  real con `gh attestation verify`, flags, bloqueo, respaldos, rollback y UAT.
- Consulta otra vez el gate al finalizar para invalidar un corte si cambió
  HEAD, publisher o release durante la revisión. Nunca usa un ancestro viejo
  si el HEAD vigente sigue con workflows pendientes.

Uso documental, sin host y sin credenciales:

~~~bash
python3 scripts/qa/operators/qa525-current-change-policy.py \
  --baseline-sha 6ff4ae0d092d4214df4ea31af484efbc969fde54
~~~

El argumento `--baseline-sha` **no acredita** qué imagen corre realmente en
glmdch; solo representa una referencia histórica. Se requiere el preflight
físico y la lectura segura de su recibo. En el corte de análisis
`dev@700d0e23f9cc98f83bc9d3cef4d55040a2dba446` GitHub Compare
observó 99 archivos cambiados desde `6ff4ae0`, incluidos
`backend/src/PMGM.Api/Program.cs` y
`backend/src/PMGM.Api/Modules/UserAccounts/`; por tanto **este corte no
puede pasar a aplicación desatendida** sin revisión de identidad.

Los operadores históricos `qa525-prepare-0bd927b.py`,
`qa525-prepare-e3e90.py` y `qa525-apply-88fb.py` siguen inutilizables
contra el HEAD actual y NO se ejecutarán. El siguiente incremento los
reemplaza/adapta sin debilitar la conservación del Master, SMTP, siete
servicios, 98 migraciones, respaldos frescos y reversión de API+WEB.
La automatización `systemd` sigue sin instalar ni habilitar.


## Preflight físico de solo lectura sobre el corte firmado actual

**Incidencia #525; PR #771 (28-10-2026).** Se añadió
`scripts/qa/operators/qa525-current-host-preflight.py`: es una puerta
de validación **sin build, descarga, ejecución de instalador ni despliegue**.
No se ha instalado ni ejecutado en glmdch; sus pruebas son herméticas.

Orden obligatorio del nuevo componente:

1. Solo en host glmdch con UID root y archivos locales no simbólicos,
   propiedad root y sin permiso de escritura de grupo/otros; valida SHA-256
   exacto del preflight revisado.
2. Reutiliza el gate firmado de `dev` y el comparador de cambios del
   PR #771, con `BASE_API` histórica como referencia analítica.
3. Si el corte modifica identidad, Master, cuentas, `Program.cs`,
   migraciones, infraestructura u otros elementos sensibles, devuelve
   `BLOCKED_QA525_HOST_PREFLIGHT` **antes** de consultar el estado
   del host; no prepara ningún paquete.
4. Solo para cortes elegibles ejecuta la validación física read-only
   existente, que comprueba siete contenedores, identidad y SMTP
   efectivos, capas Compose, 98 migraciones, histórico G6, temporizador
   inactivo y endpoints públicos.
5. Repite la comprobación íntegra del gate y la clasificación GitHub
   antes de emitir `HOST_READ_ONLY_PREFLIGHT_PASS_NOT_PREPARED`.

Esta salida significa **únicamente** que los controles de lectura
comprobaron el mismo corte y la línea base en ese instante. No equivale
a firma real descargada (staging), respaldo fresco o garantía de rollback;
el resultado incluye `may_build:false`, `may_apply:false`,
`signed_stage_executed:false` y `fresh_backup_created:false`.

Antes de cualquier ejecución real, se requiere la instalación revisada
y autorizada de los tres operadores bajo rutas root-only. El archivo
histórico `qa525-update-readiness-0bd927b.py` se reutiliza como
**auditor de la línea base actual**, no como autorizador de una release
antigua, y se fija su hash; si su hipótesis de host dejó de corresponder,
el procedimiento se bloquea. No se habilita `pmgm-qa-update-*.timer`
ni se realiza una actualización automática con este componente.

Verificación en CI, sin acceder a glmdch:

~~~bash
python3 -m unittest discover -s tests -p test_qa525_current_host_preflight.py -v
~~~

**Pendiente específico:** implementar operador independiente de
preparación de imagen API+WEB del SHA dinámico y su apply/rollback
supervisado. Debe tomar un artefacto con firma real comprobada por
`qa525-stage-current-release.py`, usar evidencia de línea base
recién verificada y proteger Master/SMTP/Keycloak sin mutaciones.
