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
    def test_root_installer_requires_isolated_no_site_python(self):
        from types import SimpleNamespace
        with patch.object(m.os, "geteuid", return_value=0), \
             patch.object(m.socket, "gethostname", return_value="glmdch"):
            for isolated, no_site in ((0, 0), (1, 0), (0, 1)):
                with self.subTest(isolated=isolated, no_site=no_site):
                    with patch.object(m.sys, "flags", SimpleNamespace(
                            isolated=isolated, no_site=no_site)):
                        with self.assertRaisesRegex(
                                m.Blocked,
                                "SIGNED_LAUNCHER_ROOT_PYTHON_ISOLATION_REQUIRED"):
                            m.require_host()

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

    def test_shared_five_lock_contention_blocks_and_releases(self):
        with tempfile.TemporaryDirectory(prefix="qa525-five-lock-") as td:
            paths = tuple(Path(td) / ("lock-" + str(i)) for i in range(5))
            for path in paths:
                path.write_bytes(b"")
                path.chmod(0o600)
            original_fstat = os.fstat
            original_lstat = Path.lstat
            def pretend_root(st):
                return SimpleNamespace(st_mode=st.st_mode, st_uid=0,
                                       st_nlink=st.st_nlink, st_dev=st.st_dev,
                                       st_ino=st.st_ino)
            with patch.object(m, "LOCKS", paths), \
                 patch.object(m, "require_host"), \
                 patch.object(m, "lock_parent"), \
                 patch.object(m.os, "fstat",
                              side_effect=lambda fd:
                              pretend_root(original_fstat(fd))), \
                 patch.object(Path, "lstat",
                              autospec=True, side_effect=lambda self:
                              pretend_root(original_lstat(self))):
                with m.all_five_locks():
                    with self.assertRaisesRegex(m.Blocked,
                                                "ANOTHER_QA_OPERATOR_RUNNING"):
                        with m.all_five_locks():
                            self.fail("Second operator must not obtain locks")
                with m.all_five_locks():
                    pass

    def test_diagnostic_missing_lock_is_rejected_without_creation(self):
        with tempfile.TemporaryDirectory(prefix="qa525-readonly-lock-missing-") as td:
            paths = tuple(Path(td) / ("absent-" + str(i)) for i in range(5))
            with patch.object(m, "LOCKS", paths), \
                 patch.object(m, "require_host"), \
                 patch.object(m, "lock_parent"):
                with self.assertRaisesRegex(
                        m.Blocked, "ROOT_READONLY_SHARED_LOCK_MISSING"):
                    with m.all_five_locks(read_only=True):
                        self.fail("Missing lock must not be created by a probe")
                self.assertTrue(all(not p.exists() for p in paths))

    def test_diagnostic_existing_locks_use_rdonly_without_creat_or_writes(self):
        with tempfile.TemporaryDirectory(prefix="qa525-readonly-locks-") as td:
            paths = tuple(Path(td) / ("lock-" + str(i)) for i in range(5))
            for index, path in enumerate(paths):
                path.write_bytes(("fixture-" + str(index)).encode())
                path.chmod(0o600)
            before = [(p.read_bytes(), p.stat().st_mtime_ns) for p in paths]
            original_fstat = os.fstat
            original_lstat = Path.lstat
            original_open = os.open
            opened = []

            def root_owner(st):
                return SimpleNamespace(st_mode=st.st_mode, st_uid=0,
                                       st_nlink=st.st_nlink, st_dev=st.st_dev,
                                       st_ino=st.st_ino)

            def traced_open(path, flags, *args, **kwargs):
                opened.append((path, flags))
                return original_open(path, flags, *args, **kwargs)

            with patch.object(m, "LOCKS", paths), \
                 patch.object(m, "require_host"), \
                 patch.object(m, "lock_parent"), \
                 patch.object(m.os, "fstat",
                              side_effect=lambda fd:
                              root_owner(original_fstat(fd))), \
                 patch.object(Path, "lstat", autospec=True,
                              side_effect=lambda self:
                              root_owner(original_lstat(self))), \
                 patch.object(m.os, "open", side_effect=traced_open):
                with m.all_five_locks(read_only=True):
                    self.assertEqual(5, len(opened))
                    for _, flags in opened:
                        self.assertEqual(os.O_RDONLY, flags & os.O_ACCMODE)
                        self.assertEqual(0, flags & os.O_CREAT)
                    with self.assertRaisesRegex(
                            m.Blocked, "ANOTHER_QA_OPERATOR_RUNNING"):
                        with m.all_five_locks():
                            self.fail("Read-only inspection must block writer")
                with m.all_five_locks():
                    with self.assertRaisesRegex(
                            m.Blocked, "ANOTHER_QA_OPERATOR_RUNNING"):
                        with m.all_five_locks(read_only=True):
                            self.fail("Writer must block read-only inspection")
            self.assertEqual(
                before, [(p.read_bytes(), p.stat().st_mtime_ns) for p in paths])

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
             patch.object(m, "verify_bootstrap_binding"), \
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
             patch.object(m, "verify_bootstrap_binding"), \
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
             patch.object(m, "verify_bootstrap_binding"), \
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
        self.assertIn('choice.add_argument("--replace-approved"', script)
        self.assertIn('choice.add_argument("--inspect-pending"', script)
        self.assertIn("add_mutually_exclusive_group(required=True)", script)
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

    def exercise(self, *, fault=None, change_approval=False, bad_manifest=False,
                 bad_postflight=False, expired_at_swap=False):
        old = b"#!/usr/bin/python3\nprint('old synthetic launcher')\n"
        new = b"#!/usr/bin/python3\nprint('signed synthetic launcher')\n"
        operator = b"print('synthetic signed operator, never executed')\n"
        manifest = b"synthetic manifest bound to external admission\n"
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
                stable_hash=lambda path, maximum=2_000_000: Path(path).read_bytes())
            def stage(package):
                package.mkdir(mode=0o700)
                folder = package
                for segment in ("scripts", "qa", "operators"):
                    folder = folder / segment
                    folder.mkdir(mode=0o700)
                location = folder / "qa525-update.py"
                location.write_bytes(operator)
                (package / "MANIFEST.sha256").write_bytes(manifest)
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
            retained_ok = {"status": "ROOT_OWNED_PINNED_SOURCE_HASH_MATCH_NOT_SIGNED"}
            retained = SimpleNamespace(inspect_retained=Mock(
                side_effect=([retained_ok, {"status": "ROOT_RETAINED_SOURCE_DRIFT"}]
                             if bad_postflight else None),
                return_value=retained_ok))
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
                stack.enter_context(patch.object(
                    m, "verify_bootstrap_binding", return_value={
                        "manifest_sha256": ("f" * 64 if bad_manifest
                                            else m.digest(manifest)),
                        "issued_at": int(__import__("time").time()) - 10,
                        "expires_at": int(__import__("time").time()) + 800}))
                if expired_at_swap:
                    stack.enter_context(patch.object(
                        m, "require_bootstrap_fresh",
                        side_effect=m.Blocked(
                            "ROOT_BOOTSTRAP_ADMISSION_EXPIRED_OR_INVALID")))
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
                elif bad_manifest:
                    with self.assertRaisesRegex(
                            m.Blocked, "ROOT_BOOTSTRAP_MANIFEST_BINDING_MISMATCH"):
                        m.replace_approved()
                elif change_approval:
                    with self.assertRaisesRegex(
                            m.Blocked, "ROOT_APPROVAL_CHANGED_DURING_RELEASE_DOWNLOAD"):
                        m.replace_approved()
                elif bad_postflight:
                    with self.assertRaisesRegex(
                            m.Blocked, "ROOT_RETAINED_SOURCE_POST_SWAP_INVALID"):
                        m.replace_approved()
                elif expired_at_swap:
                    with self.assertRaisesRegex(
                            m.Blocked, "ROOT_BOOTSTRAP_ADMISSION_EXPIRED_OR_INVALID"):
                        m.replace_approved()
                else:
                    result = m.replace_approved()
                    self.assertEqual("QA525_LAUNCHER_REPLACED_NO_API_WEB_DEPLOY",
                                     result["status"])
                    self.assertFalse(result["api_web_changed"])
                    self.assertFalse(result["may_apply"])
                    self.assertNotIn("nonce", result)
                    self.assertEqual(m.digest(NONCE.encode("ascii")),
                                     result["nonce_sha256"])
                self.assertEqual(
                    new if fault in ("after_swap", "after_receipt") or bad_postflight or
                        (not fault and not change_approval and not bad_manifest
                         and not expired_at_swap) else old,
                    launcher.read_bytes())
                pending = state / ("intent-" + NONCE + ".json")
                receipt = state / ("receipt-" + NONCE + ".json")
                backup = state / ("old-" + NONCE + ".bin")
                if fault or bad_postflight or expired_at_swap:
                    self.assertTrue(pending.exists())
                    self.assertTrue(backup.exists())
                    if bad_postflight:
                        self.assertFalse(receipt.exists())
                        self.assertEqual(2, retained.inspect_retained.call_count)
                    with self.assertRaisesRegex(
                            m.Blocked, "ROOT_LAUNCHER_PENDING_MANUAL_RECONCILIATION"):
                        m.scan_history()
                    with self.assertRaisesRegex(
                            m.Blocked, "ROOT_LAUNCHER_PENDING_MANUAL_RECONCILIATION"):
                        m.replace_approved()
                elif change_approval or bad_manifest:
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

    def test_external_manifest_drift_aborts_before_intent(self):
        self.exercise(bad_manifest=True)

    def test_post_swap_retained_drift_blocks_success_and_requires_manual_reconciliation(self):
        self.exercise(bad_postflight=True)

    def test_expired_external_admission_at_last_boundary_never_swaps_launcher(self):
        self.exercise(expired_at_swap=True)


