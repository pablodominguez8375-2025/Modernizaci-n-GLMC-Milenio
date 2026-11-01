"""QA525 root installer adversarial offline guards. NEVER touches physical host."""
import ast
from contextlib import nullcontext
import importlib.util
import io
import json
import os
from pathlib import Path
import socket
import stat
import sys
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import Mock, patch

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "scripts/qa/operators/qa525-launcher-supervised-root-install.py"
spec = importlib.util.spec_from_file_location("qa525_signed_root_installer", SOURCE)
m = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = m
spec.loader.exec_module(m)

SHA = "a" * 40
NONCE = "b" * 64


def fake_modules(*, gate_error=None, approval_error=None, contract_error=None):
    inspector = SimpleNamespace(inspect=Mock())
    preflight = SimpleNamespace(
        signed_candidate_preflight=Mock(side_effect=gate_error))
    signed = SimpleNamespace(verify_current=Mock())
    approval = SimpleNamespace(
        read_root_approval=Mock(side_effect=approval_error))
    contract = SimpleNamespace(
        assess_review=Mock(side_effect=contract_error))
    template = SimpleNamespace(render=Mock())
    retained = SimpleNamespace(inspect_retained=Mock())
    return inspector, preflight, signed, approval, contract, template, retained


class RootLauncherInstallerTests(unittest.TestCase):
    def test_exact_five_shared_locks_in_operational_order(self):
        self.assertEqual(
            [str(p) for p in m.LOCKS],
            ["/opt/centenario/qa-api-refresh/operation.lock",
             "/opt/centenario/qa-auto-lite/web-update.lock",
             "/run/lock/pmgm-master-qa-604.lock",
             "/run/lock/pmgm-qa754-web-hotfix.lock",
             "/run/lock/pmgm-qa754-api-activation.lock"])

    def test_hardlinked_shared_lock_is_rejected_before_flock(self):
        fake = SimpleNamespace(st_mode=stat.S_IFREG | 0o600,
                               st_uid=0, st_nlink=2,
                               st_dev=10, st_ino=20)
        with patch.object(m, "require_host"), \
             patch.object(m, "lock_parent"), \
             patch.object(m.os, "open", return_value=100), \
             patch.object(m.os, "fstat", return_value=fake), \
             patch.object(m.os, "close"), \
             patch.object(m.fcntl, "flock") as flock:
            with self.assertRaisesRegex(m.Blocked,
                                        "ROOT_LOCK_FILE_UNTRUSTED"):
                with m.all_five_locks():
                    self.fail("The unsafe lock must not be granted")
            flock.assert_not_called()

    def test_shared_lock_inode_swap_is_rejected_before_flock(self):
        trusted = SimpleNamespace(st_mode=stat.S_IFREG | 0o600,
                                  st_uid=0, st_nlink=1,
                                  st_dev=10, st_ino=20)
        replacement = SimpleNamespace(st_mode=stat.S_IFREG | 0o600,
                                      st_uid=0, st_nlink=1,
                                      st_dev=10, st_ino=21)
        with patch.object(m, "require_host"), \
             patch.object(m, "lock_parent"), \
             patch.object(m.os, "open", return_value=100), \
             patch.object(m.os, "fstat", return_value=trusted), \
             patch.object(Path, "lstat", return_value=replacement), \
             patch.object(m.os, "close"), \
             patch.object(m.fcntl, "flock") as flock:
            with self.assertRaisesRegex(m.Blocked,
                                        "ROOT_LOCK_PATH_CHANGED_DURING_OPEN"):
                with m.all_five_locks():
                    self.fail("The changed lock must not be granted")
            flock.assert_not_called()

    def test_wrong_host_blocks_before_import_or_filesystem(self):
        with patch.object(m.os, "geteuid", return_value=0), \
             patch.object(m.socket, "gethostname", return_value="srv01"), \
             patch.object(m, "verify_inputs") as modules:
            with self.assertRaisesRegex(m.Blocked,
                                        "SIGNED_LAUNCHER_ROOT_GLMDCH_ONLY"):
                m.replace_approved()
            modules.assert_not_called()
        with patch.object(m.os, "geteuid", return_value=1000), \
             patch.object(m.socket, "gethostname", return_value="glmdch"), \
             patch.object(m, "verify_inputs") as modules:
            with self.assertRaises(m.Blocked):
                m.replace_approved()
            modules.assert_not_called()

    def test_signature_failure_blocks_before_approval_and_any_journal(self):
        modules = fake_modules(gate_error=m.Blocked("SIGNATURE_UNAVAILABLE"))
        with patch.object(m, "require_host"), \
             patch.object(m, "verify_inputs", return_value=modules), \
             patch.object(m, "root_chain"), \
             patch.object(m, "all_five_locks", return_value=nullcontext()), \
             patch.object(m, "scan_history", return_value=set()), \
             patch.object(m, "write_once") as writer, \
             patch.object(m.os, "replace") as swap:
            with self.assertRaisesRegex(m.Blocked, "SIGNATURE_UNAVAILABLE"):
                m.replace_approved()
            modules[3].read_root_approval.assert_not_called()
            writer.assert_not_called()
            swap.assert_not_called()

    def test_missing_approval_blocks_without_transaction(self):
        modules = fake_modules(
            approval_error=m.Blocked("LAUNCHER_APPROVAL_PATH_MISSING"))
        modules[1].signed_candidate_preflight.return_value = {
            "status": "SIGNED_LAUNCHER_CANDIDATE_VERIFIED_NO_INSTALL",
            "source_sha": SHA}
        with patch.object(m, "require_host"), \
             patch.object(m, "verify_inputs", return_value=modules), \
             patch.object(m, "root_chain"), \
             patch.object(m, "all_five_locks", return_value=nullcontext()), \
             patch.object(m, "scan_history", return_value=set()), \
             patch.object(m, "write_once") as writer, \
             patch.object(m.os, "replace") as swap:
            with self.assertRaisesRegex(m.Blocked,
                                        "LAUNCHER_APPROVAL_PATH_MISSING"):
                m.replace_approved()
            writer.assert_not_called()
            swap.assert_not_called()

    def test_invalid_signed_cut_policy_rejects_before_persistence(self):
        modules = fake_modules(contract_error=m.Blocked("EXPIRED_NONCE"))
        modules[1].signed_candidate_preflight.return_value = {
            "status": "SIGNED_LAUNCHER_CANDIDATE_VERIFIED_NO_INSTALL",
            "source_sha": SHA}
        modules[3].read_root_approval.return_value = {"nonce": NONCE}
        with patch.object(m, "require_host"), \
             patch.object(m, "verify_inputs", return_value=modules), \
             patch.object(m, "root_chain"), \
             patch.object(m, "all_five_locks", return_value=nullcontext()), \
             patch.object(m, "scan_history", return_value=set()), \
             patch.object(m, "write_once") as writer:
            with self.assertRaisesRegex(m.Blocked, "EXPIRED_NONCE"):
                m.replace_approved()
            writer.assert_not_called()

    def test_durable_exclusive_write_and_non_overwrite(self):
        with tempfile.TemporaryDirectory() as td:
            dest = Path(td) / "intent.json"
            content = m.json_bytes({"nonce": NONCE, "status": "SWAP_INTENT_NO_DEPLOYMENT"})
            m.write_once(dest, content)
            self.assertEqual(dest.read_bytes(), content)
            self.assertEqual(stat.S_IMODE(dest.stat().st_mode), 0o600)
            with self.assertRaises(FileExistsError):
                m.write_once(dest, b"alteration")
            self.assertEqual(dest.read_bytes(), content)

    def test_pending_and_unknown_journals_block_replay(self):
        with tempfile.TemporaryDirectory() as td:
            old_state = m.STATE
            try:
                m.STATE = Path(td)
                with patch.object(m, "root_chain"):
                    self.assertEqual(set(), m.scan_history())
                    (m.STATE / ("intent-" + NONCE + ".json")).write_text("{}")
                    with self.assertRaisesRegex(
                            m.Blocked, "ROOT_LAUNCHER_PENDING_MANUAL_RECONCILIATION"):
                        m.scan_history()
                    (m.STATE / ("intent-" + NONCE + ".json")).unlink()
                    (m.STATE / "unknown.tmp").write_text("tamper")
                    with self.assertRaisesRegex(m.Blocked,
                                                "ROOT_LAUNCHER_UNKNOWN_JOURNAL_ENTRY"):
                        m.scan_history()
            finally:
                m.STATE = old_state

    def test_cli_does_not_offer_paths_force_or_disable_authentication(self):
        script = SOURCE.read_text()
        self.assertIn('parser.add_argument("--replace-approved"', script)
        self.assertNotIn("--force", script)
        self.assertNotIn("--unsigned", script)
        self.assertNotIn("--skip-verification", script)
        self.assertNotIn("--target", script)
        self.assertNotIn("subprocess", script)
        # Documentation may name Docker as forbidden; inspect executable calls.
        self.assertNotIn("subprocess.", script)
        self.assertNotIn("os.system(", script)
        self.assertNotIn("docker run", script)
        tree = ast.parse(script)
        funcs = {n.name:n for n in tree.body if isinstance(n, ast.FunctionDef)}
        self.assertIn("replace_approved", funcs)
        calls = [n for n in ast.walk(funcs["replace_approved"])
                 if isinstance(n, ast.Call) and isinstance(n.func, ast.Attribute)]
        needed_attributes = {"signed_candidate_preflight", "assess_review",
                             "verify_current", "verify_manifest",
                             "inspect_retained", "replace"}
        actual_attributes = {n.func.attr for n in calls}
        self.assertTrue(needed_attributes.issubset(actual_attributes))
        actual_functions = {
            n.func.id for n in ast.walk(funcs["replace_approved"])
            if isinstance(n, ast.Call) and isinstance(n.func, ast.Name)
        }
        self.assertTrue({"write_once", "sync_dir"}.issubset(actual_functions))
        self.assertIn("os.fsync(fd)", script)
        self.assertIn("ROOT_APPROVAL_CHANGED_DURING_RELEASE_DOWNLOAD", script)
        self.assertIn("ROOT_REVIEW_NONCE_CHANGED_DURING_DOWNLOAD", script)
        self.assertIn("ROOT_LAUNCHER_PENDING_MANUAL_RECONCILIATION", script)
        self.assertIn("requires_manual_reconciliation_if_intent_exists", script)

    def test_reject_any_other_cli_arguments_without_host_access(self):
        with patch.object(m, "replace_approved") as run:
            with self.assertRaises(SystemExit):
                m.main(["--replace-approved", "--target", "/tmp/unsafe"])
            run.assert_not_called()


