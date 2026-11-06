"""Offline tests for the INACTIVE observer glue (injected rows/health, no host)."""
import copy
import importlib.util
from pathlib import Path
import unittest

import test_qa525_vnext_state_reader as sr

PATH = Path(__file__).resolve().parents[1] / "scripts/qa/qa525-vnext-observer.py"
spec = importlib.util.spec_from_file_location("qa525_observer_contract", PATH)
ob = importlib.util.module_from_spec(spec)
spec.loader.exec_module(ob)

NONCE = "qa525-vnext-observer-nonce-20261106"
ADMISSION = "sha256:" + "a" * 64
TARGET = {"images": {"api": "sha256:" + "5" * 64, "web": "sha256:" + "6" * 64},
          "layers": {"api": ["/w/base.yml", "/w/dual-new-api.json"],
                     "web": ["/w/base.yml", "/w/dual-new-web.json"]},
          "web_sha": sr.WEB_NEW}


class ObserverTests(unittest.TestCase):
    def setUp(self):
        self.state = {"rows": sr.rows()}
        self.reader = sr.make(self.state)
        self.plan = ob.capture_plan(self.reader, TARGET, nonce=NONCE,
                                    admission_sha256=ADMISSION)
        self.observe = ob.make_observe(self.reader, self.plan["baseline"], TARGET)

    def test_capture_plan_reads_the_baseline_once_before_any_change(self):
        self.assertEqual(self.plan["stage"], "APPLYING")
        self.assertIs(self.plan["may_apply"], False)
        self.assertEqual(self.plan["baseline"]["images"]["api"], "sha256:" + "1" * 64)

    def test_observe_checks_each_kind_against_this_flights_baseline_and_target(self):
        # target fully applied
        self.state["rows"] = sr.rows(new=True)
        reader = sr.make(self.state, sr.WEB_NEW)
        observe = ob.make_observe(reader, self.plan["baseline"], TARGET)
        self.assertTrue(observe("APPLIED")["ok"])
        # back to baseline
        self.state["rows"] = sr.rows()
        reader = sr.make(self.state, sr.WEB_OLD)
        observe = ob.make_observe(reader, self.plan["baseline"], TARGET)
        self.assertTrue(observe("ROLLED_BACK")["ok"])
        # baseline state is NOT an applied target
        self.assertFalse(observe("APPLIED")["ok"])

    def test_unhealthy_runtime_is_not_ok_and_unknown_kind_is_rejected(self):
        self.state["rows"] = sr.rows(new=True)
        observe = ob.make_observe(sr.make(self.state, sr.WEB_NEW, healthy=False),
                                  self.plan["baseline"], TARGET)
        self.assertFalse(observe("APPLIED")["ok"])
        for kind in ("INSTALLED", "", None, 3):
            with self.subTest(kind=kind), \
                    self.assertRaisesRegex(ob.Stop, "^OBSERVER_KIND_INVALID$"):
                observe(kind)

    def test_provider_failure_is_never_an_ok_observation(self):
        def broken():
            raise RuntimeError("secret socket path")
        bad = sr.reader.StateReader(broken, lambda: sr.WEB_OLD,
                                    lambda: {"ok": True, "web_sha": sr.WEB_OLD})
        observe = ob.make_observe(bad, self.plan["baseline"], TARGET)
        report = observe("API_SWITCHED")  # the verifier maps adapter errors itself
        self.assertIs(report["ok"], False)
        self.assertNotIn("secret", str(report))
        with self.assertRaisesRegex(ob.Stop, "^OBSERVER_BASELINE_FAILED$"):
            ob.capture_plan(bad, TARGET, nonce=NONCE, admission_sha256=ADMISSION)

    def test_invalid_reader_target_and_nonce_are_rejected(self):
        with self.assertRaisesRegex(ob.Stop, "^OBSERVER_READER_INVALID$"):
            ob.make_observe(object(), self.plan["baseline"], TARGET)
        bad_target = copy.deepcopy(TARGET)
        bad_target["layers"]["api"] = []
        with self.assertRaisesRegex(ob.Stop, "^OBSERVER_CONTRACT_INVALID$"):
            ob.make_observe(self.reader, self.plan["baseline"], bad_target)
        with self.assertRaisesRegex(ob.Stop, "^OBSERVER_PLAN_INVALID$"):
            ob.capture_plan(self.reader, TARGET, nonce="short", admission_sha256=ADMISSION)

    def test_module_has_no_io_or_execution_apis(self):
        source = PATH.read_text(encoding="utf-8")
        for needle in ("import subprocess", "import socket", "import os",
                       "import urllib", "import requests", "os.system(",
                       "Popen(", "open("):
            self.assertNotIn(needle, source)


if __name__ == "__main__":
    unittest.main()
