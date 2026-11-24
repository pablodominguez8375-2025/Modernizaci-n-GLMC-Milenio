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
        e = {"app_source_sha":"b"*40, "app_archive_sha256":"c"*64,
             "candidate_operator_sha256":"d"*64, "shim_sha256":"e"*64}
        r = {"nonce":n,"status":"LAUNCHER_ATOMIC_SWAP_COMMITTED",
             "old_launcher_sha256":"sha256:"+hashlib.sha256(backup).hexdigest(),
             "new_launcher_sha256":"sha256:"+"f"*64,"evidence":e}
        return dict(nonce=n, receipt=enc(r), intent=enc(dict(r,status="SWAP_INTENT_NO_DEPLOYMENT")),
                    marker=enc(dict(op="swap",nonce=n,status="IN_FLIGHT")),
                    backup=backup,expected_evidence=e)
    def test_valid(self):
        x = self.fixture()
        self.assertEqual(j.verify_modern_swap(**x), (x["nonce"],hashlib.sha256(x["backup"]).hexdigest(),"f"*64))
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

if __name__ == "__main__":
    unittest.main()