class LauncherSandboxTransactionTests(unittest.TestCase):
    """Execute real fsync/rename/journal in a temp root, never actual QA."""

    def exercise(self, *, fault=None, change_approval=False):
        old = b"#!/usr/bin/python3\nprint('old synthetic launcher')\n"
        new = b"#!/usr/bin/python3\nprint('signed synthetic launcher')\n"
        operator = b"print('synthetic signed operator, never executed')\n"
        source_sha = SHA
        release_digest = "sha256:" + "c" * 64
        operator_hash = m.digest(operator)
        old_hash = m.digest(old)
        with tempfile.TemporaryDirectory(prefix="qa525-root-installer-test-") as td:
            base = Path(td)
            store = base / "signed"
            state = base / "journal"
            prepare = base / "prepare"
            live_dir = base / "bin"
            for item in (store, state, prepare, live_dir):
                item.mkdir(mode=0o700)
            launcher = live_dir / "pmgm-qa-update"
            launcher.write_bytes(old)
            launcher.chmod(0o700)
            plan = {
                "source_sha": source_sha,
                "release_asset_sha256": release_digest,
                "old_launcher_sha256": old_hash,
                "candidate_operator_sha256": operator_hash,
            }
            act = {"nonce": NONCE, "expires_at": int(__import__("time").time())+800}
            inspect_old = SimpleNamespace(inspect=Mock(return_value={
                "status": "LAUNCHER_OBSERVED_UPGRADE_NOT_AUTHORIZED",
                "current_launcher_sha256": old_hash,
            }))
            preflight = SimpleNamespace(
                signed_candidate_preflight=Mock(return_value=plan),
                stable_hash=lambda path: Path(path).read_bytes())
            def stage(package):
                package.mkdir(mode=0o700)
                folder = package
                for segment in ("scripts", "qa", "operators"):
                    folder = folder / segment
                    folder.mkdir(mode=0o700)
                location = folder / "qa525-update.py"
                location.write_bytes(operator)
                return {
                    "status": "SIGNATURE_VERIFIED_STAGED_NOT_DEPLOYED",
                    "source_sha": source_sha,
                    "release_asset_sha256": release_digest,
                }
            signed = SimpleNamespace(
                verify_current=Mock(side_effect=stage),
                staging=SimpleNamespace(verify_manifest=Mock(return_value=True)))
            calls = [dict(act)]
            if change_approval:
                calls.append(dict(act, nonce="e" * 64))
            approval = SimpleNamespace(
                read_root_approval=Mock(side_effect=(
                    lambda: calls.pop(0) if calls else dict(act))))
            contract = SimpleNamespace(assess_review=Mock(return_value={
                "status": "REVIEWED_SIGNED_LAUNCHER_PLAN_NO_INSTALL",
                "may_install": False, "nonce": NONCE,
            }))
            template = SimpleNamespace(
                render=Mock(return_value=new),
                inspect_template=Mock(return_value={
                    "launcher_sha256": m.digest(new), "may_install": False,
                }))
            retained = SimpleNamespace(inspect_retained=Mock(return_value={
                "status": "ROOT_OWNED_PINNED_SOURCE_HASH_MATCH_NOT_SIGNED",
            }))
            modules = (inspect_old, preflight, signed, approval,
                       contract, template, retained)
            real_replace = os.replace
            real_write = m.write_once
            def swap(old_path, new_path):
                if fault == "before_swap":
                    raise RuntimeError("SYNTHETIC_PROCESS_DEATH_BEFORE_SWAP")
                real_replace(old_path, new_path)
                if fault == "after_swap":
                    raise RuntimeError("SYNTHETIC_PROCESS_DEATH_AFTER_SWAP")
            def write_with_fault(path, payload, mode=0o600):
                real_write(path, payload, mode)
                if fault == "after_receipt" and Path(path).name.startswith("receipt-"):
                    raise RuntimeError("SYNTHETIC_PROCESS_DEATH_AFTER_RECEIPT")
            from contextlib import ExitStack
            with ExitStack() as stack:
                for key, value in (
                    ("LAUNCHER", launcher), ("STORE", store), ("STATE", state),
                    ("PREFLIGHT_STATE", prepare)):
                    stack.enter_context(patch.object(m, key, value))
                stack.enter_context(patch.object(m, "require_host"))
                stack.enter_context(patch.object(m, "root_chain"))
                stack.enter_context(patch.object(
                    m, "read_bound",
                    side_effect=lambda path, maximum=4096, exact_mode=None:
                    Path(path).read_bytes()))
                stack.enter_context(patch.object(
                    m, "all_five_locks", return_value=nullcontext()))
                stack.enter_context(patch.object(m, "verify_inputs",
                                                 return_value=modules))
                if fault in ("before_swap", "after_swap"):
                    stack.enter_context(patch.object(m.os, "replace",
                                                     side_effect=swap))
                if fault == "after_receipt":
                    stack.enter_context(patch.object(m, "write_once",
                                                     side_effect=write_with_fault))
                if fault:
                    with self.assertRaisesRegex(RuntimeError,
                                                "SYNTHETIC_PROCESS_DEATH"):
                        m.replace_approved()
                elif change_approval:
                    with self.assertRaisesRegex(
                            m.Blocked, "ROOT_APPROVAL_CHANGED_DURING_RELEASE_DOWNLOAD"):
                        m.replace_approved()
                else:
                    result = m.replace_approved()
                    self.assertEqual("QA525_LAUNCHER_REPLACED_NO_API_WEB_DEPLOY",
                                     result["status"])
                    self.assertFalse(result["api_web_changed"])
                    self.assertFalse(result["may_apply"])
                self.assertEqual(
                    new if fault in ("after_swap", "after_receipt") or
                        (not fault and not change_approval) else old,
                    launcher.read_bytes())
                pending = state / ("intent-" + NONCE + ".json")
                receipt = state / ("receipt-" + NONCE + ".json")
                backup = state / ("old-" + NONCE + ".bin")
                if fault:
                    self.assertTrue(pending.exists())
                    self.assertTrue(backup.exists())
                    with self.assertRaisesRegex(
                            m.Blocked, "ROOT_LAUNCHER_PENDING_MANUAL_RECONCILIATION"):
                        m.scan_history()
                    with self.assertRaisesRegex(
                            m.Blocked, "ROOT_LAUNCHER_PENDING_MANUAL_RECONCILIATION"):
                        m.replace_approved()
                elif change_approval:
                    self.assertFalse(pending.exists())
                    self.assertFalse(backup.exists())
                    self.assertFalse(receipt.exists())
                    self.assertEqual([], list(state.iterdir()))
                else:
                    self.assertFalse(pending.exists())
                    self.assertTrue(backup.exists())
                    self.assertTrue(receipt.exists())
                    self.assertEqual({NONCE}, m.scan_history())
                    self.assertEqual(
                        operator, (store / SHA /
                            "scripts/qa/operators/qa525-update.py").read_bytes())

    def test_successful_sandbox_transaction_persists_signed_operator_and_receipt(self):
        self.exercise()

    def test_crash_before_swap_never_autoretries(self):
        self.exercise(fault="before_swap")

    def test_crash_after_swap_never_autoretries(self):
        self.exercise(fault="after_swap")

    def test_crash_after_durable_receipt_still_requires_reconciliation(self):
        self.exercise(fault="after_receipt")

    def test_switched_approval_during_signed_download_aborts_before_intent(self):
        self.exercise(change_approval=True)


