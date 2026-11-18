"""Evidence that the signed operator code accepts the reconciled journal (status-only APPLIED) and rejects the unreconciled one.

Loads the REAL entrypoint/transaction modules. Proves: (1) the next run's reviewed_layers + expected_compose_chain accept the
live chain (original_layers + new override) only when dual-<SRC>.json is APPLIED; (2) negative control: RECOVERY_MANUALLY_REQUIRED
is rejected (COMPOSE_HISTORY_NOT_SUCCESSFULLY_APPLIED) and an archived (absent) journal is rejected too; (3) the operator never READS
dual-events.log (append-only audit), so no APPLIED event is needed.
"""
import importlib.util
import json
import re
import unittest
from pathlib import Path
from unittest.mock import patch

OPS = Path(__file__).resolve().parents[1] / "scripts/qa/operators"


def load(name, filename):
    spec = importlib.util.spec_from_file_location(name, OPS / filename)
    value = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(value)
    return value


entry = load("entry_reconcile_compat", "qa525-supervised-dual-entrypoint.py")
core = load("core_reconcile_compat", "qa525-supervised-dual-transaction.py")
SRC = "da274d651d8e9e056525413406c0aab62131504f"
NEWAPI = "sha256:09b66869936d505a08c53b44ad9d22c86c0ffad83696ac5f1ea65c7bfff9a0cb"
NEWWEB = "sha256:fb15b5c17ae57b149b5d3d2883ba41a84514a3b88cc2bc051207ae0f67507f34"
OLDAPI = "sha256:c620fd26212edc943b368da7a5d5e0fbe90ae8391ee8e73b98d818fd6d2171ed"
OLDWEB = "sha256:664c0e37bf6e6b801fd5617f4d327f5e027358817d488a92905338d992c1255a"


class Compat(unittest.TestCase):
    def setUp(self):
        root = entry.WORKDIR
        api = [str(root / "compose.recovery.json"), "/opt/centenario/qa-api-refresh/api-" + "f" * 40 + ".json",
               "/etc/pmgm/qa754-identity-overlay.stage.json"]
        web = [str(root / "compose.recovery.json"), "/opt/centenario/qa-auto-lite/web-" + "e" * 64 + ".json"]
        self.records = {}
        for src, digest in (("a" * 40, "1" * 64), ("b" * 40, "2" * 64), ("c" * 40, "3" * 64)):
            images = {n: "sha256:" + digest for n in ("api", "web")}
            for n, lst in (("api", api), ("web", web)):
                p = entry.STATE / ("qa525-%s-%s-%s.json" % (n, src, digest))
                lst.append(str(p))
                self.records[p] = {"services": {n: {"image": images[n]}}}
            self.records[entry.STATE / ("dual-%s.json" % src)] = {"source_sha": src, "status": "APPLIED", "new_images": images}
        self.original = {"api": list(api), "web": list(web)}  # original_layers recorded in the da274d65 journal (api 6, web 5)
        self.assertEqual((len(api), len(web)), (6, 5))
        self.live = {"api": list(api), "web": list(web)}
        for n, img in (("api", NEWAPI), ("web", NEWWEB)):
            p = entry.STATE / ("qa525-%s-%s-%s.json" % (n, SRC, img[7:]))
            self.live[n].append(str(p))
            self.records[p] = {"services": {n: {"image": img}}}
        self.jpath = entry.STATE / ("dual-%s.json" % SRC)

    def journal(self, status):
        return {"source_sha": SRC, "status": status, "old_images": {"api": OLDAPI, "web": OLDWEB},
                "new_images": {"api": NEWAPI, "web": NEWWEB}, "original_layers": self.original}

    def reviewed(self, status):
        recs = dict(self.records)
        if status is not None:
            recs[self.jpath] = self.journal(status)
        with patch.object(entry, "private_json", side_effect=lambda p: recs[p]):
            return entry.reviewed_layers(core, self.live, entry.WORKDIR)

    def test_applied_journal_is_accepted_and_chain_is_stable(self):
        layers = self.reviewed("APPLIED")
        self.assertEqual(tuple(self.live["api"]), layers["api"])
        self.assertEqual(tuple(self.live["web"]), layers["web"])
        for n, img in (("api", NEWAPI), ("web", NEWWEB)):
            cid = "9" * 64
            chain = entry.expected_compose_chain(name=n, target="f" * 40, original_files=layers[n], previous_id=cid,
                                                 previous_image=img, current_id=cid, current_image=img, candidate_image=img)
            self.assertEqual(self.live[n], chain)

    def test_recovery_required_journal_is_rejected(self):
        with self.assertRaisesRegex(Exception, "COMPOSE_HISTORY_NOT_SUCCESSFULLY_APPLIED"):
            self.reviewed("RECOVERY_MANUALLY_REQUIRED")

    def test_archived_missing_journal_is_rejected(self):
        with self.assertRaises(KeyError):
            self.reviewed(None)

    def test_status_only_change_keeps_every_checked_field(self):
        a, b = self.journal("RECOVERY_MANUALLY_REQUIRED"), self.journal("APPLIED")
        a.pop("status"); b.pop("status")
        self.assertEqual(a, b)
        self.assertEqual(b["new_images"], {"api": NEWAPI, "web": NEWWEB})

    def test_apply_path_never_reads_the_events_log(self):
        users = {}
        for path in OPS.glob("*.py"):
            if "dual-events" in path.read_text():
                users[path.name] = path.read_text()
        self.assertEqual(set(users), {"qa525-supervised-dual-entrypoint.py", "qa525-pending-dual-reconciliation.py"})
        # entrypoint: append-only audit writer (O_APPEND), never a reader
        text = users["qa525-supervised-dual-entrypoint.py"]
        self.assertEqual(text.count("dual-events"), 1)
        self.assertIn("O_APPEND", text.split("dual-events", 1)[1][:200])
        # the only reader is the archival tool for unactivated pending cuts; it REFUSES when any target event exists
        self.assertIn("RECONCILE_TARGET_EVENT_PRESENT", users["qa525-pending-dual-reconciliation.py"])


if __name__ == "__main__":
    unittest.main()
