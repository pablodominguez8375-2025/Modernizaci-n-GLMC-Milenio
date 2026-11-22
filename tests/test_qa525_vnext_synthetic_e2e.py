"""Synthetic E2E suite vs an in-memory fake of the #694 API (rules copied from InitialLoadApplicabilityPolicy)."""
import csv
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import unittest

HERE = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location("e2e", HERE.parent / "scripts" / "qa" / "qa525-vnext-synthetic-e2e.py")
e2e = importlib.util.module_from_spec(spec)
spec.loader.exec_module(e2e)
SHA = "a" * 40


def rows(text):
    return list(csv.DictReader(io.StringIO(text)))


class FakeApi:
    def __init__(self, **bug):
        self.bug = bug
        self.workshops, self.people, self.applied = {}, {}, set()
        self.window_open = True
        self.calls = []

    def fingerprint(self, files):
        return hashlib.sha256(json.dumps(files, sort_keys=True).encode()).hexdigest()

    def blockers(self, files):
        t, h = rows(files["Talleres.csv"]), rows(files["Hermanos.csv"])
        out = []
        if len(t) != 1 and not self.bug.get("no_one_workshop"):
            out.append("ONE_WORKSHOP_PER_BATCH_REQUIRED")
        limit = 31 if self.bug.get("limit31") else 30
        if len(h) > limit:
            out.append("MEMBER_BATCH_LIMIT_30")
        if t and any(x["workshopCode"] != t[0]["workshopCode"] for x in h) and not self.bug.get("no_cross"):
            out.append("CROSS_WORKSHOP_BATCH")
        return out

    def __call__(self, method, path, body=None):
        self.calls.append((method, path))
        if path == "/health/ready":
            return 200, None
        if path == "/qa-version.json":
            return 200, {"sha": SHA}
        if path == e2e.BASE + "/contract":
            return 200, {"revision": "INITIAL-LOAD-CSV-v1", "files": [{"name": n} for n in e2e.SHEETS]}
        if path == e2e.BASE + "/template-csv":
            return 200, b"PK\x03\x04"
        if path == e2e.REGISTRY:
            return 200, {"items": [{"name": n} for n in self.workshops.values()]}
        if path == e2e.BASE + "/preview":
            b = self.blockers(body)
            writes = 1 if self.bug.get("preview_writes") else 0
            return 200, {"valid": True, "applicable": not b, "writesPerformed": writes,
                         "applyBlockers": [{"code": c} for c in b],
                         "receipt": ("R" + self.fingerprint(body)) if not b else None}
        if path == e2e.BASE + "/apply":
            files, receipt = body["files"], body["receipt"]
            if not self.window_open:
                return 409, {"code": "WINDOW_NOT_OPEN"}
            if receipt != "R" + self.fingerprint(files) or self.blockers(files):
                return 422, {"code": "BATCH_NOT_APPLICABLE"}
            fp = self.fingerprint(files)
            if fp in self.applied:
                return 200, {"applied": False, "alreadyApplied": True, "counts": {"membersCreated": 0, "workshopsCreated": 0}}
            t, h = rows(files["Talleres.csv"])[0], rows(files["Hermanos.csv"])
            for x in h:
                prev = self.people.get(x["identityNumber"])
                if prev is not None and prev != (x["firstNames"], x["lastNames"]) and not self.bug.get("no_conflict"):
                    return 422, {"code": "MEMBER_CONFLICT_EXISTING_PERSON"}
            created = t["workshopCode"] not in self.workshops
            self.workshops[t["workshopCode"]] = t["name"]
            for x in h:
                self.people[x["identityNumber"]] = (x["firstNames"], x["lastNames"])
            self.applied.add(fp)
            return 200, {"applied": True, "alreadyApplied": False,
                         "counts": {"workshopsCreated": 1 if created else 0, "membersCreated": len(h)}}
        return 404, None


def clock_factory():
    t = [0.0]

    def clock():
        t[0] += 0.001
        return t[0]
    return clock


