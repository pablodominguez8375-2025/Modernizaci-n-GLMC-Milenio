"""QA525: signed G6 v2 per-command GO is shape/freshness only, not a physical grant."""
import datetime as dt
import importlib.util
import json
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest

BASE = Path(__file__).resolve().parents[1] / "scripts" / "qa"


def load(name, filename):
    spec = importlib.util.spec_from_file_location(name, BASE / filename)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


sheet = load("g6_go_sheet", "qa525-vnext-sponsor-signing.py")
cut = load("g6_go_cut", "qa525-vnext-go-cut-contract.py")

D = lambda c: "sha256:" + c * 64
FP = "SHA256:" + "A" * 43
OPS = "a" * 40
APP = "b" * 40
COMMON = {
    "ops_source_sha": OPS, "app_source_sha": APP,
    "app_archive_sha256": D("5"), "plan_sha256": D("6"),
    "admission_sha256": D("7"), "policy_hash": D("4"),
}
TIMES = {"issued_at": "2026-11-09T14:00:00Z",
         "expires_at": "2026-11-09T14:10:00Z"}
NOW = dt.datetime(2026, 11, 9, 14, 5, tzinfo=dt.timezone.utc)
OPERATION_DIGEST = D("8")
SIGNATURE = b"synthetic-signature-not-authority"