class RealProductionContractCompatibilityTests(unittest.TestCase):
    """No mocks of production APIs: inspect real sibling source and pure logic.

    No network, root, Docker, filesystem writes outside the test sandbox, or
    execution of downloaded candidate/operator source. Fail on API drift.
    """

    CONTRACTS = {
        "scripts/qa/operators/qa525-launcher-upgrade-inspect.py":
            {"inspect": ((), ())},
        "scripts/qa/operators/qa525-launcher-signed-preflight.py":
            {"signed_candidate_preflight":
                (("inspector", "signed"), ("stage_parent",)),
             "stable_hash": (("path", "maximum"), ())},
        "scripts/qa/qa525-stage-current-release.py":
            {"verify_current": (("output",), ("selection",))},
        "scripts/qa/qa-signed-staging.py":
            {"verify_manifest": (("package", "source_sha"), ())},
        "scripts/qa/operators/qa525-launcher-root-approval.py":
            {"read_root_approval": ((), ())},
        "scripts/qa/operators/qa525-launcher-replacement-contract.py":
            {"assess_review":
                (("preflight", "approval"),
                 ("now", "pending", "consumed_nonces"))},
        "scripts/qa/operators/qa525-launcher-pinned-template.py":
            {"render": (("source_sha",), ()),
             "inspect_template": (("source_sha", "candidate"), ())},
        "scripts/qa/operators/qa525-launcher-retained-source-inspect.py":
            {"inspect_retained":
                (("source_sha", "expected_operator_sha256"), ())},
    }

    def test_real_production_module_function_interfaces(self):
        for relative, requirements in self.CONTRACTS.items():
            with self.subTest(path=relative):
                src = ROOT / relative
                self.assertTrue(src.is_file())
                tree = ast.parse(src.read_text(encoding="utf-8"),
                                 filename=str(src))
                functions = {item.name: item for item in tree.body
                             if isinstance(item, ast.FunctionDef)}
                for name, (positional, keywords) in requirements.items():
                    self.assertIn(name, functions)
                    node = functions[name]
                    self.assertEqual(
                        list(positional),
                        [a.arg for a in node.args.posonlyargs
                         + node.args.args])
                    self.assertEqual(
                        list(keywords),
                        [a.arg for a in node.args.kwonlyargs])

    def test_real_pure_approval_contract_and_pinned_template(self):
        def from_real_file(name, path):
            spec = importlib.util.spec_from_file_location(name, ROOT / path)
            module = importlib.util.module_from_spec(spec)
            sys.modules[name] = module
            spec.loader.exec_module(module)
            return module

        contract = from_real_file(
            "qa525_root_contract_real",
            "scripts/qa/operators/qa525-launcher-replacement-contract.py")
        approval = from_real_file(
            "qa525_root_approval_real",
            "scripts/qa/operators/qa525-launcher-root-approval.py")
        template = from_real_file(
            "qa525_root_template_real",
            "scripts/qa/operators/qa525-launcher-pinned-template.py")
        now = 1793488800
        sha = "a" * 40
        old = "c" * 64
        operator = "d" * 64
        signed_digest = "sha256:" + "e" * 64
        nonce = "f" * 64
        plan = {
            "status": "SIGNED_LAUNCHER_CANDIDATE_VERIFIED_NO_INSTALL",
            "scope": "SIGNED_EXACT_DEV_PREFLIGHT_ONLY_NO_LAUNCHER_REPLACEMENT_NO_DEPLOY",
            "source_sha": sha, "release_asset_sha256": signed_digest,
            "old_launcher_sha256": old,
            "candidate_operator_sha256": operator,
            "sigstore_verified_in_this_operation": True,
            "signed_manifest_verified": True,
            "old_launcher_checked_twice": True,
            "replacement_applied": False,
            "may_install": False, "may_apply": False,
        }
        packet = {
            "source_sha": sha, "release_asset_sha256": signed_digest,
            "old_launcher_sha256": old,
            "candidate_operator_sha256": operator,
            "scope": approval.SCOPE, "action": approval.ACTION,
            "environment": approval.ENVIRONMENT,
            "operator": approval.OPERATOR, "approved": True,
            "issued_at": now - 60, "expires_at": now + 60,
            "nonce": nonce,
        }
        self.assertFalse(approval.validate(packet, now)["may_install"])
        assessed = contract.assess_review(
            plan, packet, now=now, pending=False, consumed_nonces=())
        self.assertEqual("REVIEWED_SIGNED_LAUNCHER_PLAN_NO_INSTALL",
                         assessed["status"])
        self.assertFalse(assessed["may_apply"])
        self.assertEqual(sha, assessed["source_sha"])
        self.assertEqual(nonce, assessed["nonce"])
        actual_launcher = template.render(sha)
        template_check = template.inspect_template(sha, actual_launcher)
        self.assertEqual(m.digest(actual_launcher),
                         template_check["launcher_sha256"])
        self.assertFalse(template_check["may_install"])
        with self.assertRaises(contract.Blocked):
            contract.assess_review(
                plan, packet, now=now, pending=False,
                consumed_nonces=(nonce,))
        with self.assertRaises(approval.Blocked):
            approval.validate(dict(packet, approved=1), now)


