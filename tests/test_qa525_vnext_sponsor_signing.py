"""QA525 Sponsor signing sheet builder -- hermetic: synthetic throwaway key, no secrets."""
import datetime as dt
import importlib.util
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1] / "scripts/qa"


def load(name, file):
    spec = importlib.util.spec_from_file_location(name, ROOT / file)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


sheet = load("qa525_sponsor_signing_test", "qa525-vnext-sponsor-signing.py")
opsig = load("qa525_sponsor_signing_ops_test", "qa525-vnext-ops-signature.py")
gocut = load("qa525_sponsor_signing_go_test", "qa525-vnext-go-cut-contract.py")
SSH_KEYGEN = shutil.which("ssh-keygen")
SHA40 = "a" * 40
SHA40_APP = "b" * 40
D = lambda ch: "sha256:" + ch * 64  # noqa: E731
FIELDS = dict(ops_source_sha=SHA40, program_sha256=D("1"), readiness_sha256=D("2"),
              staging_sha256=D("3"), policy_hash=D("4"), app_archive_sha256=D("5"))
GO = dict(nonce="N" * 43, ops_source_sha=SHA40, app_source_sha=SHA40_APP,
          app_archive_sha256=D("5"), plan_sha256=D("6"), admission_sha256=D("7"),
          policy_hash=D("4"), issued_at="2026-11-09T15:00:00Z",
          expires_at="2026-11-09T15:20:00Z")


