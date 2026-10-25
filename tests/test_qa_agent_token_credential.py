"""Mocked ACL/mount regression: root-owned systemd token copy is narrowly scoped."""
import importlib.util
import os
from pathlib import Path
import stat
from types import SimpleNamespace
import tempfile
import unittest
from unittest.mock import patch

FILE = Path(__file__).resolve().parents[1] / "scripts/qa/qa-artifact-download.py"
spec = importlib.util.spec_from_file_location("qa_artifact_download_acl", FILE)
qa = importlib.util.module_from_spec(spec)
spec.loader.exec_module(qa)


class CredentialTests(unittest.TestCase):
    def test_only_root_acl_copy_in_protected_readonly_credentials_mount(self):
        name = "/run/credentials/pmgm-qa-agent-stage.service"
        token = Path(name) / "qa-token"
        fileinfo = SimpleNamespace(st_uid=0, st_mode=stat.S_IFREG | 0o440)
        rootdir = SimpleNamespace(st_uid=0, st_mode=stat.S_IFDIR | 0o700)
        with patch.dict(os.environ, {"CREDENTIALS_DIRECTORY": name}), \
             patch.object(qa.os, "stat", return_value=rootdir), \
             patch.object(qa.os, "statvfs", return_value=SimpleNamespace(f_flag=os.ST_RDONLY)):
            self.assertTrue(qa.is_systemd_token_copy(token, fileinfo))
            self.assertFalse(qa.is_systemd_token_copy(Path(name) / "other", fileinfo))
            self.assertFalse(qa.is_systemd_token_copy(token, SimpleNamespace(st_uid=1000, st_mode=fileinfo.st_mode)))
            self.assertFalse(qa.is_systemd_token_copy(token, SimpleNamespace(st_uid=0, st_mode=stat.S_IFREG | 0o644)))

    def test_writable_fake_mount_or_parent_blocks(self):
        name = "/run/credentials/pmgm-qa-agent-stage.service"
        token = Path(name) / "qa-token"
        fileinfo = SimpleNamespace(st_uid=0, st_mode=stat.S_IFREG | 0o440)
        with patch.dict(os.environ, {"CREDENTIALS_DIRECTORY": name}), \
             patch.object(qa.os, "stat", return_value=SimpleNamespace(st_uid=0, st_mode=stat.S_IFDIR | 0o777)), \
             patch.object(qa.os, "statvfs", return_value=SimpleNamespace(f_flag=os.ST_RDONLY)):
            self.assertFalse(qa.is_systemd_token_copy(token, fileinfo))
        with patch.dict(os.environ, {"CREDENTIALS_DIRECTORY": name}), \
             patch.object(qa.os, "stat", return_value=SimpleNamespace(st_uid=0, st_mode=stat.S_IFDIR | 0o700)), \
             patch.object(qa.os, "statvfs", return_value=SimpleNamespace(f_flag=0)):
            self.assertFalse(qa.is_systemd_token_copy(token, fileinfo))

    def test_ordinary_group_readable_token_is_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            token = Path(tmp) / "token"
            token.write_text("synthetic-not-secret")
            token.chmod(0o440)
            with self.assertRaisesRegex(qa.Blocked, "OWNER_OR_PERMISSIONS"):
                qa.read_token(token)
            token.chmod(0o600)
            self.assertEqual("synthetic-not-secret", qa.read_token(token))

    def test_unsafe_symlink_token_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            original = Path(tmp) / "token"
            original.write_text("synthetic")
            original.chmod(0o600)
            link = Path(tmp) / "alias"
            link.symlink_to(original)
            with self.assertRaisesRegex(qa.Blocked, "OWNER_OR_PERMISSIONS"):
                qa.read_token(link)


if __name__ == "__main__":
    unittest.main()