class ReadOnlyPendingJournalTests(unittest.TestCase):
    """Never resume or roll back based solely on an on-disk journal."""

    def check(self, *, post=False, receipt=False, corrupt=None):
        old = b"old signed launcher\n"
        new = b"new signed launcher\n"
        with tempfile.TemporaryDirectory(prefix="qa525-journal-inspect-") as td:
            base = Path(td)
            journal = base / "journal"
            journal.mkdir(mode=0o700)
            launcher = base / "pmgm-qa-update"
            launcher.write_bytes(new if post else old)
            launcher.chmod(0o700)
            act = {
                "status": "SWAP_INTENT_NO_DEPLOYMENT",
                "source_sha": SHA,
                "release_asset_sha256": "sha256:" + "c" * 64,
                "old_launcher_sha256": m.digest(old),
                "new_launcher_sha256": m.digest(new),
                "nonce": NONCE,
            }
            intent = journal / ("intent-" + NONCE + ".json")
            intent.write_bytes(m.json_bytes(act))
            backup = journal / ("old-" + NONCE + ".bin")
            backup.write_bytes(old)
            if receipt:
                (journal / ("receipt-" + NONCE + ".json")).write_bytes(
                    m.json_bytes(dict(act, status="LAUNCHER_ATOMIC_SWAP_COMMITTED",
                                      candidate_operator_sha256="d"*64)))
            if corrupt == "backup":
                backup.write_bytes(b"corrupt backup\n")
            if corrupt == "unknown_live":
                launcher.write_bytes(b"unrecognised third launcher\n")
            if corrupt == "receipt":
                (journal / ("receipt-" + NONCE + ".json")).write_bytes(
                    m.json_bytes(dict(act, status="WRONG_STATUS")))
            if corrupt == "duplicate":
                intent.write_text('{"nonce":"bad","nonce":"bad-again"}')
            if corrupt == "missing_backup":
                backup.unlink()
            before = {x.name: x.read_bytes() for x in journal.iterdir()}
            with patch.object(m, "STATE", journal), \
                 patch.object(m, "LAUNCHER", launcher), \
                 patch.object(m, "require_host"), \
                 patch.object(m, "root_chain"), \
                 patch.object(m, "all_five_locks", return_value=nullcontext()), \
                 patch.object(m, "read_bound",
                              side_effect=lambda path, maximum=4096, exact_mode=None:
                              Path(path).read_bytes()), \
                 patch.object(m, "write_once") as writer, \
                 patch.object(m.os, "replace") as swap:
                if corrupt:
                    with self.assertRaises(m.Blocked):
                        m.inspect_pending_reconciliation()
                else:
                    report = m.inspect_pending_reconciliation()
                    self.assertEqual("MANUAL_RECONCILIATION_REQUIRED_NO_AUTOREPAIR",
                                     report["status"])
                    self.assertEqual("POST_ATOMIC_SWAP" if post
                                     else "PRE_ATOMIC_SWAP",
                                     report["observed_phase"])
                    self.assertEqual(receipt, report["durable_receipt_present"])
                    self.assertTrue(report["backup_hash_matches_intent"])
                    for key in ("signature_verified_by_inspection",
                                "rollback_authorized", "may_resume", "may_apply"):
                        self.assertFalse(report[key])
                writer.assert_not_called()
                swap.assert_not_called()
                self.assertEqual(before,
                    {x.name: x.read_bytes() for x in journal.iterdir()})

    def test_pending_before_swap_read_only(self):
        self.check(post=False)

    def test_pending_after_swap_read_only(self):
        self.check(post=True)

    def test_committed_receipt_with_unremoved_intent_still_requires_human(self):
        self.check(post=True, receipt=True)

    def test_corrupt_backup_live_receipt_or_missing_journal_block(self):
        for case, post in (("backup", False), ("unknown_live", False),
                           ("receipt", True), ("duplicate", False),
                           ("missing_backup", False)):
            with self.subTest(case=case):
                self.check(post=post, corrupt=case)


