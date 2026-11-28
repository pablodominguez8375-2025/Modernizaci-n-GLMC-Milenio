"""Off-host GO verifier: canonical bytes + pinned signer + ssh-keygen -Y verify, prints the hashes of go.json/go.json.sig."""
import hashlib
import importlib.util
import io
import os
import pathlib
import subprocess
import sys
import unittest

HERE = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
OPS = HERE.parent / "scripts/qa/operators"
import g6_go_support as gs  # noqa: E402


def load(path, name):
    spec = importlib.util.spec_from_file_location(name, path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


cut = load(OPS / "qa525-manual-cut-e5c40d3a.py", "cutv")
gv = load(OPS / "qa525-g6-go-verify.py", "gv")
OP, DRV = "c" * 64, "d" * 64
NOW = 1_800_000_000


@unittest.skipUnless(gs.have_keygen(), "ssh-keygen required")
class Verify(unittest.TestCase):
    def setUp(self):
        self.s = gs.Signer(cut)
        self.addCleanup(self.s.close)

    def go(self, **kw):
        p, sg = self.s.go("init", OP, DRV, NOW, **kw)
        return open(p, "rb").read(), open(sg, "rb").read(), p, sg

    def v(self, g, sg):
        return gv.verify(cut, g, sg, keygen=gs.KEYGEN, signers=self.s.signers, fingerprint=self.s.fingerprint)

    def test_good_go_verifies_and_returns_the_fields(self):
        g, sg, _, _ = self.go(nonce="e" * 32)
        go = self.v(g, sg)
        self.assertEqual((go["command"], go["nonce"], go["expires_at"]), ("init", "e" * 32, NOW + 600))

    def test_wrong_key_namespace_tamper_noncanonical_and_pin_mismatch_are_stops(self):
        g, sg, _, _ = self.go()
        for bad_g, bad_s, code in ((g + b" ", sg, "CUT_G6_GO_SIGNATURE_INVALID"), (g, sg[:-10], "CUT_G6_GO_SIGNATURE_INVALID")):
            with self.assertRaises(cut.Stop) as c:
                self.v(bad_g, bad_s)
            self.assertEqual(str(c.exception), code)
        _, other_sig, _, _ = (lambda p: (None, open(p[1], "rb").read(), None, None))(self.s.go("init", OP, DRV, NOW, key=self.s.other_key()))
        with self.assertRaises(cut.Stop):
            self.v(g, other_sig)
        _, ns_sig, _, _ = (lambda p: (None, open(p[1], "rb").read(), None, None))(self.s.go("init", OP, DRV, NOW, namespace="other-ns@x"))
        with self.assertRaises(cut.Stop):
            self.v(g, ns_sig)
        raw = b'{ "command": "init" }'
        p, sgp = self.s.go("init", OP, DRV, NOW, raw=raw)
        with self.assertRaises(cut.Stop) as c:
            self.v(open(p, "rb").read(), open(sgp, "rb").read())
        self.assertEqual(str(c.exception), "CUT_G6_GO_FORMAT_INVALID")
        with self.assertRaises(cut.Stop) as c:
            gv.verify(cut, g, sg, keygen=gs.KEYGEN, signers=self.s.signers, fingerprint="SHA256:wrong")
        self.assertEqual(str(c.exception), "CUT_G6_GO_SIGNER_PIN_MISMATCH")

    def test_cli_prints_hashes_and_checks_expectations_and_ttl(self):
        g, sg, p, sgp = self.go()
        real = gv.load_driver
        mod = load(OPS / "qa525-manual-cut-e5c40d3a.py", "cutv2")
        mod.G6_OPS_ALLOWED_SIGNERS, mod.G6_OPS_FINGERPRINT = self.s.signers, self.s.fingerprint
        gv.load_driver = lambda path: mod
        try:
            out = io.StringIO()
            self.assertEqual(gv.main(["--go", p, "--go-sig", sgp, "--now", str(NOW), "--expect-command", "init", "--expect-op", OP,
                                      "--expect-driver", DRV], out=out), 0, out.getvalue())
            lines = dict(l.split("=", 1) for l in out.getvalue().splitlines())
            self.assertEqual(lines["G6GV_GO_SHA256"], hashlib.sha256(g).hexdigest())
            self.assertEqual(lines["G6GV_GOSIG_SHA256"], hashlib.sha256(sg).hexdigest())
            self.assertEqual(lines["G6GV_VERIFY"], "PASS")
            for extra, code in ((["--expect-op", "9" * 64], "CUT_G6_GO_MISMATCH"), (["--expect-command", "backup"], "CUT_G6_GO_MISMATCH"),
                                (["--now", str(NOW + 700)], "CUT_G6_GO_TTL_OUT_OF_RANGE"),
                                (["--now", str(NOW - 1000)], "CUT_G6_GO_TTL_OUT_OF_RANGE")):
                out = io.StringIO()
                self.assertEqual(gv.main(["--go", p, "--go-sig", sgp] + extra, out=out), 1)
                self.assertEqual(out.getvalue(), "G6GV_STOP:%s\n" % code)
        finally:
            gv.load_driver = real

    def test_with_the_real_pins_an_ephemeral_signature_is_refused(self):
        _, _, p, sgp = self.go()
        out = io.StringIO()
        self.assertEqual(gv.main(["--go", p, "--go-sig", sgp, "--driver", str(OPS / "qa525-manual-cut-e5c40d3a.py")], out=out), 1)
        self.assertEqual(out.getvalue(), "G6GV_STOP:CUT_G6_GO_SIGNATURE_INVALID\n")


if __name__ == "__main__":
    unittest.main()
