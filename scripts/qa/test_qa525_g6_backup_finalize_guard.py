#!/usr/bin/env python3
"""Pure negative tests, zero Docker, sudo or systemd access."""
import importlib.util
from pathlib import Path
import unittest

p = Path(__file__).with_name("qa525-g6-backup-finalize-guard.py")
spec = importlib.util.spec_from_file_location("qa525_guard", p)
mod = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mod)

def good():
    return {
        "minio_running": True,
        "minio_health": "healthy",
        "watchdog_active_state": "inactive",
        "watchdog_sub_state": "dead",
        "bootstrap_exit": 0,
        "go_archived": True,
        "backup_markers_confirmed": True,
        "archive_name": "centenario-qa-g6-v2-CIFRADO-20261112T235959Z.tar.gz",
        "archive_sha256": "a" * 64,
        "manifest_sha256": "b" * 64,
        "snapshot_id": "c" * 64,
    }

class GuardTests(unittest.TestCase):
    def test_positive(self):
        self.assertEqual(mod.final_status(good()), "PASS_BACKUP_FINAL_STATE")
    def test_bad_type(self):
        self.assertEqual(mod.final_status(None), "STOP_EVIDENCE_INVALID")
    def test_missing_field_denies(self):
        for key in good():
            with self.subTest(key=key):
                e = good()
                del e[key]
                self.assertNotEqual(mod.final_status(e), "PASS_BACKUP_FINAL_STATE")
    def test_watchdog_still_active(self):
        for state in ("active", "failed", "unknown"):
            with self.subTest(state=state):
                e=good();e["watchdog_active_state"]=state
                self.assertEqual(mod.final_status(e), "STOP_WATCHDOG_NOT_INACTIVE")
    def test_timer_substate_not_settled(self):
        e=good();e["watchdog_sub_state"]="waiting"
        self.assertEqual(mod.final_status(e), "STOP_WATCHDOG_NOT_SETTLED")
    def test_running_but_unhealthy(self):
        for state in ("unhealthy","starting","no-healthcheck","unknown"):
            with self.subTest(state=state):
                e=good();e["minio_health"]=state
                self.assertEqual(mod.final_status(e), "STOP_MINIO_NOT_HEALTHY")
    def test_sudo_failure_means_no_proven_readback(self):
        e=good();e["watchdog_active_state"]=None
        self.assertNotEqual(mod.final_status(e), "PASS_BACKUP_FINAL_STATE")
    def test_boolean_strings_are_rejected(self):
        for field in ("minio_running","go_archived","backup_markers_confirmed"):
            e=good();e[field]="true"
            self.assertNotEqual(mod.final_status(e), "PASS_BACKUP_FINAL_STATE")
    def test_bootstrap_exit_bool_does_not_pass(self):
        e=good();e["bootstrap_exit"]=False
        self.assertEqual(mod.final_status(e), "STOP_BOOTSTRAP_FAILED")
    def test_invalid_evidence(self):
        for k,v in (("archive_sha256","a"*63),("snapshot_id","oops"),("archive_name","invalid")):
            e=good();e[k]=v
            self.assertNotEqual(mod.final_status(e), "PASS_BACKUP_FINAL_STATE")

if __name__ == "__main__":
    unittest.main()
