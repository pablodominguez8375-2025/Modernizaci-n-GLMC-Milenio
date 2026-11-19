#!/usr/bin/env bash
# Evidencia de la GUARDA DE NAMESPACE de test-a0.sh (v5). Corre en el HOST y es inocuo: NINGUNA de estas pruebas puede montar nada;
# si la guarda fallara y montara, se detecta al comparar el mount namespace y /proc/self/mountinfo del host antes/después.
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
out="$(tok | unshare --pid --fork --mount --propagation shared --mount-proc bash -c 'exec 3<&0; exec bash "$0" --a0-worker' "$T" 2>&1)"; rc=$?; [ $rc -eq 9 ] && echo "$out" | grep -q "GUARDA" && chk ok "rechazado (exit 9)" || chk bad "rc=$rc" "$out"
echo "6) el ARRANQUE con A0_TEST_NS=1 en el entorno igual crea un namespace NUEVO (la variable no se consulta)"
out="$(A0_TEST_NS=1 bash "$T" --ns-selfcheck 2>&1)"; rc=$?; w="$(echo "$out" | sed -n 's/^NS_OK worker_mnt=\(mnt:\[[0-9]*\]\).*/\1/p')"; h="$(readlink /proc/self/ns/mnt)"
[ $rc -eq 0 ] && [ -n "$w" ] && [ "$w" != "$h" ] && chk ok "mount ns del trabajador ($w) != host ($h)" || chk bad "rc=$rc" "$out"
echo "7) fallo de initdb (binarios inexistentes): sale 9, sin cluster ni procesos activos y sin montajes en el host"
out="$(A0_PGBIN=/nonexistent bash "$T" 2>&1)"; rc=$?; [ $rc -eq 9 ] && echo "$out" | grep -q "initdb falló" && ! pgrep -f "a0-suite" >/dev/null && chk ok "exit 9, sin procesos a0-suite" || chk bad "rc=$rc" "$out"
echo "8) estático: la suite no usa rm -rf, glob de borrado, drop database/role ni toca pg_hba/pgdata del host"
if ! grep -nE 'rm -rf|drop (database|role)|pg_hba|/var/tmp/pgdata' "$T" | grep -v '^[0-9]*:#' | grep -v 'GUARDA\|\[ ! -e /var/tmp/pgdata \]'; then chk ok "sin patrones prohibidos"; else chk bad "hay patrones prohibidos"; fi
AFTER="$(host_state)"
echo "9) el mount namespace y los montajes del HOST no cambiaron: antes='$BEFORE' después='$AFTER'"
[ "$BEFORE" = "$AFTER" ] && chk ok "idénticos" || chk bad "cambiaron"
echo; echo "casos=$n fallos=$fails"; [ $fails -eq 0 ] && echo "GUARDA DE NAMESPACE VERIFICADA" || exit 1