class G6SignedCutContractTests(unittest.TestCase):
    def go(self, command="backup", **over):
        values = dict(COMMON, **TIMES, nonce="e" * 64,
                      purpose="G6_V2_ONLY", g6_command=command,
                      g6_operation_sha256=OPERATION_DIGEST)
        return sheet.build_go_cut(**dict(values, **over))

    def params(self, command="backup", *, raw=None, **over):
        raw = self.go(command) if raw is None else raw
        pins = dict(COMMON, go_fingerprint=FP)
        ops = dict(schema="qa525-ops-v3", status="OPS_SIGNED_VERIFIED",
                   ops_source_sha=OPS, program_sha256=D("1"),
                   readiness_sha256=D("2"), staging_sha256=D("3"),
                   policy_hash=COMMON["policy_hash"],
                   app_archive_sha256=COMMON["app_archive_sha256"])
        subject = dict(source_sha=APP, subject_sha256=COMMON["app_archive_sha256"],
                       signer_workflow=cut.APP_WORKFLOW, oidc_issuer=cut.ISSUER)
        args = dict(go_bytes=raw, go_signature_bytes=SIGNATURE,
                    pins=pins, ops_v3_receipt=ops, app_subject=subject,
                    now_utc=NOW, verify_go_signature=lambda _b, _s, _fp: True,
                    nonce_unused=lambda _n, _admission: True,
                    purpose="G6_V2_ONLY",
                    g6_pins=dict(g6_command=command, g6_operation_sha256=OPERATION_DIGEST))
        args.update(over)
        return args

    def code(self, operation, *args, **kwargs):
        with self.assertRaises((sheet.Stop, cut.Stop)) as err:
            operation(*args, **kwargs)
        return str(err.exception)

    def test_four_operations_are_four_distinct_canonical_go_documents(self):
        signed = {}
        for cmd in ("init", "backup", "restore", "review"):
            with self.subTest(cmd=cmd):
                raw = self.go(cmd)
                doc = json.loads(raw)
                self.assertEqual(raw, (json.dumps(doc, sort_keys=True,
                                 separators=(",", ":"), ensure_ascii=True) + "\n").encode("ascii"))
                self.assertEqual(set(doc), set(sheet.G6_GO_FIELDS))
                self.assertEqual(set(doc), set(cut.G6_FIELDS))
                for key, expected in sheet.G6_GO_CONSTANTS.items():
                    self.assertEqual(doc[key], expected)
                self.assertEqual(doc["action"], "g6-v2-" + cmd + "-supervised")
                self.assertEqual(doc["g6_command"], cmd)
                self.assertEqual(doc["g6_operation_sha256"], OPERATION_DIGEST)
                self.assertEqual((doc["issued_at"], doc["expires_at"]),
                                 (TIMES["issued_at"], TIMES["expires_at"]))
                card = sheet.review_card(raw)
                self.assertEqual(card["kind"], "qa525-go-g6-v1")
                self.assertEqual(card["namespace"], sheet.GO_NAMESPACE)
                self.assertEqual([line.split(":")[0] for line in card["lines"]],
                                 list(sheet.G6_GO_FIELDS))
                self.assertFalse(card["signed_here"] or card["approval_here"] or card["may_apply"])
                result = cut.check_cut(**self.params(cmd, raw=raw))
                self.assertEqual((result["purpose"], result["g6_command"]),
                                 ("G6_V2_ONLY", cmd))
                self.assertTrue(result["g6_contract_shape_only"])
                self.assertFalse(result["may_apply"] or result["may_install"])
                self.assertFalse(result["bootstrap_authenticated_here"])
                self.assertFalse(result["go_signature_provenance_verified_here"])
                signed[cmd] = raw
        self.assertEqual(len(set(signed.values())), 4)

    def test_g6_go_does_not_approve_a_different_g6_command(self):
        for original in ("init", "backup", "restore", "review"):
            for wanted in ("init", "backup", "restore", "review"):
                if original == wanted:
                    continue
                with self.subTest(original=original, wanted=wanted):
                    code = self.code(cut.check_cut, **self.params(
                        wanted, raw=self.go(original)))
                    self.assertEqual(code, "GO_CUT_MISMATCH")

    def test_g6_go_is_never_launcher_or_api_web_and_reverse(self):
        g6_raw = self.go("backup")
        self.assertIn(self.code(cut.check_cut, **self.params("backup", raw=g6_raw,
                           purpose="API_WEB_QA_ONLY", g6_pins=None)),
                      {"GO_CUT_FIELDS_INVALID", "GO_CUT_INPUT_INVALID"})
        launch_pins = {"release_asset_sha256": D("8"),
                       "old_launcher_sha256": "f" * 64,
                       "candidate_operator_sha256": "1" * 64}
        self.assertIn(self.code(cut.check_cut, **self.params("backup", raw=g6_raw,
                           purpose="LAUNCHER_ONLY", g6_pins=None,
                           launcher_pins=launch_pins)),
                      {"GO_CUT_FIELDS_INVALID", "GO_CUT_INPUT_INVALID"})
        launcher_raw = sheet.build_go_cut(
            **COMMON, **TIMES, nonce="e" * 64,
            purpose="LAUNCHER_ONLY", **launch_pins)
        api_raw = sheet.build_go_cut(
            **COMMON, **TIMES, nonce="A" * 43, purpose="API_WEB_QA_ONLY")
        for raw in (launcher_raw, api_raw):
            with self.subTest(raw=raw[:70]):
                self.assertIn(self.code(cut.check_cut, **self.params(
                    "backup", raw=raw)), {"GO_CUT_FIELDS_INVALID", "GO_CUT_INPUT_INVALID"})
        self.assertEqual(self.code(cut.check_cut, **self.params(
            "backup", launcher_pins=launch_pins)), "GO_CUT_INPUT_INVALID")

    def test_rejects_missing_wrong_or_extra_g6_pins_and_bad_command(self):
        good = self.params()
        invalid = [None, {}, {"g6_command": "backup"},
                   {"g6_command": "backup", "g6_operation_sha256": D("8"), "other": 1},
                   {"g6_command": "delete", "g6_operation_sha256": D("8")},
                   {"g6_command": "backup", "g6_operation_sha256": "8" * 64},
                   {"g6_command": "backup", "g6_operation_sha256": D("9")}]
        for val in invalid:
            with self.subTest(val=val):
                code = self.code(cut.check_cut, **dict(good, g6_pins=val))
                self.assertTrue(code.startswith(("GO_CUT_",)))
        for bad in ("start", "stop", "apply", "delete", "init;backup", "", None, 3):
            with self.subTest(bad=bad):
                self.assertIn(self.code(self.go, bad),
                              {"SIGNSHEET_G6_COMMAND_INVALID", "SIGNSHEET_INPUT_INVALID"})
        self.assertEqual(self.code(self.go, "backup", g6_operation_sha256="8" * 64),
                         "SIGNSHEET_INPUT_INVALID")

    def test_no_cross_purpose_extra_fields_and_strict_receipt(self):
        for mode, fields in (
            ("API_WEB_QA_ONLY", {"g6_command": "backup"}),
            ("LAUNCHER_ONLY", {"g6_operation_sha256": D("8")}),
            ("G6_V2_ONLY", {"old_launcher_sha256": "f" * 64}),
            ("G6_V2_ONLY", {"release_asset_sha256": D("8")})):
            self.assertEqual(
                self.code(sheet.build_go_cut, **dict(COMMON, **TIMES,
                          nonce="e" * 64, purpose=mode, **fields)),
                "SIGNSHEET_PURPOSE_INVALID")
        original = json.loads(self.go())
        variants = [
            dict(original, extra=1),
            {k: v for k, v in original.items() if k != "g6_operation_sha256"},
            dict(original, g6_command="stop", action="g6-v2-stop-supervised"),
            dict(original, scope="QA525_LAUNCHER_ONLY_NO_DOCKER_NO_IDENTITY_NO_MIGRATION"),
            dict(original, action="g6-v2-restore-supervised"),
        ]
        for doc in variants:
            with self.subTest(doc=doc):
                raw = (json.dumps(doc, sort_keys=True, separators=(",", ":")) + "\n").encode()
                self.assertEqual(self.code(sheet.review_card, raw),
                                 "SIGNSHEET_DOCUMENT_INVALID")
                code = self.code(cut.check_cut, **self.params(raw=raw))
                self.assertTrue(code.startswith("GO_CUT_"))

    def test_nonce_ttl_and_signature_are_not_bypassed(self):
        self.assertEqual(self.code(self.go, "backup", nonce="N" * 43),
                         "SIGNSHEET_INPUT_INVALID")
        self.assertEqual(self.code(self.go, "backup", expires_at="2026-11-09T14:15:01Z"),
                         "SIGNSHEET_WINDOW_INVALID")
        for now in (dt.datetime(2026, 11, 9, 13, 59, tzinfo=dt.timezone.utc),
                    dt.datetime(2026, 11, 9, 14, 10, tzinfo=dt.timezone.utc)):
            with self.subTest(now=now):
                self.assertEqual(self.code(cut.check_cut, **self.params(now_utc=now)),
                                 "GO_CUT_EXPIRED_OR_FUTURE")
        self.assertEqual(self.code(cut.check_cut, **self.params(
            verify_go_signature=lambda *_: False)), "GO_CUT_SIGNATURE_DENIED")
        self.assertEqual(self.code(cut.check_cut, **self.params(
            nonce_unused=lambda *_: False)), "GO_CUT_NONCE_UNSAFE")
        self.assertEqual(self.code(cut.check_cut, **self.params(
            go_signature_bytes=b"")), "GO_CUT_INPUT_INVALID")
        pins = dict(self.params()["pins"], go_fingerprint="SHA256:" + "B" * 43)
        self.assertEqual(self.code(cut.check_cut, **self.params(pins=pins,
            verify_go_signature=lambda _raw, _sig, fp: fp == FP)),
            "GO_CUT_SIGNATURE_DENIED")
        raw = self.go()
        tampered = raw.replace(b"g6-v2-backup-supervised", b"g6-v2-review-supervised")
        self.assertTrue(self.code(cut.check_cut, **self.params(raw=tampered)).startswith("GO_CUT_"))

    @unittest.skipUnless(shutil.which("ssh-keygen"), "ssh-keygen unavailable")
    def test_real_ed25519_signature_uses_go_namespace_and_fails_on_other_namespace(self):
        binary = shutil.which("ssh-keygen")
        with tempfile.TemporaryDirectory(prefix="qa525-g6-go-") as directory:
            key = str(Path(directory) / "signer")
            subprocess.run([binary, "-q", "-t", "ed25519", "-N", "", "-f", key],
                           check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            pub = Path(key + ".pub").read_text().split()
            allowed = str(Path(directory) / "allowed")
            Path(allowed).write_text(
                'qa525-ops namespaces="' + sheet.GO_NAMESPACE + '" ssh-ed25519 ' + pub[1] + "\n")
            raw = self.go("backup")
            doc = Path(directory) / "doc"
            doc.write_bytes(raw)

            def sign(namespace):
                sigpath = Path(str(doc) + ".sig")
                sigpath.unlink(missing_ok=True)
                subprocess.run([binary, "-Y", "sign", "-f", key, "-n", namespace, str(doc)],
                               check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
                return sigpath.read_bytes()

            def verify_callback(payload, signature, fp):
                sig = Path(directory) / "verify.sig"
                sig.write_bytes(signature)
                done = subprocess.run([binary, "-Y", "verify", "-f", allowed,
                                       "-I", "qa525-ops", "-n", sheet.GO_NAMESPACE,
                                       "-s", str(sig)], input=payload, capture_output=True)
                return done.returncode == 0 and fp.startswith("SHA256:")
            good = sign(sheet.GO_NAMESPACE)
            positive = cut.check_cut(**self.params(
                raw=raw, go_signature_bytes=good, verify_go_signature=verify_callback))
            self.assertFalse(positive["may_apply"])
            wrong_ns = sign(sheet.OPS_NAMESPACE)
            self.assertEqual(self.code(cut.check_cut, **self.params(
                raw=raw, go_signature_bytes=wrong_ns,
                verify_go_signature=verify_callback)), "GO_CUT_SIGNATURE_DENIED")


if __name__ == "__main__":
    unittest.main()