class HistoricalReceiptIntegrityTests(unittest.TestCase):
    """Receipt+backup is a durable identity, not just a nonce marker."""

    def check(self, *, corruption=None):
        old = b"synthetic root-owned old launcher\n"
        nonce = NONCE
        record = {
            "status": "LAUNCHER_ATOMIC_SWAP_COMMITTED",
            "source_sha": SHA,
            "release_asset_sha256": "sha256:" + "c" * 64,
            "old_launcher_sha256": m.digest(old),
            "new_launcher_sha256": "d" * 64,
            "candidate_operator_sha256": "e" * 64,
            "nonce": nonce,
        }
        with tempfile.TemporaryDirectory(prefix="qa525-committed-history-") as td:
            state = Path(td)
            backup = state / ("old-" + nonce + ".bin")
            receipt = state / ("receipt-" + nonce + ".json")
            backup.write_bytes(old)
            backup.chmod(0o700)
            receipt.write_bytes(m.json_bytes(record))
            receipt.chmod(0o600)
            if corruption == "backup":
                backup.write_bytes(b"forged recovery target\n")
            if corruption == "source":
                receipt.write_bytes(m.json_bytes(
                    dict(record, source_sha="WRONG_COMMIT")))
            if corruption == "field":
                receipt.write_bytes(m.json_bytes(
                    dict(record, unauthorized="true")))
            if corruption == "new_equals_old":
                receipt.write_bytes(m.json_bytes(
                    dict(record, new_launcher_sha256=m.digest(old))))
            if corruption == "candidate":
                receipt.write_bytes(m.json_bytes(
                    dict(record, candidate_operator_sha256="bad")))
            if corruption == "duplicate":
                receipt.write_bytes(
                    b'{"status":"LAUNCHER_ATOMIC_SWAP_COMMITTED",'
                    b'"status":"LAUNCHER_ATOMIC_SWAP_COMMITTED"}')
            if corruption == "no_backup":
                backup.unlink()
            with patch.object(m, "STATE", state), \
                 patch.object(m, "root_chain"), \
                 patch.object(m, "read_bound",
                     side_effect=lambda path, maximum=4096, exact_mode=None:
                     Path(path).read_bytes()):
                if corruption is None:
                    self.assertEqual({nonce}, m.scan_history())
                else:
                    with self.assertRaises(m.Blocked):
                        m.scan_history()

    def test_committed_receipt_and_original_backup_can_be_replayed_as_history(self):
        self.check()

    def test_corrupt_historical_receipts_and_backups_fail_closed(self):
        for corruption in (
                "backup", "source", "field", "new_equals_old",
                "candidate", "duplicate", "no_backup"):
            with self.subTest(corruption=corruption):
                self.check(corruption=corruption)


