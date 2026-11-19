#!/usr/bin/env bash
# Evidencia de la GUARDA DE NAMESPACE de test-a0.sh (v5). Corre en una VM/sandbox DESCARTABLE como root. Ningún caso monta nada en el namespace del host: los casos 11 y 12 montan SOLO dentro de un namespace propio del worker (efímero);
# si la guarda fallara y montara en el host, se detecta al comparar el mount namespace y /proc/self/mountinfo del host antes/después.
# Uso (root): bash scripts/qa/a0/tests/ns-guard-test.sh
set -u
HERE="$(cd "$(dirname "$0")" && pwd)"; T="$HERE/test-a0.sh"; fails=0; n=0
[ "$(id -u)" = "0" ] || { echo "requiere root"; exit 9; }
host_state() { echo "$(readlink /proc/self/ns/mnt) $(sha256sum < /proc/self/mountinfo | cut -c1-16) $(findmnt -n -o FSTYPE /tmp) $(findmnt -n -o FSTYPE /var/tmp)"; }
chk() { n=$((n+1)); if [ "$1" = ok ]; then echo "  PASS: $2"; else echo "  FAIL: $2"; fails=$((fails+1)); echo "$3" | tail -3; fi; }
BEFORE="$(host_state)"
tok() { printf 'mnt=%s\nnet=%s\npid=%s\n' "$(readlink /proc/self/ns/mnt)" "$(readlink /proc/self/ns/net)" "$(readlink /proc/self/ns/pid)"; }

echo "1) invocación directa del trabajador desde el host (sin FD 3) => se niega"
out="$(bash "$T" --a0-worker 3</dev/null 2>&1)"; rc=$?; [ $rc -eq 9 ] && echo "$out" | grep -q "GUARDA" && chk ok "rechazado (exit 9)" || chk bad "rc=$rc" "$out"
echo "2) A0_TEST_NS=1 (variable del llamador) + trabajador directo => ignorada, se niega"
out="$(A0_TEST_NS=1 bash "$T" --a0-worker 3</dev/null 2>&1)"; rc=$?; [ $rc -eq 9 ] && echo "$out" | grep -q "GUARDA" && chk ok "rechazado (exit 9)" || chk bad "rc=$rc" "$out"
echo "3) trabajador directo con un token FALSO que dice ser de otro namespace, desde el host => se niega (no es PID 1)"
out="$(printf 'mnt=mnt:[1]\nnet=net:[2]\npid=pid:[3]\n' | bash -c 'exec 3<&0; bash "$0" --a0-worker' "$T" 2>&1)"; rc=$?; [ $rc -eq 9 ] && echo "$out" | grep -q "GUARDA" && chk ok "rechazado (exit 9)" || chk bad "rc=$rc" "$out"
echo "4) pid namespace nuevo SIN mount namespace propio (el caso peligroso) con token falso => se niega"
out="$(tok | unshare --pid --fork bash -c 'exec 3<&0; exec bash "$0" --a0-worker' "$T" 2>&1)"; rc=$?; [ $rc -eq 9 ] && echo "$out" | grep -q "GUARDA" && chk ok "rechazado (exit 9)" || chk bad "rc=$rc" "$out"
echo "5) pid+mount namespace pero montajes COMPARTIDOS (sin --propagation private) con --mount-proc => se niega"
# El caso se ejecuta DENTRO de un mount namespace externo privado desechable: aunque el worker (o --mount-proc) propagara montajes, no pueden llegar al host.
out="$(unshare --mount --propagation private bash -c 'tok() { printf "mnt=%s\nnet=%s\npid=%s\n" "$(readlink /proc/self/ns/mnt)" "$(readlink /proc/self/ns/net)" "$(readlink /proc/self/ns/pid)"; }; tok | unshare --pid --fork --mount --propagation shared --mount-proc bash -c "exec 3<&0; exec bash \"\$0\" --a0-worker" "$1"' _ "$T" 2>&1)"; rc=$?; [ $rc -eq 9 ] && echo "$out" | grep -q "GUARDA" && chk ok "rechazado (exit 9)" || chk bad "rc=$rc" "$out"
echo "6) el ARRANQUE con A0_TEST_NS=1 en el entorno igual crea un namespace NUEVO (la variable no se consulta)"
out="$(A0_TEST_NS=1 bash "$T" --ns-selfcheck 2>&1)"; rc=$?; w="$(echo "$out" | sed -n 's/^NS_OK worker_mnt=\(mnt:\[[0-9]*\]\).*/\1/p')"; h="$(readlink /proc/self/ns/mnt)"
[ $rc -eq 0 ] && [ -n "$w" ] && [ "$w" != "$h" ] && chk ok "mount ns del trabajador ($w) != host ($h)" || chk bad "rc=$rc" "$out"
echo "7) fallo de initdb (binarios inexistentes): sale 9, sin cluster ni procesos activos y sin montajes en el host"
out="$(A0_PGBIN=/nonexistent bash "$T" 2>&1)"; rc=$?; [ $rc -eq 9 ] && echo "$out" | grep -q "initdb falló" && ! pgrep -f "a0-suite" >/dev/null && chk ok "exit 9, sin procesos a0-suite" || chk bad "rc=$rc" "$out"
echo "8) estático: la suite no usa rm -rf, glob de borrado, drop database/role ni toca pg_hba/pgdata del host"
if ! grep -nE 'rm -rf|drop (database|role)|pg_hba|/var/tmp/pgdata' "$T" | grep -v '^[0-9]*:#' | grep -v 'GUARDA\|\[ ! -e /var/tmp/pgdata \]'; then chk ok "sin patrones prohibidos"; else chk bad "hay patrones prohibidos"; fi

