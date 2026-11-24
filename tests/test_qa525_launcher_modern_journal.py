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


class MixedGenerationTests(unittest.TestCase):
    def test_authenticated_edges_join_and_reject_drift(self):
        chain_path = p.with_name("qa525-launcher-custody-chain.py")
        chain_spec = importlib.util.spec_from_file_location("custody_mixed", chain_path)
        chain = importlib.util.module_from_spec(chain_spec)
        chain_spec.loader.exec_module(chain)
        legacy = LegacyJournalTests().fixture()
        modern = ModernJournalTests().fixture()
        # Bind the legacy successor to the actual modern backup bytes.
        lr = json.loads(legacy["receipt"])
        lr["new_launcher_sha256"] = hashlib.sha256(modern["backup"]).hexdigest()
        legacy["receipt"] = enc(lr)
        modern["nonce"] = "9"*64
        mr = json.loads(modern["receipt"])
        mr["nonce"] = modern["nonce"]
        modern["receipt"] = enc(mr)
        modern["intent"] = enc(dict(mr, status="SWAP_INTENT_NO_DEPLOYMENT"))
        modern["marker"] = enc(dict(op="swap", nonce=modern["nonce"], status="IN_FLIGHT"))
        first = j.verify_legacy_swap(**legacy)
        second = j.verify_modern_swap(**modern)
        self.assertEqual(chain.verify_chain(original=first[1], live=second[2],
                         edges=[second, first]), tuple(sorted((first[0], second[0]))))
        for edges, live in (([first], second[2]), ([first, second], "0"*64),
                            ([first, second, first], second[2])):
            with self.assertRaises(ValueError):
                chain.verify_chain(original=first[1], live=live, edges=edges)


class SnapshotTests(unittest.TestCase):
    def test_aggregate_snapshot_limit(self):
        record = {"backup": b"x" * (1024 * 1024)}
        with self.assertRaisesRegex(ValueError, "SNAPSHOT_TOTAL_SIZE"):
            j.reconcile_snapshot(original="0"*64, live="1"*64,
                records=[record] * 17, authenticated_pins={str(i): {} for i in range(17)},
                verify_chain=None)

    def test_pin_coverage_and_dispatch(self):
        s = importlib.util.spec_from_file_location("snapshot_chain", p.with_name("qa525-launcher-custody-chain.py"))
        c = importlib.util.module_from_spec(s)
        s.loader.exec_module(c)
        x = LegacyJournalTests().fixture()
        pins = {x["nonce"]: {"generation": "legacy", "evidence": x.pop("expected_evidence"),
                              "old_sha256": hashlib.sha256(x["backup"]).hexdigest(), "new_sha256": "f"*64}}
        record = dict(x, generation="legacy")
        args = dict(original=hashlib.sha256(x["backup"]).hexdigest(), live="f"*64,
                    records=[record], authenticated_pins=pins, verify_chain=c.verify_chain)
        self.assertEqual(j.reconcile_snapshot(**args), (x["nonce"],))
        for patch in (
            {"authenticated_pins": {}},
            {"authenticated_pins": {x["nonce"]: dict(pins[x["nonce"]], generation="modern")}},
            {"authenticated_pins": {x["nonce"]: dict(pins[x["nonce"]], new_sha256="0"*64)}},
            {"authenticated_pins": {x["nonce"]: dict(pins[x["nonce"]], old_sha256="0"*64)}},
            {"records": [dict(record, expected_evidence={})]},
            {"records": [record, record]},
            {"live": "0"*64},
        ):
            with self.subTest(patch=patch):
                with self.assertRaises(ValueError):
                    j.reconcile_snapshot(**dict(args, **patch))



    def test_complete_mixed_snapshot(self):
        spec = importlib.util.spec_from_file_location("mixed_snapshot_chain", p.with_name("qa525-launcher-custody-chain.py"))
        c = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(c)
        legacy, modern = LegacyJournalTests().fixture(), ModernJournalTests().fixture()
        modern["nonce"] = "9"*64
        lr, mr = json.loads(legacy["receipt"]), json.loads(modern["receipt"])
        lr["new_launcher_sha256"] = hashlib.sha256(modern["backup"]).hexdigest()
        mr["nonce"] = modern["nonce"]
        legacy["receipt"] = enc(lr)
        modern["receipt"] = enc(mr)
        modern["intent"] = enc(dict(mr, status="SWAP_INTENT_NO_DEPLOYMENT"))
        modern["marker"] = enc(dict(op="swap", nonce=modern["nonce"], status="IN_FLIGHT"))
        pins, records = {}, []
        for generation, item in (("legacy", legacy), ("modern", modern)):
            evidence = item.pop("expected_evidence")
            receipt = json.loads(item["receipt"])
            pins[item["nonce"]] = dict(generation=generation, evidence=evidence,
                old_sha256=receipt["old_launcher_sha256"].removeprefix("sha256:"),
                new_sha256=receipt["new_launcher_sha256"].removeprefix("sha256:"))
            records.append(dict(item, generation=generation))
        args = dict(original=lr["old_launcher_sha256"], live=mr["new_launcher_sha256"][7:],
                    records=records[::-1], authenticated_pins=pins, verify_chain=c.verify_chain)
        self.assertEqual(j.reconcile_snapshot(**args), tuple(sorted(pins)))
        for patch in (
            {"records": records[:1]},
            {"records": [records[0], dict(records[1], marker=b"{}")]},
            {"authenticated_pins": {**pins, modern["nonce"]: dict(pins[modern["nonce"]], evidence={})}},
            {"original": "0"*64},
        ):
            with self.subTest(patch=patch), self.assertRaises(ValueError):
                j.reconcile_snapshot(**dict(args, **patch))


if __name__ == "__main__":
    unittest.main()
