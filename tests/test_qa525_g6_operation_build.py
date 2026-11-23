"""Canonical g6-operation.json builder: round-trips through the REAL launcher validator and G6 runner grammar."""
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import tempfile
import unittest

BASE = Path(__file__).resolve().parents[1] / "scripts" / "qa"


def load(n, f):
    s = importlib.util.spec_from_file_location(n, BASE / f)
    m = importlib.util.module_from_spec(s)
    s.loader.exec_module(m)
    return m


op = load("g6op_t", "qa525-g6-operation-build.py")
run = load("g6run_t", "qa525-vnext-g6-v2-run.py")
launcher = load("launcher_t", "qa525-vnext-root-launcher.py")
H = "ab" * 32
EPOCH = "cd" * 16
IMG = "postgres@sha256:" + "ef" * 32
ARCH = "centenario-qa-g6-v2-CIFRADO-20261123T031500Z.tar.gz"
CASES = {
    "preflight": [("restic-sha256", H)],
    "init": [("restic-sha256", H)],
    "backup": [("restic-sha256", H), ("repo-id", H), ("key-epoch", EPOCH)],
    "restore": [("restic-sha256", H), ("repo-id", H), ("key-epoch", EPOCH), ("image", IMG),
                ("archive-name", ARCH), ("archive-sha256", H), ("snapshot-id", "a1b2c3d4"),
                ("manifest-sha256", H)],
    "review": [("archive-name", ARCH), ("drive-file-id", "1AQGNBUpCaBusKm4OwktuZ4alcsWwjMziN43qRV0PSKQ"),
               ("repo-id", H), ("key-epoch", EPOCH)],
}


class BuildTests(unittest.TestCase):
    def test_every_command_roundtrips_through_the_real_launcher_validator(self):
        for command, options in CASES.items():
            with self.subTest(command=command):
                raw, digest = op.build(command, options)
                self.assertEqual(digest, hashlib.sha256(raw).hexdigest())
                pins = {"g6_command": command, "g6_operation_sha256": digest}
                argv = launcher._g6_operation(raw, pins, run)
                self.assertEqual(argv[0], command)
                self.assertEqual(json.loads(raw)["schema"], "qa525-g6-operation-v1")

    def test_bytes_are_canonical_and_deterministic(self):
        a = op.build("backup", CASES["backup"])
        b = op.build("backup", CASES["backup"])
        self.assertEqual(a, b)
        self.assertNotIn(b" ", a[0])
        self.assertNotIn(b"\n", a[0])

    def test_required_options_and_closed_grammar(self):
        bad = [("preflight", [("repo-id", H)]),                       # not allowed for preflight
               ("backup", [("restic-sha256", H)]),                    # missing required
               ("backup", CASES["backup"] + [("repo-id", H)]),        # duplicate
               ("backup", [("restic-sha256", "zz"), ("repo-id", H), ("key-epoch", EPOCH)]),  # bad value
               ("restore", [("image", "postgres:latest")] + CASES["restore"][:0]),
               ("nope", []), ("init", [])]
        for command, options in bad:
            with self.subTest(command=command, options=options):
                with self.assertRaises(op.Stop):
                    op.build(command, options)

    def test_error_codes_do_not_echo_input(self):
        try:
            op.build("backup", [("restic-sha256", "SECRET-VALUE"), ("repo-id", H), ("key-epoch", EPOCH)])
        except op.Stop as e:
            self.assertNotIn("SECRET", str(e))

    def test_launcher_rejects_a_pin_for_another_command_or_tampered_bytes(self):
        raw, digest = op.build("backup", CASES["backup"])
        with self.assertRaises(launcher.Stop):
            launcher._g6_operation(raw, {"g6_command": "review", "g6_operation_sha256": digest}, run)
        with self.assertRaises(launcher.Stop):
            launcher._g6_operation(raw + b" ", {"g6_command": "backup", "g6_operation_sha256": digest}, run)

    def test_write_private_mode_exclusive_no_follow(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = os.path.join(tmp, "g6-operation.json")
            raw, _ = op.build("preflight", CASES["preflight"])
            op.write_private(raw, path)
            self.assertEqual(os.stat(path).st_mode & 0o777, 0o600)
            self.assertEqual(Path(path).read_bytes(), raw)
            with self.assertRaises(op.Stop):
                op.write_private(raw, path)  # exists
            link = os.path.join(tmp, "link.json")
            os.symlink(path, link)
            with self.assertRaises(op.Stop):
                op.write_private(raw, link)

    def test_main_prints_only_command_and_pin(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = os.path.join(tmp, "o.json")
            import contextlib
            import io
            out = io.StringIO()
            with contextlib.redirect_stdout(out):
                code = op.main(["backup", "--out", path, "restic-sha256=" + H, "repo-id=" + H, "key-epoch=" + EPOCH])
            self.assertEqual(code, 0)
            lines = out.getvalue().strip().splitlines()
            self.assertEqual(lines[0], "g6_command=backup")
            self.assertEqual(lines[1], "g6_operation_sha256=" + hashlib.sha256(Path(path).read_bytes()).hexdigest())
            self.assertEqual(len(lines), 2)


if __name__ == "__main__":
    unittest.main()
