"""Hermetic end-to-end tests for the observed-attempt script: the real script text runs with stubs for
sudo/docker/curl/launcher on a throw-away fixture. No host, no network. Needs root (observer uses install -o root)."""
import json
import os
import re
import stat
import subprocess
import tempfile
import time
import unittest
from pathlib import Path

SCRIPT = Path(__file__).resolve().parents[1] / "scripts/qa/operators/qa525-observed-attempt-6eed4-20261104.sh"
TEXT = SCRIPT.read_text()
SRC = "6eed4f00b7917da3cca36bbda70e16d16328648d"
OLD = "41d9c490d138419c7286445f37e7aeddf0d1b8d5"
ARCH = "centenario-qa-g6-CIFRADO-20261104T105358Z.tar.gz"
ARCHSHA = "f7e2d142a62197323df5fb0efd936aaf96528aa85fe2aa7f3ec03bf4e65d9d9b"
OLDAPI, OLDWEB, BADAPI = "aaaaaaaaaaaa", "cccccccccccc", "bbbbbbbbbbbb"


def w(path, body, mode=0o600):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(body if isinstance(body, str) else json.dumps(body))
    path.chmod(mode)


def insp(cid, st="running", h="healthy", ec=0, out=""):
    return json.dumps([{"Id": cid + "0" * 52, "State": {"Status": st, "ExitCode": ec, "OOMKilled": False,
                                                       "Health": {"Status": h, "Log": [{"ExitCode": 0 if h == "healthy" else 1, "Output": out}]}}}])


