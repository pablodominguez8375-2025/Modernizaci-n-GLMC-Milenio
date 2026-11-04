"""Hermetic tests for the quota-window wrapper (stubs only; no host, no network, no sudo)."""
import hashlib
import subprocess
import time
import tempfile
import unittest
from pathlib import Path

WRAP = Path(__file__).resolve().parents[1] / "scripts/qa/operators/qa525-observed-wait-run-20261104.sh"
TEXT = WRAP.read_text()
FAKE = '#!/usr/bin/env bash\necho "CHILD_ARGS=$*" >> "$LOG"\nexit "${CHILD_RC:-0}"\n'


class WaitRun(unittest.TestCase):
    def run_wrap(self, quotas, args=(), host="glmdch", fake=FAKE, sha=None, deadline=None, child_rc=0, reset_in=3600, launch_by=None):
        with tempfile.TemporaryDirectory() as d:
            d = Path(d)
            log = d / "calls.log"
            q = d / "quota.txt"
            q.write_text("\n".join(map(str, quotas)) + "\n")
            (d / "fake.sh").write_text(fake)
            (d / "bin").mkdir()

            def stub(name, body):
                p = d / "bin" / name
                p.write_text("#!/bin/bash\n" + body + "\n")
                p.chmod(0o755)
            stub("hostname", "echo %s" % host)
            stub("id", "echo soporte")
            stub("sudo", 'echo "sudo $@" >> "%s"; exit 0' % log)
            stub("sleep", 'echo sleep >> "%s"' % log)
            stub("curl", f'''url="${{@: -1}}"
case "$url" in
  *raw.githubusercontent.com*) out=""; prev=""; for a in "$@"; do [ "$prev" = -o ] && out="$a"; prev="$a"; done; cp "{d}/fake.sh" "$out";;
  *rate_limit*) n=$(head -1 "{d}/quota.txt"); tail -n +2 "{d}/quota.txt" > "{d}/q2" || true; [ -s "{d}/q2" ] && mv "{d}/q2" "{d}/quota.txt" || true
     printf '{{"resources":{{"core":{{"remaining":%s,"reset":%d}}}}}}' "$n" RESETVALUE;;
  *) exit 1;;
esac''')
            body = TEXT.replace("/home/soporte/", str(d) + "/")
            if launch_by:
                body = body.replace("2026-11-06 09:50:00 UTC", launch_by)
            body = body.replace("SHA=0e8e6d4e2a7f12f9fd9ec52574ac2a24a85500d17f73c5803fd08285a1b4012e",
                                "SHA=" + (sha or hashlib.sha256(fake.encode()).hexdigest()))
            if deadline:
                body = body.replace("2026-11-06 09:55:00 UTC", deadline)
            (d / "w.sh").write_text(body)
            cu = d / "bin" / "curl"
            cu.write_text(cu.read_text().replace("RESETVALUE", str(int(time.time()) + reset_in)))
            env = {"PATH": f"{d}/bin:/usr/bin:/bin", "HOME": str(d), "LOG": str(log), "CHILD_RC": str(child_rc)}
            p = subprocess.run(["bash", str(d / "w.sh"), *args], capture_output=True, text=True, env=env)
            return p.returncode, p.stdout, log.read_text() if log.exists() else ""

    def test_syntax(self):
        subprocess.run(["bash", "-n", str(WRAP)], check=True)

    def test_waits_until_window_then_runs_child_once_read_only(self):
        rc, out, log = self.run_wrap([40, 47, 52, 59])
        self.assertEqual(rc, 0, out)
        self.assertEqual(out.count("WAITING"), 3)
        self.assertIn("WINDOW=OPEN remaining=59", out)
        self.assertEqual(log.count("CHILD_ARGS="), 1)
        self.assertIn("CHILD_ARGS=\n", log)  # sin argumento => solo lectura

    def test_execute_mode_passes_exact_argument_once(self):
        rc, out, log = self.run_wrap([60], ("EXECUTE_OBSERVED_ATTEMPT",))
        self.assertEqual(rc, 0)
        self.assertEqual(log.count("CHILD_ARGS=EXECUTE_OBSERVED_ATTEMPT"), 1)

    def test_child_failure_is_not_retried(self):
        rc, out, log = self.run_wrap([60], ("EXECUTE_OBSERVED_ATTEMPT",), child_rc=2)
        self.assertEqual(rc, 2)
        self.assertEqual(log.count("CHILD_ARGS="), 1)
        self.assertIn("CHILD_EXIT=2", out)

    def test_wrong_host_stops_before_download(self):
        rc, out, log = self.run_wrap([60], host="other")
        self.assertEqual(rc, 2)
        self.assertIn("STOP_HOST_OR_USER_NO_RETRY", out)
        self.assertEqual(log, "")

    def test_hash_mismatch_stops_before_waiting(self):
        rc, out, log = self.run_wrap([10], sha="0" * 64)
        self.assertEqual(rc, 2)
        self.assertIn("STOP_SCRIPT_HASH_NO_RETRY", out)
        self.assertNotIn("sleep", log)
        self.assertNotIn("CHILD_ARGS", log)

    def test_deadline_stops_without_running_child(self):
        rc, out, log = self.run_wrap([10], deadline="2020-01-01 00:00:00 UTC")
        self.assertEqual(rc, 2)
        self.assertIn("STOP_G6_DEADLINE_REACHED_RENEW_G6_NO_RETRY", out)
        self.assertNotIn("CHILD_ARGS", log)

    def test_only_one_window_is_waited(self):
        rc, out, log = self.run_wrap([10, 10, 10], reset_in=-600)
        self.assertEqual(rc, 2)
        self.assertIn("STOP_NO_FRESH_WINDOW_ONE_WINDOW_ONLY_NO_RETRY", out)
        self.assertNotIn("CHILD_ARGS", log)

    def test_not_launched_too_close_to_g6_deadline(self):
        rc, out, log = self.run_wrap([60], launch_by="2020-01-01 00:00:00 UTC")
        self.assertEqual(rc, 2)
        self.assertIn("STOP_TOO_CLOSE_TO_G6_DEADLINE_NOT_LAUNCHING_NO_RETRY", out)
        self.assertNotIn("CHILD_ARGS", log)

    def test_bad_argument(self):
        for args in (("--x",), ("READ_ONLY", "y")):
            rc, out, log = self.run_wrap([60], args)
            self.assertEqual(rc, 2)
            self.assertIn("STOP_BAD_ARGUMENT_NO_RETRY", out)

    def test_static_pins_and_no_quota_spend_or_secrets(self):
        self.assertIn("c345e9eb350c6cb7cbd3d1188f6dc069a504acaf", TEXT)
        self.assertIn("0e8e6d4e2a7f12f9fd9ec52574ac2a24a85500d17f73c5803fd08285a1b4012e", TEXT)
        self.assertNotIn("3db21f5d", TEXT)
        self.assertEqual(TEXT.count("api.github.com/"), 1)  # solo /rate_limit
        self.assertNotIn("Authorization", TEXT)
        self.assertNotIn(".pat", TEXT)


if __name__ == "__main__":
    unittest.main()
