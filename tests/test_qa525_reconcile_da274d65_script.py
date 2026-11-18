"""Hermetic tests for the QA525 journal reconciliation script (FUNCTIONS block extracted verbatim)."""
import hashlib
import json
import os
import subprocess
import tempfile
import unittest
from pathlib import Path

SCRIPT = Path(__file__).resolve().parents[1] / "scripts/qa/operators/qa525-reconcile-da274d65-20261118.sh"
TEXT = SCRIPT.read_text()
SRC = "da274d651d8e9e056525413406c0aab62131504f"
OLDAPI = "sha256:c620fd26212edc943b368da7a5d5e0fbe90ae8391ee8e73b98d818fd6d2171ed"
OLDWEB = "sha256:664c0e37bf6e6b801fd5617f4d327f5e027358817d488a92905338d992c1255a"
NEWAPI = "sha256:09b66869936d505a08c53b44ad9d22c86c0ffad83696ac5f1ea65c7bfff9a0cb"
NEWWEB = "sha256:fb15b5c17ae57b149b5d3d2883ba41a84514a3b88cc2bc051207ae0f67507f34"
NAMES = ["api", "clamav", "gateway", "keycloak", "minio", "postgres", "web"]
NONCE = "cd" * 32
NEW = {"api": NEWAPI, "web": NEWWEB}


def sha(p):
    return hashlib.sha256(Path(p).read_bytes()).hexdigest()


def write(path, obj, mode=0o600):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(obj if isinstance(obj, str) else json.dumps(obj, sort_keys=True))
    path.chmod(mode)


