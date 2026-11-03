"""Hermetic checks for the Route A operator script (no host, no network, no sudo)."""
import os
import re
import stat
import subprocess
import tempfile
import unittest
from pathlib import Path

SCRIPT = Path(__file__).resolve().parents[1] / "scripts/qa/operators/qa525-route-a-6eed4-20261103.sh"
URL = "https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/525#issuecomment-1"


def stub(directory, name, body):
    path = Path(directory) / name
    path.write_text("#!/bin/sh\n" + body + "\n")
    path.chmod(path.stat().st_mode | stat.S_IXUSR)


class RouteAScript(unittest.TestCase):
    text = SCRIPT.read_text()

    def run_script(self, *args, host="other", review=None):
        with tempfile.TemporaryDirectory() as d:
            log = Path(d) / "calls.log"
            for name in ("sudo", "curl", "docker"):
                stub(d, name, 'echo "$0 $@" >> "%s"; exit 1' % log)
            stub(d, "hostname", 'echo %s' % host)
            stub(d, "id", "echo soporte")
            stub(d, "jq", "exit 0")
            body = self.text if review is None else self.text.replace("__REVIEW_URL__", review)
            copy = Path(d) / "s.sh"
            copy.write_text(body)
            env = {"PATH": d + ":/usr/bin:/bin", "HOME": d}
            p = subprocess.run(["bash", str(copy), *args], capture_output=True, text=True, env=env)
            calls = log.read_text() if log.exists() else ""
            return p.returncode, p.stdout, calls

    def test_syntax(self):
        subprocess.run(["bash", "-n", str(SCRIPT)], check=True)

    def test_review_url_must_be_set(self):
        rc, out, calls = self.run_script()
        self.assertEqual((rc, calls), (2, ""))
        self.assertIn("STOP_REVIEW_URL_NOT_SET_NO_RETRY", out)

    def test_wrong_host_stops_before_any_sudo(self):
        rc, out, calls = self.run_script(review=URL)
        self.assertEqual((rc, calls), (2, ""))
        self.assertIn("STOP_HOST_OR_USER_NO_RETRY", out)

    def test_bad_argument_rejected(self):
        rc, out, calls = self.run_script("--go", review=URL)
        self.assertEqual((rc, calls), (2, ""))
        self.assertIn("STOP_BAD_ARGUMENT_NO_RETRY", out)

    def test_single_deploying_command_and_no_deletes(self):
        self.assertEqual(len(re.findall(r'--review-and-run', self.text)), 1)
        self.assertNotRegex(self.text, r'\bsudo\s+rm\b|\brm\s+-')
        for line in self.text.splitlines():
            if "sudo mv" in line:
                self.assertIn("mv -n", line)
        self.assertNotRegex(self.text, r'docker\s+(run|rm|compose|restart|stop|kill|exec\s+-u)')
        self.assertNotRegex(self.text, r'\b(INSERT|UPDATE|DELETE|DROP|ALTER|TRUNCATE)\b')

    def test_mutations_only_after_explicit_execute_gate(self):
        gate = self.text.index("[[ \"$MODE\" == EXECUTE_A1_A2 ]] || {")
        for token in ("mkdir -m 700", "sudo mv ", "--review-and-run", "set -C"):
            self.assertGreater(self.text.index(token), gate, token)

    def test_no_retry_wording_and_fixed_pins(self):
        for pin in ("6eed4f00b7917da3cca36bbda70e16d16328648d",
                    "sha256:9bdeaa217f09f5d0e19e50d214a4abb936a2b89246ebbcf71fb71178e5574af2",
                    "61e3f1f7e7dd561567e5fac415a6fa9695c2f74fa86adde21dd8ed0330d03cd3",
                    "support00_postdb"):
            self.assertIn(pin, self.text)
        self.assertNotIn("while ", self.text.split("# ---- A2")[1])


if __name__ == "__main__":
    unittest.main()
