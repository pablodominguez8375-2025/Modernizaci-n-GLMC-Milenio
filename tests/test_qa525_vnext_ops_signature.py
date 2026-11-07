"""QA525 OPS signature contract: hermetic fakes plus a real ssh-keygen round trip."""
import ast
import base64
import hashlib
import importlib.util
import json
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest

SOURCE = Path(__file__).resolve().parents[1] / "scripts/qa/qa525-vnext-ops-signature.py"
spec = importlib.util.spec_from_file_location("qa525_vnext_ops_signature_test", SOURCE)
sig = importlib.util.module_from_spec(spec)
spec.loader.exec_module(sig)
KEYGEN = shutil.which("ssh-keygen")
APP = frozenset({"SHA256:" + "A" * 43})
POLICY = "sha256:" + "9" * 64
FIELDS = {"schema": "qa525-ops-v2", "status": "OPS_SIGNED_VERIFIED", "ops_source_sha": "a" * 40,
          "program_sha256": "sha256:" + "1" * 64, "readiness_sha256": "sha256:" + "2" * 64,
          "staging_sha256": "sha256:" + "3" * 64, "policy_hash": POLICY}


def encode(doc):
    return json.dumps(doc, sort_keys=True).encode() + b"\n"


RECEIPT = encode(FIELDS)


def make_key(directory, name="ops"):
    path = Path(directory) / name
    subprocess.run([KEYGEN, "-q", "-t", "ed25519", "-N", "", "-f", str(path), "-C", ""],
                   check=True)
    pub = (Path(str(path) + ".pub")).read_text().split()
    line = ('%s namespaces="%s" ssh-ed25519 %s\n' % (sig.PRINCIPAL, sig.NAMESPACE, pub[1])).encode()
    fp = subprocess.run([KEYGEN, "-lf", str(path) + ".pub"], check=True,
                        capture_output=True, text=True).stdout.split()[1]
    return path, line, fp


def sign(path, data, namespace=sig.NAMESPACE):
    target = Path(tempfile.mkdtemp(dir=str(Path(path).parent))) / "data"
    target.write_bytes(data)
    subprocess.run([KEYGEN, "-Y", "sign", "-q", "-f", str(path), "-n", namespace, str(target)],
                   check=True)
    return Path(str(target) + ".sig").read_bytes()


def real_verifier(allowed, signature, principal, namespace, data):
    with tempfile.TemporaryDirectory() as tmp:
        a, s = Path(tmp) / "allowed", Path(tmp) / "sig"
        a.write_bytes(allowed)
        s.write_bytes(signature)
        argv = sig.ssh_keygen_argv(str(a), str(s))
        done = subprocess.run(argv, input=data, capture_output=True)
        return done.returncode, done.stdout