class Fixture:
    def __init__(self, root):
        self.t = Path(root)
        self.state = self.t / "state"
        self.etc = self.t / "etc"
        self.bin = self.t / "bin"
        self.locks = [self.t / "locks" / ("l%d.lock" % i) for i in range(5)]
        for d in (self.state, self.bin):
            d.mkdir(parents=True, exist_ok=True)
        self.state.chmod(0o700)
        for lk in self.locks:
            write(lk, "")
        S = str(self.state)
        self.layers = {"api": ["/b/%d.yml" % i for i in range(6)], "web": ["/w/%d.yml" % i for i in range(5)]}
        write(self.state / ("dual-%s.json" % SRC), {
            "source_sha": SRC, "status": "RECOVERY_MANUALLY_REQUIRED",
            "old_images": {"api": OLDAPI, "web": OLDWEB}, "new_images": dict(NEW), "original_layers": self.layers})
        self.jsha = sha(self.state / ("dual-%s.json" % SRC))
        self.ovr = {}
        for n, i in (("api", NEWAPI), ("web", NEWWEB), ("api", OLDAPI), ("web", OLDWEB)):
            f = self.state / ("qa525-%s-%s-%s.json" % (n, SRC, i[7:]))
            write(f, {"services": {n: {"image": i}}})
            self.ovr[f.name] = sha(f)
        base = {}
        for i, n in enumerate(NAMES):
            base[n] = {"container_fingerprint": "d" * 64, "image_id": "sha256:" + str(i) * 64, "running": True}
        write(self.state / "prepared.json", {"status": "X", "baseline": base})
        write(self.etc / "qa525-supervised-cut.json", {"target_sha": SRC, "nonce": NONCE})
        write(self.state / ("approval-consumed-" + NONCE), SRC + "\n")
        write(self.state / "dual-events.log", "1 DUAL_API_WEB_RECOVERY_REQUIRED %s\n" % SRC)
        for c in "12":
            write(self.state / ("dual-%s.json" % (c * 40)), {"status": "APPLIED", "source_sha": c * 40})
        self.cur = {"api": "c" * 64, "web": "e" * 64}
        self.receipt = {
            "status": "MANUAL_APPLY_VERIFIED_API_WEB", "source_sha": SRC, "script_sha256": "dc0c470c234214145d4b841dfbf4400c61aa1dc005f62cba591406131dff44df",
            "overrides_sha256": self.ovr, "new_images": dict(NEW), "old_images": {"api": OLDAPI, "web": OLDWEB},
            "current_containers": self.cur, "public_version": {"environment": "QA_SYNTHETIC", "source_sha": SRC},
            "operator_journal_sha256": self.jsha, "operator_journal_untouched": True, "no_second_apply_without_approved_reconciliation": True}
        write(self.state / ("manual-apply-%s-20261118T013903Z.result.json" % SRC), self.receipt)
        write(self.state / ("manual-apply-%s-20261118T013903Z.started.json" % SRC), {"status": "STARTED"})
        for i, n in enumerate(NAMES):
            if n in NEW:
                self.set_container(n, self.cur[n], NEW[n], "running", SRC)
                nd = NEW[n][7:]
                self.set_layers(n, ",".join(self.layers[n] + ["%s/qa525-%s-%s-%s.json" % (S, n, SRC, nd)]))
            else:
                self.set_container(n, "d" * 64, "sha256:" + str(i) * 64, "running", "")
        self.public = {"environment": "QA_SYNTHETIC", "source_sha": SRC}
        self._stubs()

    def set_container(self, n, cid, img, st, rev):
        write(self.t / "containers" / n, "%s %s %s centenario-qa-lab2 %s %s" % (cid, img, st, n, rev), 0o644)

    def set_layers(self, n, v):
        write(self.t / "containers" / (n + ".layers"), v, 0o644)

    def _stubs(self):
        def stub(name, body):
            p = self.bin / name
            p.write_text("#!/bin/bash\n" + body + "\n")
            p.chmod(0o755)
        T = str(self.t)
        stub("docker", ('name="${@: -1}"; name="${name#centenario-qa-lab2-}"; name="${name%-1}"; '
                        'case "$*" in *NetworkSettings*) echo "172.20.0.9 ";; *config_files*) cat "@T@/containers/$name.layers" || exit 1;; *) cat "@T@/containers/$name" || exit 1;; esac').replace("@T@", T))
        stub("curl", ('u="${@: -1}"; case "$u" in http://172.20.0.9:8080/*) [ -e "@T@/api_direct_fail" ] && exit 22; exit 0;; http://172.20.0.9:80/health/web) [ -e "@T@/web_direct_fail" ] && exit 22; exit 0;; http://172.20.0.9:80/qa-version.json) cat "@T@/web_direct.json";; */qa-version.json) cat "@T@/public.json";; *) [ -e "@T@/health_fail" ] && exit 22; exit 0;; esac').replace("@T@", T))
        write(self.t / "public.json", self.public, 0o644)
        write(self.t / "web_direct.json", self.public, 0o644)

    def functions(self):
        block = TEXT.split("# >>> FUNCTIONS", 1)[1].split("\n", 1)[1].split("# <<< FUNCTIONS", 1)[0]
        block = block.replace("/etc/pmgm/", str(self.etc) + "/")
        (self.t / "fn.sh").write_text(block)
        return self.t / "fn.sh"

    def run(self, call, env=None, hook=""):
        lock_arr = " ".join('"%s"' % x for x in self.locks)
        script = f'''set -Eeuo pipefail
export PATH="{self.bin}:$PATH"
sudo() {{ "$@"; }}
SRC={SRC}; OLDAPI={OLDAPI}; OLDWEB={OLDWEB}; NEWAPI={NEWAPI}; NEWWEB={NEWWEB}; JSHA={self.jsha}
APPLY_SHA=dc0c470c234214145d4b841dfbf4400c61aa1dc005f62cba591406131dff44df
STATE="{self.state}"; OWNER="$(id -u):$(id -g)"; SITE=https://x.invalid
J="$STATE/dual-$SRC.json"; P="$STATE/prepared.json"; E="$STATE/dual-events.log"; CUT="{self.etc}/qa525-supervised-cut.json"
LOCKS=({lock_arr})
source "{self.functions()}"
{hook}
{call}
'''
        return subprocess.run(["bash", "-c", script], capture_output=True, text=True, env=dict(os.environ, **(env or {})))

    def journal(self):
        return json.loads((self.state / ("dual-%s.json" % SRC)).read_text())

    def backups(self):
        return sorted(self.state.glob("reconcile-*"))


