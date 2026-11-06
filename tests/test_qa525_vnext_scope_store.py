"""Durable scope manifest tests; private temporary files only."""
import importlib.util
import json
import os
from pathlib import Path
import tempfile
import unittest

import test_qa525_vnext_journal as jt

ROOT = Path(__file__).resolve().parents[1]
PATH = ROOT / "scripts/qa/qa525-vnext-scope-store.py"
spec = importlib.util.spec_from_file_location("qa525_scope_store_test", PATH)
store = importlib.util.module_from_spec(spec)
spec.loader.exec_module(store)
store.journal = jt.mod


class ScopeStoreTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.fx = jt.JournalFixture(self.tmp.name)
        self.lease = jt.mod.begin(self.fx.root, self.fx.admission,
                                  self.fx.digest, jt.NOW)
        stem = "qa525-" + "1" * 16 + "-"
        self.units = [stem + "api.scope", stem + "web.scope"]

    def tearDown(self):
        self.lease.close()
        self.tmp.cleanup()

    @property
    def path(self):
        return self.fx.root / ("scopes-" + jt.NONCE + ".json")

    def test_manifest_is_durable_exact_and_both_units_are_accepted(self):
        record = store.initialize(self.lease, self.fx.digest, self.units)
        self.assertEqual(record["units"], self.units)
        self.assertEqual(oct(self.path.stat().st_mode & 0o777), "0o600")
        for unit in self.units:
            self.assertEqual(store.require_recorded(
                self.lease, self.fx.digest, unit), record["manifest_sha256"])
        self.assertFalse(record["may_apply"])
        self.assertFalse(record["may_install"])

    def test_duplicate_foreign_and_closed_lease_fail_closed(self):
        store.initialize(self.lease, self.fx.digest, self.units)
        with self.assertRaisesRegex(store.Stop, "^SCOPE_MANIFEST_EXISTS$"):
            store.initialize(self.lease, self.fx.digest, self.units)
        with self.assertRaisesRegex(store.Stop, "^SCOPE_UNIT_NOT_RECORDED$"):
            store.require_recorded(self.lease, self.fx.digest,
                                   "qa525-" + "2" * 16 + "-api.scope")
        self.lease.close()
        with self.assertRaisesRegex(store.Stop, "^SCOPE_LEASE_NOT_HELD$"):
            store.require_recorded(self.lease, self.fx.digest, self.units[0])

    def test_tamper_mode_symlink_and_hardlink_are_rejected(self):
        store.initialize(self.lease, self.fx.digest, self.units)
        data = json.loads(self.path.read_text())
        data["units"].reverse()
        self.path.write_text(json.dumps(data))
        self.path.chmod(0o600)
        with self.assertRaisesRegex(store.Stop, "^SCOPE_UNIT_NOT_RECORDED$"):
            store.require_recorded(self.lease, self.fx.digest, self.units[0])
        self.path.chmod(0o644)
        with self.assertRaisesRegex(store.Stop, "^SCOPE_MANIFEST_UNTRUSTED$"):
            store.require_recorded(self.lease, self.fx.digest, self.units[0])
        self.path.unlink()
        real = self.fx.root / "scope-real"
        real.write_text("{}")
        real.chmod(0o600)
        self.path.symlink_to(real)
        with self.assertRaisesRegex(store.Stop, "^SCOPE_MANIFEST_UNTRUSTED$"):
            store.require_recorded(self.lease, self.fx.digest, self.units[0])
        self.path.unlink()
        os.link(real, self.path)
        with self.assertRaisesRegex(store.Stop, "^SCOPE_MANIFEST_UNTRUSTED$"):
            store.require_recorded(self.lease, self.fx.digest, self.units[0])

    def test_source_has_no_runner_network_or_physical_lock_interface(self):
        raw = PATH.read_text(encoding="utf-8")
        for token in ("import subprocess", "import socket", "urllib", "requests",
                      "Popen(", "os.system(", "docker compose", "--review-and-run"):
            self.assertNotIn(token, raw)


if __name__ == "__main__":
    unittest.main()