class Fx:
    def __init__(self, root, g6_cutoff="2099-01-01 00:00:00 UTC", pins=True):
        self.t = Path(root)
        t = self.t
        self.state, self.etc, self.bin, self.obs = t / "state", t / "etc", t / "bin", t / "obsroot"
        for d in (self.state, self.bin, self.obs):
            d.mkdir(parents=True, exist_ok=True)
        self.state.chmod(0o700)
        self.locks = [t / "locks" / n for n in ("a.lock", "b.lock", "c.lock", "d.lock", "e.lock")]
        for lk in self.locks:
            w(lk, "")
        self.acta = self.etc / "qa525-g6-reviewed-20261104T105358Z.json"
        w(self.acta, {"g6_offhost_sha256_verified": True, "g6_archive_name": ARCH, "g6_archive_sha256": ARCHSHA})
        import hashlib
        self.actasha = hashlib.sha256(self.acta.read_bytes()).hexdigest()
        for n, cid, rev in (("api", OLDAPI, OLD), ("web", OLDWEB, OLD)):
            w(t / "d" / ("ps_" + n), cid, 0o644)
            w(t / "d" / ("rev_" + cid), rev, 0o644)
            w(t / "d" / ("insp_" + n), insp(cid), 0o644)
        w(t / "d" / "events", "2026-11-04T05:00:01Z create centenario-qa-lab2-api-1 exit=\n2026-11-04T05:00:02Z die centenario-qa-lab2-api-1 exit=1\n", 0o644)
        w(t / "d" / "logs", "2026-11-04T05:00:01Z fail: Microsoft.Extensions.Hosting password=hunter2 contact ops@example.org\n2026-11-04T05:00:02Z info ok\n", 0o644)
        w(t / "d" / "public.json", {"environment": "QA_SYNTHETIC", "source_sha": OLD}, 0o644)
        w(t / "d" / "status.json", {"status": "QA525_UPDATE_STATUS_READ_ONLY", "services_running": True, "prepared_exists": False,
                                      "archive_reconciliation_required": False, "may_apply": False}, 0o644)
        w(t / "d" / "quota", "60", 0o644)
        w(self.state / ("dual-" + "1" * 40 + ".json"), {"status": "APPLIED", "source_sha": "1" * 40})
        self._stubs()
        body = TEXT
        body = body.replace("/etc/pmgm/", str(self.etc) + "/")
        body = body.replace("STATE=/opt/centenario/qa525-signed-prepare", "STATE=%s" % self.state)
        body = body.replace("OBSROOT=/opt/centenario\n", "OBSROOT=%s\n" % self.obs)
        body = body.replace("L=/usr/local/sbin/pmgm-qa-update", "L=%s/pmgm-qa-update" % self.bin)
        body = body.replace("OWNER=0:0", "OWNER=%d:%d" % (os.getuid(), os.getgid()))
        body = body.replace("SITE=https://sistema.granlogiamixta.cl", "SITE=https://x.invalid")
        body = body.replace("INTERVAL=1\n", "INTERVAL=0.3\n")
        body = body.replace("G6_CUTOFF='2026-11-06 10:00:00 UTC'", "G6_CUTOFF='%s'" % g6_cutoff)
        for i, lk in enumerate(("/opt/centenario/qa-api-refresh/operation.lock", "/opt/centenario/qa-auto-lite/web-update.lock", "/run/lock/pmgm-master-qa-604.lock", "/run/lock/pmgm-qa754-web-hotfix.lock", "/run/lock/pmgm-qa754-api-activation.lock")):
            body = body.replace(lk, str(self.locks[i]))
        import hashlib as h2
        body = body.replace("61e3f1f7e7dd561567e5fac415a6fa9695c2f74fa86adde21dd8ed0330d03cd3", h2.sha256((self.bin / "pmgm-qa-update").read_bytes()).hexdigest())
        real_sha = "471a7cc5a4fca11b16ff0e187ae5a6406cbb8f78192419886be3010526de4cf8"
        assert "G6SHA=" + real_sha in body and "G6=%s/qa525-g6-reviewed-20261104T105358Z.json" % self.etc in body
        if pins:
            body = body.replace(real_sha, self.actasha)
        else:  # variante deliberada: pines ausentes
            body = body.replace("G6=%s/qa525-g6-reviewed-20261104T105358Z.json" % self.etc, "G6=__G6_ACTA_PATH__").replace("G6SHA=" + real_sha, "G6SHA=__G6_ACTA_SHA256__")
        (t / "s.sh").write_text(body)

    def _stubs(self):
        def stub(name, body, d=None):
            p = (d or self.bin) / name
            p.write_text("#!/bin/bash\n" + body + "\n")
            p.chmod(0o755)
        D = str(self.t / "d")
        stub("hostname", "echo glmdch")
        stub("id", 'if [ "$1" = -un ]; then echo soporte; else exec /usr/bin/id "$@"; fi')
        stub("sudo", 'case "$1" in -v) exit 0;; -n) exit 0;; esac; exec "$@"')
        stub("curl", ('u="${@: -1}"; case "$u" in *rate_limit*) printf \'{"resources":{"core":{"remaining":%s,"reset":%d}}}\' "$(cat @D@/quota)" 4102444800;;'
                      ' */qa-version.json) cat @D@/public.json;; *) [ -e @D@/health_fail ] && exit 22; exit 0;; esac').replace("@D@", D))
        stub("docker", ('case "$1" in\n'
                        ' ps) for a in "$@"; do case "$a" in label=com.docker.compose.service=*) s="${a##*=}";; esac; done; cat @D@/ps_$s; echo;;\n'
                        ' inspect) if [ "$2" = --format ]; then cat "@D@/rev_${@: -1}"; echo; else n="${@: -1}"; n="${n#centenario-qa-lab2-}"; n="${n%-1}"; cat "@D@/insp_$n" || exit 1; fi;;\n'
                        ' events) cat @D@/events; exec sleep 300;;\n'
                        ' logs) cat @D@/logs; exec sleep 300;;\n'
                        ' exec) cat @D@/ef 2>/dev/null || echo "100|1|1";;\n'
                        ' *) exit 9;;\nesac').replace("@D@", D))
        stub("pmgm-qa-update", ('echo "$@" >> @D@/launcher.calls\n'
                                'if [ "$1" = --status ]; then cat @D@/status.json; exit 0; fi\n'
                                'case "$(cat @D@/mode 2>/dev/null)" in\n'
                                ' block) sleep 0.8; cat > @D@/insp_api <<J\n@BAD@\nJ\n sleep 1; cat > @D@/insp_api <<J\n@OK@\nJ\n'
                                '   echo \'{"status":"RECOVERY_MANUALLY_REQUIRED","source_sha":"@S@"}\' > @ST@/dual-@S@.json; chmod 600 @ST@/dual-@S@.json\n'
                                '   echo \'{"status":"BLOCKED_QA525_UPDATE","source_sha":null,"code":"UPDATE_NOT_COMPLETED","may_apply":false}\'; exit 2;;\n'
                                ' pass) sleep 0.5; echo \'{"status":"APPLIED","source_sha":"@S@"}\' > @ST@/dual-@S@.json; chmod 600 @ST@/dual-@S@.json\n'
                                '   echo @S@ > @D@/rev_new; echo @S@ > @D@/rev_@OA@; echo @S@ > @D@/rev_@OW@\n'
                                '   echo \'{"environment":"QA_SYNTHETIC","source_sha":"@S@"}\' > @D@/public.json\n'
                                '   echo \'{"status":"DUAL_API_WEB_APPLIED","source_sha":"@S@"}\'; exit 0;;\n'
                                ' *) echo unexpected-launch >> @D@/launcher.calls; exit 3;;\nesac')
             .replace("@D@", D).replace("@ST@", str(self.state)).replace("@S@", SRC).replace("@OA@", OLDAPI).replace("@OW@", OLDWEB)
             .replace("@BAD@", insp(BADAPI, "running", "unhealthy", 0, "HTTP 503 from /health/ready password=hunter2 server=db.internal ops@example.org eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9abcdef")).replace("@OK@", insp(OLDAPI)))

    def run(self, *args):
        env = dict(os.environ, PATH="%s:%s" % (self.bin, os.environ["PATH"]))
        return subprocess.run(["bash", str(self.t / "s.sh"), *args], capture_output=True, text=True, env=env, timeout=120)

    def calls(self):
        p = self.t / "d" / "launcher.calls"
        return p.read_text().splitlines() if p.exists() else []

    def set_mode(self, m):
        w(self.t / "d" / "mode", m, 0o644)


