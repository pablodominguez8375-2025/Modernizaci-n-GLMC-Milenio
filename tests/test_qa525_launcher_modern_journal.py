import hashlib
import importlib.util
import json
from pathlib import Path
import unittest

p = Path(__file__).resolve().parents[1] / "scripts/qa/qa525-launcher-modern-journal.py"
spec = importlib.util.spec_from_file_location("journal", p)
j = importlib.util.module_from_spec(spec)
spec.loader.exec_module(j)

def enc(x):
    return (json.dumps(x, sort_keys=True, separators=(",", ":")) + "\n").encode("ascii")

class ModernJournalTests(unittest.TestCase):
    def fixture(self):
        n = "a"*64
        backup = b"old launcher"
        e = {"app_source_sha":"b"*40, "app_archive_sha256":"sha256:"+"c"*64,
             "candidate_operator_sha256":"sha256:"+"d"*64, "shim_sha256":"sha256:"+"f"*64}
        r = {"nonce":n,"status":"LAUNCHER_ATOMIC_SWAP_COMMITTED",
             "old_launcher_sha256":"sha256:"+hashlib.sha256(backup).hexdigest(),
             "new_launcher_sha256":"sha256:"+"f"*64,"evidence":e}
        return dict(nonce=n, receipt=enc(r), intent=enc(dict(r,status="SWAP_INTENT_NO_DEPLOYMENT")),
                    marker=enc(dict(op="swap",nonce=n,status="IN_FLIGHT")),
                    backup=backup,expected_evidence=e)
    def test_valid(self):
        x = self.fixture()
        self.assertEqual(j.verify_modern_swap(**x), (x["nonce"],hashlib.sha256(x["backup"]).hexdigest(),"f"*64))
    def test_evidence_format_and_shim_binding(self):
        for key, value in [("shim_sha256", "sha256:"+"e"*64),
                           ("app_archive_sha256", "c"*64),
                           ("candidate_operator_sha256", "d"*64)]:
            with self.subTest(key=key):
                x = self.fixture()
                r = json.loads(x["receipt"])
                r["evidence"][key] = value
                x["expected_evidence"] = r["evidence"]
                x["receipt"] = enc(r)
                x["intent"] = enc(dict(r, status="SWAP_INTENT_NO_DEPLOYMENT"))
                with self.assertRaises(ValueError):
                    j.verify_modern_swap(**x)
    def test_invalid(self):
        for key, value in [
            ("nonce","b"*64),("backup",b"wrong"),("expected_evidence",{}),
            ("marker",b"{}\n"),("intent",b"{}\n"),("receipt",b"{}\n"),
            ("receipt",b'{"nonce":"a","nonce":"b"}\n'),
            ("receipt",b"{}"),("backup",b"")]:
            with self.subTest(key=key,value=value):
                x=self.fixture()
                x[key]=value
                with self.assertRaises(ValueError):
                    j.verify_modern_swap(**x)

class LegacyJournalTests(unittest.TestCase):
    def fixture(self):
        backup = b"legacy launcher"
        evidence = {"source_sha": "b"*40, "release_asset_sha256": "sha256:"+"c"*64,
                    "candidate_operator_sha256": "d"*64}
        r = dict(evidence, status="LAUNCHER_ATOMIC_SWAP_COMMITTED", nonce="a"*64,
                 old_launcher_sha256=hashlib.sha256(backup).hexdigest(), new_launcher_sha256="f"*64)
        return dict(nonce="a"*64, receipt=enc(r), backup=backup, expected_evidence=evidence)

    def test_legacy_edge(self):
        x = self.fixture()
        self.assertEqual(j.verify_legacy_swap(**x),
                         (x["nonce"], hashlib.sha256(x["backup"]).hexdigest(), "f"*64))

    def test_reject_untrusted_legacy(self):
        for key, value in (("nonce", "b"*64), ("backup", b"wrong"),
                           ("expected_evidence", {}), ("receipt", b"{}"),
                           ("receipt", b'{"nonce":"a","nonce":"b"}')):
            with self.subTest(key=key):
                x = self.fixture()
                x[key] = value
                with self.assertRaises(ValueError):
                    j.verify_legacy_swap(**x)

    def test_reject_mixed_digest(self):
        x = self.fixture()
        r = json.loads(x["receipt"])
        r["new_launcher_sha256"] = "sha256:" + r["new_launcher_sha256"]
        x["receipt"] = enc(r)
        with self.assertRaises(ValueError):
            j.verify_legacy_swap(**x)


if __name__ == "__main__":
    unittest.main()
