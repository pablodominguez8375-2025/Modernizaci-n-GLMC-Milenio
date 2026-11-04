"""Hermetic tests for the Route A operator script: real flock/mv/sync/jq on throw-away fixtures.

The FUNCTIONS block of the script is extracted verbatim; only absolute host paths and the
root owner are substituted. No host, no network, no sudo (sudo is a pass-through function).
"""
import hashlib
import json
import fcntl
import os
import re
import stat
import subprocess
import tempfile
import unittest
from pathlib import Path

SCRIPT = Path(__file__).resolve().parents[1] / "scripts/qa/operators/qa525-route-a-6eed4-20261103.sh"
TEXT = SCRIPT.read_text()
PEND = "cfe4e6573ece2ae81414cc252047d36cc56e5c73"
DIGEST = "sha256:fab78644c2fe95d2f747794c0f85b21cb6ed438679bb11fd629e9a9e54b5d358"
NONCE = "ab" * 32
URL = "https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/525#issuecomment-1"


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
        baseline = {n: {"container_fingerprint": c * 64, "image_id": "sha256:" + c * 64, "running": True}
                    for n, c in (("api", "1"), ("web", "2"), ("db", "3"))}
        self.baseline = baseline
        write(self.state / "prepared.json", {
            "status": "PREPARED_API_WEB_SIGNATURE_VERIFIED_NO_DEPLOYMENT", "source_sha": PEND,
            "source_asset_sha256": DIGEST, "schema_mode": "support00_postdb",
            "review_mode": "single_operator_qa", "may_apply": False, "images_built": True,
            "services_unchanged": True, "baseline": baseline,
            "api_image_id": "sha256:" + "a" * 64, "web_image_id": "sha256:" + "b" * 64})
        write(self.state / ("dual-%s.json" % PEND), {
            "source_sha": PEND, "status": "PENDING",
            "old_images": {"api": baseline["api"]["image_id"], "web": baseline["web"]["image_id"]},
            "new_images": {"api": "sha256:" + "a" * 64, "web": "sha256:" + "b" * 64}})
        write(self.etc / "qa525-sensitive-reviewed.json", {"source_sha": PEND, "release_asset_sha256": DIGEST})
        write(self.etc / "qa525-single-operator-postdb-approved.json", {"source_sha": PEND, "release_asset_sha256": DIGEST})
        write(self.etc / "qa525-supervised-cut.json", {"target_sha": PEND, "release_asset_sha256": DIGEST, "nonce": NONCE})
        write(self.state / ("approval-consumed-" + NONCE), PEND + "\n")
        for n, b in baseline.items():
            write(self.t / "containers" / n, "%s %s running centenario-qa-lab2 %s" % (b["container_fingerprint"], b["image_id"], n), 0o644)
        self._stubs()
        self.originals = {p.name: sha(p) for p in self.five()}

    def five(self):
        return [self.state / "prepared.json", self.etc / "qa525-sensitive-reviewed.json",
                self.etc / "qa525-single-operator-postdb-approved.json",
                self.etc / "qa525-supervised-cut.json", self.state / ("dual-%s.json" % PEND)]

    def _stubs(self):
        def stub(name, body):
            p = self.bin / name
            p.write_text("#!/bin/bash\n" + body + "\n")
            p.chmod(0o755)
        stub("docker", 'name="${@: -1}"; name="${name#centenario-qa-lab2-}"; name="${name%-1}"; cat "@T@/containers/$name" || exit 1'.replace("@T@", str(self.t)))
        stub("mv", ('f="@T@/mvcount"; n=$(cat "$f" 2>/dev/null || echo 0); n=$((n+1)); echo $n > "$f"; '
                   '[ "${FAIL_MV_AT:-0}" = "$n" ] && exit 1; exec /usr/bin/mv "$@"').replace("@T@", str(self.t)))
        stub("grep_fail", "exit 2")

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
PEND={PEND}; PENDDIGEST={DIGEST}; STATE="{self.state}"; OWNER="$(id -u):$(id -g)"; MIN_RL=58
J="$STATE/dual-$PEND.json"; P="$STATE/prepared.json"; M="$STATE/expired-prepared-archive-pending.json"
E="$STATE/dual-events.log"; CUT="{self.etc}/qa525-supervised-cut.json"
F=("$P" "{self.etc}/qa525-sensitive-reviewed.json" "{self.etc}/qa525-single-operator-postdb-approved.json" "$CUT" "$J")
LOCKS=({lock_arr})
source "{self.functions()}"
{hook}
{call}
'''
        e = dict(os.environ, **(env or {}))
        return subprocess.run(["bash", "-c", script], capture_output=True, text=True, env=e)

    def archive(self):
        dirs = sorted(self.state.glob("route-a-custody-*"))
        return dirs[0] if dirs else None


def hook_for(fx, phase, idx, action):
    lock = fx.locks[idx]
    body = {
        "swap": '/usr/bin/mv "$2" "$2.old"; : > "$2"; chmod 600 "$2"',
        "delete": '/usr/bin/mv "$2" "$2.old"',
        "symlink": '/usr/bin/mv "$2" "$2.old"; ln -s "$2.old" "$2"',
    }[action]
    return 'lock_hook() { if [[ "$1" == %s && "$2" == "%s" ]]; then %s; fi; }' % (phase, lock, body)


class Custody(unittest.TestCase):
    def setUp(self):
        self._d = tempfile.TemporaryDirectory()
        self.fx = Fixture(self._d.name)

    def tearDown(self):
        self._d.cleanup()

    def assert_untouched(self):
        self.assertIsNone(self.fx.archive())
        for p in self.fx.five():
            self.assertEqual(sha(p), self.fx.originals[p.name])
        self.assertFalse((self.fx.state / "expired-prepared-archive-pending.json").exists())

    def test_happy_path_moves_five_records_with_ledger_and_receipt(self):
        r = self.fx.run("run_custody_locked")
        self.assertEqual(r.returncode, 0, r.stdout + r.stderr)
        a = self.fx.archive()
        self.assertIn("QA525_ROUTE_A_CUSTODY=PASS", r.stdout)
        for p in self.fx.five():
            self.assertFalse(p.exists())
            self.assertEqual(sha(a / p.name), self.fx.originals[p.name])
        ledger = json.loads((a / "ledger.json").read_text())
        self.assertEqual({f["name"]: f["sha256"] for f in ledger["files"]}, self.fx.originals)
        self.assertEqual(json.loads((a / "receipt.json").read_text())["status"], "ROUTE_A_CUSTODY_COMPLETE_NO_DEPLOYMENT")
        self.assertTrue((a / "marker-cleared.json").exists())
        self.assertFalse((self.fx.state / "expired-prepared-archive-pending.json").exists())
        self.assertTrue((self.fx.state / ("approval-consumed-" + NONCE)).exists())
        for name in ("ledger.json", "receipt.json", "marker-cleared.json"):
            self.assertEqual(stat.S_IMODE((a / name).stat().st_mode), 0o600)

    def test_interruption_after_every_rename_keeps_bytes_and_blocker(self):
        for n in range(1, 7):
            with tempfile.TemporaryDirectory() as d:
                fx = Fixture(d)
                r = fx.run("run_custody_locked", {"FAIL_MV_AT": str(n)})
                self.assertNotEqual(r.returncode, 0, n)
                a = fx.archive()
                self.assertIsNotNone(a)
                ledger = json.loads((a / "ledger.json").read_text())  # durable BEFORE the first move
                self.assertEqual({f["name"]: f["sha256"] for f in ledger["files"]}, fx.originals)
                marker = fx.state / "expired-prepared-archive-pending.json"
                self.assertTrue(marker.exists(), "blocker must persist after failure at move %d" % n)
                for p in fx.five():  # every byte is recoverable from exactly one place
                    here = p.exists()
                    there = (a / p.name).exists()
                    self.assertTrue(here != there, (n, p.name))
                    self.assertEqual(sha(p if here else a / p.name), fx.originals[p.name])

    def test_busy_lock_aborts_before_any_change(self):
        for i in range(5):
            with tempfile.TemporaryDirectory() as d:
                fx = Fixture(d)
                with open(fx.locks[i], "r+") as held:
                    fcntl.flock(held, fcntl.LOCK_EX | fcntl.LOCK_NB)
                    r = fx.run("run_custody_locked")
                self.assertNotEqual(r.returncode, 0)
                self.assertIn("LOCKS_BUSY_ANOTHER_OPERATOR", r.stdout)
                self.assertIsNone(fx.archive())
                for p in fx.five():
                    self.assertEqual(sha(p), fx.originals[p.name])


    def test_lock_replaced_or_removed_between_validation_open_and_acquire(self):
        cases = [("pre_open", "swap", "LOCK_IDENTITY_CHANGED"),
                 ("pre_open", "delete", "LOCK_OPEN_FAILED_NOT_CREATED"),
                 ("pre_open", "symlink", "LOCK_PATH_REPLACED"),
                 ("pre_acquire", "swap", "LOCK_PATH_REPLACED"),
                 ("pre_acquire", "delete", "LOCK_PATH_REPLACED"),
                 ("post_acquire", "swap", "LOCK_PATH_REPLACED"),
                 ("post_acquire", "delete", "LOCK_PATH_REPLACED"),
                 ("post_acquire", "symlink", "LOCK_PATH_REPLACED")]
        for phase, action, code in cases:
            for idx in (0, 4):
                with tempfile.TemporaryDirectory() as d:
                    fx = Fixture(d)
                    r = fx.run("run_custody_locked", hook=hook_for(fx, phase, idx, action))
                    label = (phase, action, idx, r.stdout)
                    self.assertNotEqual(r.returncode, 0, label)
                    self.assertIn("STOP_" + code, r.stdout, label)
                    self.assertIsNone(fx.archive(), label)
                    for p in fx.five():
                        self.assertEqual(sha(p), fx.originals[p.name], label)
                    if action == "delete":  # a lock that vanished is never re-created
                        self.assertFalse(fx.locks[idx].exists(), label)

    def test_original_lock_held_elsewhere_while_path_is_replaced_never_passes(self):
        fx = self.fx
        with open(fx.locks[0], "r+") as held:  # the "other operator" keeps the original inode
            fcntl.flock(held, fcntl.LOCK_EX | fcntl.LOCK_NB)
            r = fx.run("run_custody_locked", hook=hook_for(fx, "pre_open", 0, "swap"))
        self.assertNotEqual(r.returncode, 0, r.stdout)
        self.assertNotIn("QA525_ROUTE_A_CUSTODY=PASS", r.stdout)
        self.assertIsNone(fx.archive())

    def test_missing_or_loose_lock_file_stops(self):
        self.fx.locks[2].unlink()
        r = self.fx.run("run_custody_locked")
        self.assertIn("STOP_LOCK_FILE_UNTRUSTED_OR_MISSING", r.stdout)
        self.assertFalse(self.fx.locks[2].exists(), "locks must never be created")
        self.assert_untouched()

    def expect_stop(self, code, mutate):
        mutate(self.fx)
        before = {str(p): (p.is_symlink(), sha(p) if p.exists() else None) for p in self.fx.five()}
        r = self.fx.run("run_custody_locked")
        self.assertNotEqual(r.returncode, 0)
        self.assertIn("STOP_" + code, r.stdout)
        self.assertIsNone(self.fx.archive())
        self.assertFalse((self.fx.state / "expired-prepared-archive-pending.json").exists() and code != "ARCHIVE_MARKER_PRESENT")
        self.assertEqual(before, {str(p): (p.is_symlink(), sha(p) if p.exists() else None) for p in self.fx.five()})

    def test_target_event_present(self):
        self.expect_stop("TARGET_EVENT_PRESENT", lambda fx: write(fx.state / "dual-events.log", "1 X %s\n" % PEND))

    def test_events_read_error_is_not_absence(self):
        write(self.fx.state / "dual-events.log", "1 X other\n")
        self.fx.bin.joinpath("grep").write_text("#!/bin/bash\nexit 2\n")
        self.fx.bin.joinpath("grep").chmod(0o755)
        r = self.fx.run("run_custody_locked")
        self.assertIn("STOP_EVENTS_READ_ERROR", r.stdout)
        self.assert_untouched()

    def test_unrelated_event_log_is_fine(self):
        write(self.fx.state / "dual-events.log", "1 DUAL_API_WEB_APPLIED %s\n" % ("0" * 40))
        r = self.fx.run("run_custody_locked")
        self.assertEqual(r.returncode, 0, r.stdout)

    def test_nonce_marker_missing_or_wrong(self):
        self.expect_stop("NONCE_MARKER_UNTRUSTED", lambda fx: (fx.state / ("approval-consumed-" + NONCE)).unlink())

    def test_nonce_for_other_cut(self):
        self.expect_stop("NONCE_NOT_CONSUMED_FOR_CUT", lambda fx: write(fx.state / ("approval-consumed-" + NONCE), "0" * 40 + "\n"))

    def test_same_image_different_container_id(self):
        self.expect_stop("BASELINE_DRIFT", lambda fx: write(fx.t / "containers" / "api", "%s %s running centenario-qa-lab2 api" % ("9" * 64, fx.baseline["api"]["image_id"]), 0o644))

    def test_container_not_running(self):
        self.expect_stop("BASELINE_STATE", lambda fx: write(fx.t / "containers" / "web", "%s %s exited centenario-qa-lab2 web" % ("2" * 64, fx.baseline["web"]["image_id"]), 0o644))

    def test_other_journal_or_marker_or_next(self):
        self.expect_stop("JOURNAL_OTHER_NOT_APPLIED", lambda fx: write(fx.state / ("dual-%s.json" % ("1" * 40)), {"status": "APPLIED"}))

    def _hist(self, fx, sha40, **kw):
        obj = {"status": "APPLIED", "source_sha": sha40}
        obj.update(kw.pop("obj", {}))
        write(fx.state / ("dual-%s.json" % sha40), obj, kw.get("mode", 0o600))

    def test_historical_applied_journals_allowed_and_untouched(self):
        hs = [self.fx.state / ("dual-%s.json" % (c * 40)) for c in "12"]
        for c in "12":
            self._hist(self.fx, c * 40)
        before = {p.name: sha(p) for p in hs}
        r = self.fx.run("run_custody_locked")
        self.assertEqual(r.returncode, 0, r.stdout + r.stderr)
        self.assertEqual(before, {p.name: sha(p) for p in hs})
        self.assertFalse(any((self.fx.archive() / p.name).exists() for p in hs))

    def test_historical_other_pending(self):
        self.expect_stop("JOURNAL_OTHER_NOT_APPLIED", lambda fx: self._hist(fx, "1" * 40, obj={"status": "PENDING"}))

    def test_historical_source_mismatch(self):
        self.expect_stop("JOURNAL_OTHER_NOT_APPLIED", lambda fx: self._hist(fx, "1" * 40, obj={"source_sha": "2" * 40}))

    def test_historical_bad_filename(self):
        self.expect_stop("JOURNAL_OTHER_NAME", lambda fx: write(fx.state / "dual-old.json", {"status": "APPLIED"}))

    def test_historical_loose_mode(self):
        self.expect_stop("JOURNAL_OTHER_UNTRUSTED", lambda fx: self._hist(fx, "1" * 40, mode=0o644))

    def test_historical_symlink(self):
        def m(fx):
            write(fx.t / "elsewhere.json", {"status": "APPLIED", "source_sha": "1" * 40})
            (fx.state / ("dual-%s.json" % ("1" * 40))).symlink_to(fx.t / "elsewhere.json")
        self.expect_stop("JOURNAL_OTHER_UNTRUSTED", m)

    def test_historical_not_dict(self):
        self.expect_stop("JOURNAL_OTHER_NOT_APPLIED", lambda fx: write(fx.state / ("dual-%s.json" % ("1" * 40)), "[]"))

    def test_historical_next_present(self):
        def m(fx):
            self._hist(fx, "1" * 40)
            write(fx.state / ("dual-%s.json.next" % ("1" * 40)), "{}")
        self.expect_stop("JOURNAL_NEXT_PRESENT", m)

    def test_historical_next_symlink(self):
        def m(fx):
            (fx.state / ("dual-%s.json.next" % ("1" * 40))).symlink_to(fx.t / "nowhere")
        self.expect_stop("JOURNAL_NEXT_PRESENT", m)

    def test_existing_blocker_marker(self):
        self.expect_stop("ARCHIVE_MARKER_PRESENT", lambda fx: write(fx.state / "expired-prepared-archive-pending.json", "{}"))

    def test_journal_next_present(self):
        self.expect_stop("JOURNAL_NEXT_PRESENT", lambda fx: write(fx.state / ("dual-%s.json.next" % PEND), "{}"))

    def test_changed_evidence_content(self):
        self.expect_stop("JOURNAL_NOT_PENDING", lambda fx: write(fx.state / ("dual-%s.json" % PEND), {"source_sha": PEND, "status": "APPLIED"}))

    def test_loose_mode_file(self):
        self.expect_stop("EVIDENCE_FILE_UNTRUSTED", lambda fx: fx.five()[1].chmod(0o644))

    def test_hardlinked_file(self):
        self.expect_stop("EVIDENCE_FILE_UNTRUSTED", lambda fx: os.link(fx.five()[2], fx.t / "extra-link"))

    def test_symlinked_file(self):
        def mutate(fx):
            real = fx.t / "real.json"
            fx.five()[3].rename(real)
            os.symlink(real, fx.five()[3])
        self.expect_stop("EVIDENCE_FILE_UNTRUSTED", mutate)

    def test_journal_prepared_binding(self):
        self.expect_stop("JOURNAL_PREPARED_BINDING", lambda fx: write(fx.state / ("dual-%s.json" % PEND), {
            "source_sha": PEND, "status": "PENDING",
            "old_images": {"api": fx.baseline["api"]["image_id"], "web": fx.baseline["web"]["image_id"]},
            "new_images": {"api": "sha256:" + "c" * 64, "web": "sha256:" + "b" * 64}}))

    def test_prepared_for_other_digest(self):
        def mutate(fx):
            d = json.loads((fx.state / "prepared.json").read_text())
            d["source_asset_sha256"] = "sha256:" + "0" * 64
            write(fx.state / "prepared.json", d)
        self.expect_stop("PREPARED_MISMATCH", mutate)


class StaticAndGuards(unittest.TestCase):
    def run_script(self, *args, host="other"):
        with tempfile.TemporaryDirectory() as d:
            log = Path(d) / "calls.log"
            for name in ("sudo", "curl", "docker"):
                p = Path(d) / name
                p.write_text('#!/bin/sh\necho "$0 $@" >> "%s"; exit 1\n' % log)
                p.chmod(0o755)
            for name, out in (("hostname", host), ("id", "soporte")):
                p = Path(d) / name
                p.write_text("#!/bin/sh\necho %s\n" % out)
                p.chmod(0o755)
            copy = Path(d) / "s.sh"
            copy.write_text(TEXT)
            p = subprocess.run(["bash", str(copy), *args], capture_output=True, text=True,
                               env={"PATH": d + ":/usr/bin:/bin", "HOME": d})
            return p.returncode, p.stdout, log.read_text() if log.exists() else ""

    def test_syntax(self):
        subprocess.run(["bash", "-n", str(SCRIPT)], check=True)

    def test_review_url_is_pure_json_comment(self):
        self.assertIn("issues/525#issuecomment-6604960072", TEXT)

    def test_wrong_host_stops_before_any_sudo(self):
        rc, out, calls = self.run_script()
        self.assertEqual((rc, calls), (2, ""))
        self.assertIn("STOP_HOST_OR_USER_NO_RETRY", out)

    def test_bad_argument_rejected(self):
        for args in (("--go",), ("READ_ONLY", "x")):
            rc, out, calls = self.run_script(*args)
            self.assertEqual((rc, calls), (2, ""))
            self.assertIn("STOP_BAD_ARGUMENT_NO_RETRY", out)

    def test_single_deploying_command_and_no_deletes(self):
        self.assertEqual(len(re.findall(r'--review-and-run', TEXT)), 1)
        self.assertNotRegex(TEXT, r'\brm\s+-|\bsudo\s+rm\b|\brmdir\b|\bunlink\b')
        for line in TEXT.splitlines():
            if re.search(r'\bmv\b', line) and not line.lstrip().startswith("#"):
                self.assertIn("mv -n", line)
        self.assertNotRegex(TEXT, r'docker\s+(run|rm|compose|restart|stop|kill)')
        self.assertNotRegex(TEXT, r'\b(INSERT|UPDATE|DELETE|DROP|ALTER|TRUNCATE)\b')

    def test_mutations_only_after_execute_gate(self):
        gate = TEXT.index('[[ "$MODE" == EXECUTE_A1_A2 ]] || {')
        tail = TEXT[gate:]
        self.assertIn("run_custody_locked\n", tail)
        self.assertLess(tail.index("run_custody_locked\n"), tail.index("--review-and-run"))
        self.assertLess(tail.index("quota_ok"), tail.index("--review-and-run"))
        head = TEXT[:gate].split("# <<< FUNCTIONS")[1]
        for token in ("run_custody_locked", "--review-and-run", "mkdir", "mv "):
            self.assertNotIn(token, head)

    def test_quota_and_g6_rechecked_right_before_custody_and_before_attempt(self):
        gate = TEXT.index('[[ "$MODE" == EXECUTE_A1_A2 ]] || {')
        tail = TEXT[gate:]
        call = tail.index("\nrun_custody_locked\n")
        before, after = tail[:call], tail[call:]
        self.assertIn("\nquota_ok\ng6_ok", before)  # justo antes de mover nada
        a2 = after.index("--review-and-run")
        self.assertIn("\nquota_ok\ng6_ok\n", after[:a2])  # justo antes del unico intento
        head = TEXT[:gate]
        self.assertIn("g6_ok\nquota_ok\n", head)  # fail-fast en A0
        self.assertNotIn("11-04 11:", TEXT)

    def test_pins_and_no_retry_loop(self):
        for pin in ("6eed4f00b7917da3cca36bbda70e16d16328648d",
                    "sha256:9bdeaa217f09f5d0e19e50d214a4abb936a2b89246ebbcf71fb71178e5574af2",
                    "61e3f1f7e7dd561567e5fac415a6fa9695c2f74fa86adde21dd8ed0330d03cd3",
                    "support00_postdb"):
            self.assertIn(pin, TEXT)
        self.assertNotRegex(TEXT.split("# ---- A2")[1], r'\b(while|until)\b')


if __name__ == "__main__":
    unittest.main()
