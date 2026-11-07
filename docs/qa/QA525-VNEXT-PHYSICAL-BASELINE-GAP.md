# QA525 vNext — evidencia física faltante para la línea base (solo diseño y pruebas)

**Alcance:** complemento hermético al Issue #822. No ejecuta Docker, no instala, no genera nuevos recibos, no solicita clave privada, no autoriza root ni cambia `glmdch`. Fuente exacta de desarrollo: `dev@95ffc07313e6ac2cced929b74a213b7dd00c2a59`.

## Qué se conoce y qué NO

El operador y Claude registraron en GitHub Issue #525 comentarios [#6699949734](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/525#issuecomment-6699949734) y [#6699974381](https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/525#issuecomment-6699974381) siete servicios **running** y capas Compose API=6, WEB=5, con los tres overrides históricos image-only en orden 725bc319, 41d9c490 y 6eed4f00. Los hashes de imágenes y las etiquetas finales concuerdan con 6eed4f00; la existencia de imágenes previas **NO** demuestra rollback ni restauración de datos.

El script `scripts/qa/operators/qa525-host-inventory-readonly.py` tiene un `SERVICES` de **7** (api, web, keycloak, postgres, minio, gateway, clamav) y comprueba esos siete; su salida PASS **no es** un PASS para el motor de diez servicios. `qa525-vnext-state-reader.py` exige además **tres contenedores oneshot exactos**: `db-grants`, `migrator` y `minio-init` con estado `exited` y código `0`. No se debe omitir esa evidencia ni inferirla de CI.

El lector compartido `qa-lite-compose-adapter.py::metadata(run)` usa `ps -a` y campos `inspect` filtrados: cabe efectuar una **lectura complementaria**, sin volver a ejecutar la fase 1 completa ni consultar `Config.Env`. Un operador con el canal autorizado deberá verificar con método previamente revisado los **10 nombres exactos** (7 activos + 3 terminados), IDs, estado/código de salida y que no hay extras. Publicar solo un informe saneado (nunca JSON íntegro ni secretos) y registrar SHA del procedimiento/host, fecha y resultado PASS/STOP. **No se ha realizado esa lectura en este PR.**

## Contrato de capas reales y pruebas herméticas

El test `tests/test_qa525_vnext_physical_baseline_contract.py` recrea solo la **forma** de las etiquetas saneadas, con nombres y SHA sintéticos, sin copiar archivos reales:

- Siete `running` más tres oneshot `exited/0`: `baseline_from_rows`, `validate_baseline` y verificación `ROLLED_BACK` pasan.
- Contenedor faltante, adicional o oneshot en estado incorrecto hacen STOP.
- API con seis capas y WEB con cinco pasan como baseline **únicamente si ambas comparten el primer Compose base**; no se validan por simple conteo.
- Pila reordenada, truncada o con capa fantasma no se acepta como `ROLLED_BACK`.
- Target propuesto conserva el historial completo y agrega una capa nueva por servicio; se comprueba **solo la forma**, no la autenticidad de los archivos ni la autorización de aplicarlo.

**Crítico:** la ruta antigua mutante `qa-lite-compose-adapter.py::ComposeWebAdapter.reverify` asume como máximo un override después del binding; **no es** la ruta física vNext para pilas acumuladas de 6/5. Reusar únicamente `metadata()` con controles de autenticidad; todo ejecutor físico requerirá adaptador específico, hashes reales de cada JSON, locks/lease/cgroup, ancla OPS/APP independiente, G6 offhost y restauración real, ensayo de rollback y GO Sponsor específico.

**Estado:** QA físico sigue `NO-GO` por Issue #822. Este test no modifica `main`, producción, identidad, DB, Compose ni servidor, y no obliga a un nuevo rebuild de la release ya publicada para `dev@95ffc073`.