class IndependentBootstrapAdmissionTests(unittest.TestCase):
    """Cross-check an externally provisioned receipt, never forge a signature."""

    def fixture(self, *, alter=None, bad_sha=False, missing=False,
                time_case=None):
        from contextlib import ExitStack
        with tempfile.TemporaryDirectory(prefix="qa525-bootstrap-read-") as td:
            base = Path(td)
            root_source = base / "signed-root-entry.py"
            helper = base / "signed-helper.py"
            root_source.write_bytes(b"print('root launcher synthetic')\n")
            helper.write_bytes(b"print('helper synthetic')\n")
            admission = base / "admission.json"
            identity = {"source_sha": SHA,
                        "release_asset_sha256": "sha256:" + "c" * 64}
            now = int(__import__("time").time())
            packet = {
                "status": m.BOOTSTRAP_STATUS,
                **identity,
                "issued_at": now - 10,
                "expires_at": now + 800,
                "manifest_sha256": "e" * 64,
                "trusted_verifier_sha256": "d" * 64,
                "files": {str(path): m.digest(path.read_bytes())
                          for path in (root_source, helper)},
            }
            if bad_sha:
                packet["source_sha"] = "invalid"
            if time_case == "expired":
                packet["expires_at"] = now - 1
            elif time_case == "future":
                packet["issued_at"] = now + 30
            elif time_case == "too_long":
                packet["expires_at"] = now + 901
            elif time_case == "bool":
                packet["issued_at"] = True
            elif time_case == "missing":
                del packet["expires_at"]
            if missing:
                del packet["files"][str(helper)]
            if alter == "source":
                root_source.write_bytes(b"print('unreviewed root code')\n")
            if alter == "admission":
                packet["status"] = "AUTHORIZE_UNCHECKED_ROOT_EXECUTION"
            admission.write_bytes(m.json_bytes(packet))
            with ExitStack() as stack:
                stack.enter_context(patch.object(m, "require_host"))
                stack.enter_context(patch.object(m, "root_chain"))
                stack.enter_context(patch.object(m, "SOURCES",
                                                (root_source, helper)))
                stack.enter_context(patch.object(m, "ADMISSION", admission))
                stack.enter_context(patch.object(m, "read_bound",
                    side_effect=lambda path, maximum=4096, exact_mode=None:
                    Path(path).read_bytes()))
                if alter or bad_sha or missing or time_case:
                    with self.assertRaises(m.Blocked):
                        m.bootstrap_trusted_sources()
                    with patch.object(m, "load") as imported:
                        with self.assertRaises(m.Blocked):
                            m.verify_inputs()
                        imported.assert_not_called()
                else:
                    self.assertEqual(packet, m.bootstrap_trusted_sources())
                    self.assertEqual(packet, m.verify_bootstrap_binding(identity))
                    with self.assertRaisesRegex(
                            m.Blocked, "ROOT_BOOTSTRAP_SIGNED_RELEASE_IDENTITY_MISMATCH"):
                        m.verify_bootstrap_binding(
                            {**identity, "source_sha": "f" * 40})
                    with self.assertRaisesRegex(
                            m.Blocked, "ROOT_BOOTSTRAP_SIGNED_RELEASE_IDENTITY_MISMATCH"):
                        m.verify_bootstrap_binding(
                            {**identity,
                             "release_asset_sha256": "sha256:" + "f" * 64})

    def test_valid_preimport_admission_matches_all_helper_bytes(self):
        self.fixture()

    def test_expired_future_overlong_boolean_and_missing_admissions_fail_before_import(self):
        for case in ("expired", "future", "too_long", "bool", "missing"):
            with self.subTest(case=case):
                self.fixture(time_case=case)

    def test_bootstrap_admission_freshness_rejects_boundary_and_bad_clock(self):
        packet = {"issued_at": 1_000, "expires_at": 1_900}
        m.require_bootstrap_fresh(packet, 1_899)
        for changed, now in (
                (packet, 1_900),
                (packet, 999),
                ({"issued_at": 1_000, "expires_at": 1_901}, 1_500),
                ({"issued_at": 1_000, "expires_at": True}, 1_500),
                ({"issued_at": 1_000}, 1_500)):
            with self.assertRaisesRegex(
                    m.Blocked, "ROOT_BOOTSTRAP_ADMISSION_EXPIRED_OR_INVALID"):
                m.require_bootstrap_fresh(changed, now)

    def test_edited_root_installer_is_rejected_before_module_import(self):
        self.fixture(alter="source")

    def test_forged_admission_status_does_not_grant_execution(self):
        self.fixture(alter="admission")

    def test_missing_signed_helper_fails_closed(self):
        self.fixture(missing=True)

    def test_invalid_source_sha_fails_closed(self):
        self.fixture(bad_sha=True)

    def test_missing_admission_never_attempts_to_import_helpers(self):
        with patch.object(m, "require_host"), \
             patch.object(m, "root_chain",
                          side_effect=m.Blocked("ROOT_PATH_MISSING")), \
             patch.object(m, "load") as imported:
            with self.assertRaisesRegex(m.Blocked, "ROOT_PATH_MISSING"):
                m.verify_inputs()
            imported.assert_not_called()

    def test_no_bootstrap_bypass_cli_or_receipt_creation(self):
        code = SOURCE.read_text()
        self.assertIn('ADMISSION = Path("/etc/pmgm/qa525-bootstrap-admission.json")',
                      code)
        self.assertIn("verify_bootstrap_binding(plan)", code)
        self.assertIn("ROOT_BOOTSTRAP_SIGNED_SOURCE_HASH_MISMATCH", code)
        self.assertNotIn("--ignore-admission", code)
        self.assertNotIn("write_once(ADMISSION", code)