class SponsorSigningBuilderTests(unittest.TestCase):
    def code(self, call, *a, **k):
        with self.assertRaises(sheet.Stop) as ctx:
            call(*a, **k)
        return str(ctx.exception)

    def test_receipt_is_canonical_eight_fields_and_deterministic(self):
        raw = sheet.build_ops_v3_receipt(**FIELDS)
        self.assertEqual(raw, sheet.build_ops_v3_receipt(**FIELDS))
        doc = json.loads(raw)
        self.assertEqual(set(doc), set(sheet.OPS_FIELDS))
        self.assertEqual((doc["schema"], doc["status"]), ("qa525-ops-v3", "OPS_SIGNED_VERIFIED"))
        self.assertTrue(raw.endswith(b"\n") and raw.count(b"\n") == 1)
        self.assertEqual(raw, (json.dumps(doc, sort_keys=True, separators=(",", ":")) + "\n").encode())

    def test_receipt_rejects_every_malformed_input(self):
        for key in FIELDS:
            for bad in (None, 5, "", "x", "SHA256:" + "1" * 64, "sha256:" + "G" * 64,
                        "sha256:" + "1" * 63, "A" * 40 if key == "ops_source_sha" else "sha256:" + "A" * 64):
                with self.subTest(key=key, bad=bad):
                    self.assertEqual(self.code(sheet.build_ops_v3_receipt, **{**FIELDS, key: bad}),
                                     "SIGNSHEET_INPUT_INVALID")

    def test_go_cut_has_exact_fields_constants_and_canonical_bytes(self):
        raw = sheet.build_go_cut(**GO)
        doc = json.loads(raw)
        self.assertEqual(set(doc), set(sheet.GO_FIELDS))
        for key, value in sheet.GO_CONSTANTS.items():
            self.assertEqual(doc[key], value)
        self.assertEqual(raw, (json.dumps(doc, sort_keys=True, separators=(",", ":")) + "\n").encode())

    def test_go_cut_window_and_inputs_fail_closed(self):
        for issued, expires, code in (
                ("2026-11-09T15:00:00Z", "2026-11-09T15:00:00Z", "SIGNSHEET_WINDOW_INVALID"),
                ("2026-11-09T15:00:00Z", "2026-11-09T14:59:59Z", "SIGNSHEET_WINDOW_INVALID"),
                ("2026-11-09T15:00:00Z", "2026-11-09T15:30:01Z", "SIGNSHEET_WINDOW_INVALID"),
                ("2026-11-09 15:00:00", "2026-11-09T15:10:00Z", "SIGNSHEET_TIME_INVALID"),
                ("2026-13-09T15:00:00Z", "2026-11-09T15:10:00Z", "SIGNSHEET_TIME_INVALID"),
                ("2026-02-31T15:00:00Z", "2026-11-09T15:10:00Z", "SIGNSHEET_TIME_INVALID")):
            with self.subTest(issued=issued, expires=expires):
                self.assertEqual(self.code(sheet.build_go_cut, **{**GO, "issued_at": issued,
                                                                  "expires_at": expires}), code)
        self.assertTrue(sheet.build_go_cut(**{**GO, "expires_at": "2026-11-09T15:30:00Z"}))
        for key in ("nonce", "ops_source_sha", "app_source_sha", "plan_sha256"):
            for bad in (None, "", "x", "short"):
                with self.subTest(key=key, bad=bad):
                    self.assertEqual(self.code(sheet.build_go_cut, **{**GO, key: bad}),
                                     "SIGNSHEET_INPUT_INVALID")

    def test_launcher_go_is_canonical_separate_and_binds_existing_root_approval(self):
        args = dict(GO, nonce="e" * 64, purpose="LAUNCHER_ONLY",
                    release_asset_sha256=D("8"), old_launcher_sha256="8" * 64,
                    candidate_operator_sha256="9" * 64,
                    expires_at="2026-11-09T15:10:00Z")
        raw = sheet.build_go_cut(**args)
        doc = json.loads(raw)
        self.assertEqual(set(doc), set(sheet.LAUNCHER_GO_FIELDS))
        self.assertEqual({k: doc[k] for k in sheet.LAUNCHER_GO_CONSTANTS},
                         sheet.LAUNCHER_GO_CONSTANTS)
        self.assertEqual((doc["release_asset_sha256"],
                          doc["old_launcher_sha256"],
                          doc["candidate_operator_sha256"]),
                         (D("8"), "8" * 64, "9" * 64))
        self.assertEqual(raw, sheet.build_go_cut(**args))
        card = sheet.review_card(raw)
        self.assertEqual(card["kind"], "qa525-go-launcher-v1")
        self.assertEqual(card["namespace"], sheet.GO_NAMESPACE)
        self.assertFalse(card["signed_here"] or card["approval_here"] or card["may_apply"])
        self.assertEqual(
            self.code(sheet.build_go_cut, **dict(args, purpose="API_WEB_QA_ONLY")),
            "SIGNSHEET_PURPOSE_INVALID")
        self.assertEqual(
            self.code(sheet.build_go_cut,
                      **dict(args, expires_at="2026-11-09T15:15:01Z")),
            "SIGNSHEET_WINDOW_INVALID")
        for k, bad in (("nonce", "N" * 43), ("old_launcher_sha256", D("8")),
                       ("release_asset_sha256", "8" * 64),
                       ("candidate_operator_sha256", "H" * 64)):
            self.assertEqual(self.code(sheet.build_go_cut, **dict(args, **{k: bad})),
                             "SIGNSHEET_INPUT_INVALID")
        self.assertEqual(sheet.new_launcher_nonce(bytes(range(32))), bytes(range(32)).hex())
        self.assertEqual(
            self.code(sheet.new_launcher_nonce, b"Q" * 32),
            "SIGNSHEET_NONCE_ENTROPY_INVALID")
        # Unsigned modified purpose/scope must never produce a valid review card.
        modified = dict(doc, scope="API_WEB_QA_ONLY")
        corrupted = (json.dumps(modified, sort_keys=True, separators=(",", ":")) +
                     "\n").encode()
        self.assertEqual(self.code(sheet.review_card, corrupted),
                         "SIGNSHEET_DOCUMENT_INVALID")

    def test_nonce_comes_from_caller_entropy_only(self):
        nonce = sheet.new_nonce(bytes(range(32)))
        self.assertRegex(nonce, r"^[A-Za-z0-9_-]{43}$")
        for bad in (b"", b"\x00" * 32, bytes(31), "x" * 32, None, bytes(range(33))):
            self.assertEqual(self.code(sheet.new_nonce, bad), "SIGNSHEET_NONCE_ENTROPY_INVALID")

    def test_review_card_lists_fields_in_fixed_order_and_never_approves(self):
        raw = sheet.build_ops_v3_receipt(**FIELDS)
        card = sheet.review_card(raw)
        self.assertEqual(card["kind"], "qa525-ops-v3")
        self.assertEqual(card["namespace"], sheet.OPS_NAMESPACE)
        self.assertRegex(card["code"], r"^[0-9a-f]{4}(-[0-9a-f]{4}){3}$")
        self.assertEqual([l.split(":")[0] for l in card["lines"]], list(sheet.OPS_FIELDS))
        self.assertFalse(card["signed_here"] or card["approval_here"] or card["may_apply"])
        other = sheet.build_ops_v3_receipt(**{**FIELDS, "program_sha256": D("9")})
        self.assertNotEqual(sheet.short_code(other), card["code"])
        gcard = sheet.review_card(sheet.build_go_cut(**GO))
        self.assertEqual((gcard["kind"], gcard["namespace"]), ("qa525-go-cut-v1", sheet.GO_NAMESPACE))
        self.assertEqual([l.split(":")[0] for l in gcard["lines"]], list(sheet.GO_FIELDS))

    def test_review_card_refuses_noncanonical_unknown_or_extra_documents(self):
        good = json.loads(sheet.build_ops_v3_receipt(**FIELDS))
        cases = {
            "indent": json.dumps(good, indent=2).encode(),
            "unsorted": json.dumps(good).encode() + b"\n",
            "extra": (json.dumps({**good, "x": "y"}, sort_keys=True, separators=(",", ":")) + "\n").encode(),
            "schema": (json.dumps({**good, "schema": "other"}, sort_keys=True, separators=(",", ":")) + "\n").encode(),
            "missing": (json.dumps({k: v for k, v in good.items() if k != "policy_hash"},
                                   sort_keys=True, separators=(",", ":")) + "\n").encode(),
            "nan": b'{"schema":NaN}\n', "text": b"not json\n", "list": b"[]\n",
        }
        for label, raw in cases.items():
            with self.subTest(label):
                self.assertIn(self.code(sheet.review_card, raw),
                              ("SIGNSHEET_NOT_CANONICAL", "SIGNSHEET_DOCUMENT_INVALID"))
        for bad in (None, "str", b"", b"x" * 65537):
            self.assertEqual(self.code(sheet.review_card, bad), "SIGNSHEET_INPUT_INVALID")

    def test_static_contract_is_pure(self):
        text = (ROOT / "qa525-vnext-sponsor-signing.py").read_text()
        for forbidden in ("import os", "import subprocess", "import socket", "open(",
                          "import random", "urllib", "import secrets", "os.urandom"):
            self.assertNotIn(forbidden, text)
        with self.assertRaises(SystemExit):
            exec(compile(text, "x", "exec"), {"__name__": "__main__"})