class GeneratorTests(unittest.TestCase):
    def test_reference_volume_is_deterministic_and_synthetic(self):
        lots, manifest = e2e.generate()
        self.assertEqual((manifest["lotes"], manifest["talleres"], manifest["hermanos"], manifest["talleres_con_2_lotes"]),
                         (74, 70, 1008, 4))
        self.assertEqual(manifest, e2e.generate()[1])
        ruts, refs = set(), set()
        for lot in lots:
            self.assertLessEqual(lot["members"], 30)
            for row in rows(lot["files"]["Hermanos.csv"]):
                self.assertTrue(row["email"].endswith("@example.org"))
                self.assertTrue(row["personRef"].startswith("QA-SINT-"))
                self.assertGreaterEqual(int(row["identityNumber"].split("-")[0].replace(".", "")), 30_000_000)
                ruts.add(row["identityNumber"])
                refs.add(row["personRef"])
        self.assertEqual(len(ruts), 1008)
        self.assertEqual(len(refs), 1008)


class SmokeTests(unittest.TestCase):
    def test_all_smoke_checks_pass_against_a_correct_api(self):
        api = FakeApi()
        res = e2e.smoke(api, SHA, unauth=lambda m, p: (401, None))
        self.assertTrue(all(res.values()), res)
        self.assertEqual(len(res), 10)
        self.assertFalse(any(m == "POST" and p.endswith("/apply") for m, p in api.calls))  # never writes
        self.assertEqual(api.workshops, {})

    def test_each_bug_is_detected(self):
        for bug, check in (({"preview_writes": True}, "preview_valid_no_writes"),
                           ({"limit31": True}, "preview_blocks_over30"),
                           ({"no_one_workshop": True}, "preview_blocks_two_workshops"),
                           ({"no_cross": True}, "preview_blocks_cross_workshop")):
            res = e2e.smoke(FakeApi(**bug), SHA)
            self.assertFalse(res[check], bug)
        self.assertFalse(e2e.smoke(FakeApi(), "b" * 40)["qa_version_matches"])
        self.assertFalse(e2e.smoke(FakeApi(), SHA, unauth=lambda m, p: (200, {}))["unauthenticated_denied"])

    def test_http_errors_make_checks_false_and_never_raise(self):
        def boom(*a, **k):
            raise RuntimeError("secret detail")
        res = e2e.smoke(boom, SHA)
        self.assertFalse(any(res.values()))
        self.assertNotIn("secret", json.dumps(res))

    def test_uat_checks_run_smoke_once_and_are_callables(self):
        api = FakeApi()
        checks = e2e.uat_checks(api, SHA, unauth=lambda m, p: (403, None))
        self.assertTrue(all(callable(c) for c in checks))
        self.assertTrue(all(c() is True for c in checks))
        previews = sum(1 for m, p in api.calls if p.endswith("/preview"))
        self.assertTrue(all(c() is True for c in checks))
        self.assertEqual(previews, sum(1 for m, p in api.calls if p.endswith("/preview")))  # cached, no rerun
        bad = e2e.uat_checks(FakeApi(limit31=True), SHA)
        self.assertFalse(all(c() is True for c in bad))


class RehearsalTests(unittest.TestCase):
    def test_full_volume_rehearsal_passes(self):
        lots, manifest = e2e.generate()
        api = FakeApi()
        checks, stats = e2e.rehearsal(api, lots, clock_factory())
        self.assertTrue(all(checks.values()), checks)
        self.assertEqual(stats["applied"], 74)
        self.assertEqual(len(api.workshops), 70)
        self.assertEqual(len(api.people), 1008)

    def test_conflict_and_idempotency_regressions_are_caught(self):
        lots, _ = e2e.generate(talleres=3)
        self.assertFalse(e2e.rehearsal(FakeApi(no_conflict=True), lots, clock_factory())[0]["conflict_rolls_back"])
        closed = FakeApi()
        closed.window_open = False
        checks, stats = e2e.rehearsal(closed, lots, clock_factory())
        self.assertFalse(checks["first_apply"])
        self.assertEqual(closed.workshops, {})

    def test_second_lot_of_same_workshop_reuses_it(self):
        lots, _ = e2e.generate()
        checks, _ = e2e.rehearsal(FakeApi(), lots, clock_factory())
        self.assertTrue(checks["second_lot_reuses_workshop"])


if __name__ == "__main__":
    unittest.main()