class Reconcile(unittest.TestCase):
    def setUp(self):
        self._d = tempfile.TemporaryDirectory()
        self.fx = Fixture(self._d.name)

    def tearDown(self):
        self._d.cleanup()

    def stop(self, code, mutate):
        mutate(self.fx)
        j = self.fx.state / ("dual-%s.json" % SRC)
        before = sha(j)
        r = self.fx.run("run_reconcile_locked")
        self.assertNotEqual(r.returncode, 0)
        self.assertIn("STOP_" + code, r.stdout)
        self.assertEqual(sha(j), before)
        self.assertEqual(self.fx.backups(), [])

    def test_happy_path_only_status_changes(self):
        orig = self.fx.journal()
        r = self.fx.run("run_reconcile_locked")
        self.assertEqual(r.returncode, 0, r.stdout + r.stderr)
        self.assertIn("QA525_RECONCILE_JOURNAL=APPLIED", r.stdout)
        new = self.fx.journal()
        self.assertEqual(new["status"], "APPLIED")
        orig.pop("status"); new.pop("status")
        self.assertEqual(orig, new)
        self.assertFalse((self.fx.state / ("dual-%s.json.next" % SRC)).exists())
        b = self.fx.backups()
        self.assertEqual(len(b), 1)
        self.assertEqual(sha(b[0] / ("dual-%s.json.orig" % SRC)), self.fx.jsha)
        rc = json.loads((b[0] / "receipt.json").read_text())
        self.assertTrue(rc["only_status_field_changed"])
        self.assertEqual(oct((self.fx.state / ("dual-%s.json" % SRC)).stat().st_mode & 0o777), "0o600")

    def test_second_run_refused(self):
        self.assertEqual(self.fx.run("run_reconcile_locked").returncode, 0)
        r = self.fx.run("run_reconcile_locked")
        self.assertNotEqual(r.returncode, 0)
        self.assertIn("STOP_", r.stdout)
        self.assertEqual(len(self.fx.backups()), 1)

    def test_journal_hash_changed(self):
        self.stop("JOURNAL_HASH_CHANGED", lambda f: write(f.state / ("dual-%s.json" % SRC), dict(f.journal(), extra=1)))

    def test_journal_already_applied(self):
        def m(f):
            j = f.journal(); j["status"] = "APPLIED"; write(f.state / ("dual-%s.json" % SRC), j)
            f.jsha = sha(f.state / ("dual-%s.json" % SRC))
        self.stop("JOURNAL_CONTENT", m)

    def test_other_journal_not_applied(self):
        self.stop("JOURNAL_OTHER_NOT_APPLIED", lambda f: write(f.state / ("dual-%s.json" % ("1" * 40)), {"status": "ROLLED_BACK", "source_sha": "1" * 40}))

    def test_receipt_missing(self):
        self.stop("RECEIPT_NOT_UNIQUE", lambda f: (f.state / ("manual-apply-%s-20261118T013903Z.result.json" % SRC)).unlink())

    def test_receipt_two(self):
        self.stop("RECEIPT_NOT_UNIQUE", lambda f: write(f.state / ("manual-apply-%s-20261118T020000Z.result.json" % SRC), f.receipt))

    def test_receipt_wrong_status(self):
        self.stop("RECEIPT_CONTENT", lambda f: write(f.state / ("manual-apply-%s-20261118T013903Z.result.json" % SRC), dict(f.receipt, status="MANUAL_APPLY_ROLLED_BACK_VERIFIED")))

    def test_receipt_wrong_script(self):
        self.stop("RECEIPT_CONTENT", lambda f: write(f.state / ("manual-apply-%s-20261118T013903Z.result.json" % SRC), dict(f.receipt, script_sha256="0" * 64)))

    def test_nonce_not_consumed(self):
        self.stop("NONCE_MARKER_UNTRUSTED", lambda f: (f.state / ("approval-consumed-" + NONCE)).unlink())

    def test_applied_event_present(self):
        self.stop("TARGET_APPLIED_EVENT", lambda f: (f.state / "dual-events.log").write_text("1 DUAL_API_WEB_RECOVERY_REQUIRED %s\n2 DUAL_API_WEB_APPLIED %s\n" % (SRC, SRC)))

    def test_override_content(self):
        self.stop("OVERRIDE_CONTENT", lambda f: write(f.state / ("qa525-api-%s-%s.json" % (SRC, NEWAPI[7:])), {"services": {"api": {"image": OLDAPI}}}))

    def test_override_hash(self):
        def m(f):
            p = f.state / ("qa525-web-%s-%s.json" % (SRC, OLDWEB[7:]))
            p.write_text(json.dumps({"services": {"web": {"image": OLDWEB}}}) + "\n")
        self.stop("OVERRIDE_HASH", m)

    def test_api_not_new_image(self):
        self.stop("TARGET_NOT_NEW_REVISION", lambda f: f.set_container("api", "c" * 64, NEWAPI, "running", "6eed4f00b7917da3cca36bbda70e16d16328648d"))

    def test_api_container_changed_since_receipt(self):
        self.stop("TARGET_CONTAINER_CHANGED_SINCE_RECEIPT", lambda f: f.set_container("api", "9" * 64, NEWAPI, "running", SRC))

    def test_layers_mismatch(self):
        self.stop("COMPOSE_LAYERS_MISMATCH", lambda f: f.set_layers("web", ",".join(f.layers["web"])))

    def test_dependency_changed(self):
        self.stop("DEPENDENCY_CHANGED", lambda f: f.set_container("postgres", "f" * 64, "sha256:" + "5" * 64, "running", ""))

    def test_web_stopped(self):
        self.stop("BASELINE_STATE", lambda f: f.set_container("web", f.cur["web"], NEWWEB, "exited", SRC))

    def test_public_version_not_new(self):
        self.stop("PUBLIC_VERSION_NOT_NEW", lambda f: write(f.t / "public.json", {"environment": "QA_SYNTHETIC", "source_sha": "6" * 40}, 0o644))

    def test_api_direct_not_ready(self):
        self.stop("API_DIRECT_NOT_READY", lambda f: write(f.t / "api_direct_fail", "", 0o644))

    def test_web_direct_unhealthy(self):
        self.stop("WEB_DIRECT_NOT_HEALTHY", lambda f: write(f.t / "web_direct_fail", "", 0o644))

    def test_web_direct_old_version(self):
        self.stop("WEB_DIRECT_VERSION_NOT_NEW", lambda f: write(f.t / "web_direct.json", {"environment": "QA_SYNTHETIC", "source_sha": "6" * 40}, 0o644))

    def test_public_health(self):
        self.stop("PUBLIC_HEALTH", lambda f: write(f.t / "health_fail", "", 0o644))

    def test_stale_artifact_blocks(self):
        (self.fx.state / "reconcile-old").mkdir()
        j = self.fx.state / ("dual-%s.json" % SRC)
        before = sha(j)
        r = self.fx.run("run_reconcile_locked")
        self.assertNotEqual(r.returncode, 0)
        self.assertIn("STOP_STALE_OR_PRIOR_RECONCILE_ARTIFACT", r.stdout)
        self.assertEqual(sha(j), before)
        self.assertEqual([x.name for x in self.fx.backups()], ["reconcile-old"])

    def test_journal_next_blocks(self):
        self.stop("STALE_OR_PRIOR_RECONCILE_ARTIFACT", lambda f: write(f.state / ("dual-%s.json.next" % SRC), {}))

    def test_locks_busy(self):
        import fcntl
        fd = os.open(self.fx.locks[2], os.O_RDONLY)
        fcntl.flock(fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
        try:
            self.stop("LOCKS_BUSY_ANOTHER_OPERATOR", lambda f: None)
        finally:
            os.close(fd)

    def test_static_safety(self):
        for bad in ("docker compose", "docker restart", "docker stop", "docker rm", "rm -", "psql", "git push", "dual-events.log\" >>"):
            self.assertNotIn(bad, TEXT)
        self.assertIn("set -Eeuo pipefail", TEXT)
        self.assertEqual(TEXT.count("os.replace"), 1)


if __name__ == "__main__":
    unittest.main()
