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
