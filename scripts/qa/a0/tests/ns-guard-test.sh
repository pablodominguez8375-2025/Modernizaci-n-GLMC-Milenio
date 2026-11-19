#!/usr/bin/env bash
# Evidencia de la GUARDA DE NAMESPACE de test-a0.sh (v5). Corre en una VM/sandbox DESCARTABLE como root. Ningún caso monta nada en el namespace del host: los casos 11, 12, 14 y 15 montan SOLO dentro de un namespace privado propio del worker (efímero), nunca en el host;
# si la guarda fallara y montara en el host, se detecta al comparar el mount namespace y /proc/self/mountinfo del host antes/después.
# Uso (root): bash scripts/qa/a0/tests/ns-guard-test.sh
set -u
HERE="$(cd "$(dirname "$0")" && pwd)"; T="$HERE/test-a0.sh"; fails=0; n=0
[ "$(id -u)" = "0" ] || { echo "requiere root"; exit 9; }
host_state() { echo "$(readlink /proc/self/ns/mnt) $(sha256sum < /proc/self/mountinfo | cut -c1-16) $(findmnt -n -o FSTYPE /tmp) $(findmnt -n -o FSTYPE /var/tmp)"; }
chk() { n=$((n+1)); if [ "$1" = ok ]; then echo "  PASS: $2"; else echo "  FAIL: $2"; fails=$((fails+1)); echo "$3" | tail -3; fi; }
BEFORE="$(host_state)"
tok() { printf 'mnt=%s\nnet=%s\npid=%s\n' "$(readlink /proc/self/ns/mnt)" "$(readlink /proc/self/ns/net)" "$(readlink /proc/self/ns/pid)"; }
HM="$(readlink /proc/self/ns/mnt)"; HN="$(readlink /proc/self/ns/net)"; HP="$(readlink /proc/self/ns/pid)"
pst() { awk '{print $22}' "/proc/$1/stat" 2>/dev/null; }      # starttime
ppd() { awk '{print $4}' "/proc/$1/stat" 2>/dev/null; }       # ppid
# Procesos de la suite por IDENTIDAD (sin pgrep -f global): cmdline con la ruta exacta de test-a0.sh Y mount ns distinto del host. Un proceso ajeno del host no cuenta.
suite_procs() { local n=0 d p; for d in /proc/[0-9]*; do p="${d#/proc/}"; [ "$p" = "$$" ] && continue
  tr '\0' ' ' < "$d/cmdline" 2>/dev/null | grep -qF -- "$T" && [ "$(readlink "$d/ns/mnt" 2>/dev/null)" != "$HM" ] && n=$((n+1)); done; echo "$n"; }
# ¿hay un postgres dentro del mount ns $1?
ns_has_pg() { local d; for d in /proc/[0-9]*; do [ "$(readlink "$d/ns/mnt" 2>/dev/null)" = "$1" ] && [ "$(cat "$d/comm" 2>/dev/null)" = "postgres" ] && return 0; done; return 1; }
# safe_kill PID STARTTIME_ESPERADO PPID_ESPERADO MODO(worker|own): señal por PIDFD (identidad estable). pidfd_open ANTES de verificar; todo se re-lee con el pidfd ya abierto:
# starttime y ppid (y, en modo worker, NSpid==1 y mnt/net/pid ns distintos del host). Si el PID se reutilizó, el starttime difiere y NO se envía señal; si el proceso murió, pidfd_send_signal da ESRCH
# y jamás alcanza a un proceso nuevo. Salida: 0 enviada, 3 identidad no coincide, 4 proceso inexistente.
SK_PY='
import os,sys,signal
pid=int(sys.argv[1]); est,eppid,mode,hm,hn,hp=sys.argv[2:8]
try: fd=os.pidfd_open(pid)
except OSError: sys.exit(4)
try:
    t=open(f"/proc/{pid}/stat").read(); r=t[t.rindex(")")+2:].split(); st,pp=r[19],r[1]
    ok = st==est and pp==eppid
    if ok and mode=="worker":
        nsp=[l for l in open(f"/proc/{pid}/status") if l.startswith("NSpid:")][0].split()[1:]
        ns=lambda k: os.readlink(f"/proc/{pid}/ns/{k}")
        ok = len(nsp)>=2 and nsp[-1]=="1" and ns("mnt")!=hm and ns("net")!=hn and ns("pid")!=hp
