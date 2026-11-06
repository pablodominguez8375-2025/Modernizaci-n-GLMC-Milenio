"""Hermetic tests for byte-binding only; NO cryptographic signature is tested."""
import copy
import datetime as dt
import hashlib
import importlib.util
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
MODULE = ROOT / "scripts/qa/qa525-vnext-trust-binding.py"
spec = importlib.util.spec_from_file_location("qa525_test_binding", MODULE)
binding = importlib.util.module_from_spec(spec)
spec.loader.exec_module(binding)

NOW = dt.datetime(2026, 11, 5, 18, 5, 0, tzinfo=dt.timezone.utc)
SHA_A = "a" * 40
SHA_O = "b" * 40
DIG_A = "sha256:" + "c" * 64
DIG_B = "sha256:" + "d" * 64
DIG_C = "sha256:" + "e" * 64
DIG_D = "sha256:" + "f" * 64
POLICY = "sha256:" + "1" * 64
G6 = "sha256:" + "2" * 64
SPONSOR = "sha256:" + "3" * 64
IDENTITY = {"device": 12, "inode": 345, "size": 9876, "mtime_ns": 1780000000000}


def admission_record():
    return {
        "schema": "qa525-admission-v2", "status": "ADMITTED",
        "ops_source_sha": SHA_O, "app_source_sha": SHA_A, "artifact_id": 56,
        "artifact_sha256": DIG_A, "silo_workflow_run": 1234,
        "signed_installable_sha256": DIG_B, "artifact_identity": dict(IDENTITY),
        "manifest_sha256": DIG_C, "attestation_bundle_sha256": DIG_D,
        "policy_hash": POLICY, "nonce": "n" * 64,
        "nonce_consumed": False, "nonce_consume_transition": "APPLYING",
        "admitted_at": "2026-11-05T18:02:00Z",
        "valid_until": "2026-11-05T18:25:00Z",
        "g6_expires_at": "2026-11-06T18:00:00Z", "network_allowed": False,
    }


def canonical(doc):
    return (json.dumps(doc, sort_keys=True) + "\n").encode()


def approval_record(admission):
    return {
        "schema": "qa525-approval-anchor-v1",
        "status": "BYTE_BINDING_INPUT_ONLY_NOT_VERIFIED_HERE",
        "scope": "API_WEB_QA_ONLY", "host": "glmdch",
        "environment": "QA_SYNTHETIC",
        "nonce": admission["nonce"],
        "admission_sha256": "sha256:" + hashlib.sha256(canonical(admission)).hexdigest(),
        "ops_source_sha": admission["ops_source_sha"],
        "app_source_sha": admission["app_source_sha"],
        "policy_hash": admission["policy_hash"],
        "artifact_id": admission["artifact_id"],
        "artifact_sha256": admission["artifact_sha256"],
        "signed_installable_sha256": admission["signed_installable_sha256"],
        "manifest_sha256": admission["manifest_sha256"],
        "attestation_bundle_sha256": admission["attestation_bundle_sha256"],
        "artifact_identity": dict(admission["artifact_identity"]),
        "g6_review_sha256": G6,
        "sponsor_approval_sha256": SPONSOR,
        "g6_expires_at": admission["g6_expires_at"],
        "silo_workflow_run": admission["silo_workflow_run"],
        "issued_at": "2026-11-05T18:03:00Z",
        "expires_at": "2026-11-05T18:20:00Z",
    }