# Directorio de trabajo PROPIO (mktemp -d, root:root 0700); se verifica identidad y se borra al final solo por rutas exactas registradas (sin rm -r).
pgrep -f "a0-suite" >/dev/null && { echo "ABORTA: ya hay procesos a0-suite ajenos; no se ejecuta (ni se mata nada)"; exit 9; }
FAKE="$(mktemp -d /root/a0-guard.XXXXXX)" || exit 9
FINO="$(stat -c '%u:%a:%d:%i' "$FAKE")"; case "$FINO" in 0:700:*) ;; *) echo "ABORTA: $FAKE no es root:0700"; exit 9;; esac
KLOG="$FAKE/kill.out"
guard_cleanup() { [ "$(stat -c '%u:%a:%d:%i' "$FAKE" 2>/dev/null)" = "$FINO" ] || { echo "limpieza OMITIDA: $FAKE cambió de identidad"; return 0; }
  for f in unshare mount count kill.out; do [ -f "$FAKE/$f" ] && [ ! -L "$FAKE/$f" ] && rm -f -- "$FAKE/$f"; done; rmdir -- "$FAKE" 2>/dev/null; return 0; }
trap guard_cleanup EXIT
echo "10) fallo de unshare (unshare simulado que falla): el arranque sale != 0 sin worker, sin montajes ni procesos"
printf '#!/bin/sh\nexit 1\n' > "$FAKE/unshare"; chmod +x "$FAKE/unshare"
out="$(PATH="$FAKE:$PATH" bash "$T" 2>&1)"; rc=$?; [ $rc -ne 0 ] && ! pgrep -f "a0-suite" >/dev/null && [ "$(host_state)" = "$BEFORE" ] && chk ok "rc=$rc, sin residuos, host idéntico" || chk bad "rc=$rc" "$out"
rm -f "$FAKE/unshare"
echo "11) fallo del SEGUNDO mount (mount simulado: 1.º real, 2.º falla; el 1.º monta SOLO dentro del namespace propio del worker): exit 9, sin suite activa, host idéntico"
cat > "$FAKE/mount" <<EOM
#!/bin/sh
C="$FAKE/count"; n=\$(cat "\$C" 2>/dev/null || echo 0); n=\$((n+1)); echo \$n > "\$C"
[ "\$n" -ge 2 ] && exit 1
exec /usr/bin/mount "\$@"
EOM
chmod +x "$FAKE/mount"; rm -f "$FAKE/count"
out="$(PATH="$FAKE:$PATH" bash "$T" 2>&1)"; rc=$?; [ $rc -eq 9 ] && echo "$out" | grep -q "no se pudieron montar" && ! pgrep -f "a0-suite" >/dev/null && [ "$(host_state)" = "$BEFORE" ] && chk ok "exit 9, sin residuos, host idéntico" || chk bad "rc=$rc" "$out"
rm -f "$FAKE/mount" "$FAKE/count"
echo "12) SIGKILL del worker: identidad PID+starttime+ppid+NSpid+namespaces validada ANTES de la señal (sin kill por patrón)"
bash "$T" >"$KLOG" 2>&1 &
BGPID=$!
WP=""; PGUP=no
for i in $(seq 1 80); do
  pgrep -f "a0-suite" >/dev/null && PGUP=si
  for p in $(pgrep -P "$BGPID"); do grep -Eq '^NSpid:.*[[:space:]]1$' "/proc/$p/status" 2>/dev/null && WP="$p"; done
  [ -n "$WP" ] && [ "$PGUP" = si ] && break; sleep 0.5
