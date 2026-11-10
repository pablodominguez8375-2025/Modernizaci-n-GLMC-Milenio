"""Offline tests for the launcher child (temporary paths only; never touches a real launcher)."""
import hashlib
import importlib.util
import json
import os
import pathlib
import tempfile
import unittest
from unittest import mock

SRC = pathlib.Path(__file__).resolve().parents[1] / "scripts/qa/qa525-vnext-root-launcher-child.py"
spec = importlib.util.spec_from_file_location("launcher_child", SRC)
child = importlib.util.module_from_spec(spec)
spec.loader.exec_module(child)

OLD, NEW = b"#!/bin/sh\necho old\n", b"#!/bin/sh\necho new\n"
NONCE = "a1" * 32


def sha(b):
    return "sha256:" + hashlib.sha256(b).hexdigest()


class ChildTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        root = pathlib.Path(self.tmp.name)
        self.uid = os.getuid()
        (root / "sbin").mkdir(mode=0o755)
        (root / "state").mkdir()
        os.chmod(root / "state", 0o700)
        self.root = str(root)
        self.launcher = str(root / "sbin/pmgm-qa-update")
        self.state = str(root / "state")
        fd = os.open(self.launcher, os.O_WRONLY | os.O_CREAT, 0o700)
        os.write(fd, OLD)
        os.close(fd)
        os.chmod(self.launcher, 0o700)
        os.chmod(root / "sbin", 0o755)

    def swap(self, **kw):
        args = dict(launcher=self.launcher, state=self.state, nonce=NONCE, old_sha256=sha(OLD),
                    expected_new_sha256=sha(NEW), new_bytes=NEW, uid=self.uid, anchor=self.root)
        args.update(kw)
        return child.swap(**args)

    def code(self, fn, *a, **kw):
        with self.assertRaises(child.Stop) as cm:
            fn(*a, **kw)
        return str(cm.exception)

    def read(self, p):
        return pathlib.Path(p).read_bytes()

    def test_swap_commits_with_backup_receipt_and_archived_intent(self):
        receipt = self.swap()
        self.assertEqual(receipt["status"], "LAUNCHER_ATOMIC_SWAP_COMMITTED")
        self.assertEqual(self.read(self.launcher), NEW)
        names = sorted(os.listdir(self.state))
        self.assertEqual(names, ["inflight-%s.done" % NONCE, "intent-%s.superseded" % NONCE, "old-%s.bin" % NONCE,
                                 "receipt-%s.json" % NONCE])
        self.assertEqual(self.read(os.path.join(self.state, "old-%s.bin" % NONCE)), OLD)
        self.assertEqual(os.stat(self.launcher).st_mode & 0o777, 0o700)
        self.assertEqual(os.listdir(os.path.dirname(self.launcher)), ["pmgm-qa-update"])

    def test_old_drift_stops_before_any_state(self):
        self.assertEqual(self.code(self.swap, old_sha256=sha(b"other")), "CHILD_OLD_LAUNCHER_DRIFT")
        self.assertEqual(os.listdir(self.state), [])
        self.assertEqual(self.read(self.launcher), OLD)

    def test_replay_and_partial_state_stop(self):
        self.swap()
        self.assertEqual(self.code(self.swap, old_sha256=sha(NEW), expected_new_sha256=sha(OLD), new_bytes=OLD), "CHILD_REPLAY_OR_PARTIAL_STATE")
        open(os.path.join(self.state, "intent-%s.json" % ("b2" * 32)), "w").close()
        self.assertEqual(self.code(self.swap, nonce="b2" * 32, old_sha256=sha(NEW), expected_new_sha256=sha(OLD), new_bytes=OLD),
                         "CHILD_REPLAY_OR_PARTIAL_STATE")

    def test_argument_shapes_are_closed(self):
        for kw in ({"nonce": "xyz"}, {"old_sha256": "abc"}, {"new_bytes": "text"}, {"new_bytes": b""},
                   {"launcher": "relative"}, {"state": "/a/../b"}, {"uid": True}):
            self.assertEqual(self.code(self.swap, **kw), "CHILD_ARGS_INVALID", kw)
        self.assertEqual(self.code(self.swap, new_bytes=OLD, expected_new_sha256=sha(OLD)), "CHILD_NEW_EQUALS_OLD")

    def test_state_dir_must_be_private(self):
        os.chmod(self.state, 0o755)
        self.assertEqual(self.code(self.swap), "CHILD_STATE_UNSAFE")
        os.chmod(self.state, 0o700)
        os.chmod(self.launcher, 0o755)
        self.assertEqual(self.code(self.swap), "CHILD_OLD_LAUNCHER_DRIFT")

    def test_symlinked_launcher_is_refused(self):
        real = self.launcher + ".real"
        os.rename(self.launcher, real)
        os.symlink(real, self.launcher)
        self.assertEqual(self.code(self.swap), "CHILD_LAUNCHER_UNSAFE")

    def test_hardlinked_launcher_is_refused(self):
        os.link(self.launcher, self.launcher + ".twin")
        self.assertEqual(self.code(self.swap), "CHILD_LAUNCHER_UNSAFE")

    def test_launcher_changed_after_staging_is_not_overwritten(self):
        real = child._read
        calls = {"n": 0}

        def racing(dfd, name, uid, code):
            if name == "pmgm-qa-update":
                calls["n"] += 1
                if calls["n"] == 2:  # the re-check right before os.replace
                    pathlib.Path(self.launcher).write_bytes(b"#!/bin/sh\necho intruder\n")
            return real(dfd, name, uid, code)
        with mock.patch.object(child, "_read", racing):
            self.assertEqual(self.code(self.swap), "CHILD_LAUNCHER_DRIFT_BEFORE_SWAP")
        self.assertEqual(self.read(self.launcher), b"#!/bin/sh\necho intruder\n")
        self.assertTrue(os.path.exists(os.path.join(self.state, "intent-%s.json" % NONCE)))  # no auto retry

    def test_swap_that_does_not_land_is_reverted_from_backup(self):
        real = os.replace

        def bad_replace(src, dst, **kw):
            if dst == "pmgm-qa-update" and src.endswith(".tmp"):
                pathlib.Path(self.launcher).with_name(src).write_bytes(b"#!/bin/sh\necho corrupted\n")
            return real(src, dst, **kw)
        with mock.patch.object(child.os, "replace", bad_replace):
            self.assertEqual(self.code(self.swap), "CHILD_SWAP_REVERTED")
        self.assertEqual(self.read(self.launcher), OLD)
        self.assertFalse(os.path.exists(os.path.join(self.state, "receipt-%s.json" % NONCE)))

    def test_unprovable_restore_reports_failure_not_success(self):
        real = os.replace
        seen = {"n": 0}

        def bad_replace(src, dst, **kw):
            seen["n"] += 1
            if seen["n"] == 1:
                pathlib.Path(self.launcher).with_name(src).write_bytes(b"#!/bin/sh\necho corrupted\n")
            if seen["n"] == 2:
                raise OSError("disk")
            return real(src, dst, **kw)
        with mock.patch.object(child.os, "replace", bad_replace):
            self.assertEqual(self.code(self.swap), "CHILD_RESTORE_UNPROVEN")

    def test_rollback_restores_old_and_is_single_use(self):
        self.swap()
        out = child.rollback(launcher=self.launcher, state=self.state, nonce=NONCE, uid=self.uid, anchor=self.root)
        self.assertEqual(out["status"], "LAUNCHER_ROLLED_BACK")
        self.assertEqual(self.read(self.launcher), OLD)
        self.assertEqual(self.code(child.rollback, launcher=self.launcher, state=self.state, nonce=NONCE,
                                   uid=self.uid, anchor=self.root), "CHILD_NEW_LAUNCHER_DRIFT")

    def test_rollback_refuses_without_receipt_or_on_drift(self):
        self.assertEqual(self.code(child.rollback, launcher=self.launcher, state=self.state, nonce=NONCE,
                                   uid=self.uid, anchor=self.root), "CHILD_RECEIPT_MISSING")
        self.swap()
        pathlib.Path(self.launcher).write_bytes(b"#!/bin/sh\necho other\n")
        os.chmod(self.launcher, 0o700)
        self.assertEqual(self.code(child.rollback, launcher=self.launcher, state=self.state, nonce=NONCE,
                                   uid=self.uid, anchor=self.root), "CHILD_NEW_LAUNCHER_DRIFT")

    def test_rollback_refuses_tampered_backup(self):
        self.swap()
        b = os.path.join(self.state, "old-%s.bin" % NONCE)
        os.chmod(b, 0o700)
        pathlib.Path(b).write_bytes(b"#!/bin/sh\necho evil\n")
        self.assertEqual(self.code(child.rollback, launcher=self.launcher, state=self.state, nonce=NONCE,
                                   uid=self.uid, anchor=self.root), "CHILD_BACKUP_UNVERIFIED")
        self.assertEqual(self.read(self.launcher), NEW)

    def test_receipt_is_canonical_json(self):
        self.swap()
        doc = json.loads(self.read(os.path.join(self.state, "receipt-%s.json" % NONCE)))
        self.assertEqual(doc["old_launcher_sha256"], sha(OLD))
        self.assertEqual(doc["new_launcher_sha256"], sha(NEW))

    def test_candidate_digest_must_match_the_authenticated_pin_before_any_mutation(self):
        other = b"#!/bin/sh\necho well-formed but different\n"
        self.assertEqual(self.code(self.swap, new_bytes=other), "CHILD_CANDIDATE_MISMATCH")
        self.assertEqual(self.code(self.swap, expected_new_sha256="sha256:" + "0" * 64), "CHILD_CANDIDATE_MISMATCH")
        self.assertEqual(self.code(self.swap, expected_new_sha256=None), "CHILD_ARGS_INVALID")
        self.assertEqual(os.listdir(self.state), [])
        self.assertEqual(sorted(os.listdir(os.path.dirname(self.launcher))), ["pmgm-qa-update"])
        self.assertEqual(self.read(self.launcher), OLD)

    def test_group_writable_launcher_directory_is_refused(self):
        os.chmod(os.path.dirname(self.launcher), 0o775)
        self.assertEqual(self.code(self.swap), "CHILD_STATE_UNSAFE")
        self.assertEqual(os.listdir(self.state), [])
        self.assertEqual(self.read(self.launcher), OLD)

    def test_group_writable_ancestor_is_refused(self):
        os.chmod(self.root, 0o775)
        self.assertEqual(self.code(self.swap), "CHILD_STATE_UNSAFE")
        os.chmod(self.root, 0o700)

    def test_symlinked_directories_are_refused(self):
        sbin = os.path.dirname(self.launcher)
        os.rename(sbin, sbin + ".real")
        os.symlink(sbin + ".real", sbin)
        self.assertEqual(self.code(self.swap), "CHILD_STATE_UNSAFE")
        os.unlink(sbin)
        os.rename(sbin + ".real", sbin)
        os.rename(self.state, self.state + ".real")
        os.symlink(self.state + ".real", self.state)
        self.assertEqual(self.code(self.swap), "CHILD_STATE_UNSAFE")

    def test_path_outside_anchor_is_refused(self):
        self.assertEqual(self.code(self.swap, anchor=os.path.join(self.root, "state")), "CHILD_STATE_UNSAFE")

    def test_directory_swapped_after_open_does_not_redirect_the_replace(self):
        """The operations go through directory FDs: re-binding the path afterwards must not move the target."""
        sbin = os.path.dirname(self.launcher)
        decoy = sbin + ".decoy"
        os.mkdir(decoy, 0o755)
        pathlib.Path(decoy, "pmgm-qa-update").write_bytes(OLD)
        os.chmod(os.path.join(decoy, "pmgm-qa-update"), 0o700)
        real = child._write_once
        done = {"n": 0}

        def rebind(dfd, name, data, mode):
            real(dfd, name, data, mode)
            if name.endswith(".tmp") and not done["n"]:
                done["n"] = 1
                os.rename(sbin, sbin + ".moved")
                os.rename(decoy, sbin)
        with mock.patch.object(child, "_write_once", rebind):
            self.swap()
        self.assertEqual(self.read(os.path.join(sbin + ".moved", "pmgm-qa-update")), NEW)
        self.assertEqual(self.read(os.path.join(sbin, "pmgm-qa-update")), OLD)

    def test_crash_after_intent_of_A_blocks_any_other_nonce(self):
        B = "b2" * 32
        real = child._write_once

        def crash(dfd, name, data, mode):
            real(dfd, name, data, mode)
            if name == "old-%s.bin" % NONCE:
                raise KeyboardInterrupt  # SIGKILL stand-in: nothing after this line runs
        with mock.patch.object(child, "_write_once", crash):
            with self.assertRaises(KeyboardInterrupt):
                self.swap()
        before = sorted(os.listdir(self.state))
        self.assertIn("intent-%s.json" % NONCE, before)
        self.assertEqual(self.code(self.swap, nonce=B), "CHILD_RECOVERY_REQUIRED")
        self.assertEqual(self.code(self.swap, nonce=NONCE), "CHILD_REPLAY_OR_PARTIAL_STATE")
        self.assertEqual(sorted(os.listdir(self.state)), before)  # no new writes at all
        self.assertEqual(self.read(self.launcher), OLD)

    def test_stray_unarchived_intent_blocks_even_without_marker(self):
        open(os.path.join(self.state, "intent-%s.json" % ("c3" * 32)), "w").close()
        self.assertEqual(self.code(self.swap), "CHILD_RECOVERY_REQUIRED")
        self.assertEqual(len(os.listdir(self.state)), 1)

    def test_reverted_swap_keeps_the_marker_until_supervised_reconciliation(self):
        real = os.replace

        def bad_replace(src, dst, **kw):
            if dst == "pmgm-qa-update" and src.endswith(".tmp"):
                pathlib.Path(self.launcher).with_name(src).write_bytes(b"#!/bin/sh\necho corrupted\n")
            return real(src, dst, **kw)
        with mock.patch.object(child.os, "replace", bad_replace):
            self.assertEqual(self.code(self.swap), "CHILD_SWAP_REVERTED")
        self.assertEqual(self.code(self.swap, nonce="d4" * 32), "CHILD_RECOVERY_REQUIRED")

    def test_crash_during_rollback_blocks_everything_until_reconciled(self):
        self.swap()
        real = child._restore

        def crash(*a, **kw):
            raise KeyboardInterrupt
        with mock.patch.object(child, "_restore", crash):
            with self.assertRaises(KeyboardInterrupt):
                child.rollback(launcher=self.launcher, state=self.state, nonce=NONCE, uid=self.uid, anchor=self.root)
        names = os.listdir(self.state)
        self.assertIn("rollback-intent-%s.json" % NONCE, names)
        self.assertIn("inflight.json", names)
        self.assertEqual(self.code(child.rollback, launcher=self.launcher, state=self.state, nonce=NONCE,
                                   uid=self.uid, anchor=self.root), "CHILD_RECOVERY_REQUIRED")
        n2 = "e5" * 32
        self.assertEqual(self.code(self.swap, nonce=n2, old_sha256=sha(NEW), expected_new_sha256=sha(OLD),
                                   new_bytes=OLD), "CHILD_RECOVERY_REQUIRED")
        self.assertEqual(self.read(self.launcher), NEW)

    def test_rollback_leaves_no_pending_marker_on_success(self):
        self.swap()
        child.rollback(launcher=self.launcher, state=self.state, nonce=NONCE, uid=self.uid, anchor=self.root)
        names = os.listdir(self.state)
        self.assertNotIn("inflight.json", names)
        self.assertIn("rollback-intent-%s.superseded" % NONCE, names)

    def test_act_is_a_closed_stub(self):
        self.assertEqual(self.code(child.act, 3), "CHILD_NOT_WIRED")
        self.assertEqual(self.read(self.launcher), OLD)


if __name__ == "__main__":
    unittest.main()
