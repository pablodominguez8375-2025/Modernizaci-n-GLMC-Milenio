# #754 — Operador QA reversible solo API, 27-10-2026 — PR draft #761

## Contexto y fuentes verificadas
- GitHub issue #754 reservado a `agente:chatgpt`; se abrió PR DRAFT #761 `feature/qa754-api-identity-activation-20261027-gpt` desde `dev@731fa29ee30a18a13c50b5a03b5847746242313f`; `main@6dfb9546a4873baff15955cf86abfd7d47e3d111` protegida. No se fusionó ni desplegó al terminar.
- QA físico glmdch `centenario-qa-lab2`: 2 capas activas `compose.recovery.json` + `api-<SHA>.json`, API/WEB sincronizadas 6ff4ae0 según journal previo, timer WEB-only inactivo. Cliente técnico Keycloak `pmgm-account-admin` ya creado y probado. **NO** duplicar cliente, cuenta Máster, realm ni Taller Libertad 23.
- Stage exclusivo identidad y LoginUrl `/etc/pmgm/qa754-identity-overlay.stage.json` root0600, ya generado y validado por Sponsor. No contiene configuración SMTP. Nunca exportarlo.
- SMTP Google STARTTLS de prueba entregado y recibido por Sponsor 27-10 08:39 CL; **no** prueba por sí solo el envío de clave inicial ni SMTP de recuperación Keycloak.
- G6 nuevo tras SMTP: `centenario-qa-g6-CIFRADO-20261027T115953Z.tar.gz`, 1.027.549 bytes, SHA256 `e4184cb798e3b44414c09d85735db6e6b2002e75027de67da8ed5b930a7ca8a6`, copia privada Drive ID `15UgqmcXRu4RR5ypE1cIu51HZgh7EqHyU` verificada bytes SHA remoto = host. Respaldo de bases `pmgm`, `pmgm_keycloak`, globals y Silo + Restic check/restore de archivos local PASS. **NO CERTIFICADA** restauración integral aislada Postgres/Silo ni custodia de clave aparte/DR en host nuevo.

## Entrega de código en PR
- `scripts/qa/operators/pmgm-qa754-api-identity-activate.py`, Python stdlib, SHA256 de bytes exactos `8ae051c4a66d8787319e71844f9e858f44c7ff37517050d2cb3bb74ce0591e28`, blob Git `7b0a2fdf012429889274b67936a6cc16c225555a`. Instalación futura debe permanecer root:root modo0700, nunca desde archivo mutable de usuario.
- `tests/test_qa754_api_identity_activate.py`, SHA256 `314c4c0d302d77dcfc59dec4683ec25aa1e627a51833e6c35ace0b532f8d30b2`, blob Git `55ebcc37dd2141f535a161974ddf6470ea079552`.
- **20/20 tests sintéticos locales PASS** vía `python3 -m unittest discover -s tests -p test_qa754_api_identity_activate.py` en árbol equivalente. `python3 -m py_compile` PASS. No se corrió contra Docker físico ni se acreditó CI de PR. Gates CI/Showcase/QA Installable exact-head pendientes, por lo que PR continúa draft. No hay cambios frontend ni DEMO Pages.
- Modo predeterminado `--check` SOLO LECTURA: exige tag+ID OCI real igual a config Compose, runtime env igual a baseline+defaults imagen, migraciones en arranque deshabilitadas, project labels y 2 capas exactas, API/Web/Keycloak/Postgres/Minio running, timer WEB inactivo, root0600 stage y secreto coincidentes, diferencia tercera capa SOLO Identity/Issuer/ClientId/ClientSecret/LoginUrl, SHA G6 nuevo local y GET health/ready HTTPS 200. Nunca imprime valores de secretos, Docker inspect crudo, Compose, tokens o datos de usuarios.
- `--apply --confirm QA754-ACTIVAR-SOLO-API` disponible **solo para ejecución futura explícita por operador autorizado en QA**. Ejecuta `docker compose -p ... -f capa1 -f capa2 -f stage up -d --no-deps --no-build --force-recreate api` tras checks. Comprueba imagen/variables/estado de 4 servicios no objetivo y health público; ante falla intenta rollback solo API con las dos capas originales; devuelve códigos fijos. **El rollback no está probado físicamente y no puede garantizar recuperarse de kill -9, fallo Docker/disk o caída completa del host**. No reactivar timer WEB ni ejecutar migraciones, no modificar Keycloak/BD/SMTP ni main.
- Se recomienda ejecutar PRIMERO `--check` (sin cambios) y revisar salida antes de autorizar la opción `--apply`. Si BLOCKED detener, no forzar.

## Instalación operativa futura por Sponsor desde Termius (NO EJECUTADA)
1. Transferir archivo a `/home/soporte/pmgm-qa754-api-identity-activate.py` vía SFTP Android; comparar `sha256sum` con huella pinneada.
2. Solo si coincide: `sudo install -o root -g root -m 0700 /home/soporte/pmgm-qa754-api-identity-activate.py /opt/centenario/qa-tools/pmgm-qa754-api-identity-activate.py`; luego `sudo sha256sum` del destino y compararlo.
3. Ejecutar `sudo python3 /opt/centenario/qa-tools/pmgm-qa754-api-identity-activate.py --check` y compartir ÚNICAMENTE estados filtrados. **NO** ejecutar `--apply` en esta entrega sin contrastar los resultados y completar evaluación de riesgo QA.

## Criterios para cerrar #754
- Tras intervención supervisada: `GET /api/system/user-accounts/eligible-members` con Máster verifica `deliveryConfigured` (y si QA ya integra #755, `identityConfigured` y `mailConfigured`), sin imprimir datos de personas.
- Cuenta y buzón *exclusivamente sintéticos autorizados*: un alta, mensaje temporal recibido, cambio de clave obligatorio y acceso restringido; rechazo de duplicado/retirado/perfil no permitido; recuperación Keycloak por canal SMTP separado; no mostrar secretos.
- Mapper `pmgm-account-kind` PR #757 en realm físico aún exige despliegue/validación separada. Restauración aislada real G6/DR y gestión de llave fuera de host siguen abiertas para control de continuidad.
- Cierre de esta entrega = PR draft con código+tests y registro Drive, **no** implementación QA, no merge, no despliegue ni UAT. La próxima IA debe continuar desde issue #754 / PR #761 sin rehacer respaldos ya validados.