done
if [ -n "$WP" ]; then
  ST1="$(awk '{print $22}' "/proc/$WP/stat" 2>/dev/null)"; PP1="$(awk '{print $4}' "/proc/$WP/stat" 2>/dev/null)"; MN1="$(readlink "/proc/$WP/ns/mnt")"
  HOSTMNT="$(readlink /proc/self/ns/mnt)"
  # re-validación inmediatamente antes de la señal: mismo starttime, hijo directo de nuestro BGPID, PID 1 en su pid ns y mount ns distinto del host
  if [ -n "$ST1" ] && [ "$PP1" = "$BGPID" ] && [ "$MN1" != "$HOSTMNT" ] && [ "$(awk '{print $22}' "/proc/$WP/stat")" = "$ST1" ] && grep -Eq '^NSpid:.*[[:space:]]1$' "/proc/$WP/status"; then kill -9 "$WP"; else echo "identidad del worker NO validada: no se envía señal"; WP=""; fi
fi
wait "$BGPID" 2>/dev/null; sleep 1
if [ -n "$WP" ] && [ "$PGUP" = si ] && ! pgrep -f "a0-suite" >/dev/null && [ "$(host_state)" = "$BEFORE" ] && ! ls -d /tmp/a0-suite.* >/dev/null 2>&1; then chk ok "worker $WP (starttime $ST1, ppid $PP1) muerto con suite activa; 0 procesos, 0 dirs en /tmp del host, host idéntico"; else chk bad "WP=$WP PGUP=$PGUP" "$(tail -3 "$KLOG")"; fi
echo "13) limpieza no alcanza recursos externos: el trap solo recorre CREATED[] (rutas exactas) y no hay rm -r"
grep -q 'for p in "${CREATED\[@\]:-}"' "$T" && ! grep -nE 'rm -(r|f[a-z]*r)' "$T" | grep -v '^[0-9]*:#' | grep -q . && chk ok "estático" || chk bad "estático"
AFTER="$(host_state)"
echo "9) el mount namespace y los montajes del HOST no cambiaron: antes='$BEFORE' después='$AFTER'"
[ "$BEFORE" = "$AFTER" ] && chk ok "idénticos" || chk bad "cambiaron"
echo; echo "casos=$n fallos=$fails"; [ $fails -eq 0 ] && echo "GUARDA DE NAMESPACE VERIFICADA" || exit 1
