#!/usr/bin/env bash
# G3: puente opt-in SOLO QA sintetico glmdch. No modifica main/QA LAB2 ni firewall.
# Uso: sudo bash scripts/qa/install-g3-port80-bridge.sh
set -Eeuo pipefail
umask 077
HOST="sistema.granlogiamixta.cl"
PRIVATE_IP="172.17.5.107"
PRIVATE_CIDR="172.17.5.0/24"
NAME="centenario-qa-g3-port80"
DIR="/etc/centenario/qa-g3-port80"
die() { printf 'BLOQUEADO_G3: %s\n' "$*" >&2; exit 1; }
info() { printf 'G3: %s\n' "$*"; }

[ "$EUID" -eq 0 ] || die "requiere sudo/root"
[ "$#" -eq 0 ] || die "sin argumentos; no acepta cambios arbitrarios"
for cmd in docker ip ss curl install mktemp cp cat grep awk wc seq sleep; do
  command -v "$cmd" >/dev/null 2>&1 || die "falta herramienta: $cmd"
done
ip -4 -o addr show dev ens18 | grep -Fq " $PRIVATE_IP/24 " ||
  die "no se observo interfaz ens18 con IP QA aprobada"
if [ -n "$(ss -ltnH '( sport = :80 )')" ]; then
  die "TCP 80 ya esta ocupado; no reemplazar servicio existente"
fi
if docker container inspect "$NAME" >/dev/null 2>&1; then
  die "contenedor $NAME ya existe; revisar antes de repetir"
fi
docker info >/dev/null 2>&1 || die "Docker no disponible"
[ -r /etc/ssl/certs/ca-certificates.crt ] ||
  die "no existe bundle CA del sistema"

# Identificar un solo gateway QA local y usar SU imagen instalada (sin pulls).
gateway_ids="$(docker ps --format '{{.ID}}|{{.Ports}}' |
  awk -F'|' 'index($2, "127.0.0.1:18443->443/tcp") > 0 { print $1 }')"
[ -n "$gateway_ids" ] || die "gateway QA 127.0.0.1:18443 ausente"
[ "$(printf '%s\n' "$gateway_ids" | wc -l)" -eq 1 ] ||
  die "se esperaba un gateway QA unico, no multiples"
GATEWAY_ID="$gateway_ids"
GATEWAY_IMAGE="$(docker inspect --format '{{.Image}}' "$GATEWAY_ID")"
docker exec "$GATEWAY_ID" caddy version >/dev/null 2>&1 ||
  die "gateway no ejecuta Caddy"
docker image inspect "$GATEWAY_IMAGE" >/dev/null 2>&1 ||
  die "imagen local de gateway no encontrada"

# Combinar CA publicas del host con CA local Caddy si existe: nunca copiar llaves.
tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT
cp /etc/ssl/certs/ca-certificates.crt "$tmp/trust.pem"
if docker cp "$GATEWAY_ID:/data/caddy/pki/authorities/local/root.crt" \
  "$tmp/caddy-local-root.crt" >/dev/null 2>&1; then
  cat "$tmp/caddy-local-root.crt" >> "$tmp/trust.pem"
  info "CA local publica de Caddy incorporada"
fi
status="$(curl --noproxy '*' --silent --show-error --max-time 12 \
  --cacert "$tmp/trust.pem" \
  --resolve "$HOST:18443:127.0.0.1" \
  --output /dev/null --write-out '%{http_code}' \
  "https://$HOST:18443/health/web")" ||
  die "TLS o CA del gateway privado no verificados; no abrir puerto 80"
[ "$status" = 200 ] || die "gateway privado no responde 200 (fue $status)"

