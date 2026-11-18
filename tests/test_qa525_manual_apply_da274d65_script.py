"""Hermetic tests for the QA525 manual API+WEB apply (route B). FUNCTIONS block extracted verbatim; docker/curl/sleep are stubs."""
import hashlib, json, os, subprocess, tempfile, unittest
from pathlib import Path

SCRIPT = Path(__file__).resolve().parents[1] / "scripts/qa/operators/qa525-manual-apply-da274d65-20261118.sh"
TEXT = SCRIPT.read_text()
SRC = "da274d651d8e9e056525413406c0aab62131504f"
DIGEST = "sha256:f869f3e12e048d70aaecf5e4e2f5b348d6378cf02aaa9fc0e5ee3243460f2e82"
OLD = "6eed4f00b7917da3cca36bbda70e16d16328648d"
NONCE = "cd" * 32
NAMES = ["api", "clamav", "gateway", "keycloak", "minio", "postgres", "web"]
OLDI = {"api": "sha256:" + "1" * 64, "web": "sha256:" + "2" * 64}
NEWI = {"api": "sha256:" + "a" * 64, "web": "sha256:" + "b" * 64}


def write(path, obj, mode=0o600):
    path = Path(path); path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(obj if isinstance(obj, str) else json.dumps(obj)); path.chmod(mode)


def sha(p):
    return hashlib.sha256(Path(p).read_bytes()).hexdigest()