@unittest.skipIf(os.geteuid() != 0, "needs root (install -o root)")
class Observed(unittest.TestCase):
    def setUp(self):
        self._d = tempfile.TemporaryDirectory()
        self.fx = Fx(self._d.name)

    def tearDown(self):
        subprocess.run(["pkill", "-f", self._d.name], check=False)
        self._d.cleanup()

    def stops(self, code, args=(), mutate=None):
        if mutate:
            mutate(self.fx)
        r = self.fx.run(*args)
        self.assertEqual(r.returncode, 2, r.stdout + r.stderr)
        self.assertIn("STOP_" + code, r.stdout, r.stdout + r.stderr)
        self.assertFalse([c for c in self.fx.calls() if "--review-and-run" in c])
        self.assertEqual(list(self.fx.obs.iterdir()), [])
        return r

    def test_read_only_pass_changes_nothing(self):
        r = self.fx.run()
        self.assertEqual(r.returncode, 0, r.stdout + r.stderr)
        self.assertIn("QA525_OBSERVED=READ_ONLY_PASS_NO_CHANGES", r.stdout)
        self.assertEqual([c for c in self.fx.calls() if "--review-and-run" in c], [])
        self.assertEqual(list(self.fx.obs.iterdir()), [])

    def test_blocked_attempt_is_observed_and_reported(self):
        self.fx.set_mode("block")
        r = self.fx.run("EXECUTE_OBSERVED_ATTEMPT")
        out = r.stdout
        self.assertEqual(r.returncode, 2, out + r.stderr)
        self.assertIn("STOP_LAUNCHER_BLOCKED_OR_INCOMPLETE_SEE_TIMELINE", out)
        calls = [c for c in self.fx.calls() if "--review-and-run" in c]
        self.assertEqual(len(calls), 1)
        self.assertIn("--source %s" % SRC, calls[0])
        self.assertIn("--digest sha256:9bdeaa217f09f5d0e19e50d214a4abb936a2b89246ebbcf71fb71178e5574af2", calls[0])
        self.assertIn("--operator-name soporte@glmdch --schema-mode support00_postdb", calls[0])
        self.assertIn("QA525_OBSERVED_TIMELINE", out)
        self.assertIn("die centenario-qa-lab2-api-1 exit=1", out)
        self.assertIn(BADAPI, out)  # primera anomalía en contenedor nuevo
        self.assertRegex(out, r'"h":"unhealthy"')
        self.assertIn("HTTP 503", out)
        self.assertIn("RECOVERY_MANUALLY_REQUIRED", out)
        for secret in ("hunter2", "ops@example.org", "db.internal", "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9abcdef"):
            self.assertNotIn(secret, out)
        self.assertIn("[REDACTED]", out)
        self.assertIn("[EMAIL]", out)
        self.assertIn("[LONGTOKEN]", out)
        pl = (next(iter(self.fx.obs.iterdir())) / "poll.log").read_text()
        self.assertIn("hunter2", pl)  # el original solo vive en el directorio root 0600
        self.assertRegex(out, r'"h":"unhealthy"[^\n]*HTTP 503')
        anomaly = out.split("primera anomalia en un contenedor NUEVO ---")[1].splitlines()[1]
        self.assertIn(BADAPI, anomaly)
        json.loads(anomaly.split(" ", 2)[2])  # la línea redactada sigue siendo JSON válido
        d = [p for p in self.fx.obs.iterdir()]
        self.assertEqual(len(d), 1)
        self.assertEqual(stat.S_IMODE(d[0].stat().st_mode), 0o700)
        self.assertTrue((d[0] / "events.log").exists() and (d[0] / "poll.log").exists())
        self.assertTrue(list(d[0].glob("logs-api-*.log")))
        # observadores terminados
        for pf in ("events.pid", "poll.pid"):
            pid = int((d[0] / pf).read_text())
            time.sleep(0.2)
            self.assertFalse(Path("/proc/%d" % pid).exists(), pf)
        # el observador no escribe en el directorio de estado del lanzador
        self.assertEqual(sorted(p.name for p in self.fx.state.iterdir()), sorted(["dual-%s.json" % ("1" * 40), "dual-%s.json" % SRC]))

    def test_pass_attempt_postflight(self):
        self.fx.set_mode("pass")
        r = self.fx.run("EXECUTE_OBSERVED_ATTEMPT")
        self.assertEqual(r.returncode, 0, r.stdout + r.stderr)
        self.assertIn("QA525_OBSERVED=PASS_DUAL_API_WEB_APPLIED", r.stdout)
        self.assertEqual(len([c for c in self.fx.calls() if "--review-and-run" in c]), 1)

    def test_pass_response_without_applied_journal_is_stop(self):
        self.fx.set_mode("pass")
        w(self.fx.t / "d" / "mode", "pass", 0o644)
        sc = (self.fx.bin / "pmgm-qa-update")
        sc.write_text(sc.read_text().replace('"APPLIED"', '"PENDING"'))
        import hashlib
        s = (self.fx.t / "s.sh")
        old = re.search(r"printf '%s  %s\\n' ([a-f0-9]{64}) ", s.read_text()).group(1)
        s.write_text(s.read_text().replace(old, hashlib.sha256(sc.read_bytes()).hexdigest()))
        r = self.fx.run("EXECUTE_OBSERVED_ATTEMPT")
        self.assertEqual(r.returncode, 2)
        self.assertIn("STOP_JOURNAL_NOT_APPLIED", r.stdout)

    def test_a0_stops(self):
        def rewrite(fx, f):
            f(fx)
        cases = {
            "PLANE_NOT_CLEAN": lambda fx: w(fx.state / "prepared.json", "{}"),
            "JOURNAL_OTHER_NOT_APPLIED": lambda fx: w(fx.state / ("dual-%s.json" % ("2" * 40)), {"status": "PENDING", "source_sha": "2" * 40}),
            "JOURNAL_NEXT_PRESENT": lambda fx: w(fx.state / ("dual-%s.json.next" % ("1" * 40)), "{}"),
            "RUNTIME_NOT_OLD": lambda fx: w(fx.t / "d" / ("rev_" + OLDAPI), SRC, 0o644),
            "PUBLIC_VERSION_NOT_OLD": lambda fx: w(fx.t / "d" / "public.json", {"environment": "QA_SYNTHETIC", "source_sha": SRC}, 0o644),
            "STATUS_MISMATCH": lambda fx: w(fx.t / "d" / "status.json", {"status": "QA525_UPDATE_STATUS_READ_ONLY", "services_running": True, "prepared_exists": True, "archive_reconciliation_required": False, "may_apply": False}, 0o644),
            "GITHUB_QUOTA_LOW_WAIT_FOR_RESET_THEN_RERUN": lambda fx: w(fx.t / "d" / "quota", "40", 0o644),
            "EF_HISTORY_DRIFT": lambda fx: w(fx.t / "d" / "ef", "100|0|1", 0o644),
            "G6_ACTA_HASH": lambda fx: w(fx.acta, {"g6_offhost_sha256_verified": True, "g6_archive_name": ARCH, "g6_archive_sha256": ARCHSHA, "x": 1}),
            "G6_ACTA_MODE": lambda fx: os.chmod(fx.acta, 0o644),
            "LOCK_FILE_UNTRUSTED_OR_MISSING": lambda fx: fx.locks[2].unlink(),
        }
        for code, m in cases.items():
            with self.subTest(code=code):
                self.tearDown(); self.setUp()
                self.stops(code, mutate=m)

    def test_g6_content_and_expiry_and_pins(self):
        bad = {"g6_offhost_sha256_verified": False, "g6_archive_name": ARCH, "g6_archive_sha256": ARCHSHA}
        def content(fx):
            import hashlib
            w(fx.acta, bad)
            s = fx.t / "s.sh"
            s.write_text(s.read_text().replace(fx.actasha, hashlib.sha256(fx.acta.read_bytes()).hexdigest()))
        self.stops("G6_ACTA_CONTENT", mutate=content)
        self.tearDown(); self.setUp()
        self.fx = Fx(self._d.name + "/x", g6_cutoff="2020-01-01 00:00:00 UTC") if False else self.fx
        s = self.fx.t / "s.sh"
        s.write_text(s.read_text().replace("2099-01-01 00:00:00 UTC", "2020-01-01 00:00:00 UTC"))
        self.stops("G6_TOO_OLD_RENEW")
        with tempfile.TemporaryDirectory() as d2:
            fx2 = Fx(d2, pins=False)
            r = fx2.run()
            self.assertIn("STOP_G6_PINS_NOT_SET", r.stdout)
            r = fx2.run("EXECUTE_OBSERVED_ATTEMPT")
            self.assertIn("STOP_G6_PINS_NOT_SET", r.stdout)
            self.assertEqual(fx2.calls(), [])

    def test_bad_argument_and_old_acta(self):
        self.stops("BAD_ARGUMENT", args=("EXECUTE_A1_A2",))
        s = self.fx.t / "s.sh"
        s.write_text(s.read_text().replace("qa525-g6-reviewed-20261104T105358Z.json", "qa525-g6-reviewed-20261102T110545Z.json"))
        self.stops("G6_OLD_ACTA")

    def test_static_single_launch_and_ordering(self):
        code = "\n".join(l for l in TEXT.splitlines() if not l.lstrip().startswith("#"))
        self.assertEqual(code.count("--review-and-run"), 1)
        main = code.split("# <<< FUNCTIONS")[-1]
        i = {k: main.find(k) for k in ("g6_ok", "quota_ok", "observe_start", "--review-and-run", "observe_stop")}
        a0 = main.find("QA525_OBSERVED_A0=PASS")
        att = main.find("# ---- UN intento")
        self.assertTrue(0 < a0)
        exec_part = TEXT.split("# ---- UN intento")[1]
        self.assertLess(exec_part.find("g6_ok"), exec_part.find("observe_start"))
        self.assertLess(exec_part.find("quota_ok"), exec_part.find("observe_start"))
        self.assertLess(exec_part.find("observe_start"), exec_part.find("--review-and-run"))
        self.assertLess(exec_part.find("--review-and-run"), exec_part.find("observe_stop\ntrap"))
        for bad in ("docker compose", "docker rm", "docker stop", "docker restart", "docker run", "docker kill", "docker start", "rm -", "mv ", "systemctl", "--reconcile"):
            self.assertNotIn(bad, code, bad)
        self.assertEqual(set(re.findall(r"docker (\w+)", code)), {"events", "inspect", "logs", "ps", "exec"})


if __name__ == "__main__":
    unittest.main()