install -d -m 0755 "$DIR"
install -m 0444 "$tmp/trust.pem" "$DIR/trust.pem"
cat > "$DIR/Caddyfile" <<'CADDY'
{
    admin off
    auto_https off
}
http://sistema.granlogiamixta.cl:80 {
    bind 172.17.5.107
    # Usar peer TCP real, nunca cabeceras declaradas por cliente.
    @outside not remote_ip 172.17.5.0/24
    @deny_identity path /identity/admin /identity/admin/* /identity/realms/master /identity/realms/master/*
    route {
      respond @outside "Forbidden" 403
      respond @deny_identity "Not Found" 404
      reverse_proxy https://127.0.0.1:18443 {
        header_up Host sistema.granlogiamixta.cl
        header_up X-Forwarded-Proto https
        header_up X-Forwarded-Host sistema.granlogiamixta.cl
        header_up -Forwarded
        header_up -X-Real-IP
        transport http {
            tls_server_name sistema.granlogiamixta.cl
            tls_trust_pool file /etc/caddy/trust.pem
        }
      }
    }
}
CADDY
chmod 0444 "$DIR/Caddyfile"
docker run --rm --pull never --network none --read-only --tmpfs /config:rw,nosuid,nodev,size=2m --tmpfs /data:rw,nosuid,nodev,size=2m \
  --mount type=bind,source="$DIR/Caddyfile",target=/etc/caddy/Caddyfile,readonly \
  --mount type=bind,source="$DIR/trust.pem",target=/etc/caddy/trust.pem,readonly \
  "$GATEWAY_IMAGE" caddy validate --config /etc/caddy/Caddyfile --adapter caddyfile ||
  die "Caddyfile invalido; no se instalo puente"

docker run -d --name "$NAME" --pull never --network host \
  --restart no --read-only --user 10001:10001 \
  --tmpfs /config:rw,nosuid,nodev,size=2m --tmpfs /data:rw,nosuid,nodev,size=2m \
  --cap-drop ALL --cap-add NET_BIND_SERVICE --security-opt no-new-privileges \
  --mount type=bind,source="$DIR/Caddyfile",target=/etc/caddy/Caddyfile,readonly \
  --mount type=bind,source="$DIR/trust.pem",target=/etc/caddy/trust.pem,readonly \
  "$GATEWAY_IMAGE" caddy run --config /etc/caddy/Caddyfile --adapter caddyfile >/dev/null ||
  die "no se pudo iniciar puente"

# Health con ventana de arranque acotada: docker run -d no garantiza Caddy LISTEN.
# Por seguridad el nuevo contenedor NO tiene politica de reinicio hasta pasar health.
# Capturar diagnosticos de arranque ANTES de limpiar contenedor fallido.
status="000"
ready=false
for intento in $(seq 1 24); do
  status="$(curl --noproxy '*' --silent --max-time 2 \
    --resolve "$HOST:80:$PRIVATE_IP" \
    --output /dev/null --write-out '%{http_code}' \
    "http://$HOST/health/web")" || status="000"
  if [ "$status" = 200 ]; then
    ready=true
    break
  fi
  sleep 0.5
done
if [ "$ready" != true ]; then
  info "DIAGNOSTICO: no se obtuvo HTTP 200 por TCP80 en 24 intentos (ultimo: $status)"
  if [ -r /proc/sys/net/ipv4/ip_unprivileged_port_start ]; then
    info "Host ip_unprivileged_port_start=$(cat /proc/sys/net/ipv4/ip_unprivileged_port_start)"
  fi
  docker inspect --format 'G3_CONTENEDOR estado={{.State.Status}} exit={{.State.ExitCode}} error={{.State.Error}}' "$NAME" >&2 || true
  docker logs --tail 20 "$NAME" >&2 || true
  docker rm -f "$NAME" >/dev/null 2>&1 || true
  die "puente no paso health, contenedor nuevo retirado; revisar DIAGNOSTICO"
fi
docker update --restart unless-stopped "$NAME" >/dev/null ||
  { docker rm -f "$NAME" >/dev/null 2>&1 || true; die "no se pudo habilitar reinicio controlado"; }
info "PUENTE_LOCAL_PASS TCP $PRIVATE_IP:80 -> HTTPS 127.0.0.1:18443"
info "IP privada solamente; origen permitido $PRIVATE_CIDR"
info "No se modificaron DNS, NAT, UFW, main, volumenes, BD ni gateway QA"
info "Probar desde otra red: https://$HOST/health/web"
info "G3 publico, IP cliente, G8 avanzada y G10 siguen sin certificar"
# Reversion: sudo docker rm -f centenario-qa-g3-port80