except (OSError,IndexError,ValueError): sys.exit(3)
if not ok: sys.exit(3)
try: signal.pidfd_send_signal(fd, signal.SIGKILL)
except OSError: sys.exit(4)
sys.exit(0)'
safe_kill() { python3 -I -c "$SK_PY" "$1" "$2" "$3" "$4" "$HM" "$HN" "$HP"; }

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
out="$(A0_PGBIN=/nonexistent bash "$T" 2>&1)"; rc=$?; [ $rc -eq 9 ] && echo "$out" | grep -q "initdb falló" && [ "$(suite_procs)" = 0 ] && chk ok "exit 9, sin procesos a0-suite" || chk bad "rc=$rc" "$out"
echo "8) estático: la suite no usa rm -rf, glob de borrado, drop database/role ni toca pg_hba/pgdata del host"
if ! grep -nE 'rm -rf|drop (database|role)|pg_hba|/var/tmp/pgdata' "$T" | grep -v '^[0-9]*:#' | grep -v 'GUARDA\|\[ ! -e /var/tmp/pgdata \]'; then chk ok "sin patrones prohibidos"; else chk bad "hay patrones prohibidos"; fi

# Directorio de trabajo PROPIO (mktemp -d, root:root 0700); se verifica identidad y se borra al final solo por rutas exactas registradas (sin rm -r).
[ "$(suite_procs)" = 0 ] || { echo "ABORTA: ya hay procesos de la suite ajenos; no se ejecuta (ni se mata nada)"; exit 9; }
FAKE="$(mktemp -d /root/a0-guard.XXXXXX)" || exit 9
FINO="$(stat -c '%u:%a:%d:%i' "$FAKE")"; case "$FINO" in 0:700:*) ;; *) echo "ABORTA: $FAKE no es root:0700"; exit 9;; esac
KLOG="$FAKE/kill.out"
guard_cleanup() { [ "$(stat -c '%u:%a:%d:%i' "$FAKE" 2>/dev/null)" = "$FINO" ] || { echo "limpieza OMITIDA: $FAKE cambió de identidad"; return 0; }
  for f in unshare mount count kill.out; do [ -f "$FAKE/$f" ] && [ ! -L "$FAKE/$f" ] && rm -f -- "$FAKE/$f"; done; rmdir -- "$FAKE" 2>/dev/null; return 0; }
trap guard_cleanup EXIT
echo "10) fallo de unshare (unshare simulado que falla): el arranque sale != 0 sin worker, sin montajes ni procesos"
printf '#!/bin/sh\nexit 1\n' > "$FAKE/unshare"; chmod +x "$FAKE/unshare"
out="$(PATH="$FAKE:$PATH" bash "$T" 2>&1)"; rc=$?; [ $rc -ne 0 ] && [ "$(suite_procs)" = 0 ] && [ "$(host_state)" = "$BEFORE" ] && chk ok "rc=$rc, sin residuos, host idéntico" || chk bad "rc=$rc" "$out"
rm -f "$FAKE/unshare"
echo "11) fallo del SEGUNDO mount (mount simulado: 1.º real, 2.º falla; el 1.º monta SOLO dentro del namespace propio del worker): exit 9, sin suite activa, host idéntico"
cat > "$FAKE/mount" <<EOM
#!/bin/sh
C="$FAKE/count"; n=\$(cat "\$C" 2>/dev/null || echo 0); n=\$((n+1)); echo \$n > "\$C"
[ "\$n" -ge 2 ] && exit 1
exec /usr/bin/mount "\$@"
EOM
chmod +x "$FAKE/mount"; rm -f "$FAKE/count"
out="$(PATH="$FAKE:$PATH" bash "$T" 2>&1)"; rc=$?; [ $rc -eq 9 ] && echo "$out" | grep -q "no se pudieron montar" && [ "$(suite_procs)" = 0 ] && [ "$(host_state)" = "$BEFORE" ] && chk ok "exit 9, sin residuos, host idéntico" || chk bad "rc=$rc" "$out"
rm -f "$FAKE/mount" "$FAKE/count"
# --- Casos 12/14/15/16: SIGKILL por PIDFD con identidad re-leída; cleanup solo de procesos propios verificados ---
# kill_case ITERS PPID_QUE_SE_EXIGE(real|falso) : SENT/PGUP/WP quedan en variables globales
kill_case() { local iters="$1" mode="$2" i p ex wmnt=""
  bash "$T" >"$KLOG" 2>&1 &
  BGPID=$!; BGST="$(pst "$BGPID")"; WP=""; ST1=""; PGUP=no; SENT=no
  for i in $(seq 1 "$iters"); do
    for p in $(pgrep -P "$BGPID"); do [ "$(ppd "$p")" = "$BGPID" ] && grep -Eq '^NSpid:.*[[:space:]]1$' "/proc/$p/status" 2>/dev/null && { WP="$p"; ST1="$(pst "$p")"; wmnt="$(readlink "/proc/$p/ns/mnt" 2>/dev/null)"; }; done
    [ -n "$wmnt" ] && [ "$wmnt" != "$HM" ] && ns_has_pg "$wmnt" && PGUP=si
    [ -n "$WP" ] && [ "$PGUP" = si ] && break; sleep 0.5
  done
  ex="$BGPID"; [ "$mode" = falso ] && ex=1
  if [ -n "$WP" ] && safe_kill "$WP" "$ST1" "$ex" worker; then SENT=si; else echo "identidad NO validada o worker no seleccionado: no se envía SIGKILL por el camino principal"; fi
  # cleanup: solo procesos PROPIOS; starttime capturado al enumerar y señal por pidfd con identidad completa (worker) o starttime+ppid (BGPID, hijo nuestro)
  for p in $(pgrep -P "$BGPID"); do safe_kill "$p" "$(pst "$p")" "$BGPID" worker; done
  sleep 0.5
  safe_kill "$BGPID" "$BGST" "$$" own
  wait "$BGPID" 2>/dev/null; sleep 1; }
