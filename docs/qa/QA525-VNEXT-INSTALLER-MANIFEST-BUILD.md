# QA525 vNext — generador offline del manifiesto de instalador PRE-4

Estado: código INACTIVO. NO_GO_APPLY. No autoriza root, glmdch, GO, firmas ni main (#525, contrato de ChatGPT 6880248111).

## Qué hace
`scripts/qa/operators/qa525-vnext-installer-manifest-build.py` produce los bytes canónicos del manifiesto v3
(`{config,files,helpers,interpreter}`) que verifica `qa525-vnext-root-bootstrap.py`. Solo lee; escribe un único
archivo nuevo (0600, creación exclusiva, sin seguir symlinks). Sin red, sin root, sin extraer el ZIP, sin ejecutar
ningún módulo del paquete.

## Entradas (todas físicas/externas; faltantes => `BUILD_MISSING_PHYSICAL_INPUTS <lista>`, exit 3)
- `--asset` ZIP externo exacto + `--asset-sha256` (integridad, NO procedencia) + `--source-sha`. Debe estar verificado
  antes con Sigstore (`SIGSTORE_VERIFIED_BY_BUILDER=false`). Se compara contenido externo, BUILD-INFO (SOURCE_SHA y marca
  de silo, una vez cada uno), MANIFEST.sha256 canónico y conjunto de archivos idéntico, hash de cada miembro; rechaza
  traversal, symlinks, duplicados, tipos raros y tamaños excesivos.
- `--config` JSON del Sponsor (<=64 KiB). Forma validada contra el contrato de `launch-config` (paridad probada en tests);
  `launcher_pins.release_asset_sha256` debe ser `sha256:<asset>`. Nunca se imprime su contenido, solo su nombre y sha256.
- `--interpreter RUTAREAL=SHA256` y `--helper gh=RUTAREAL=SHA256 --helper ssh-keygen=RUTAREAL=SHA256`: valores de
  inventario read-only del host (el launcher fija ambos helpers). No se abre ningún binario aquí.
- `--output` archivo nuevo.

## Cierre de archivos
Cierre transitivo estático (nombres `qa525-vnext-*.py` como constantes de cadena, sin docstrings) desde launcher,
launch-config y los módulos hijo de cada alcance. Falta de miembro => STOP. El bootstrap no entra en `files` (se
instala aparte; su sha256 se imprime). En b0fbb6aa: 48 archivos.

## Qué NO hace
No instala INSTALL_DIR (fase distinta, root, con aprobación), no fija ni autoriza el pin: el hash impreso es solo
CANDIDATO. El Sponsor lo recomputa en una máquina independiente y lo fija fuera de banda (Bitwarden). `BOOT_CHECK_OK`
no concede GO. Un corte nuevo de dev exige regenerar release, manifiesto y pin.
