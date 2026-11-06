"""Hermetic tests: terminal writer + journal reading of finished flights."""
import copy
import importlib.util
import json
import os
from pathlib import Path
import signal
import tempfile
import unittest

import test_qa525_vnext_journal as jt
import test_qa525_vnext_verify as vt

BASE = Path(__file__).resolve().parents[1] / "scripts" / "qa"
spec = importlib.util.spec_from_file_location("qa525_vnext_terminal", BASE / "qa525-vnext-terminal.py")
term = importlib.util.module_from_spec(spec)
spec.loader.exec_module(term)
journal, verify = term.journal, term.verify
NOW = jt.NOW


def ok_result(kind="APPLIED"):
    if kind == "APPLIED":
        return verify.verify(vt.Fake(vt.observation()), vt.baseline(), "APPLIED", vt.target())
    return verify.verify(vt.Fake(vt.observation("ROLLED_BACK")), vt.baseline(), "ROLLED_BACK")


def args(kind="APPLIED", **change):
    base = {"kind": kind, "result": ok_result(kind),
            "baseline_sha256": verify.validate_baseline(vt.baseline()),
            "expected_target_sha256": verify.expected_target_sha256(
                vt.baseline(), kind, vt.target() if kind == "APPLIED" else None),
            "final_stage": term.KINDS[kind], "now": NOW}
    base.update(change)
    return base


class TerminalWriterTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.fx = jt.JournalFixture(self.tmp.name)

    def tearDown(self):
        self.tmp.cleanup()

    def test_applied_and_rolled_back_are_written_once_and_read_back(self):
        for kind in ("APPLIED", "ROLLED_BACK"):
            with self.subTest(kind), tempfile.TemporaryDirectory() as directory:
                fx = jt.JournalFixture(directory)
                with fx.begin() as lease:
                    self.assertEqual(term.finish(lease, **args(kind)), kind)
                    record = json.loads((fx.root / ("terminal-" + jt.NONCE + ".json")).read_text())
                    self.assertEqual(record["status"], kind)
                    self.assertEqual(record["authenticated_by"], "CHECKSUM_ONLY_NOT_A_SIGNATURE")
                    self.assertEqual(oct((fx.root / ("terminal-" + jt.NONCE + ".json")).stat().st_mode & 0o777), "0o600")
                    with self.assertRaisesRegex(term.Stop, "FLIGHT_NOT_APPLYING"):  # no second terminal
                        term.finish(lease, **args(kind))
                self.assertEqual(journal.inspect(fx.root, jt.NONCE), kind)
                # A finished flight is not a crash: reconcile must not write recovery.
                self.assertEqual(journal.reconcile_after_crash(fx.root, jt.NONCE, NOW), kind)
                self.assertFalse((fx.root / ("recovery-" + jt.NONCE + ".json")).exists())

    def test_nothing_is_written_for_a_bad_result_kind_stage_or_digest(self):
        bad_result = ok_result()
        bad_result = dict(bad_result, ok=False, code="HEALTH_FAILED")
        cases = {
            "failed result": args(result=bad_result),
            "edited result": args(result=dict(ok_result(), evidence_sha256="sha256:" + "0" * 64)),
            "wrong kind": args(kind="ROLLED_BACK", result=ok_result("APPLIED")),
            "wrong stage": args(final_stage="ROLLBACK_VERIFYING"),
            "other baseline": args(baseline_sha256="sha256:" + "9" * 64),
            "other target": args(expected_target_sha256="sha256:" + "8" * 64),
            "bad digest": args(baseline_sha256="nope"),
            "unknown kind": dict(args(), kind="DONE"),
            "naive clock": args(now=NOW.replace(tzinfo=None)),
        }
        for name, kwargs in cases.items():
            with self.subTest(name), tempfile.TemporaryDirectory() as directory:
                fx = jt.JournalFixture(directory)
                with fx.begin() as lease, self.assertRaises(term.Stop):
                    term.finish(lease, **kwargs)
                self.assertFalse((fx.root / ("terminal-" + jt.NONCE + ".json")).exists())
                self.assertEqual(journal.inspect(fx.root, jt.NONCE), "APPLYING")

    def test_lease_must_be_held_and_real(self):
        lease = self.fx.begin()
        lease.close()
        with self.assertRaisesRegex(term.Stop, "LEASE_NOT_HELD"):
            term.finish(lease, **args())
        for fake in (None, object(), "lease"):
            with self.subTest(fake=str(fake)), self.assertRaisesRegex(term.Stop, "LEASE_NOT_HELD"):
                term.finish(fake, **args())
        self.assertFalse(list(self.fx.root.glob("terminal-*")))

    def test_recovery_required_flights_cannot_be_declared_applied(self):
        self.fx.begin().close()
        self.assertEqual(journal.reconcile_after_crash(self.fx.root, jt.NONCE, NOW), "RECOVERY_REQUIRED")
        fd = journal._lock(self.fx.root)
        try:
            lease = journal.Lease(fd, self.fx.root, jt.NONCE)
            with self.assertRaisesRegex(term.Stop, "FLIGHT_NOT_APPLYING"):
                term.finish(lease, **args())
        finally:
            os.close(fd)
        self.assertFalse(list(self.fx.root.glob("terminal-*")))

    def test_contradictory_or_forged_terminal_files_fail_closed(self):
        with self.fx.begin() as lease:
            term.finish(lease, **args())
        path = self.fx.root / ("terminal-" + jt.NONCE + ".json")
        original = json.loads(path.read_text())
        for name, change in {"status": {"status": "RECOVERY_REQUIRED"}, "nonce": {"nonce": "x" * 30},
                             "admission": {"admission_sha256": "sha256:" + "1" * 64},
                             "schema": {"schema": "other"}}.items():
            with self.subTest(name):
                path.write_text(json.dumps(dict(original, **change)) + "\n")
                os.chmod(path, 0o600)
                with self.assertRaises(journal.Stop):
                    journal.inspect(self.fx.root, jt.NONCE)
        path.write_text(json.dumps(original) + "\n")
        os.chmod(path, 0o600)
        recovery = self.fx.root / ("recovery-" + jt.NONCE + ".json")
        recovery.write_text(json.dumps({"status": "RECOVERY_REQUIRED"}) + "\n")
        os.chmod(recovery, 0o600)
        with self.assertRaisesRegex(journal.Stop, "TERMINAL_AND_RECOVERY_CONFLICT"):
            journal.inspect(self.fx.root, jt.NONCE)
        recovery.unlink()
        # A terminal file with no flight behind it is rejected too.
        (self.fx.root / ("journal-" + jt.NONCE + ".json")).unlink()
        with self.assertRaises(journal.Stop):
            journal.inspect(self.fx.root, jt.NONCE)

    def test_terminal_files_block_the_next_flight_like_any_prior_nonce(self):
        with self.fx.begin() as lease:
            term.finish(lease, **args())
        other = jt.fake_admission()
        other["nonce"] = "another-qa525-vnext-nonce-20261105"
        self.fx.put(other)
        with self.assertRaisesRegex(jt.mod.Stop, "PRIOR_NONCE_UNRESOLVED"):  # fixture's own journal copy
            self.fx.begin()
        (self.fx.root / "terminal-short.json").write_text("{}")
        with self.assertRaisesRegex(jt.mod.Stop, "PRIOR_NONCE_UNRESOLVED|JOURNAL_UNKNOWN_RESIDUE"):
            self.fx.begin()

    def test_sigkill_after_the_terminal_link_leaves_a_consistent_finished_flight(self):
        for phase in ("after_journal_temp_fsync", "after_journal_link", "after_journal_dirsync"):
            with self.subTest(phase), tempfile.TemporaryDirectory() as directory:
                fx = jt.JournalFixture(directory)
                pid = os.fork()
                if pid == 0:
                    try:
                        lease = fx.begin()
                        original = journal._atomic_new

                        def kill_at(path, obj, checkpoint=None):
                            original(path, obj, lambda point: os.kill(os.getpid(), signal.SIGKILL)
                                     if point == phase else None)
                        journal._atomic_new = kill_at
                        term.finish(lease, **args())
                    except BaseException:
                        os._exit(91)
                    os._exit(92)
                _, status = os.waitpid(pid, 0)
                self.assertTrue(os.WIFSIGNALED(status), status)
                state = journal.inspect(fx.root, jt.NONCE)
                if phase == "after_journal_temp_fsync":  # killed before the link: still APPLYING
                    self.assertEqual(state, "APPLYING")
                    self.assertEqual(journal.reconcile_after_crash(fx.root, jt.NONCE, NOW), "RECOVERY_REQUIRED")
                else:  # the terminal record exists: finished, never rewritten as a crash
                    self.assertEqual(state, "APPLIED")
                    self.assertEqual(journal.reconcile_after_crash(fx.root, jt.NONCE, NOW), "APPLIED")
                    self.assertFalse((fx.root / ("recovery-" + jt.NONCE + ".json")).exists())

    def test_module_has_no_io_beyond_the_journal_and_no_host_actions(self):
        source = (BASE / "qa525-vnext-terminal.py").read_text(encoding="utf-8")
        for forbidden in ("import subprocess", "import socket", "docker", "Popen",
                          "os.system", "os.kill", "urllib", "requests", "open("):
            self.assertNotIn(forbidden, source.replace("Docker, network, process or host", ""), forbidden)


if __name__ == "__main__":
    unittest.main()