clean_after() { [ "$(suite_procs)" = 0 ] && [ "$(host_state)" = "$BEFORE" ] && ! ls -d /tmp/a0-suite.* >/dev/null 2>&1; }
echo "12) SIGKILL del worker: starttime, ppid==BGPID, NSpid==1 y mnt/net/pid ns distintos del host RE-LEÍDOS en el predicado final"
kill_case 80 real
if [ "$SENT" = si ] && [ "$PGUP" = si ] && clean_after; then chk ok "worker $WP (starttime $ST1) muerto con suite activa; 0 procesos, 0 dirs en /tmp del host, host idéntico"; else chk bad "SENT=$SENT WP=$WP PGUP=$PGUP" "$(tail -3 "$KLOG")"; fi
echo "14) fallo INDUCIDO de identidad (se exige un ppid falso): NO se envía SIGKILL por el camino principal; el cleanup termina solo el worker propio verificado; sin huérfanos"
kill_case 80 falso
if [ "$SENT" = no ] && [ -n "$WP" ] && clean_after; then chk ok "señal rechazada por identidad; cleanup propio dejó 0 procesos/dirs y host idéntico"; else chk bad "SENT=$SENT WP=$WP" "$(tail -3 "$KLOG")"; fi
echo "15) TIMEOUT inducido (1 iteración de espera: el worker aún puede no existir): cleanup solo de procesos propios, sin huérfanos"
kill_case 1 real
if clean_after; then chk ok "SENT=$SENT; 0 procesos/dirs residuales y host idéntico"; else chk bad "SENT=$SENT" "$(tail -3 "$KLOG")"; fi
echo "16) safe_kill con PID reutilizado/identidad falsa (simulado con un proceso inofensivo propio): rechaza, el proceso sigue vivo; con identidad correcta mata; PID ya muerto => no señala"
sleep 120 & SP=$!; SPST="$(pst "$SP")"; ok16=1
safe_kill "$SP" "$((SPST+1))" "$$" own; [ $? -eq 3 ] || ok16=0; kill -0 "$SP" 2>/dev/null || ok16=0       # starttime distinto (PID reutilizado)
safe_kill "$SP" "$SPST" "999999" own; [ $? -eq 3 ] || ok16=0; kill -0 "$SP" 2>/dev/null || ok16=0         # ppid distinto
safe_kill "$SP" "$SPST" "$$" worker; [ $? -eq 3 ] || ok16=0; kill -0 "$SP" 2>/dev/null || ok16=0          # modo worker sobre proceso del namespace del host
safe_kill "$SP" "$SPST" "$$" own; [ $? -eq 0 ] || ok16=0; wait "$SP" 2>/dev/null; kill -0 "$SP" 2>/dev/null && ok16=0   # identidad correcta
safe_kill "$SP" "$SPST" "$$" own; rc16=$?; [ $rc16 -ne 0 ] || ok16=0                                      # ya muerto/reaped: nunca señala
[ $ok16 -eq 1 ] && chk ok "rechazos 3/3, vivo tras cada rechazo, muerte con identidad correcta, sin señal a PID muerto (rc=$rc16)" || chk bad "ok16=$ok16 rc16=$rc16"
echo "13) limpieza no alcanza recursos externos: el trap solo recorre CREATED[] (rutas exactas) y no hay rm -r"
grep -q 'for p in "${CREATED\[@\]:-}"' "$T" && ! grep -nE 'rm -(r|f[a-z]*r)' "$T" | grep -v '^[0-9]*:#' | grep -q . && chk ok "estático" || chk bad "estático"
AFTER="$(host_state)"
echo "9) el mount namespace y los montajes del HOST no cambiaron: antes='$BEFORE' después='$AFTER'"
[ "$BEFORE" = "$AFTER" ] && chk ok "idénticos" || chk bad "cambiaron"
echo; echo "casos=$n fallos=$fails"; [ $fails -eq 0 ] && echo "GUARDA DE NAMESPACE VERIFICADA" || exit 1
