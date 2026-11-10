"""Offline tests for the launcher child (temporary paths only; never touches a real launcher)."""
import hashlib
import importlib.util
import io
import json
import os
import pathlib
import stat
import tempfile
import zipfile
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
        self.assertEqual(names, ["inflight-swap-%s.done" % NONCE, "intent-%s.superseded" % NONCE, "old-%s.bin" % NONCE,
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


TPL = importlib.util.spec_from_file_location(
    "pinned_template", SRC.parent / "operators/qa525-launcher-pinned-template.py")
template = importlib.util.module_from_spec(TPL)
TPL.loader.exec_module(template)
SRC40 = "c3" * 20
OPERATOR_BYTES = b"print('qa525 operator')\n"
OPERATOR_REL = "scripts/qa/operators/qa525-update.py"


def hx(b):
    return hashlib.sha256(b).hexdigest()


def build_release(files=None, manifest=None, info=None, extra=None, source_sha=SRC40, root_name=None):
    """Signed app ZIP's real layout: one Proyecto-Centenario-QA-srv01-<sha12>/ folder."""
    files = {OPERATOR_REL: OPERATOR_BYTES, "scripts/qa/other.py": b"x = 1\n"} if files is None else files
    info = ("SOURCE_SHA=%s\nSTORAGE=x\n" % source_sha).encode() if info is None else info
    members = dict(files, **{"BUILD-INFO.txt": info})
    if manifest is None:
        manifest = "".join("%s  ./%s\n" % (hx(v), k) for k, v in sorted(members.items())).encode()
    root = "Proyecto-Centenario-QA-srv01-" + source_sha[:12] if root_name is None else root_name
    prefix = root + "/" if root else ""
    out = io.BytesIO()
    with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as z:
        if prefix:
            z.writestr(prefix, b"")
        for k, v in sorted(members.items()):
            z.writestr(prefix + k, v)
        z.writestr(prefix + "MANIFEST.sha256", manifest)
        for name, data in (extra or {}).items():
            if isinstance(data, zipfile.ZipInfo):
                entry = zipfile.ZipInfo(prefix + data.filename)
                entry.external_attr = data.external_attr
                entry.compress_type = data.compress_type
                z.writestr(entry, b"target")
            else:
                z.writestr(prefix + name, data)
    return out.getvalue()

class InstallTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        root = pathlib.Path(self.tmp.name)
        self.uid, self.root = os.getuid(), str(root)
        for d, mode in (("sbin", 0o755), ("state", 0o700), ("store", 0o700)):
            (root / d).mkdir()
            os.chmod(root / d, mode)
        self.launcher, self.state, self.store = (str(root / "sbin/pmgm-qa-update"), str(root / "state"),
                                                 str(root / "store"))
        pathlib.Path(self.launcher).write_bytes(OLD)
        os.chmod(self.launcher, 0o700)
        self.fds = []
        self.addCleanup(lambda: [os.close(f) for f in self.fds if f >= 0])

    def memfd(self, data):
        fd = os.memfd_create("zip")
        os.write(fd, data)
        self.fds.append(fd)
        return fd

    def claims(self, data, **over):
        base = {"purpose": "LAUNCHER_ONLY", "nonce": NONCE, "app_source_sha": SRC40,
                "app_archive_sha256": sha(data)}
        base.update(over)
        return base

    def pins(self, **over):
        base = {"release_asset_sha256": sha(b"asset"), "old_launcher_sha256": hx(OLD),
                "candidate_operator_sha256": hx(OPERATOR_BYTES)}
        base.update(over)
        return base

    def run_install(self, data=None, claims=None, pins=None):
        data = build_release() if data is None else data
        return child.install(fd=self.memfd(data), claims=claims or self.claims(data), launcher_pins=pins or self.pins(),
                             uid=self.uid, anchor=self.root, launcher=self.launcher, store=self.store,
                             state=self.state)

    def code(self, *a, **kw):
        with self.assertRaises(child.Stop) as cm:
            self.run_install(*a, **kw)
        return str(cm.exception)

    def untouched(self):
        self.assertEqual(pathlib.Path(self.launcher).read_bytes(), OLD)

    def clean(self):
        self.assertEqual(os.listdir(self.store), [])
        self.assertEqual(os.listdir(self.state), [])
        self.untouched()

    def test_render_is_byte_identical_to_the_operators_template(self):
        self.assertEqual(child.render(SRC40), template.render(SRC40))
        self.assertEqual(template.inspect_template(SRC40, child.render(SRC40))["launcher_sha256"],
                         hx(child.render(SRC40)))

    def test_retains_the_release_then_swaps_the_shim(self):
        data = build_release()
        receipt = self.run_install(data)
        shim = child.render(SRC40, self.store)
        self.assertEqual(pathlib.Path(self.launcher).read_bytes(), shim)
        tree = pathlib.Path(self.store, SRC40)
        self.assertEqual((tree / OPERATOR_REL).read_bytes(), OPERATOR_BYTES)
        self.assertEqual(sorted(p.relative_to(tree).as_posix() for p in tree.rglob("*") if p.is_file()),
                         ["BUILD-INFO.txt", "MANIFEST.sha256", OPERATOR_REL, "scripts/qa/other.py"])
        self.assertEqual(stat.S_IMODE((tree / OPERATOR_REL).stat().st_mode), 0o644)
        self.assertEqual(stat.S_IMODE(tree.stat().st_mode), 0o755)
        self.assertEqual(os.listdir(self.store), [SRC40])
        ev = receipt["evidence"]
        self.assertEqual(ev["shim_sha256"], sha(shim))
        self.assertEqual(ev["candidate_operator_sha256"], "sha256:" + hx(OPERATOR_BYTES))
        self.assertEqual(ev["app_archive_sha256"], sha(data))
        self.assertEqual(ev["app_source_sha"], SRC40)
        self.assertIn("inflight-swap-%s.done" % NONCE, os.listdir(self.state))
        self.assertNotIn("inflight.json", os.listdir(self.state))

    def test_published_zip_has_exact_source_bound_root(self):
        data = build_release()
        expected_root = "Proyecto-Centenario-QA-srv01-" + SRC40[:12]
        with zipfile.ZipFile(io.BytesIO(data)) as zf:
            self.assertIn(expected_root + "/", zf.namelist())
            self.assertEqual(set(child._scan_archive(zf, SRC40)),
                             {"MANIFEST.sha256", "BUILD-INFO.txt", OPERATOR_REL, "scripts/qa/other.py"})

    def test_rejects_wrong_root_or_rootless_package_before_writes(self):
        for root_name in ("", "Proyecto-Centenario-QA-srv01-" + "f" * 12, "stranger"):
            with self.subTest(root_name=root_name):
                self.assertEqual(self.code(build_release(root_name=root_name)), "CHILD_ARCHIVE_UNSAFE")
                self.clean()

    def test_rejects_mixed_root_member_even_with_correct_zip_hash(self):
        data = io.BytesIO(build_release())
        with zipfile.ZipFile(data, "a") as zf:
            zf.writestr("outside.py", b"x=1")
        self.assertEqual(self.code(data.getvalue()), "CHILD_ARCHIVE_UNSAFE")
        self.clean()

    def test_retained_manifest_read_supports_validation_limit(self):
        payload = b"M" * (child.MAX_LAUNCHER + 1)
        pathlib.Path(self.state, "MANIFEST.sha256").write_bytes(payload)
        dfd = os.open(self.state, os.O_RDONLY | os.O_DIRECTORY)
        try:
            self.assertEqual(child._read(dfd, "MANIFEST.sha256", self.uid, "TEST", limit=child.MAX_TEXT),
                             payload)
            with self.assertRaises(child.Stop):
                child._read(dfd, "MANIFEST.sha256", self.uid, "TEST")  # launcher-sized read remains strict
        finally:
            os.close(dfd)

    def test_rollback_restores_the_old_launcher_and_never_deletes_the_store(self):
        self.run_install()
        child.rollback(launcher=self.launcher, state=self.state, nonce=NONCE, uid=self.uid, anchor=self.root)
        self.untouched()
        self.assertEqual(os.listdir(self.store), [SRC40])
        self.assertIn("inflight-rollback-%s.done" % NONCE, os.listdir(self.state))
        self.assertIn("inflight-swap-%s.done" % NONCE, os.listdir(self.state))  # both audit records survive

    def test_archive_that_is_not_the_signed_one_is_refused_before_any_write(self):
        data = build_release()
        self.assertEqual(self.code(data, claims=self.claims(data, app_archive_sha256=sha(b"other"))),
                         "CHILD_ARCHIVE_DIGEST_MISMATCH")
        self.clean()

    def test_operator_that_differs_from_the_signed_pin_is_refused_before_any_write(self):
        self.assertEqual(self.code(pins=self.pins(candidate_operator_sha256=hx(b"evil"))), "CHILD_CANDIDATE_MISMATCH")
        evil = build_release(files={OPERATOR_REL: b"print('evil')\n"})
        self.assertEqual(self.code(evil), "CHILD_CANDIDATE_MISMATCH")
        self.clean()

    def test_hostile_archives_are_refused_before_any_write(self):
        link = zipfile.ZipInfo("scripts/qa/link.py")
        link.external_attr = (stat.S_IFLNK | 0o777) << 16
        cases = {
            "traversal": build_release(extra={"../escape.py": b"x"}),
            "absolute": build_release(extra={"/etc/x": b"x"}),
            "backslash": build_release(extra={"a\\b.py": b"x"}),
            "dot": build_release(extra={"a/./b.py": b"x"}),
            "symlink": build_release(extra={"scripts/qa/link.py": link}),
            "file_and_dir": build_release(files={OPERATOR_REL: OPERATOR_BYTES, "scripts/qa/operators": b"x"}),
            "bomb": build_release(files={OPERATOR_REL: OPERATOR_BYTES, "big.bin": b"\0" * (32 * 1024 * 1024)}),  # ratio bomb
            "not_a_zip": b"PK\x03\x04 not really a zip",
        }
        for label, data in cases.items():
            with self.subTest(label):
                self.assertIn(self.code(data), ("CHILD_ARCHIVE_UNSAFE", "CHILD_MANIFEST_INVALID"), label)
                self.clean()

    def test_limits_admit_the_real_attested_release_shape(self):
        """Rehearsal regression (10-11): release c952a4a0 carries storage-bundle/silo-qa-review-only.tar of
        229 277 184 bytes and 245 MiB in total; a 128 MiB per-member cap refused it AFTER the nonce was spent."""
        self.assertGreaterEqual(child.MAX_MEMBER, 1024 ** 3)
        self.assertGreaterEqual(child.MAX_TOTAL, 2 * 1024 ** 3)
        self.assertGreaterEqual(child.MAX_MEMBERS, 2488 * 2)

    def test_duplicate_member_names_are_refused(self):
        import warnings
        out = io.BytesIO()
        with warnings.catch_warnings():
            warnings.simplefilter("ignore")
            with zipfile.ZipFile(out, "w") as z:
                p = "Proyecto-Centenario-QA-srv01-" + SRC40[:12] + "/"
                z.writestr(p + "BUILD-INFO.txt", ("SOURCE_SHA=%s\n" % SRC40).encode())
                z.writestr(p + OPERATOR_REL, OPERATOR_BYTES)
                z.writestr(p + OPERATOR_REL, b"second")
                z.writestr(p + "MANIFEST.sha256", b"")
        self.assertEqual(self.code(out.getvalue()), "CHILD_ARCHIVE_UNSAFE")
        self.clean()

    def test_manifest_must_list_exactly_the_files(self):
        files = {OPERATOR_REL: OPERATOR_BYTES, "scripts/qa/other.py": b"x = 1\n"}
        info = ("SOURCE_SHA=%s\n" % SRC40).encode()
        allm = {**files, "BUILD-INFO.txt": info}
        lines = ["%s  ./%s\n" % (hx(v), k) for k, v in sorted(allm.items())]
        cases = {
            "missing_manifest_line": "".join(lines[:-1]),
            "extra_listed_file": "".join(lines) + "%s  ./ghost.py\n" % hx(b"g"),
            "noncanonical": "".join(lines).replace("  ./", " ./"),
            "empty": "",
            "dup": "".join(lines) + lines[0],
            "no_operator": "".join(l for l in lines if "qa525-update" not in l),
        }
        for label, manifest in cases.items():
            with self.subTest(label):
                self.assertEqual(self.code(build_release(files=files, manifest=manifest.encode())),
                                 "CHILD_MANIFEST_INVALID")
                self.clean()
        extra_file = build_release(files=files, extra={"surprise.py": b"x"})
        self.assertEqual(self.code(extra_file), "CHILD_MANIFEST_INVALID")
        self.clean()

    def test_build_info_must_bind_the_signed_source_sha(self):
        self.assertEqual(self.code(build_release(info=b"SOURCE_SHA=" + b"d4" * 20 + b"\n")), "CHILD_MANIFEST_INVALID")
        self.assertEqual(self.code(build_release(info=b"nothing\n")), "CHILD_MANIFEST_INVALID")
        self.clean()

    def test_member_bytes_that_do_not_match_the_manifest_stop_and_leave_the_marker(self):
        files = {OPERATOR_REL: OPERATOR_BYTES, "scripts/qa/other.py": b"x = 1\n"}
        allm = {**files, "BUILD-INFO.txt": ("SOURCE_SHA=%s\n" % SRC40).encode()}
        manifest = "".join("%s  ./%s\n" % (hx(b"different" if k.endswith("other.py") else v), k)
                           for k, v in sorted(allm.items())).encode()
        self.assertEqual(self.code(build_release(files=files, manifest=manifest)), "CHILD_MANIFEST_MISMATCH")
        self.untouched()
        self.assertIn("inflight.json", os.listdir(self.state))  # recovery required, nothing cleaned automatically
        self.assertIn(SRC40 + ".partial", os.listdir(self.store))
        self.assertNotIn(SRC40, os.listdir(self.store))

    def test_existing_store_target_is_never_replaced(self):
        os.mkdir(os.path.join(self.store, SRC40), 0o755)
        pathlib.Path(self.store, SRC40, "mine").write_bytes(b"keep")
        self.assertEqual(self.code(), "CHILD_STORE_EXISTS")
        self.assertEqual(pathlib.Path(self.store, SRC40, "mine").read_bytes(), b"keep")
        self.assertEqual(os.listdir(self.state), [])
        self.untouched()
        os.rename(os.path.join(self.store, SRC40), os.path.join(self.store, "elsewhere"))
        os.mkdir(os.path.join(self.store, SRC40 + ".partial"), 0o700)
        self.assertEqual(self.code(), "CHILD_STORE_EXISTS")

    def test_store_that_appears_right_before_the_rename_is_not_clobbered(self):
        real = child._extract

        def racing(zf, files, expected, pfd, uid):
            real(zf, files, expected, pfd, uid)
            os.mkdir(os.path.join(self.store, SRC40), 0o755)
            pathlib.Path(self.store, SRC40, "mine").write_bytes(b"keep")
        with mock.patch.object(child, "_extract", racing):
            self.assertEqual(self.code(), "CHILD_STORE_EXISTS")
        self.assertEqual(pathlib.Path(self.store, SRC40, "mine").read_bytes(), b"keep")
        self.untouched()

    def test_crash_after_the_store_rename_blocks_everything_until_reconciled(self):
        with mock.patch.object(child, "_verify_retained", side_effect=KeyboardInterrupt):
            with self.assertRaises(KeyboardInterrupt):
                self.run_install()
        self.untouched()
        self.assertIn(SRC40, os.listdir(self.store))
        self.assertIn("inflight.json", os.listdir(self.state))
        self.assertEqual(self.code(), "CHILD_STORE_EXISTS")
        other = "f6" * 20
        data = build_release(info=("SOURCE_SHA=%s\n" % other).encode(), source_sha=other)
        claims = self.claims(data, nonce="9a" * 32, app_source_sha=other)
        self.assertEqual(self.code(data, claims=claims), "CHILD_RECOVERY_REQUIRED")
        self.untouched()

    def test_crash_mid_extraction_leaves_only_the_partial_and_the_marker(self):
        with mock.patch.object(child, "_extract", side_effect=KeyboardInterrupt):
            with self.assertRaises(KeyboardInterrupt):
                self.run_install()
        self.assertEqual(os.listdir(self.store), [SRC40 + ".partial"])
        self.assertIn("inflight.json", os.listdir(self.state))
        self.untouched()
        self.assertEqual(self.code(), "CHILD_STORE_EXISTS")

    def test_launcher_drift_is_refused_before_the_store_is_touched(self):
        pathlib.Path(self.launcher).write_bytes(b"#!/bin/sh\necho drift\n")
        os.chmod(self.launcher, 0o700)
        self.assertEqual(self.code(), "CHILD_OLD_LAUNCHER_DRIFT")
        self.assertEqual(os.listdir(self.store), [])
        self.assertEqual(os.listdir(self.state), [])

    def test_retained_tree_that_drifts_after_publish_stops_before_the_swap(self):
        real = child._verify_retained

        def tamper(store_fd, sha_, operator_hex, manifest_sha, uid):
            os.chmod(os.path.join(self.store, SRC40, OPERATOR_REL), 0o666)  # group/other writable
            return real(store_fd, sha_, operator_hex, manifest_sha, uid)
        with mock.patch.object(child, "_verify_retained", tamper):
            self.assertEqual(self.code(), "CHILD_RETAINED_DRIFT")
        self.untouched()

    def test_claims_and_pins_shapes_are_closed(self):
        data = build_release()
        bad_claims = [{"purpose": "G6_V2_ONLY"}, {"nonce": "zz"}, {"app_source_sha": "abc"},
                      {"app_archive_sha256": "deadbeef"}]
        for over in bad_claims:
            with self.subTest(over):
                self.assertEqual(self.code(data, claims=self.claims(data, **over)), "CHILD_CLAIMS_INVALID")
        self.assertEqual(self.code(data, pins=self.pins(extra="x")), "CHILD_CLAIMS_INVALID")
        self.assertEqual(self.code(data, pins=self.pins(old_launcher_sha256="sha256:" + hx(OLD))),
                         "CHILD_CLAIMS_INVALID")
        self.clean()

    def test_act_for_builds_the_runner_only_from_valid_claims_and_uses_real_anchors(self):
        data = build_release()
        with self.assertRaises(child.Stop):
            child.act_for(claims=self.claims(data, purpose="API_WEB_QA_ONLY"), launcher_pins=self.pins())
        run = child.act_for(claims=self.claims(data), launcher_pins=self.pins())
        with mock.patch.object(child, "LAUNCHER", self.launcher), mock.patch.object(child, "STORE", self.store), \
                mock.patch.object(child, "STATE", self.state):
            with self.assertRaises(child.Stop) as cm:
                run(self.memfd(data))  # production anchor "/" : /tmp is not a trusted chain
        self.assertEqual(str(cm.exception), "CHILD_STATE_UNSAFE")
        self.clean()

    def test_act_for_snapshots_its_inputs(self):
        data = build_release()
        claims, pins = self.claims(data), self.pins()
        run = child.act_for(claims=claims, launcher_pins=pins)
        claims["nonce"] = "zz"
        pins["candidate_operator_sha256"] = "0" * 64
        self.assertTrue(callable(run))


if __name__ == "__main__":
    unittest.main()