class RetainedPackageDurabilityTests(unittest.TestCase):
    def test_fsync_accepts_private_source_tree_before_publish(self):
        with tempfile.TemporaryDirectory() as td:
            package = Path(td) / "package"
            package.mkdir(mode=0o700)
            folder = package / "scripts"
            folder.mkdir(mode=0o700)
            member = folder / "qa525-update.py"
            member.write_bytes(b"synthetic attested Python operator\n")
            m.fsync_retained_package(package)
            self.assertEqual(b"synthetic attested Python operator\n",
                             member.read_bytes())

    def test_reject_symlinks_hardlinks_and_writable_members(self):
        with tempfile.TemporaryDirectory() as td:
            package = Path(td) / "package"
            package.mkdir(mode=0o700)
            member = package / "qa525-update.py"
            member.write_bytes(b"safe synthetic bytes")
            with self.subTest(case="symlink"):
                link = package / "symlink"
                link.symlink_to(member)
                with self.assertRaisesRegex(
                        m.Blocked, "RETAINED_PACKAGE_FILE_UNTRUSTED"):
                    m.fsync_retained_package(package)
                link.unlink()
            with self.subTest(case="hardlink"):
                link = package / "hardlink"
                os.link(member, link)
                with self.assertRaisesRegex(
                        m.Blocked, "RETAINED_PACKAGE_FILE_UNTRUSTED"):
                    m.fsync_retained_package(package)
                link.unlink()
            with self.subTest(case="world_writable"):
                member.chmod(0o666)
                with self.assertRaisesRegex(
                        m.Blocked, "RETAINED_PACKAGE_FILE_UNTRUSTED"):
                    m.fsync_retained_package(package)
                member.chmod(0o600)
            with self.subTest(case="untrusted_directory"):
                subdir = package / "folder"
                subdir.mkdir(mode=0o777)
                subdir.chmod(0o755)
                with self.assertRaisesRegex(
                        m.Blocked, "RETAINED_PACKAGE_DIRECTORY_UNTRUSTED"):
                    m.fsync_retained_package(package)