class ReadOnlyDiagnosticCliTests(unittest.TestCase):
    """A diagnostic CLI must never become an install/recovery capability."""

    def test_diagnostic_requires_bootstrap_and_does_not_mutate(self):
        report = {
            "status": "MANUAL_RECONCILIATION_REQUIRED_NO_AUTOREPAIR",
            "observed_phase": "POST_ATOMIC_SWAP",
            "nonce_sha256": m.digest(NONCE.encode("ascii")),
            "may_resume": False, "may_apply": False,
            "rollback_authorized": False,
        }
        with patch.object(m, "bootstrap_trusted_sources",
                          return_value={"status": m.BOOTSTRAP_STATUS}) as gate, \
             patch.object(m, "inspect_pending_reconciliation",
                          return_value=report) as inspect, \
             patch.object(m, "replace_approved") as install, \
             patch("sys.stdout", new_callable=io.StringIO) as output:
            self.assertEqual(0, m.main(["--inspect-pending"]))
            self.assertEqual(report, json.loads(output.getvalue()))
            gate.assert_called_once_with()
            inspect.assert_called_once_with()
            install.assert_not_called()

    def test_missing_bootstrap_blocks_read_only_diagnostics_before_inspection(self):
        with patch.object(m, "bootstrap_trusted_sources",
                          side_effect=m.Blocked("ROOT_PATH_MISSING")), \
             patch.object(m, "inspect_pending_reconciliation") as inspect, \
             patch.object(m, "replace_approved") as install, \
             patch("sys.stdout", new_callable=io.StringIO) as output:
            self.assertEqual(2, m.main(["--inspect-pending"]))
            result = json.loads(output.getvalue())
            self.assertEqual("BLOCKED_QA525_LAUNCHER_INSPECTION",
                             result["status"])
            self.assertEqual("ROOT_PATH_MISSING", result["code"])
            self.assertFalse(result["may_apply"])
            inspect.assert_not_called()
            install.assert_not_called()

    def test_two_actions_cannot_be_requested_together(self):
        with patch.object(m, "replace_approved") as install, \
             patch.object(m, "inspect_pending_reconciliation") as inspect, \
             patch("sys.stderr", new_callable=io.StringIO):
            with self.assertRaises(SystemExit):
                m.main(["--replace-approved", "--inspect-pending"])
            install.assert_not_called()
            inspect.assert_not_called()