@unittest.skipIf(SSH_KEYGEN is None, "ssh-keygen not installed")
class SponsorSigningRealSshKeygenTests(unittest.TestCase):
    """Builder output signed by REAL ssh-keygen is accepted by the EXISTING verifiers."""

    @classmethod
    def setUpClass(cls):
        cls.tmp = tempfile.TemporaryDirectory()
        cls.key = os.path.join(cls.tmp.name, "throwaway")
        subprocess.run([SSH_KEYGEN, "-q", "-t", "ed25519", "-N", "", "-C", "synthetic",
                        "-f", cls.key], check=True)
        cls.pub = Path(cls.key + ".pub").read_text()
        cls.fp = sheet.fingerprint_of_public_key(cls.pub)

    @classmethod
    def tearDownClass(cls):
        cls.tmp.cleanup()

    def sign(self, raw, namespace):
        path = os.path.join(self.tmp.name, "doc-%d" % len(os.listdir(self.tmp.name)))
        Path(path).write_bytes(raw)
        subprocess.run([SSH_KEYGEN, "-Y", "sign", "-f", self.key, "-n", namespace, path],
                       check=True, capture_output=True)
        return Path(path + ".sig").read_bytes()

    def verifier(self, allowed, signature, principal, namespace, receipt):
        base = os.path.join(self.tmp.name, "v%d" % len(os.listdir(self.tmp.name)))
        Path(base + ".allowed").write_bytes(allowed)
        Path(base + ".sig").write_bytes(signature)
        done = subprocess.run([SSH_KEYGEN, "-Y", "verify", "-f", base + ".allowed", "-I", principal,
                               "-n", namespace, "-s", base + ".sig"], input=receipt,
                              capture_output=True)
        return done.returncode, done.stdout

    def test_fingerprint_matches_ssh_keygen_and_allowed_line_is_accepted(self):
        shown = subprocess.run([SSH_KEYGEN, "-l", "-f", self.key + ".pub"], capture_output=True,
                               text=True, check=True).stdout.split()[1]
        self.assertEqual(self.fp, shown)
        line = sheet.allowed_signers_line(self.pub, sheet.OPS_NAMESPACE, self.fp)
        self.assertEqual(opsig.fingerprint_of(line.encode()), self.fp)

    def test_ops_v3_receipt_signed_for_real_passes_the_existing_v3_verifier(self):
        raw = sheet.build_ops_v3_receipt(**FIELDS)
        sig = self.sign(raw, sheet.OPS_NAMESPACE)
        allowed = sheet.allowed_signers_line(self.pub, sheet.OPS_NAMESPACE, self.fp).encode()
        result = opsig.verify_ops_receipt(
            receipt=raw, signature=sig, allowed_signers=allowed, ops_fingerprint=self.fp,
            app_fingerprints=frozenset({"SHA256:" + "A" * 43}), policy_hash=FIELDS["policy_hash"],
            verifier=self.verifier, expected_app_archive_sha256=FIELDS["app_archive_sha256"])
        self.assertTrue(result["app_archive_bound"])
        self.assertFalse(result["may_apply"])
        # a different archive digest must not combine with this signature
        with self.assertRaises(opsig.Stop) as ctx:
            opsig.verify_ops_receipt(
                receipt=raw, signature=sig, allowed_signers=allowed, ops_fingerprint=self.fp,
                app_fingerprints=frozenset({"SHA256:" + "A" * 43}),
                policy_hash=FIELDS["policy_hash"], verifier=self.verifier,
                expected_app_archive_sha256=D("8"))
        self.assertEqual(str(ctx.exception), "OPSSIG_APP_ARCHIVE_MISMATCH")

    def test_go_cut_flows_through_check_cut_with_a_real_signature(self):
        ops_raw = sheet.build_ops_v3_receipt(**FIELDS)
        go_raw = sheet.build_go_cut(**GO)
        go_sig = self.sign(go_raw, sheet.GO_NAMESPACE)
        go_allowed = sheet.allowed_signers_line(self.pub, sheet.GO_NAMESPACE, self.fp).encode()

        def verify_go(raw, sig, fingerprint):
            code, out = self.verifier(go_allowed, sig, sheet.PRINCIPAL, sheet.GO_NAMESPACE, raw)
            return code == 0 and fingerprint in out.decode()
        pins = {k: GO[k] for k in ("ops_source_sha", "app_source_sha", "app_archive_sha256",
                                   "plan_sha256", "admission_sha256", "policy_hash")}
        pins["go_fingerprint"] = self.fp
        subject = {"source_sha": GO["app_source_sha"], "subject_sha256": GO["app_archive_sha256"],
                   "signer_workflow": gocut.APP_WORKFLOW, "oidc_issuer": gocut.ISSUER}
        now = dt.datetime(2026, 11, 9, 15, 5, tzinfo=dt.timezone.utc)
        result = gocut.check_cut(go_bytes=go_raw, go_signature_bytes=go_sig, pins=pins,
                                 ops_v3_receipt=json.loads(ops_raw), app_subject=subject,
                                 now_utc=now, verify_go_signature=verify_go,
                                 nonce_unused=lambda nonce, adm: True)
        self.assertFalse(result["may_apply"] or result["may_install"])
        late = dt.datetime(2026, 11, 9, 15, 21, tzinfo=dt.timezone.utc)
        with self.assertRaises(gocut.Stop):
            gocut.check_cut(go_bytes=go_raw, go_signature_bytes=go_sig, pins=pins,
                            ops_v3_receipt=json.loads(ops_raw), app_subject=subject,
                            now_utc=late, verify_go_signature=verify_go,
                            nonce_unused=lambda nonce, adm: True)

    def test_launcher_go_uses_real_ed25519_signature_and_cannot_approve_api(self):
        args = dict(GO, nonce="e" * 64, purpose="LAUNCHER_ONLY",
                    release_asset_sha256=D("8"), old_launcher_sha256="8" * 64,
                    candidate_operator_sha256="9" * 64,
                    expires_at="2026-11-09T15:10:00Z")
        raw = sheet.build_go_cut(**args)
        signed = self.sign(raw, sheet.GO_NAMESPACE)
        allowed = sheet.allowed_signers_line(
            self.pub, sheet.GO_NAMESPACE, self.fp).encode()

        def verify_go(raw_, signature_, fingerprint):
            code, output = self.verifier(
                allowed, signature_, sheet.PRINCIPAL, sheet.GO_NAMESPACE, raw_)
            return code == 0 and fingerprint in output.decode()
        cut_pins = {k: args[k] for k in (
            "ops_source_sha", "app_source_sha", "app_archive_sha256",
            "plan_sha256", "admission_sha256", "policy_hash")}
        cut_pins["go_fingerprint"] = self.fp
        launch_pins = {k: args[k] for k in (
            "release_asset_sha256", "old_launcher_sha256",
            "candidate_operator_sha256")}
        subject = dict(source_sha=GO["app_source_sha"],
                       subject_sha256=GO["app_archive_sha256"],
                       signer_workflow=gocut.APP_WORKFLOW, oidc_issuer=gocut.ISSUER)
        call = dict(go_bytes=raw, go_signature_bytes=signed,
                    pins=cut_pins, ops_v3_receipt=json.loads(
                        sheet.build_ops_v3_receipt(**FIELDS)),
                    app_subject=subject, purpose="LAUNCHER_ONLY",
                    launcher_pins=launch_pins,
                    now_utc=dt.datetime(2026, 11, 9, 15, 5, tzinfo=dt.timezone.utc),
                    verify_go_signature=verify_go,
                    nonce_unused=lambda nonce, admission: True)
        result = gocut.check_cut(**call)
        self.assertEqual(result["purpose"], "LAUNCHER_ONLY")
        self.assertFalse(result["may_install"] or result["may_apply"])
        # The same cryptographic signature does NOT authorize API/WEB.
        with self.assertRaises(gocut.Stop):
            gocut.check_cut(**dict(call, purpose="API_WEB_QA_ONLY", launcher_pins=None))
        # Signed text or signature swap is not accepted by OpenSSH.
        altered = bytearray(raw)
        altered[24] ^= 1
        with self.assertRaises(gocut.Stop):
            gocut.check_cut(**dict(call, go_bytes=bytes(altered)))
        wrong_namespace = self.sign(raw, sheet.OPS_NAMESPACE)
        with self.assertRaises(gocut.Stop):
            gocut.check_cut(**dict(call, go_signature_bytes=wrong_namespace))

    def test_signature_for_one_namespace_is_useless_in_the_other(self):
        raw = sheet.build_ops_v3_receipt(**FIELDS)
        sig = self.sign(raw, sheet.GO_NAMESPACE)
        allowed = sheet.allowed_signers_line(self.pub, sheet.OPS_NAMESPACE, self.fp).encode()
        code, _ = self.verifier(allowed, sig, sheet.PRINCIPAL, sheet.OPS_NAMESPACE, raw)
        self.assertNotEqual(code, 0)

    def test_signature_namespace_mismatch_fails_in_both_directions(self):
        ops_raw = sheet.build_ops_v3_receipt(**FIELDS)
        go_raw = sheet.build_go_cut(**GO)
        for raw, signed_ns, verify_ns in ((ops_raw, sheet.GO_NAMESPACE, sheet.OPS_NAMESPACE),
                                          (go_raw, sheet.OPS_NAMESPACE, sheet.GO_NAMESPACE),
                                          (go_raw, sheet.GO_NAMESPACE, sheet.OPS_NAMESPACE),
                                          (ops_raw, sheet.OPS_NAMESPACE, sheet.GO_NAMESPACE)):
            with self.subTest(signed=signed_ns, verify=verify_ns, same_doc=raw is go_raw):
                sig = self.sign(raw, signed_ns)
                allowed = sheet.allowed_signers_line(self.pub, verify_ns, self.fp).encode()
                code, _ = self.verifier(allowed, sig, sheet.PRINCIPAL, verify_ns, raw)
                self.assertNotEqual(code, 0)

    def test_namespace_non_string_is_a_fixed_stop(self):
        for bad in (None, 5, ["x"], {"a": 1}, b"qa525-ops-v2@pmgm"):
            self.assertEqual(self._code(sheet.allowed_signers_line, self.pub, bad, self.fp),
                             "SIGNSHEET_NAMESPACE_INVALID")

    def test_allowed_line_refuses_wrong_pin_namespace_and_bad_keys(self):
        other = "SHA256:" + "B" * 43
        self.assertEqual(self._code(sheet.allowed_signers_line, self.pub, sheet.OPS_NAMESPACE, other),
                         "SIGNSHEET_FINGERPRINT_MISMATCH")
        self.assertEqual(self._code(sheet.allowed_signers_line, self.pub, "other@ns", self.fp),
                         "SIGNSHEET_NAMESPACE_INVALID")
        self.assertEqual(self._code(sheet.allowed_signers_line, self.pub, sheet.OPS_NAMESPACE, "x"),
                         "SIGNSHEET_KEY_INVALID")
        for bad in ("", "ssh-rsa AAAA", None, self.pub.replace("ssh-ed25519", "ssh-rsa"),
                    "ssh-ed25519 " + "A" * 68, "x" * 600):
            with self.subTest(bad=bad):
                self.assertEqual(self._code(sheet.fingerprint_of_public_key, bad),
                                 "SIGNSHEET_KEY_INVALID")

    def _code(self, call, *a, **k):
        with self.assertRaises(sheet.Stop) as ctx:
            call(*a, **k)
        return str(ctx.exception)


if __name__ == "__main__":
    unittest.main()
