"""Hermetic QA525 launcher-only root approval reader tests (no real host)."""
import ast
from pathlib import Path
import importlib.util
import os
import socket
import stat
import sys
import tempfile
import time
import types
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]
FILE = ROOT / "scripts/qa/operators/qa525-launcher-root-approval.py"
POLICY = ROOT / "scripts/qa/operators/qa525-launcher-replacement-contract.py"


def load(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec)
    sys.modules[name] = result
    spec.loader.exec_module(result)
    return result


approval_module = load("qa525_launcher_root_approval_test", FILE)
contract = load("qa525_launcher_replacement_policy_in_approval_test", POLICY)
NOW = 1793488800


def act():
    return {
        "source_sha": "a" * 40,
        "release_asset_sha256": "sha256:" + "b" * 64,
        "old_launcher_sha256": "c" * 64,
        "candidate_operator_sha256": "d" * 64,
        "scope": approval_module.SCOPE,
        "action": approval_module.ACTION,
        "environment": approval_module.ENVIRONMENT,
        "operator": approval_module.OPERATOR,
        "approved": True,
        "issued_at": NOW - 30,
        "expires_at": NOW + 60,
        "nonce": "e" * 64,
    }


class LauncherLocalApprovalTests(unittest.TestCase):
    def test_exact_act_is_diagnostic_only(self):
        record = approval_module.validate(act(), NOW)
        self.assertEqual("LOCAL_LAUNCHER_APPROVAL_SHAPE_MATCH_NO_INSTALL",
                         record["status"])
        self.assertFalse(record["may_install"])
        self.assertFalse(record["may_apply"])
        self.assertFalse(record["signed_release_verified_by_reader"])
        self.assertFalse(record["nonce_consumed_by_reader"])
        self.assertEqual(contract.SCOPE, approval_module.SCOPE)
        self.assertEqual(contract.ACTION, approval_module.ACTION)
        self.assertEqual(contract.ENVIRONMENT, approval_module.ENVIRONMENT)

    def test_fields_missing_extra_or_non_object_block(self):
        for bad in (None, [], True, "raw", dict(act(), extra="unexpected")):
            with self.subTest(bad=bad), self.assertRaises(approval_module.Blocked):
                approval_module.validate(bad, NOW)
        r = act()
        del r["nonce"]
        with self.assertRaisesRegex(approval_module.Blocked,
                                    "LAUNCHER_APPROVAL_FIELDS_INVALID"):
            approval_module.validate(r, NOW)

    def test_signed_identity_and_action_tampering_block(self):
        for key, value in (
            ("source_sha", "bad"), ("release_asset_sha256", "sha256:bad"),
            ("old_launcher_sha256", "z" * 64), ("candidate_operator_sha256", ""),
            ("nonce", "bad"), ("scope", "LOCAL_SUPERVISION_MATCH_ONLY_NO_APPLY"),
            ("action", "first-qa-api-web-supervised"),
            ("environment", "PRODUCTION"), ("operator", "admin@unknown"),
            ("approved", 1), ("approved", False)):
            data = act()
            data[key] = value
            with self.subTest(key=key, value=value), self.assertRaises(
                    approval_module.Blocked):
                approval_module.validate(data, NOW)

    def test_time_window_fail_closed_and_bool_not_integer(self):
        for issued, expires, now in (
            (NOW + 1, NOW + 100, NOW),
            (NOW - 900, NOW + 100, NOW),
            (NOW - 90, NOW, NOW),
            (NOW, NOW, NOW),
            (True, NOW + 1, NOW),
            (NOW - 1, True, NOW)):
            data = act()
            data.update(issued_at=issued, expires_at=expires)
            with self.subTest(issued=issued, expires=expires):
                with self.assertRaises(approval_module.Blocked):
                    approval_module.validate(data, now)
        with self.assertRaises(approval_module.Blocked):
            approval_module.validate(act(), True)

    def test_json_duplicate_keys_rejected_not_last_wins(self):
        with self.assertRaisesRegex(approval_module.Blocked,
                                    "LAUNCHER_APPROVAL_DUPLICATE_FIELD"):
            approval_module.no_duplicate_fields([("nonce", "a"), ("nonce", "b")])

    def test_wrong_host_rejected_before_approval_path_access(self):
        with patch.object(approval_module.os, "geteuid", return_value=0), \
             patch.object(approval_module.socket, "gethostname",
                          return_value="srv01"), \
             patch.object(approval_module, "trusted_chain") as chain:
            with self.assertRaisesRegex(approval_module.Blocked,
                                        "LAUNCHER_APPROVAL_ONLY_ROOT_GLMDCH"):
                approval_module.read_root_approval()
            chain.assert_not_called()
        with patch.object(approval_module.os, "geteuid", return_value=1000), \
             patch.object(approval_module.socket, "gethostname",
                          return_value="glmdch"), \
             patch.object(approval_module, "trusted_chain") as chain:
            with self.assertRaises(approval_module.Blocked):
                approval_module.read_root_approval()
            chain.assert_not_called()

    def test_trusted_root0600_posixpath_accepted_with_safe_ancestors(self):
        # Actual Ubuntu Path is PosixPath, never exact type Path. Mock only
        # root ownership of our temp directory/ancestors; no sudo or host QA.
        with tempfile.TemporaryDirectory(prefix="qa525-posixpath-") as td:
            file = Path(td) / "root0600-approval.json"
            file.write_text("{}")
            file.chmod(0o600)
            real_lstat = Path.lstat

            def root_metadata(path):
                actual = real_lstat(path)
                final = path == file
                return types.SimpleNamespace(
                    st_mode=(stat.S_IFREG | 0o600 if final
                             else stat.S_IFDIR | 0o755),
                    st_uid=0,
                    st_nlink=1 if final else actual.st_nlink,
                    st_dev=actual.st_dev,
                    st_ino=actual.st_ino,
                    st_size=actual.st_size,
                )

            with patch.object(Path, "lstat", autospec=True,
                              side_effect=root_metadata):
                observed = approval_module.trusted_chain(file)
                self.assertEqual(file.stat().st_size, observed.st_size)
                self.assertEqual(0, observed.st_uid)
                self.assertEqual(stat.S_IFREG,
                                 stat.S_IFMT(observed.st_mode))

    def test_real_valid_approval_read_has_no_untrusted_path_false_negative(self):
        import json
        with tempfile.TemporaryDirectory(prefix="qa525-root0600-") as td:
            file = Path(td) / "approval.json"
            file.write_text(json.dumps(act(), sort_keys=True))
            file.chmod(0o600)
            real_lstat = Path.lstat
            real_fstat = os.fstat

            def fake_owner(original, *, final):
                return types.SimpleNamespace(
                    st_mode=(stat.S_IFREG | 0o600 if final
                             else stat.S_IFDIR | 0o755),
                    st_uid=0,
                    st_nlink=1 if final else original.st_nlink,
                    st_dev=original.st_dev,
                    st_ino=original.st_ino,
                    st_size=original.st_size,
                )

            with patch.object(approval_module, "APPROVAL", file), \
                 patch.object(approval_module.os, "geteuid", return_value=0), \
                 patch.object(approval_module.socket, "gethostname",
                              return_value="glmdch"), \
                 patch.object(approval_module.time, "time",
                              return_value=NOW), \
                 patch.object(Path, "lstat", autospec=True,
                              side_effect=lambda p:
                              fake_owner(real_lstat(p), final=p == file)), \
                 patch.object(approval_module.os, "fstat",
                              side_effect=lambda fd:
                              fake_owner(real_fstat(fd), final=True)):
                result = approval_module.read_root_approval()
                self.assertEqual(act(), result)
                report = approval_module.validate(result, NOW)
                self.assertFalse(report["may_install"])
                self.assertFalse(report["may_apply"])

    def test_non_path_and_relative_inputs_still_rejected(self):
        for path in (None, True, "/etc/pmgm/approval.json",
                     Path("relative/approval.json"), 17):
            with self.subTest(path=path), self.assertRaisesRegex(
                    approval_module.Blocked, "LAUNCHER_APPROVAL_PATH_INVALID"):
                approval_module.trusted_chain(path)

    def test_real_fs_guard_rejects_symlink_hardlink_and_untrusted_ancestors(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            file = root / "act.json"
            file.write_text("{}")
            os.chmod(file, 0o600)
            alias = root / "alias.json"
            alias.symlink_to(file)
            with self.assertRaises(approval_module.Blocked):
                approval_module.trusted_chain(alias)
            with self.assertRaises(approval_module.Blocked):
                approval_module.trusted_chain(file)
            hardlink = root / "hardlink.json"
            os.link(file, hardlink)
            with self.assertRaises(approval_module.Blocked):
                approval_module.trusted_chain(file)

    def test_real_read_fails_closed_for_duplicate_json_or_invalid_scope(self):
        # Mock only the real-host ownership precondition. All bytes come from
        # a real file descriptor, opened O_RDONLY | O_NOFOLLOW.
        with tempfile.TemporaryDirectory() as temp:
            file = Path(temp) / "approval.json"
            file.write_text('{"nonce":"one","nonce":"two"}')
            os.chmod(file, 0o600)
            original = file.lstat()
            fake = types.SimpleNamespace(st_mode=stat.S_IFREG | 0o600,
                 st_uid=0, st_nlink=1, st_dev=original.st_dev,
                 st_ino=original.st_ino, st_size=original.st_size)
            with patch.object(approval_module, "APPROVAL", file), \
                 patch.object(approval_module.os, "geteuid", return_value=0), \
                 patch.object(approval_module.socket, "gethostname",
                              return_value="glmdch"), \
                 patch.object(approval_module, "trusted_chain", return_value=fake), \
                 patch.object(approval_module.os, "fstat", return_value=fake):
                with self.assertRaisesRegex(approval_module.Blocked,
                                            "LAUNCHER_APPROVAL_DUPLICATE_FIELD"):
                    approval_module.read_root_approval()
            file.write_text('{"scope":"NOT_QA"}')
            with patch.object(approval_module, "APPROVAL", file), \
                 patch.object(approval_module.os, "geteuid", return_value=0), \
                 patch.object(approval_module.socket, "gethostname",
                              return_value="glmdch"), \
                 patch.object(approval_module, "trusted_chain",
                              return_value=types.SimpleNamespace(
                                  st_dev=file.stat().st_dev,
                                  st_ino=file.stat().st_ino,
                                  st_size=file.stat().st_size)), \
                 patch.object(approval_module.os, "fstat",
                              return_value=types.SimpleNamespace(
                                  st_mode=stat.S_IFREG | 0o600, st_uid=0,
                                  st_nlink=1, st_dev=file.stat().st_dev,
                                  st_ino=file.stat().st_ino,
                                  st_size=file.stat().st_size)):
                with self.assertRaisesRegex(approval_module.Blocked,
                                            "LAUNCHER_APPROVAL_FIELDS_INVALID"):
                    approval_module.read_root_approval()

    def test_library_has_no_install_or_mutating_entrypoint(self):
        data = FILE.read_text()
        tree = ast.parse(data)
        forbidden = {"replace", "rename", "unlink", "remove", "chmod",
                     "chown", "system", "popen", "execv", "execve",
                     "write_text", "write_bytes"}
        for node in ast.walk(tree):
            if isinstance(node, ast.Call) and isinstance(node.func, ast.Attribute):
                self.assertNotIn(node.func.attr, forbidden)
        imports = [a.name for n in ast.walk(tree)
                   if isinstance(n, ast.Import) for a in n.names]
        self.assertNotIn("docker", imports)
        self.assertNotIn("O_CREAT", data)
        self.assertNotIn("O_WRONLY", data)
        self.assertIn("LIBRARY_ONLY_NO_INSTALL_ENTRYPOINT", data)


if __name__ == "__main__":
    unittest.main()