class ReadOnlyPendingJournalTests(unittest.TestCase):
    """Never resume or roll back based solely on an on-disk journal."""

    def check(self, *, post=False, receipt=False, corrupt=None,
              historical=None):
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
            if historical is not None:
                previous_nonce = "c" * 64
                previous_old = b"previous known signed launcher\n"
                history_old = journal / ("old-" + previous_nonce + ".bin")
                history_old.write_bytes(previous_old)
                history_old.chmod(0o700)
                if historical != "missing_receipt":
                    history_record = {
                        "status": "LAUNCHER_ATOMIC_SWAP_COMMITTED",
                        "source_sha": "d" * 40,
                        "release_asset_sha256": "sha256:" + "e" * 64,
                        "old_launcher_sha256": m.digest(previous_old),
                        "new_launcher_sha256": "f" * 64,
                        "candidate_operator_sha256": "a" * 64,
                        "nonce": previous_nonce,
                    }
                    if historical == "forged_receipt":
                        history_record["new_launcher_sha256"] = "wrong"
                    history_receipt = journal / ("receipt-" + previous_nonce + ".json")
                    history_receipt.write_bytes(m.json_bytes(history_record))
                    history_receipt.chmod(0o600)
                if historical == "forged_backup":
                    history_old.write_bytes(b"forged previous backup\n")
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
            if corrupt == "intent_equals_old":
                intent.write_bytes(m.json_bytes(
                    dict(act, new_launcher_sha256=m.digest(old))))
            if corrupt == "unknown_entry":
                (journal / "unknown-journal.tmp").write_bytes(b"junk")
            if corrupt in ("receipt_extra", "receipt_missing_candidate",
                           "receipt_wrong_candidate"):
                committed = dict(
                    act, status="LAUNCHER_ATOMIC_SWAP_COMMITTED",
                    candidate_operator_sha256="d"*64)
                if corrupt == "receipt_extra":
                    committed["unauthorized"] = True
                if corrupt == "receipt_missing_candidate":
                    del committed["candidate_operator_sha256"]
                if corrupt == "receipt_wrong_candidate":
                    committed["candidate_operator_sha256"] = "WRONG_HASH"
                (journal / ("receipt-" + NONCE + ".json")).write_bytes(
                    m.json_bytes(committed))
            before = {x.name: x.read_bytes() for x in journal.iterdir()}
            with patch.object(m, "STATE", journal), \
                 patch.object(m, "LAUNCHER", launcher), \
                 patch.object(m, "require_host"), \
                 patch.object(m, "root_chain"), \
                 patch.object(m, "all_five_locks",
                              return_value=nullcontext()) as acquired, \
                 patch.object(m, "read_bound",
                              side_effect=lambda path, maximum=4096, exact_mode=None:
                              Path(path).read_bytes()), \
                 patch.object(m, "write_once") as writer, \
                 patch.object(m.os, "replace") as swap:
                if corrupt or historical in (
                        "forged_receipt", "forged_backup",
                        "missing_receipt"):
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
                    self.assertNotIn("nonce", report)
                    self.assertEqual(m.digest(NONCE.encode("ascii")),
                                     report["nonce_sha256"])
                    self.assertTrue(report["backup_hash_matches_intent"])
                    self.assertEqual(1 if historical == "valid" else 0,
                                     report["historical_receipts_verified"])
                    for key in ("signature_verified_by_inspection",
                                "rollback_authorized", "may_resume", "may_apply"):
                        self.assertFalse(report[key])
                acquired.assert_called_once_with(read_only=True)
                writer.assert_not_called()
                swap.assert_not_called()
                self.assertEqual(before,
                    {x.name: x.read_bytes() for x in journal.iterdir()})

    def test_pending_before_swap_read_only(self):
        self.check(post=False)

    def test_pending_with_valid_prior_receipt_validates_history_read_only(self):
        self.check(historical="valid")

    def test_pending_with_forged_prior_receipt_or_backup_fails_closed(self):
        for variant in ("forged_receipt", "forged_backup",
                        "missing_receipt"):
            with self.subTest(variant=variant):
                self.check(historical=variant)

    def test_pending_after_swap_read_only(self):
        self.check(post=True)

    def test_committed_receipt_with_unremoved_intent_still_requires_human(self):
        self.check(post=True, receipt=True)

    def test_corrupt_backup_live_receipt_or_missing_journal_block(self):
        for case, post in (("backup", False), ("unknown_live", False),
                           ("receipt", True), ("duplicate", False),
                           ("missing_backup", False),
                           ("intent_equals_old", False),
                           ("unknown_entry", False),
                           ("receipt_extra", True),
                           ("receipt_missing_candidate", True),
                           ("receipt_wrong_candidate", True)):
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