class KilledSubprocessDurabilityTests(unittest.TestCase):
    """Actual SIGKILL between durable root-journal and atomic swap.

    A disposable child runs the real write_once/os.replace/fsync operations,
    never the real installer entrypoint, never an external host or Docker.
    """

    CHILD = r"""
import importlib.util
import os
from pathlib import Path
import signal
import sys
base, source, step = Path(sys.argv[1]), Path(sys.argv[2]), sys.argv[3]
spec = importlib.util.spec_from_file_location("qa525_kill_child_installer", source)
mod = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = mod
spec.loader.exec_module(mod)
state = base / "state"
bin_dir = base / "bin"
live = bin_dir / "pmgm-qa-update"
old = b"old-root-owned-synthetic-launcher\n"
new = b"new-signed-synthetic-launcher\n"
nonce = "b" * 64
mod.write_once(state / ("intent-" + nonce + ".json"),
               mod.json_bytes({"status": "SWAP_INTENT_NO_DEPLOYMENT",
                               "nonce": nonce}), mode=0o600)
mod.write_once(state / ("old-" + nonce + ".bin"), old, mode=0o700)
if step == "after_atomic_swap":
    staged = bin_dir / ".pmgm-qa-update.tmp"
    mod.write_once(staged, new, mode=0o700)
    os.replace(staged, live)
    mod.sync_dir(bin_dir)
os.kill(os.getpid(), signal.SIGKILL)
raise SystemExit("UNREACHABLE")
"""

    def check_kill(self, phase):
        import signal
        import subprocess
        with tempfile.TemporaryDirectory(prefix="qa525-sigkill-") as td:
            base = Path(td)
            state = base / "state"
            bin_dir = base / "bin"
            state.mkdir(mode=0o700)
            bin_dir.mkdir(mode=0o700)
            live = bin_dir / "pmgm-qa-update"
            live.write_bytes(b"old-root-owned-synthetic-launcher\n")
            live.chmod(0o700)
            child = subprocess.run(
                [sys.executable, "-B", "-c", self.CHILD,
                 str(base), str(SOURCE), phase],
                capture_output=True, text=True, timeout=15)
            self.assertEqual(-signal.SIGKILL, child.returncode,
                             msg=child.stdout + child.stderr)
            expected = (b"new-signed-synthetic-launcher\n"
                        if phase == "after_atomic_swap"
                        else b"old-root-owned-synthetic-launcher\n")
            self.assertEqual(expected, live.read_bytes())
            nonce = "b" * 64
            intent = state / ("intent-" + nonce + ".json")
            backup = state / ("old-" + nonce + ".bin")
            self.assertTrue(intent.is_file())
            self.assertEqual(b"old-root-owned-synthetic-launcher\n",
                             backup.read_bytes())
            self.assertEqual(0o600, stat.S_IMODE(intent.stat().st_mode))
            self.assertEqual(0o700, stat.S_IMODE(backup.stat().st_mode))
            with patch.object(m, "STATE", state), \
                 patch.object(m, "root_chain"):
                with self.assertRaisesRegex(
                        m.Blocked, "ROOT_LAUNCHER_PENDING_MANUAL_RECONCILIATION"):
                    m.scan_history()

    def test_actual_sigkill_before_swap_replays_nothing(self):
        self.check_kill("before_atomic_swap")

    def test_actual_sigkill_after_swap_leaves_intent_and_blocks_retry(self):
        self.check_kill("after_atomic_swap")


if __name__ == "__main__":
    unittest.main()
