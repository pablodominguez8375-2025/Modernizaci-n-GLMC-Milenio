"""Hermetic tests for the QA525 no-deploy recovery script (FUNCTIONS block extracted verbatim)."""
import hashlib
import json
import os
import subprocess
import tempfile
import unittest
from pathlib import Path

SCRIPT = Path(__file__).resolve().parents[1] / "scripts/qa/operators/qa525-recovery-6eed4-20261104.sh"
TEXT = SCRIPT.read_text()
SRC = "6eed4f00b7917da3cca36bbda70e16d16328648d"
DIGEST = "sha256:9bdeaa217f09f5d0e19e50d214a4abb936a2b89246ebbcf71fb71178e5574af2"
PEND = "cfe4e6573ece2ae81414cc252047d36cc56e5c73"
OLD = "41d9c490d138419c7286445f37e7aeddf0d1b8d5"
NONCE = "cd" * 32
CD = "route-a-custody-20261104T012737Z-3429524"


def sha(p):
    return hashlib.sha256(Path(p).read_bytes()).hexdigest()


def write(path, obj, mode=0o600):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(obj if isinstance(obj, str) else json.dumps(obj))
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
        self.baseline = {n: {"container_fingerprint": c * 64, "image_id": "sha256:" + c * 64, "running": True}
                         for n, c in (("api", "1"), ("web", "2"), ("db", "3"))}
        b = self.baseline
        write(self.state / "prepared.json", {
            "status": "PREPARED_API_WEB_SIGNATURE_VERIFIED_NO_DEPLOYMENT", "source_sha": SRC,
            "source_asset_sha256": DIGEST, "schema_mode": "support00_postdb", "review_mode": "single_operator_qa",
            "may_apply": False, "images_built": True, "services_unchanged": True, "baseline": b,
            "api_image_id": "sha256:" + "a" * 64, "web_image_id": "sha256:" + "b" * 64})
        write(self.state / ("dual-%s.json" % SRC), {
            "source_sha": SRC, "status": "RECOVERY_MANUALLY_REQUIRED",
            "old_images": {"api": b["api"]["image_id"], "web": b["web"]["image_id"]},
            "new_images": {"api": "sha256:" + "a" * 64, "web": "sha256:" + "b" * 64}})
        write(self.etc / "qa525-sensitive-reviewed.json", {"source_sha": SRC, "release_asset_sha256": DIGEST})
        write(self.etc / "qa525-single-operator-postdb-approved.json", {"source_sha": SRC, "release_asset_sha256": DIGEST})
        write(self.etc / "qa525-supervised-cut.json", {"target_sha": SRC, "release_asset_sha256": DIGEST, "nonce": NONCE})
        write(self.state / ("approval-consumed-" + NONCE), SRC + "\n")
        write(self.state / "dual-events.log", "1 DUAL_API_WEB_RECOVERY_REQUIRED %s\n" % SRC)
        for c in "12":
            write(self.state / ("dual-%s.json" % (c * 40)), {"status": "APPLIED", "source_sha": c * 40})
        # cfe4 custody archive (completed earlier)
        a = self.state / CD
        a.mkdir(mode=0o700)
        files = []
        for n in ("prepared.json", "qa525-sensitive-reviewed.json", "qa525-single-operator-postdb-approved.json", "qa525-supervised-cut.json", "dual-%s.json" % PEND):
            write(a / n, {"n": n})
            files.append({"name": n, "from": "x", "sha256": sha(a / n)})
        write(a / "ledger.json", {"status": "ROUTE_A_CUSTODY_LEDGER", "files": files})
        write(a / "receipt.json", {"status": "ROUTE_A_CUSTODY_COMPLETE_NO_DEPLOYMENT", "source_sha": PEND, "ledger_sha256": sha(a / "ledger.json"), "files": files})
        for n, bb in b.items():
            self.set_container(n, bb["container_fingerprint"], bb["image_id"], "running", OLD if n in ("api", "web") else "")
        write(self.t / "images" / ("sha256:" + "a" * 64), SRC, 0o644)
        write(self.t / "images" / ("sha256:" + "b" * 64), SRC, 0o644)
        self.public = {"environment": "QA_SYNTHETIC", "source_sha": OLD}
        self._stubs()
        self.originals = {p.name: sha(p) for p in self.five()}

    def set_container(self, n, cid, img, st, rev):
        write(self.t / "containers" / n, "%s %s %s centenario-qa-lab2 %s %s" % (cid, img, st, n, rev), 0o644)

    def five(self):
        return [self.state / "prepared.json", self.etc / "qa525-sensitive-reviewed.json",
                self.etc / "qa525-single-operator-postdb-approved.json", self.etc / "qa525-supervised-cut.json",
                self.state / ("dual-%s.json" % SRC)]

    def _stubs(self):
        def stub(name, body):
            p = self.bin / name
            p.write_text("#!/bin/bash\n" + body + "\n")
            p.chmod(0o755)
        T = str(self.t)
        stub("docker", ('if [ "$1" = image ]; then cat "@T@/images/${@: -1}" || exit 1; exit 0; fi; '
                        'name="${@: -1}"; name="${name#centenario-qa-lab2-}"; name="${name%-1}"; cat "@T@/containers/$name" || exit 1').replace("@T@", T))
        stub("curl", ('u="${@: -1}"; case "$u" in */qa-version.json) cat "@T@/public.json";; *) [ -e "@T@/health_fail" ] && exit 22; exit 0;; esac').replace("@T@", T))
        write(self.t / "public.json", self.public, 0o644)
        stub("mv", ('f="@T@/mvcount"; n=$(cat "$f" 2>/dev/null || echo 0); n=$((n+1)); echo $n > "$f"; '
                    '[ "${FAIL_MV_AT:-0}" = "$n" ] && exit 1; exec /usr/bin/mv "$@"').replace("@T@", T))

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
SRC={SRC}; DIGEST={DIGEST}; PEND={PEND}; OLD={OLD}; CUSTODY_DIR={CD}; STATE="{self.state}"; OWNER="$(id -u):$(id -g)"; SITE=https://x.invalid
J="$STATE/dual-$SRC.json"; P="$STATE/prepared.json"; M="$STATE/expired-prepared-archive-pending.json"
E="$STATE/dual-events.log"; CUT="{self.etc}/qa525-supervised-cut.json"
F=("$P" "{self.etc}/qa525-sensitive-reviewed.json" "{self.etc}/qa525-single-operator-postdb-approved.json" "$CUT" "$J")
LOCKS=({lock_arr})
source "{self.functions()}"
{hook}
{call}
'''
        return subprocess.run(["bash", "-c", script], capture_output=True, text=True, env=dict(os.environ, **(env or {})))

    def archive(self):
        d = sorted(self.state.glob("route-a-recovery-*"))
        return d[0] if d else None


class Recovery(unittest.TestCase):
    def setUp(self):
        self._d = tempfile.TemporaryDirectory()
        self.fx = Fixture(self._d.name)

    def tearDown(self):
        self._d.cleanup()

    def stop(self, code, mutate):
        mutate(self.fx)
        before = {str(p): (p.is_symlink(), sha(p) if p.exists() else None) for p in self.fx.five()}
        r = self.fx.run("run_recovery_locked")
        self.assertNotEqual(r.returncode, 0)
        self.assertIn("STOP_" + code, r.stdout)
        self.assertIsNone(self.fx.archive())
        self.assertFalse((self.fx.state / "expired-prepared-archive-pending.json").exists())
        self.assertEqual(before, {str(p): (p.is_symlink(), sha(p) if p.exists() else None) for p in self.fx.five()})

    def test_happy_path(self):
        hist = [self.fx.state / ("dual-%s.json" % (c * 40)) for c in "12"]
        cust = {p.name: sha(p) for p in (self.fx.state / CD).iterdir()}
        hb = {p.name: sha(p) for p in hist}
        r = self.fx.run("run_recovery_locked")
        self.assertEqual(r.returncode, 0, r.stdout + r.stderr)
        a = self.fx.archive()
        self.assertIn("QA525_RECOVERY=PASS_ARCHIVED", r.stdout)
        for p in self.fx.five():
            self.assertFalse(p.exists())
            self.assertEqual(sha(a / p.name), self.fx.originals[p.name])
        self.assertEqual(json.loads((a / "receipt.json").read_text())["status"], "MANUAL_RECOVERY_CONFIRMED_BASELINE_RESTORED_NO_DEPLOYMENT")
        self.assertTrue((a / "marker-cleared.json").exists())
        self.assertFalse((self.fx.state / "expired-prepared-archive-pending.json").exists())
        self.assertEqual(hb, {p.name: sha(p) for p in hist})
        self.assertEqual(cust, {p.name: sha(p) for p in (self.fx.state / CD).iterdir()})
        self.assertTrue((self.fx.state / ("approval-consumed-" + NONCE)).exists())
        self.assertIn("RECOVERY_REQUIRED", (self.fx.state / "dual-events.log").read_text())

    def test_interruption_after_every_rename(self):
        for n in range(1, 7):
            with tempfile.TemporaryDirectory() as d:
                fx = Fixture(d)
                r = fx.run("run_recovery_locked", {"FAIL_MV_AT": str(n)})
                self.assertNotEqual(r.returncode, 0, n)
                self.assertTrue((fx.state / "expired-prepared-archive-pending.json").exists(), n)
                a = fx.archive()
                for p in fx.five():
                    self.assertTrue(p.exists() or (a / p.name).exists(), (n, p.name))
                    if not p.exists():
                        self.assertEqual(sha(a / p.name), fx.originals[p.name])
                fx2 = fx.run("run_recovery_locked", {})
                self.assertIn("STOP_", fx2.stdout)

    def test_busy_lock(self):
        import fcntl
        fd = os.open(self.fx.locks[3], os.O_RDONLY)
        fcntl.flock(fd, fcntl.LOCK_EX)
        try:
            self.stop("RECOVERY_FAILED_SEE_ABOVE_MARKER_MAY_REMAIN", lambda fx: None)
        finally:
            os.close(fd)

    def test_journal_applied_or_rolled_back_status(self):
        self.stop("RECOVERY_FAILED_SEE_ABOVE_MARKER_MAY_REMAIN", lambda fx: write(fx.state / ("dual-%s.json" % SRC), {"source_sha": SRC, "status": "ROLLED_BACK"}))

    def codes(self, mutate):
        mutate(self.fx)
        r = self.fx.run("run_recovery_locked")
        self.assertNotEqual(r.returncode, 0)
        self.assertIsNone(self.fx.archive())
        return r.stdout

    def test_specific_codes(self):
        cases = {
            "JOURNAL_NOT_RECOVERY_REQUIRED": lambda fx: write(fx.state / ("dual-%s.json" % SRC), {"source_sha": SRC, "status": "ROLLED_BACK"}),
            "JOURNAL_NEXT_PRESENT": lambda fx: write(fx.state / ("dual-%s.json.next" % SRC), "{}"),
            "JOURNAL_OTHER_NOT_APPLIED": lambda fx: write(fx.state / ("dual-%s.json" % ("1" * 40)), {"source_sha": "1" * 40, "status": "PENDING"}),
            "RECOVERY_EVENT_MISSING": lambda fx: write(fx.state / "dual-events.log", "1 X other\n"),
            "TARGET_APPLIED_EVENT": lambda fx: write(fx.state / "dual-events.log", "DUAL_API_WEB_RECOVERY_REQUIRED:%s\nDUAL_API_WEB_APPLIED:%s\n" % (SRC, SRC)),
            "TARGET_ROLLED_BACK_EVENT": lambda fx: write(fx.state / "dual-events.log", "DUAL_API_WEB_RECOVERY_REQUIRED:%s\nDUAL_API_WEB_ROLLED_BACK %s\n" % (SRC, SRC)),
            "ARCHIVE_MARKER_PRESENT": lambda fx: write(fx.state / "expired-prepared-archive-pending.json", "{}"),
            "TARGET_NOT_OLD_REVISION": lambda fx: fx.set_container("api", "1" * 64, "sha256:" + "1" * 64, "running", SRC),
            "BASELINE_IMAGE_DRIFT": lambda fx: fx.set_container("web", "2" * 64, "sha256:" + "9" * 64, "running", OLD),
            "BASELINE_STATE": lambda fx: fx.set_container("api", "1" * 64, "sha256:" + "1" * 64, "exited", OLD),
            "DEPENDENCY_CONTAINER_CHANGED": lambda fx: fx.set_container("db", "9" * 64, "sha256:" + "3" * 64, "running", ""),
            "CANDIDATE_IMAGE_REVISION": lambda fx: write(fx.t / "images" / ("sha256:" + "a" * 64), OLD, 0o644),
            "PUBLIC_VERSION_NOT_OLD": lambda fx: write(fx.t / "public.json", {"environment": "QA_SYNTHETIC", "source_sha": SRC}, 0o644),
            "PUBLIC_HEALTH": lambda fx: write(fx.t / "health_fail", ""),
            "CUSTODY_HASH": lambda fx: write(fx.state / CD / "prepared.json", {"tampered": 1}),
            "CUSTODY_RECEIPT": lambda fx: write(fx.state / CD / "receipt.json", {"status": "ROUTE_A_CUSTODY_INCOMPLETE"}),
            "NONCE_NOT_CONSUMED_FOR_CUT": lambda fx: write(fx.state / ("approval-consumed-" + NONCE), "0" * 40 + "\n"),
            "PREPARED_MISMATCH": lambda fx: write(fx.state / "prepared.json", {"status": "X"}),
            "EVIDENCE_FILE_UNTRUSTED": lambda fx: os.chmod(fx.etc / "qa525-supervised-cut.json", 0o644),
        }
        for code, m in cases.items():
            with self.subTest(code=code):
                self.tearDown(); self.setUp()
                self.assertIn("STOP_" + code, self.codes(m))

    def test_find_failure_is_stop(self):
        for pat, code in (("dual-*.json.next", "JOURNAL_NEXT_SCAN"), ("dual-*.json", "JOURNAL_SCAN")):
            with self.subTest(code=code):
                self.tearDown(); self.setUp()
                def m(fx, pat=pat):
                    p = fx.bin / "find"
                    p.write_text('#!/bin/bash\n[ "${@: -1}" = "%s" ] && exit 2\nexec /usr/bin/find "$@"\n' % pat)
                    p.chmod(0o755)
                self.assertIn("STOP_" + code, self.codes(m))

    def test_events_read_error_is_not_absence(self):
        def m(fx):
            (fx.bin / "grep").write_text("#!/bin/bash\nexit 2\n")
            (fx.bin / "grep").chmod(0o755)
        self.assertIn("STOP_EVENTS_READ_ERROR", self.codes(m))

    def test_static_no_deploy_commands(self):
        import re
        for bad in ("--review-and-run", "docker compose", "compose up", "docker rm", "docker stop", "docker restart", "docker run", "docker kill", "docker start", "rm -", "rmdir", "unlink", "docker network", "docker volume"):
            self.assertNotIn(bad, TEXT, bad)
        self.assertEqual(set(re.findall(r"docker (\w+)", TEXT)), {"inspect", "image", "events", "exec", "ps"})

    def test_protected_custody_directory_checks_are_privileged(self):
        self.assertIn('sudo test -d "$A"', TEXT)
        self.assertIn('sudo test ! -L "$A"', TEXT)
        self.assertNotIn('[[ -d "$A"', TEXT)


if __name__ == "__main__":
    unittest.main()