@unittest.skipUnless(os.geteuid() == 0, "fixture needs root ownership like the host")
class Manual(unittest.TestCase):
    def setUp(self):
        self._d = tempfile.TemporaryDirectory(); self.t = Path(self._d.name)
        t = self.t; self.state = t / "state"; self.etc = t / "etc"; self.bin = t / "bin"; self.cont = t / "containers"
        self.state.mkdir(mode=0o700); self.bin.mkdir(); self.cont.mkdir(); self.etc.mkdir()
        self.locks = [t / "locks" / ("l%d.lock" % i) for i in range(5)]
        for lk in self.locks: write(lk, "")
        lay = {"api": [str(t / "lay" / x) for x in ("a0.json", "a1.json", "a2.json")], "web": [str(t / "lay" / x) for x in ("w0.json", "w1.json")]}
        for f in sum(lay.values(), []): write(f, "{}", 0o644)
        for f in sum(lay.values(), []): os.chmod(f, 0o644)
        write(self.state / "prepared.json", {"status": "PREPARED_API_WEB_SIGNATURE_VERIFIED_NO_DEPLOYMENT", "source_sha": SRC, "source_asset_sha256": DIGEST,
              "schema_mode": "support00_postdb", "review_mode": "single_operator_qa", "may_apply": False, "images_built": True, "services_unchanged": True,
              "baseline": self.base(), "api_image_id": NEWI["api"], "web_image_id": NEWI["web"]})
        write(self.state / ("dual-%s.json" % SRC), {"source_sha": SRC, "status": "RECOVERY_MANUALLY_REQUIRED", "old_images": OLDI, "new_images": NEWI, "original_layers": lay})
        write(self.etc / "qa525-sensitive-reviewed.json", {"source_sha": SRC, "release_asset_sha256": DIGEST})
        write(self.etc / "qa525-single-operator-postdb-approved.json", {"source_sha": SRC, "release_asset_sha256": DIGEST})
        write(self.etc / "qa525-supervised-cut.json", {"target_sha": SRC, "release_asset_sha256": DIGEST, "nonce": NONCE})
        write(self.state / ("approval-consumed-" + NONCE), SRC + "\n")
        write(self.state / "dual-events.log", "1 DUAL_API_WEB_RECOVERY_REQUIRED %s\n" % SRC)
        for c in "12": write(self.state / ("dual-%s.json" % (c * 40)), {"status": "APPLIED", "source_sha": c * 40})
        for s, im in (("api", NEWI["api"]), ("web", NEWI["web"]), ("api", OLDI["api"]), ("web", OLDI["web"])):
            write(self.state / ("qa525-%s-%s-%s.json" % (s, SRC, im[7:])), json.dumps({"services": {s: {"image": im}}}, sort_keys=False) + "\n")
        for n in NAMES:
            self.set(n, "c" * 64 if n in OLDI else "d" * 64, OLDI.get(n, "sha256:" + "9" * 64), "running")
        for n in ("db-grants", "migrator", "minio-init"): self.set(n, "e" * 64, "sha256:" + "8" * 64, "exited")
        for n, im in (("api", "x"), ("web", "y")): pass
        write(t / "ctl", "")  # flags dir marker
        self.stubs(); self.orig_journal = sha(self.state / ("dual-%s.json" % SRC))

    def tearDown(self): self._d.cleanup()

    def base(self):
        return {n: {"container_fingerprint": "c" * 64 if n in OLDI else "d" * 64, "image_id": OLDI.get(n, "sha256:" + "9" * 64), "running": True} for n in NAMES}

    def set(self, n, cid, img, st):
        rev = OLD if n in ("api", "web") else ""
        write(self.cont / n, "%s %s %s centenario-qa-lab2 %s %s" % (cid, img, st, n, rev), 0o644)

    def stubs(self):
        T = str(self.t)
        def stub(name, body):
            p = self.bin / name; p.write_text("#!/bin/bash\n" + body.replace("@T@", T) + "\n"); p.chmod(0o755)
        # docker inspect: print container file; docker compose up: pick image from last --file, flip service image
        stub("docker", r'''
echo "$@" >> @T@/docker.log
if [ "$1" = image ]; then echo "da274d651d8e9e056525413406c0aab62131504f"; exit 0; fi
if [ "$1" = inspect ]; then n="${@: -1}"; n="${n#centenario-qa-lab2-}"; n="${n%-1}"; cat "@T@/containers/$n" || exit 1; exit 0; fi
if [ "$1" = compose ]; then
  svc="${@: -1}"; ov=""; prev=""; for a in "$@"; do [ "$prev" = --file ] && ov="$a"; prev="$a"; done
  c=$(cat @T@/ctl_count 2>/dev/null || echo 0); c=$((c+1)); echo $c > @T@/ctl_count
  [ "${FAIL_COMPOSE_AT:-0}" = "$c" ] && exit 1
  [ "$svc" = api ] && [ -n "${SLOW_AFTER_API:-}" ] && echo "$SLOW_AFTER_API" > @T@/slow
  [ -n "${HEALTH_FAIL_AFTER_COMPOSE:-}" ] && : > @T@/health_fail
  img=$(jq -r ".services.$svc.image" "$ov")
  echo "$(cut -d' ' -f1 @T@/containers/$svc | sed 's/./f/g') $img running centenario-qa-lab2 $svc $(cut -d' ' -f6 @T@/containers/$svc)" > @T@/containers/$svc
  # rev follows the image: new image => da274d65, else OLD
  exit 0
fi
exit 0''')
        stub("sleep", "exit 0")
        # curl: health paths succeed after SLOW_API failures; qa-version follows web image
        stub("curl", r'''
u="${@: -1}"; echo "$u" >> @T@/curl.log
case "$u" in
 */qa-version.json) w=$(cut -d' ' -f2 @T@/containers/web)
   if [ "$w" = "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb" ] && [ ! -e @T@/web_bad ]; then s=da274d651d8e9e056525413406c0aab62131504f; else s=6eed4f00b7917da3cca36bbda70e16d16328648d; fi
   echo "{\"environment\":\"QA_SYNTHETIC\",\"source_sha\":\"$s\"}";;
 *) n=$(cat @T@/slow 2>/dev/null || echo 0); if [ "$n" -gt 0 ]; then echo $((n-1)) > @T@/slow; exit 22; fi
    [ -e @T@/health_fail ] && exit 22; exit 0;;
esac''')

    def go(self, call, env=None):
        arr = " ".join('"%s"' % x for x in self.locks)
        block = TEXT.split("# >>> FUNCTIONS", 1)[1].split("\n", 1)[1].split("# <<< FUNCTIONS", 1)[0].replace("/etc/pmgm/", str(self.etc) + "/")
        (self.t / "fn.sh").write_text(block)
        script = f'''set -Eeuo pipefail
export PATH="{self.bin}:$PATH"
sudo() {{ "$@"; }}
SRC={SRC}; DIGEST={DIGEST}; OLD={OLD}; G6_END=$(( $(date -u +%s) + 90000 )); WORKDIR="{self.t}"; ENVF=/dev/null; PROJ=centenario-qa-lab2
DEPS=(keycloak postgres minio gateway clamav); ONES=(db-grants migrator minio-init); HPATHS=(/ /health/web /health/ready /identity/realms/pmgm/.well-known/openid-configuration)
STATE="{self.state}"; OWNER="$(id -u):$(id -g)"; SITE=https://x.invalid
J="$STATE/dual-$SRC.json"; P="$STATE/prepared.json"; M="$STATE/expired-prepared-archive-pending.json"
E="$STATE/dual-events.log"; CUT="{self.etc}/qa525-supervised-cut.json"
F=("$P" "{self.etc}/qa525-sensitive-reviewed.json" "{self.etc}/qa525-single-operator-postdb-approved.json" "$CUT" "$J")
LOCKS=({arr})
source "{self.t}/fn.sh"
{call}
'''
        return subprocess.run(["bash", "-c", script], capture_output=True, text=True, env=dict(os.environ, **(env or {})))

    def receipts(self): return sorted(self.state.glob("manual-apply-*"))
    def images(self): return {n: (self.cont / n).read_text().split()[1] for n in ("api", "web")}

    def test_happy_path_waits_for_slow_api(self):
        # new web image must make qa-version return the new sha; new api image flips only image
        r = self.go("run_apply_locked", {"SLOW_AFTER_API": "6"})  # API not listening for the first 6 probes after recreate
        self.assertEqual(r.returncode, 0, r.stdout + r.stderr)
        self.assertIn("QA525_MANUAL_APPLY=PASS_VERIFIED", r.stdout)
        self.assertEqual(self.images(), NEWI)
        res = [p for p in self.receipts() if p.name.endswith(".result.json")]
        self.assertEqual(json.loads(res[0].read_text())["status"], "MANUAL_APPLY_VERIFIED_API_WEB")
        self.assertEqual(sha(self.state / ("dual-%s.json" % SRC)), self.orig_journal)
        self.assertTrue((self.state / ("approval-consumed-" + NONCE)).exists())

    def test_web_compose_failure_rolls_back_verified(self):
        r = self.go("run_apply_locked", {"FAIL_COMPOSE_AT": "2"})
        self.assertNotEqual(r.returncode, 0)
        self.assertIn("MANUAL_APPLY_FAILED_ROLLED_BACK_OK", r.stdout)
        self.assertEqual(self.images(), OLDI)
        res = [p for p in self.receipts() if p.name.endswith(".result.json")]
        self.assertEqual(json.loads(res[0].read_text())["status"], "MANUAL_APPLY_ROLLED_BACK_VERIFIED")
        self.assertEqual(sha(self.state / ("dual-%s.json" % SRC)), self.orig_journal)

    def test_public_version_never_new_rolls_back(self):
        write(self.t / "web_bad", "")
        r = self.go("run_apply_locked")
        self.assertNotEqual(r.returncode, 0)
        self.assertIn("MANUAL_APPLY_FAILED_ROLLED_BACK_OK", r.stdout)
        self.assertEqual(self.images(), OLDI)

    def test_rollback_failure_is_reported_not_hidden(self):
        r = self.go("run_apply_locked", {"HEALTH_FAIL_AFTER_COMPOSE": "1"})  # health never recovers: forward and rollback both fail
        self.assertNotEqual(r.returncode, 0)
        self.assertIn("MANUAL_APPLY_ROLLBACK_FAILED_MANUAL_RECOVERY", r.stdout)
        res = [p for p in self.receipts() if p.name.endswith(".result.json")]
        self.assertEqual(json.loads(res[0].read_text())["status"], "MANUAL_APPLY_ROLLBACK_FAILED_MANUAL_RECOVERY")

    def test_dependency_container_changed_during_apply_rolls_back(self):
        # compose stub recreates only api/web; simulate a dependency recreation by wrapping docker
        p = self.bin / "docker"; body = p.read_text()
        p.write_text(body.replace('if [ "$1" = compose ]; then', 'if [ "$1" = compose ]; then echo "ffff sha256:9 running centenario-qa-lab2 minio " > @T@/containers/minio;'.replace("@T@", str(self.t)), 1))
        r = self.go("run_apply_locked")
        self.assertNotEqual(r.returncode, 0)
        self.assertNotIn("PASS_VERIFIED", r.stdout)

    def stop_code(self, code, mutate):
        mutate(); before = self.images(); r = self.go("run_apply_locked")
        self.assertNotEqual(r.returncode, 0)
        self.assertIn("STOP_" + code, r.stdout + r.stderr)
        self.assertEqual(self.receipts(), []); self.assertEqual(self.images(), before)
        self.assertFalse((self.t / "docker.log").exists() and any(" compose " in (" " + l) for l in (self.t / "docker.log").read_text().splitlines()))

    def test_gates_stop_before_any_compose(self):
        cases = {
            "JOURNAL_NOT_RECOVERY_REQUIRED": lambda: write(self.state / ("dual-%s.json" % SRC), {"source_sha": SRC, "status": "ROLLED_BACK", "old_images": OLDI, "new_images": NEWI}),
            "ARCHIVE_MARKER_PRESENT": lambda: write(self.state / "expired-prepared-archive-pending.json", "{}"),
            "JOURNAL_OTHER_NOT_APPLIED": lambda: write(self.state / ("dual-%s.json" % ("1" * 40)), {"source_sha": "1" * 40, "status": "PENDING"}),
            "TARGET_APPLIED_EVENT": lambda: write(self.state / "dual-events.log", "DUAL_API_WEB_RECOVERY_REQUIRED:%s\nDUAL_API_WEB_APPLIED:%s\n" % (SRC, SRC)),
            "BASELINE_IMAGE_DRIFT": lambda: self.set("web", "2" * 64, "sha256:" + "7" * 64, "running"),
            "BASELINE_STATE": lambda: self.set("api", "c" * 64, OLDI["api"], "exited"),
            "PREPARED_MISMATCH": lambda: write(self.state / "prepared.json", {"status": "X"}),
            "NONCE_NOT_CONSUMED_FOR_CUT": lambda: write(self.state / ("approval-consumed-" + NONCE), "0" * 40 + "\n"),
            "OVERRIDE_CONTENT": lambda: write(self.state / ("qa525-api-%s-%s.json" % (SRC, NEWI["api"][7:])), '{"services": {"api": {"image": "sha256:evil"}}}\n'),
            "OVERRIDE_MISSING_OR_UNTRUSTED": lambda: (self.state / ("qa525-web-%s-%s.json" % (SRC, OLDI["web"][7:]))).unlink(),
            "LAYER_UNTRUSTED": lambda: os.chmod(self.t / "lay" / "a1.json", 0o666),
            "G6_WINDOW_TOO_CLOSE_OR_EXPIRED": lambda: None,
        }
        for code, m in cases.items():
            with self.subTest(code=code):
                self.tearDown(); self.setUp()
                if code == "G6_WINDOW_TOO_CLOSE_OR_EXPIRED":
                    r = self.go("G6_END=$(( $(date -u +%s) + 100 )); run_apply_locked")
                    self.assertIn("STOP_" + code, r.stdout); self.assertEqual(self.receipts(), [])
                else:
                    self.stop_code(code, m)

    def test_a0_prereq_checks_layers_and_overrides_without_changes(self):
        r = self.go("prereq_ok; echo PREREQ_OK")
        self.assertIn("PREREQ_OK", r.stdout, r.stdout + r.stderr)
        for code, m in (("OVERRIDE_CONTENT", lambda: write(self.state / ("qa525-api-%s-%s.json" % (SRC, OLDI["api"][7:])), "{}\n")),
                        ("OVERRIDE_MISSING_OR_UNTRUSTED", lambda: (self.state / ("qa525-web-%s-%s.json" % (SRC, NEWI["web"][7:]))).unlink()),
                        ("LAYER_UNTRUSTED", lambda: os.chmod(self.t / "lay" / "w1.json", 0o666))):
            with self.subTest(code=code):
                self.tearDown(); self.setUp(); m()
                r = self.go("prereq_ok; echo PREREQ_OK")
                self.assertIn("STOP_" + code, r.stdout + r.stderr); self.assertNotIn("PREREQ_OK", r.stdout)

    def test_busy_lock(self):
        import fcntl
        fd = os.open(self.locks[3], os.O_RDONLY); fcntl.flock(fd, fcntl.LOCK_EX)
        try:
            r = self.go("run_apply_locked")
            self.assertNotEqual(r.returncode, 0); self.assertEqual(self.receipts(), []); self.assertEqual(self.images(), OLDI)
        finally: os.close(fd)

    def test_static_safety(self):
        import re
        for bad in ("--review-and-run", "docker rm", "docker stop", "docker restart", "docker kill", "docker run", "docker network", "docker volume", "compose down", "rm -", "rmdir", "unlink", "psql", "dual-events.log >", "> \"$J\"", "mv "):
            self.assertNotIn(bad, TEXT, bad)
        self.assertEqual(set(re.findall(r"docker (\w+)", TEXT)) - {"inspect", "image", "compose"}, set())
        self.assertIn("--no-deps", TEXT); self.assertIn("--no-build", TEXT); self.assertIn("--pull never", TEXT)


if __name__ == "__main__":
    unittest.main()
