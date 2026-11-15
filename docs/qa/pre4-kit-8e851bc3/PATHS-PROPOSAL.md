# QA525 PRE-4 — propuesta de rutas y uids (8e851bc3). PROPUESTA: no ejecutada, no autoriza nada

Cada valor sale de un requisito del código del release 8e851bc3 o de un hecho físico ya reportado (#6887458024, #6887477532).
Los 4 requisitos de modo se comprobaron con el código REAL (`go-nonce-journal._journal`: 0700 root OK, 0755 → `NONCE_ROOT_UNTRUSTED`;
`root-launcher._open_archive`: regular, root, nlink 1, sin escritura g/o) en un directorio desechable. Falta la prueba física en glmdch.

| Campo | Valor propuesto | Origen / requisito en código | Estado |
|---|---|---|---|
| `inbox` | `/var/lib/pmgm-qa525/inbox-launcher-8e851bc3` | acordado por ChatGPT; el histórico `inbox-launcher` NO se toca | NUEVO (no existe) |
| `temp_root` | `/var/lib/pmgm-qa525/launcher-8e851bc3/tmp` | `authenticity.app_check_for`: dir uid root, `mode & 0o077 == 0` (0700) | NUEVO |
| `journal_root` | `/var/lib/pmgm-qa525/launcher-8e851bc3/journal` | `go-nonce-journal._journal`: ancestros sin escritura g/o, dir root 0700, sin symlinks | NUEVO (diario separado por corte; nonces son hex64 aleatorios) |
| `lease_dir` | `/var/lib/pmgm-qa525/launcher-8e851bc3/lease` | `root-plane._take_lease`: dir root 0700 y `.launcher-only.lease` regular root 0600 nlink 1; **sin O_CREAT** → debe existir ANTES | NUEVO + archivo |
| `socket_path` | `/run/pmgm-qa525/launcher-8e851bc3.sock` | `root-launcher._accept`: dir padre root-owned sin symlinks (hecho: `/run/pmgm-qa525` root 0755), nombre ≤ 60, sin entrada previa (bind falla si existe); el launcher lo pasa a `peer_uid` 0600 | padre existe |
| `peer_uid` | `1000` | hecho físico: `soporte` UID=GID=1000 | físico |
| `authenticity.app_archive_path` | `/var/lib/pmgm-qa525/launcher-8e851bc3/app/inner.zip` | `_open_archive`: regular, root, nlink 1, sin escritura g/o, tamaño > 0; su sha256 debe ser `9dbd8d5b…07a5` (`AUTH_ARCHIVE_DIGEST_MISMATCH`) | NUEVO (copia root) |
| `drop_uid` / `drop_gid` | `65534` / `65534` (propuesto) | > 0 y ≠ `peer_uid`; el verificador `gh` corre sin privilegios con ese id | **PENDIENTE** `getent passwd 65534`/`getent group 65534` read-only |
| `timeout` | `60` | máximo del contrato (0 < t ≤ 60) | fijo |

Padre `/var/lib/pmgm-qa525` es root 0700 (físico): NO se afloja. Con ese padre solo root recorre todo lo anterior.

## Provisión (SOLO root, SOLO después de: compuerta pre-root, aprobación PO y dictamen ChatGPT; coreutils, un comando a la vez)
```
sudo install -d -m 0700 -o root -g root /var/lib/pmgm-qa525/launcher-8e851bc3
sudo install -d -m 0700 -o root -g root /var/lib/pmgm-qa525/launcher-8e851bc3/tmp
sudo install -d -m 0700 -o root -g root /var/lib/pmgm-qa525/launcher-8e851bc3/journal
sudo install -d -m 0700 -o root -g root /var/lib/pmgm-qa525/launcher-8e851bc3/lease
sudo install -d -m 0700 -o root -g root /var/lib/pmgm-qa525/launcher-8e851bc3/app
sudo install -m 0600 -o root -g root /dev/null /var/lib/pmgm-qa525/launcher-8e851bc3/lease/.launcher-only.lease
sudo install -m 0644 -o root -g root -T /home/soporte/qa525-release-8e851bc3d2f1615a94009591f0781f88d6d3fe6f/<ZIP_INTERNO> /var/lib/pmgm-qa525/launcher-8e851bc3/app/inner.zip
echo "9dbd8d5bec1aa86c07664d1c78137b8798ecfdbd49972f9de531d26f0a0207a5  /var/lib/pmgm-qa525/launcher-8e851bc3/app/inner.zip" | sudo sha256sum -c -
sudo install -d -m 0700 -o root -g root /var/lib/pmgm-qa525/inbox-launcher-8e851bc3
```
(`install` copia como root a una ruta root-only y el `sha256sum -c` se hace sobre la COPIA: sin TOCTOU.) Los 7 archivos del inbox se copian igual (`install -m 0600 -T`) con `sha256sum -c` contra los hashes del config; `go.json`+`go.json.sig` se copian al final, dentro del TTL de 900 s. Si algo ya existe: STOP, no se borra ni sobrescribe.
