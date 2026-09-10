# Proyecto Milenio — paquete de implementación v1.0.0-rc1

## Qué contiene el paquete

El artefacto `PMGM-v1.0.0-rc1-definitive-deployment.zip` se genera automáticamente desde CI e incluye:

```text
PMGM-v1.0.0-rc1/
├── repository/
│   └── PMGM-v1.0.0-rc1.bundle
├── operator/
│   ├── PMGM-VM-DEFINITIVA-V100.md
│   ├── PMGM-PILOT-OPERACIONAL-V032.md
│   ├── PMGM-UAT-V100-RC1.md
│   ├── PMGM-DEPLOYMENT-PACKAGE-1.0.0-rc1.json
│   ├── PMGM-UAT-1.0.0-rc1.template.json
│   └── scripts/
│       ├── preflight-definitive-host.sh
│       └── update-definitive.sh
├── APPLICATION-SOURCE-SHA.txt
├── README-INSTALAR.txt
└── CHECKSUMS-SHA256.txt
```

El bundle Git conserva la referencia exacta del candidato:

`739ba0b3a8d89087177b1edabd61c2981eed08a2`

## Qué debes tener antes de comenzar

1. VM Linux definitiva.
2. Recomendado: 8 vCPU, 32 GB RAM, 500 GB SSD/NVMe expandible.
3. FQDN institucional definido.
4. DNS apuntando a la VM/gateway.
5. TCP 80/443 disponibles.
6. SSH administrativo restringido.
7. Docker Engine y Compose v2.
8. Git, Python 3, curl, OpenSSL, sha256sum y tar.
9. destino de backup **fuera de la VM**.
10. correo del primer administrador institucional.
11. cuatro secretos/contraseñas aleatorias y distintas, nunca guardadas en Git.
12. repositorio configurado como privado antes de incorporar datos reales, salvo decisión institucional expresa distinta.

## Instalación desde el bundle

Copiar el ZIP a la VM por un canal administrativo autorizado y validar primero `CHECKSUMS-SHA256.txt`.

Crear ubicación:

```bash
sudo mkdir -p /opt/pmgm
sudo chown pmgm:pmgm /opt/pmgm
```

Clonar desde el bundle:

```bash
cd /opt/pmgm
git clone --branch pmgm-package-rc1 /ruta/al/paquete/PMGM-v1.0.0-rc1/repository/PMGM-v1.0.0-rc1.bundle app
cd app
git checkout 739ba0b3a8d89087177b1edabd61c2981eed08a2
test "$(git rev-parse HEAD)" = "$(cat /ruta/al/paquete/PMGM-v1.0.0-rc1/APPLICATION-SOURCE-SHA.txt)"
```

Después configurar `/etc/pmgm/pmgm.env` según `infrastructure/pilot.env.example` y seguir `operator/PMGM-VM-DEFINITIVA-V100.md`.

## Flujo inicial

```text
validar paquete
→ clonar bundle
→ comprobar SHA
→ configurar secretos/FQDN
→ preflight
→ start
→ smoke
→ backup inicial
→ copiar backup fuera de VM
→ UAT 20 casos
→ aprobación Sponsor/Product Owner
→ promoción in-place a v1.0.0 estable
```

## Actualizaciones futuras

Una vez que la instalación esté conectada al repositorio privado/autorizado, configurar `origin` con credencial de sólo lectura o deploy key.

La VM nunca debe seguir `dev` automáticamente. Cada actualización utiliza un tag/SHA aprobado y el procedimiento:

```text
backup + verify → fetch versión aprobada → preflight → deploy → smoke
```

El script `scripts/update-definitive.sh` se incorpora al árbol de desarrollo y quedará disponible en la versión estable. Exige `--approved`, hace backup antes del cambio y no restaura automáticamente una base de datos si una migración pudo ejecutarse.

## Datos reales

El paquete RC está listo para implementación y UAT. **No constituye aún autorización para cargar datos personales reales.** Para ello se requiere completar los gates de Ley 21.719 y la aprobación institucional definida en Proyecto Milenio.
