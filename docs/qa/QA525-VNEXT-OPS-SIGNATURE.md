# QA525 vNext – contrato de firma OPS (INACTIVO, #822 puntos 6/7)

`scripts/qa/qa525-vnext-ops-signature.py` es una biblioteca sin efectos: no abre archivos, no ejecuta procesos, no usa red. No autoriza instalar ni aplicar.

## Problema
`qa525-vnext-cache.py::load_verified_ops` confía en un recibo root 0600 (`OPS_SIGNED_VERIFIED`) sin probar **quién** lo firmó, y OPS y APP no son identidades distintas.

## Qué verifica
Una firma separada Ed25519 de `ssh-keygen -Y` sobre los **bytes exactos** del recibo (namespace `qa525-ops-v2@pmgm`, principal `qa525-ops`):
- El archivo `allowed_signers` tiene exactamente una línea, con namespace fijado y clave `ssh-ed25519` de 32 bytes, sin comentario.
- Su huella SHA256 coincide con la huella que el Sponsor fija fuera de banda, y **no** está entre las huellas APP (Sigstore/GitHub) entregadas.
- El verificador inyectado (futuro `ssh-keygen -Y verify`, recibo por stdin; ver `ssh_keygen_argv`) devuelve código 0 y la salida exacta `Good "…" signature for qa525-ops with ED25519 key SHA256:…`.
- El recibo es JSON estricto (sin claves duplicadas ni NaN) con `schema=qa525-ops-v2`.

La respuesta siempre trae `may_apply=False` y marca como NO verificados aquí: procedencia de la huella, identidad APP, custodia de la clave y aprobación del Sponsor.

## Qué NO hace
Quién guarda la clave privada OPS es decisión del Sponsor (fuera de línea, nunca en glmdch/GitHub/Drive/chat). La huella fijada debe llegar por un canal autenticado independiente. Conectar este módulo a `load_verified_ops` y al bootstrap root es trabajo futuro con revisión y GO físico separados.

## Pruebas
`tests/test_qa525_vnext_ops_signature.py`: caminos con `ssh-keygen` real (firma buena, byte alterado, namespace distinto, otra clave, huella APP reutilizada, huella no coincidente; se omiten si no hay `ssh-keygen`) y casos herméticos de entrada, salida exacta, JSON y ausencia de superficie de procesos/archivos/red.
