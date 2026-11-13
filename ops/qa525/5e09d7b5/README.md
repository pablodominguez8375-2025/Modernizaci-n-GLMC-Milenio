# Guiones operativos QA525 — corte 5e09d7b5 (rama de entrega, NO forma parte de dev ni del corte firmado)

Copia byte a byte de los guiones publicados y revisados en el Issue #525. Sirve solo para descargarlos con `curl`
desde una URL fijada por commit y cotejar su SHA256; no cambia `dev`, no dispara CI y no autoriza nada.
Cada guion solo se ejecuta tras la revisión de ChatGPT y la decisión del Sponsor registradas en #525.

- G6 `init` (#525 comentario 6835837067): `qa525-g6-init-kit-A`, `qa525-g6-init-kit-B`, `qa525-g6-go-termux`, `qa525-g6-init-go-host`.

`SHA256SUMS` contiene los hashes declarados en el Issue.
- G6 `backup` (propuesta v2, pendiente de revisión en #525): `qa525-g6-backup-kit-A`, `qa525-g6-backup-kit-B`, `qa525-g6-backup-go-host` (ventana MinIO en una sesión; GO-T es el mismo de `init`).