class TrustBindingContractTests(unittest.TestCase):
    def setUp(self):
        self.admission = admission_record()
        self.anchor = approval_record(self.admission)

    def evaluate(self, admission=None, anchor=None, now=NOW, policy=POLICY):
        a = self.admission if admission is None else admission
        e = self.anchor if anchor is None else anchor
        return binding.bind(canonical(a), canonical(e),
                            trusted_policy_hash=policy, now=now)

    def test_valid_bytes_are_consistent_but_never_authorize_install(self):
        r = self.evaluate()
        self.assertEqual(r["status"],
                         "BYTE_BINDING_MATCH_ONLY_EXTERNAL_AUTH_NOT_VERIFIED")
        self.assertEqual(r["nonce"], "n" * 64)
        self.assertTrue(r["external_anchor_sha256"].startswith("sha256:"))
        self.assertEqual(r["valid_until"], "2026-11-05T18:20:00Z")
        for k in ("signature_verified_here", "g6_verified_here",
                  "sponsor_approved_here", "may_apply", "may_install"):
            self.assertIs(r[k], False)

    def test_out_of_range_number_is_a_stop_not_a_valueerror(self):
        raw = canonical(self.admission).rstrip(b"\n")[:-1] + b', "x": 1e999}\n'
        with self.assertRaisesRegex(binding.Stop, "BINDING_ADMISSION_NONCANONICAL"):
            binding.bind(raw, canonical(self.anchor), trusted_policy_hash=POLICY, now=NOW)

    def test_admission_any_bit_changed_rejected_even_if_rehashed_by_attacker(self):
        mutations = (
            ("artifact_sha256", DIG_C), ("signed_installable_sha256", DIG_A),
            ("manifest_sha256", DIG_B), ("attestation_bundle_sha256", DIG_C),
            ("ops_source_sha", SHA_A), ("app_source_sha", SHA_O),
            ("nonce", "z" * 64), ("policy_hash", DIG_A),
            ("artifact_id", 98), ("valid_until", "2026-11-05T18:23:00Z"),
            ("g6_expires_at", "2026-11-05T18:04:00Z"),
        )
        for key, value in mutations:
            with self.subTest(key=key), self.assertRaises(binding.Stop):
                a = copy.deepcopy(self.admission)
                a[key] = value
                self.evaluate(admission=a)

    def test_independent_anchor_any_cut_binding_changed_rejected(self):
        for key, value in (
            ("nonce", "m" * 64), ("artifact_sha256", DIG_C),
            ("app_source_sha", SHA_O), ("ops_source_sha", SHA_A),
            ("artifact_id", 9), ("policy_hash", DIG_C),
            ("admission_sha256", DIG_A), ("manifest_sha256", DIG_A),
            ("silo_workflow_run", 9876),
            ("g6_expires_at", "2026-11-07T18:00:00Z"),
            ("signed_installable_sha256", DIG_D),
            ("attestation_bundle_sha256", DIG_B),
        ):
            with self.subTest(key=key), self.assertRaises(binding.Stop):
                e = copy.deepcopy(self.anchor)
                e[key] = value
                self.evaluate(anchor=e)

    def test_external_anchor_issued_after_admission_is_required(self):
        self.assertEqual(self.evaluate()["nonce"], "n" * 64)
        e = copy.deepcopy(self.anchor)
        e["issued_at"] = "2026-11-05T18:01:00Z"
        with self.assertRaisesRegex(binding.Stop, "BINDING_WINDOW_INVALID"):
            self.evaluate(anchor=e)
        e["issued_at"] = "2026-11-05T18:06:00Z"
        with self.assertRaisesRegex(binding.Stop, "BINDING_WINDOW_INVALID"):
            self.evaluate(anchor=e)

    def test_anchor_types_cannot_masquerade_as_ints(self):
        a = copy.deepcopy(self.admission)
        a["artifact_id"] = 1
        a_bytes = canonical(a)
        e = approval_record(a)
        for field, value in (("artifact_id", True), ("artifact_id", 1.0),
                             ("silo_workflow_run", True),
                             ("silo_workflow_run", float(a["silo_workflow_run"]))):
            changed = copy.deepcopy(e)
            changed[field] = value
            with self.subTest(field=field, value=value), self.assertRaises(binding.Stop):
                binding.bind(a_bytes, canonical(changed),
                             trusted_policy_hash=POLICY, now=NOW)
        changed = copy.deepcopy(e)
        changed["artifact_identity"]["device"] = 12.0
        with self.assertRaises(binding.Stop):
            binding.bind(a_bytes, canonical(changed),
                         trusted_policy_hash=POLICY, now=NOW)

    def test_identity_changed_even_with_same_digest_is_rejected(self):
        for location in ("admission", "anchor"):
            with self.subTest(where=location), self.assertRaises(binding.Stop):
                a, e = copy.deepcopy(self.admission), copy.deepcopy(self.anchor)
                (a if location == "admission" else e)["artifact_identity"]["inode"] += 1
                self.evaluate(admission=a, anchor=e)

    def test_no_authentication_from_forged_self_consistent_pair(self):
        a = copy.deepcopy(self.admission)
        a["app_source_sha"] = "4" * 40
        e = approval_record(a)
        result = self.evaluate(admission=a, anchor=e)
        # Positive byte binding does NOT mean a Sponsor actually signed it.
        self.assertIs(result["signature_verified_here"], False)
        self.assertIs(result["sponsor_approved_here"], False)
        self.assertIs(result["may_apply"], False)

    def test_fail_closed_on_time_and_expiry(self):
        for now in (
            dt.datetime(2026, 11, 5, 17, 58, tzinfo=dt.timezone.utc),
            dt.datetime(2026, 11, 5, 18, 20, tzinfo=dt.timezone.utc),
            dt.datetime(2026, 11, 6, 18, 0, tzinfo=dt.timezone.utc),
        ):
            with self.subTest(now=now), self.assertRaises(binding.Stop):
                self.evaluate(now=now)
        with self.assertRaises(binding.Stop):
            self.evaluate(now=dt.datetime(2026, 11, 5, 18, 5))
        with self.assertRaises(binding.Stop):
            self.evaluate(policy=DIG_A)
        e = copy.deepcopy(self.anchor)
        e["expires_at"] = "2026-11-05T20:00:00Z"
        with self.assertRaises(binding.Stop):
            self.evaluate(anchor=e)
        a = copy.deepcopy(self.admission)
        a["valid_until"] = "2026-11-05T18:40:00Z"
        with self.assertRaises(binding.Stop):
            self.evaluate(admission=a)

    def test_noncanonical_duplicate_json_oversize_and_nan_rejected(self):
        a = canonical(self.admission)
        e = canonical(self.anchor)
        invalid = (
            a.strip(), a.replace(b'"nonce":', b'"nonce":"x", "nonce":', 1),
            a + b" ", b"{" + b"x" * binding.MAX_RECEIPT_BYTES + b"}",
            a.replace(b'"artifact_id": 56', b'"artifact_id": NaN', 1),
            b"[]",
        )
        for payload in invalid:
            with self.subTest(raw=payload[:24]), self.assertRaises(binding.Stop):
                binding.bind(payload, e, trusted_policy_hash=POLICY, now=NOW)
        with self.assertRaises(binding.Stop):
            binding.bind(a, b'{"nonce":"x","nonce":"y"}',
                         trusted_policy_hash=POLICY, now=NOW)

    def test_strict_flags_schema_and_identity(self):
        for key, value in (
            ("nonce_consumed", True), ("network_allowed", True),
            ("nonce_consume_transition", "APPLIED"),
            ("schema", "qa525-admission-v1"),
            ("silo_workflow_run", 0), ("artifact_id", True),
            ("artifact_identity", {"inode": 1}), ("artifact_sha256", "bad"),
        ):
            with self.subTest(field=key), self.assertRaises(binding.Stop):
                a = copy.deepcopy(self.admission)
                a[key] = value
                self.evaluate(admission=a)
        e = copy.deepcopy(self.anchor)
        e["status"] = "SIGNED_BY_SELF"
        with self.assertRaises(binding.Stop):
            self.evaluate(anchor=e)
        e = copy.deepcopy(self.anchor)
        e["may_apply"] = True
        with self.assertRaises(binding.Stop):
            self.evaluate(anchor=e)

    def test_no_io_or_execution_apis_in_binding_module(self):
        source = MODULE.read_text(encoding="utf-8")
        self.assertNotIn("import subprocess", source)
        self.assertNotIn("import socket", source)
        self.assertNotIn("import os", source)
        self.assertNotIn("import urllib", source)
        self.assertNotIn("import requests", source)
        self.assertNotIn("os.system(", source)
        self.assertNotIn("docker", source.lower().replace("docker, host", ""))


if __name__ == "__main__":
    unittest.main()