@unittest.skipUnless(KEYGEN, "ssh-keygen not available")
class RealRoundTrip(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.key, self.allowed, self.fp = make_key(self.tmp.name)
        self.sig = sign(self.key, RECEIPT)

    def call(self, **kw):
        args = dict(receipt=RECEIPT, signature=self.sig, allowed_signers=self.allowed,
                    ops_fingerprint=self.fp, app_fingerprints=APP, policy_hash=POLICY,
                    verifier=real_verifier)
        args.update(kw)
        return sig.verify_ops_receipt(**args)

    def code(self, **kw):
        with self.assertRaises(sig.Stop) as c:
            self.call(**kw)
        return str(c.exception)

    def test_good_signature_passes_and_grants_nothing(self):
        out = self.call()
        self.assertEqual(out["receipt"], FIELDS)
        self.assertTrue(out["signature_verified_here"])
        for flag in ("may_apply", "sponsor_approved_here", "key_custody_verified_here",
                     "fingerprint_provenance_verified_here", "app_identity_verified_here"):
            self.assertIs(out[flag], False)
        self.assertEqual(out["receipt_sha256"], "sha256:" + hashlib.sha256(RECEIPT).hexdigest())

    def test_one_byte_change_in_receipt_is_rejected(self):
        self.assertEqual(self.code(receipt=RECEIPT.replace(b"OPS_SIGNED", b"OPS_SIGNEX")),
                         "OPSSIG_NOT_VERIFIED")

    def test_wrong_namespace_signature_is_rejected(self):
        other = sign(self.key, RECEIPT, namespace="other-namespace")
        self.assertEqual(self.code(signature=other), "OPSSIG_NOT_VERIFIED")

    def test_signature_by_another_key_is_rejected(self):
        key2, _, _ = make_key(self.tmp.name, "intruder")
        self.assertEqual(self.code(signature=sign(key2, RECEIPT)), "OPSSIG_NOT_VERIFIED")

    def test_ops_key_equal_to_an_app_identity_is_rejected(self):
        self.assertEqual(self.code(app_fingerprints=frozenset({self.fp})),
                         "OPSSIG_APP_IDENTITY_REUSED")

    def test_pinned_fingerprint_must_match_the_signers_key(self):
        _, allowed2, fp2 = make_key(self.tmp.name, "other")
        self.assertEqual(self.code(allowed_signers=allowed2), "OPSSIG_FINGERPRINT_MISMATCH")
        self.assertEqual(self.code(ops_fingerprint=fp2), "OPSSIG_FINGERPRINT_MISMATCH")


class Contract(unittest.TestCase):
    KEY = base64.b64encode(sig.BLOB_PREFIX + bytes(range(32))).decode()
    SIGNERS = ('qa525-ops namespaces="qa525-ops-v2@pmgm" ssh-ed25519 %s\n' % KEY).encode()
    FP = "SHA256:" + base64.b64encode(hashlib.sha256(
        sig.BLOB_PREFIX + bytes(range(32))).digest()).decode().rstrip("=")
    SIGNATURE = sig.SIG_BEGIN + b"AAAA\n" + sig.SIG_END

    def good_verifier(self, *args):
        self.calls = args
        return 0, ('Good "%s" signature for %s with ED25519 key %s\n'
                   % (sig.NAMESPACE, sig.PRINCIPAL, self.FP)).encode()

    def call(self, **kw):
        args = dict(receipt=RECEIPT, signature=self.SIGNATURE, allowed_signers=self.SIGNERS,
                    ops_fingerprint=self.FP, app_fingerprints=APP, policy_hash=POLICY,
                    verifier=self.good_verifier)
        args.update(kw)
        return sig.verify_ops_receipt(**args)

    def code(self, **kw):
        with self.assertRaises(sig.Stop) as c:
            self.call(**kw)
        self.assertIsNone(c.exception.__cause__)
        return str(c.exception)

    def test_fake_good_path_passes_exact_arguments_to_port(self):
        self.call()
        self.assertEqual(self.calls, (self.SIGNERS, self.SIGNATURE, "qa525-ops",
                                      "qa525-ops-v2@pmgm", RECEIPT))

    def test_verifier_output_must_be_exact(self):
        good = self.good_verifier()[1]
        for result in ((1, good), (0, b""), (0, good + b"x"), (0, good.replace(b"Good", b"Bad ")),
                       (0, good.replace(b"ED25519", b"RSA")), (True, good), (0.0, good),
                       (0, good.decode()), (None, None)):
            with self.subTest(result=result):
                self.assertEqual(self.code(verifier=lambda *a, r=result: r), "OPSSIG_NOT_VERIFIED")

    def test_verifier_exceptions_become_fixed_code_without_leak(self):
        def boom(*a):
            raise RuntimeError("secret /etc/key")
        self.assertEqual(self.code(verifier=boom), "OPSSIG_VERIFIER_FAILED")

    def test_input_validation(self):
        bad = {"receipt": (None, "x", b"", b"x" * (sig.MAX_RECEIPT + 1)),
               "signature": (None, b"", b"junk", b"junk\n" + sig.SIG_END, self.SIGNATURE + b"x", sig.SIG_BEGIN + b"x"),
               "ops_fingerprint": (None, "SHA256:short", self.FP.replace("SHA256:", "MD5:")),
               "app_fingerprints": (None, set(), frozenset(), frozenset({"x"}), [APP]),
               "policy_hash": (None, "sha256:short", "9" * 64, POLICY.upper()),
               "verifier": (None, 1)}
        for field, values in bad.items():
            for value in values:
                with self.subTest(field=field, value=value):
                    self.assertEqual(self.code(**{field: value}), "OPSSIG_INPUT_INVALID")

    def test_signers_file_must_be_exactly_one_pinned_ed25519_line(self):
        s = self.SIGNERS
        for bad in (s + s, s.replace(b"qa525-ops ", b"someone "), s.replace(b'namespaces="qa525-ops-v2@pmgm"', b""),
                    s.replace(b"ssh-ed25519", b"ssh-rsa"), s.rstrip(b"\n") + b" comment\n",
                    s.replace(b'"qa525-ops-v2@pmgm"', b'"*"'), b"", b"\xff", "x".encode() * 2000,
                    s.replace(self.KEY.encode(), base64.b64encode(b"x" * 51)),
                    s.replace(self.KEY.encode(), self.KEY[:-4].encode() + b"@@@@")):
            with self.subTest(bad=bad[:40]):
                self.assertEqual(self.code(allowed_signers=bad), "OPSSIG_SIGNERS_INVALID")

    def test_receipt_json_strictness(self):
        bad_docs = [dict(FIELDS, schema="other"), dict(FIELDS, status="OPS_PENDING"),
                    dict(FIELDS, ops_source_sha="a" * 39), dict(FIELDS, ops_source_sha="A" * 40),
                    dict(FIELDS, ops_source_sha=1), dict(FIELDS, program_sha256="x"),
                    dict(FIELDS, readiness_sha256="sha256:" + "G" * 64),
                    dict(FIELDS, staging_sha256=None), dict(FIELDS, policy_hash="sha256:" + "8" * 64),
                    dict(FIELDS, extra=1), {k: v for k, v in FIELDS.items() if k != "policy_hash"}]
        for doc in bad_docs:
            with self.subTest(doc=str(doc)[:70]):
                self.assertEqual(self.code(receipt=encode(doc)), "OPSSIG_RECEIPT_INVALID")
        for raw in (b"[]\n", b"{\"schema\":\"other\"}", b"{\"schema\":\"qa525-ops-v2\",\"schema\":\"qa525-ops-v2\"}",
                    b"{\"schema\":\"qa525-ops-v2\",\"x\":NaN}", b"\xff", b"not json"):
            with self.subTest(raw=raw):
                self.assertEqual(self.code(receipt=raw), "OPSSIG_RECEIPT_INVALID")

    def test_argv_builder_is_fixed_and_path_checked(self):
        self.assertEqual(sig.ssh_keygen_argv("/run/a", "/run/b"),
                         ("/usr/bin/ssh-keygen", "-Y", "verify", "-f", "/run/a", "-I", "qa525-ops",
                          "-n", "qa525-ops-v2@pmgm", "-s", "/run/b"))
        for bad in ("rel", "/a/../b", "/a b", "/a\x00b", "", None, "/" + "a" * 300, "/a;b"):
            with self.assertRaises(sig.Stop):
                sig.ssh_keygen_argv(bad, "/run/b")
            with self.assertRaises(sig.Stop):
                sig.ssh_keygen_argv("/run/a", bad)

    def test_library_has_no_process_file_or_network_surface(self):
        tree = ast.parse(SOURCE.read_text())
        mods = {a.name for n in ast.walk(tree) if isinstance(n, ast.Import) for a in n.names}
        self.assertEqual(mods, {"base64", "hashlib", "hmac", "json", "re"})
        names = {n.attr for n in ast.walk(tree) if isinstance(n, ast.Attribute)}
        for forbidden in ("run", "Popen", "system", "environ", "open", "read_bytes", "socket"):
            self.assertNotIn(forbidden, names)
        self.assertNotIn("__main__", SOURCE.read_text().replace('"""', ""))


if __name__ == "__main__":
    unittest.main()