# Include disposable reversal regressions in the existing QA525 root-review gate.
from test_qa525_launcher_recovery_sandbox import RecoverySandboxTests



class ReversalPolicyGateTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        src = ROOT / "scripts/qa/operators/qa525-launcher-replacement-contract.py"
        sp = importlib.util.spec_from_file_location("qa525_reversal_policy", src)
        cls.policy = importlib.util.module_from_spec(sp)
        sys.modules[sp.name] = cls.policy
        sp.loader.exec_module(cls.policy)

    def setUp(self):
        self.receipt = {
            "status": "LAUNCHER_ATOMIC_SWAP_COMMITTED",
            "source_sha": "a" * 40, "release_asset_sha256": "sha256:" + "b" * 64,
            "old_launcher_sha256": "c" * 64,
            "new_launcher_sha256": "d" * 64,
            "candidate_operator_sha256": "e" * 64,
            "nonce": "1" * 64,
        }
        self.approval = {
            "action": "rollback-root-launcher-supervised",
            "scope": self.policy.RECOVERY_SCOPE,
            "environment": self.policy.ENVIRONMENT,
            "operator": "soporte@glmdch", "approved": True,
            "cut_nonce": self.receipt["nonce"], "nonce": "2" * 64,
            "source_sha": self.receipt["source_sha"],
            "release_asset_sha256": self.receipt["release_asset_sha256"],
            "old_launcher_sha256": self.receipt["old_launcher_sha256"],
            "new_launcher_sha256": self.receipt["new_launcher_sha256"],
            "issued_at": 100, "expires_at": 200,
        }

    def review(self, *, receipt=None, approval=None, current=None,
               backup=None, pending=False, consumed=()):
        return self.policy.assess_reversal(
            self.receipt if receipt is None else receipt,
            self.approval if approval is None else approval,
            current_launcher_sha256=self.receipt["new_launcher_sha256"]
                if current is None else current,
            backup_sha256=self.receipt["old_launcher_sha256"]
                if backup is None else backup,
            pending=pending, now=120, consumed_nonces=consumed)

    def test_distinct_approval_yields_review_only_never_rollback_permission(self):
        outcome = self.review()
        self.assertEqual("REVIEWED_LAUNCHER_REVERSAL_PLAN_NO_INSTALL",
                         outcome["status"])
        for flag in ("may_install", "may_apply", "may_recover",
                     "rollback_complete"):
            self.assertFalse(outcome[flag])
        self.assertTrue(outcome["requires_independent_signed_root_executor"])

    def test_nonce_replay_and_expired_forged_approvals_rejected(self):
        for changed in ({"nonce": self.receipt["nonce"]},
                        {"nonce": "3" * 64},
                        {"approved": False},
                        {"scope": "API_AND_WEB"},
                        {"cut_nonce": "7" * 64},
                        {"source_sha": "f" * 40},
                        {"expires_at": 120},
                        {"extra": True}):
            approval = dict(self.approval, **changed)
            with self.subTest(changed=changed):
                if changed == {"nonce": "3" * 64}:
                    with self.assertRaises(self.policy.Blocked):
                        self.review(approval=approval, consumed=("3" * 64,))
                else:
                    with self.assertRaises(self.policy.Blocked):
                        self.review(approval=approval)

    def test_pending_unknown_live_and_bad_backup_rejected(self):
        with self.assertRaises(self.policy.Blocked):
            self.review(pending=True)
        with self.assertRaises(self.policy.Blocked):
            self.review(current="0" * 64)
        with self.assertRaises(self.policy.Blocked):
            self.review(backup="0" * 64)
        with self.assertRaises(self.policy.Blocked):
            self.review(receipt={**self.receipt, "status": "INCOMPLETE"})





