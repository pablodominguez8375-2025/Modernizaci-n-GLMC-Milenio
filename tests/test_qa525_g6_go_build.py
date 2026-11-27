"""Off-host G6 GO builder: canonical bytes identical to what the driver demands; TTL/field limits; no overwrite."""
import hashlib
import importlib.util
import os
import pathlib
import sys
import tempfile
import unittest
from contextlib import redirect_stderr, redirect_stdout
import io

HERE = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import test_qa525_manual_cut_g6_adapter as ad  # noqa: E402
cut = ad.cut
spec = importlib.util.spec_from_file_location("g6gb", HERE.parent / "scripts/qa/operators/qa525-g6-go-build.py")
gb = importlib.util.module_from_spec(spec)
spec.loader.exec_module(gb)
OP, DRV = hashlib.sha256(b"o").hexdigest(), hashlib.sha256(b"d").hexdigest()


class Builder(unittest.TestCase):
    def test_output_parses_with_the_driver_and_is_canonical(self):
        data = gb.build("backup", OP, DRV, 1000, ttl=900, nonce="a" * 32)
        go = cut.g6_go_parse(data)
        self.assertEqual((go["command"], go["expires_at"], go["nonce"]), ("backup", 1900, "a" * 32))
        self.assertEqual(cut.g6_go_canonical(go), data)
        self.assertEqual(gb.NAMESPACE, cut.G6_GO_NAMESPACE)
        self.assertEqual(gb.TTL_MAX, cut.G6_GO_TTL_MAX)
        self.assertEqual(gb.COMMANDS, cut.G6_COMMANDS)

    def test_invalid_inputs_are_refused(self):
        for kw in ({"command": "nope"}, {"op_sha256": "x"}, {"driver_sha256": DRV.upper()}, {"ttl": 901}, {"ttl": 0},
                   {"ttl": True}, {"nonce": "a" * 31}, {"nonce": "G" * 32}):
            args = dict(command="init", op_sha256=OP, driver_sha256=DRV, now=1000, ttl=600)
            args.update(kw)
            with self.assertRaises(gb.BuildError):
                gb.build(**args)

    def test_nonce_is_fresh_each_time(self):
        a, b = (cut.g6_go_parse(gb.build("init", OP, DRV, 1000))["nonce"] for _ in range(2))
        self.assertNotEqual(a, b)

    def test_host_epoch_is_required_integer_and_sets_expires_at(self):
        with tempfile.TemporaryDirectory() as d:
            for bad in ([], ["--host-epoch", "abc"]):
                with self.assertRaises(SystemExit), redirect_stderr(io.StringIO()):
                    gb.main(["--command", "init", "--op-sha256", OP, "--driver-sha256", DRV, "--out", os.path.join(d, "a.json")] + bad)
            out = os.path.join(d, "ok.json")
            buf = io.StringIO()
            with redirect_stdout(buf):
                self.assertEqual(gb.main(["--command", "init", "--op-sha256", OP, "--driver-sha256", DRV, "--ttl", "900",
                                          "--host-epoch", "1800000000", "--out", out]), 0)
            self.assertEqual(__import__("json").load(open(out))["expires_at"], 1800000900)
            self.assertIn("QA525_G6_GO_EXPIRES_AT=1800000900", buf.getvalue())

    def test_cli_writes_0600_and_never_overwrites(self):
        with tempfile.TemporaryDirectory() as d:
            out = os.path.join(d, "go.json")
            buf = io.StringIO()
            with redirect_stdout(buf):
                self.assertEqual(gb.main(["--command", "init", "--op-sha256", OP, "--driver-sha256", DRV, "--host-epoch", "1800000000", "--out", out]), 0)
            self.assertEqual(oct(os.stat(out).st_mode & 0o777), "0o600")
            self.assertIn("-n qa525-go-g6-v1@pmgm", buf.getvalue())
            with self.assertRaises(FileExistsError):
                gb.main(["--command", "init", "--op-sha256", OP, "--driver-sha256", DRV, "--host-epoch", "1800000000", "--out", out])
            buf = io.StringIO()
            with redirect_stdout(buf):
                self.assertEqual(gb.main(["--command", "init", "--op-sha256", "zz", "--driver-sha256", DRV, "--host-epoch", "1800000000"]), 1)
            self.assertEqual(buf.getvalue(), "QA525_G6_GO=STOP GO_BUILD_INVALID\n")


if __name__ == "__main__":
    unittest.main()