class RootLauncherReversalTransactionTests(unittest.TestCase):
    """Real fsync and os.replace in disposable paths, never physical host."""

    def setUp(self):
        import time
        self.temp = tempfile.TemporaryDirectory(prefix="qa525-root-reverse-")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.state = self.root / "state"
        self.recovery = self.root / "recovery"
        self.bin = self.root / "bin"
        for p in (self.state, self.recovery, self.bin):
            p.mkdir(mode=0o700)
        self.launcher = self.bin / "pmgm-qa-update"
        self.old = b"original launcher verified\n"
        self.new = b"synthetic signed replacement\n"
        self.launcher.write_bytes(self.new)
        self.launcher.chmod(0o700)
        self.cut = "1" * 64
        self.nonce = "2" * 64
        self.sha = "a" * 40
        self.release = "sha256:" + "b" * 64
        self.receipt = {
            "status": "LAUNCHER_ATOMIC_SWAP_COMMITTED",
            "source_sha": self.sha, "release_asset_sha256": self.release,
            "old_launcher_sha256": m.digest(self.old),
            "new_launcher_sha256": m.digest(self.new),
            "candidate_operator_sha256": "e" * 64,
            "nonce": self.cut,
        }
        old = self.state / ("old-" + self.cut + ".bin")
        old.write_bytes(self.old)
        old.chmod(0o700)
        saved = self.state / ("receipt-" + self.cut + ".json")
        saved.write_bytes(m.json_bytes(self.receipt))
        saved.chmod(0o600)
        self.approval_file = self.root / "recovery-approval.json"
        self.now = int(time.time())
        self.approval = {
            "action": "rollback-root-launcher-supervised",
            "scope": "QA525_LAUNCHER_ONLY_RECOVERY_NO_DOCKER_NO_IDENTITY_NO_MIGRATION",
            "environment": "QA_GLMDCH_ONLY",
            "operator": "soporte@glmdch", "approved": True,
            "cut_nonce": self.cut, "nonce": self.nonce,
            "source_sha": self.sha, "release_asset_sha256": self.release,
            "old_launcher_sha256": m.digest(self.old),
            "new_launcher_sha256": m.digest(self.new),
            "issued_at": self.now - 5, "expires_at": self.now + 120,
        }
        self.save_approval()
        self.packet = {
            "source_sha": self.sha, "release_asset_sha256": self.release,
            "issued_at": self.now - 5, "expires_at": self.now + 120,
        }

    def save_approval(self):
        self.approval_file.write_bytes(m.json_bytes(self.approval))
        self.approval_file.chmod(0o600)

    def run_reversal(self):
        with patch.object(m, "STATE", self.state), \
             patch.object(m, "RECOVERY_STATE", self.recovery), \
             patch.object(m, "RECOVERY_APPROVAL", self.approval_file), \
             patch.object(m, "LAUNCHER", self.launcher), \
             patch.object(m, "require_host"), \
             patch.object(m, "root_chain"), \
             patch.object(m, "all_five_locks", return_value=nullcontext()) as locks, \
             patch.object(m, "bootstrap_trusted_sources",
                          return_value=self.packet) as admitted, \
             patch.object(m, "read_bound",
                          side_effect=lambda path, maximum=4096, exact_mode=None:
                          Path(path).read_bytes()):
            result = m.rollback_approved()
            admitted.assert_called_once()
            locks.assert_called_once()
            return result

    def test_separate_approved_root_reversal_preserves_old_receipt(self):
        result = self.run_reversal()
        self.assertEqual("QA525_LAUNCHER_ROLLED_BACK_NO_API_WEB_DEPLOY",
                         result["status"])
        self.assertEqual(self.old, self.launcher.read_bytes())
        self.assertEqual(self.new, (self.recovery /
            ("rollback-new-" + self.nonce + ".bin")).read_bytes())
        self.assertTrue((self.recovery /
            ("rollback-receipt-" + self.nonce + ".json")).is_file())
        self.assertFalse((self.recovery /
            ("rollback-intent-" + self.nonce + ".json")).exists())
        self.assertTrue((self.state /
            ("receipt-" + self.cut + ".json")).is_file())
        self.assertFalse(result["api_web_changed"])
        self.assertFalse(result["may_apply"])
        with self.assertRaises(m.Blocked):
            self.run_reversal()

    def test_invalid_approval_or_pending_cut_never_writes(self):
        self.approval["approved"] = False
        self.save_approval()
        with self.assertRaises(m.Blocked):
            self.run_reversal()
        self.assertEqual(self.new, self.launcher.read_bytes())
        self.assertFalse(any(self.recovery.iterdir()))
        self.approval["approved"] = True
        self.save_approval()
        pending = self.state / ("intent-" + self.cut + ".json")
        pending.write_text("uncertain")
        with self.assertRaises(m.Blocked):
            self.run_reversal()
        self.assertEqual(self.new, self.launcher.read_bytes())
        self.assertFalse(any(self.recovery.iterdir()))

    def test_recovery_interruption_after_durable_intent_blocks_retry(self):
        before = m.write_once
        def interrupt(path, content, mode=0o600):
            if Path(path).name.startswith("rollback-new-"):
                raise RuntimeError("synthetic crash before backup")
            return before(path, content, mode=mode)
        with patch.object(m, "write_once", side_effect=interrupt):
            with self.assertRaisesRegex(RuntimeError, "synthetic crash"):
                self.run_reversal()
        self.assertEqual(self.new, self.launcher.read_bytes())
        self.assertEqual(["rollback-intent-" + self.nonce + ".json"],
                         [x.name for x in self.recovery.iterdir()])
        with self.assertRaises(m.Blocked):
            self.run_reversal()

    def test_recovery_interruption_after_swap_blocks_retry(self):
        original = m.os.replace
        def crash_after_atomic_replace(source, dest):
            original(source, dest)
            raise RuntimeError("synthetic crash after atomic swap")
        with patch.object(m.os, "replace", side_effect=crash_after_atomic_replace):
            with self.assertRaisesRegex(RuntimeError, "synthetic crash"):
                self.run_reversal()
        self.assertEqual(self.old, self.launcher.read_bytes())
        self.assertTrue((self.recovery /
            ("rollback-intent-" + self.nonce + ".json")).is_file())
        with self.assertRaises(m.Blocked):
            self.run_reversal()




if __name__ == "__main__":
    unittest.main()
