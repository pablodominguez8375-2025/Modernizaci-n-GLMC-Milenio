"""Hermetic adversarial retained QA525 source inspection: NO physical QA."""
import ast
import hashlib
import importlib.util
import os
from pathlib import Path
import socket
import stat
import sys
import tempfile
import types
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "scripts/qa/operators/qa525-launcher-retained-source-inspect.py"
spec = importlib.util.spec_from_file_location("qa525_retained_source_test", SOURCE)
m = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = m
spec.loader.exec_module(m)
SHA = "a" * 40
DATA = b"#!/usr/bin/python3\nprint('content not executed')\n"
DIGEST = hashlib.sha256(DATA).hexdigest()


def fake_stat(*, file=False, owner=0, mode=None, links=1, size=len(DATA),
              device=100, inode=200, modified=300):
    return types.SimpleNamespace(
        st_uid=owner, st_mode=mode if mode is not None else
        ((stat.S_IFREG | 0o644) if file else (stat.S_IFDIR | 0o755)),
        st_nlink=links, st_dev=device, st_ino=inode, st_size=size,
        st_mtime_ns=modified)


class RetainedSourceTests(unittest.TestCase):
    def test_exact_sha_path_is_required(self):
        path = m.source_path(SHA)
        self.assertEqual(str(path),
            "/opt/centenario/qa525-signed-stage/" + SHA +
            "/scripts/qa/operators/qa525-update.py")
        for value in (None, True, b"a"*40, "dev", "/etc/shadow",
                      "../"+SHA, SHA.upper(), SHA+"0", SHA[:39],
                      "qa-signed-"+SHA, SHA+"/../../etc/passwd"):
            with self.subTest(value=value), self.assertRaisesRegex(
                    m.Blocked, "RETAINED_SOURCE_SHA_INVALID"):
                m.source_path(value)

    def test_chain_rejects_unsafe_ancestors_file_and_links(self):
        path = m.source_path(SHA)
        private = {m.ROOT, m.ROOT / SHA}
        def check_with(changed, replacement):
            def info(item):
                if item == changed:
                    return replacement
                if item == path:
                    return fake_stat(file=True)
                return fake_stat(mode=stat.S_IFDIR |
                                 (0o700 if item in private else 0o755))
            with patch.object(Path, "lstat", info):
                return m.trusted_chain(path)
        self.assertEqual(200, check_with(None, None).st_ino)
        cases = [
            (path, fake_stat(file=True, owner=1000)),
            (path, fake_stat(file=True, links=2)),
            (path, fake_stat(file=True, mode=stat.S_IFLNK | 0o777)),
            (path, fake_stat(file=True, mode=stat.S_IFREG | 0o666)),
            (m.ROOT, fake_stat(mode=stat.S_IFDIR | 0o755)),
            (m.ROOT / SHA, fake_stat(mode=stat.S_IFDIR | 0o755)),
            (path.parent, fake_stat(mode=stat.S_IFDIR | 0o777)),
            (path.parent, fake_stat(mode=stat.S_IFREG | 0o644)),
        ]
        for changed, replacement in cases:
            with self.subTest(changed=str(changed)), self.assertRaises(m.Blocked):
                check_with(changed, replacement)

    def test_wrong_host_blocks_before_touching_any_path(self):
        with patch.object(m.os, "geteuid", return_value=0), \
             patch.object(m.socket, "gethostname", return_value="srv01"), \
             patch.object(m, "source_path") as getpath:
            with self.assertRaisesRegex(m.Blocked, "RETAINED_SOURCE_ONLY_GLMDCH_ROOT"):
                m.inspect_retained(SHA, DIGEST)
            getpath.assert_not_called()
        with patch.object(m.os, "geteuid", return_value=1000), \
             patch.object(m.socket, "gethostname", return_value="glmdch"), \
             patch.object(m, "source_path") as getpath:
            with self.assertRaises(m.Blocked):
                m.inspect_retained(SHA, DIGEST)
            getpath.assert_not_called()

    def test_exact_file_read_is_not_sigstore_or_install_authority(self):
        with tempfile.TemporaryDirectory() as td:
            source = Path(td) / "source.py"
            source.write_bytes(DATA)
            s = source.stat()
            record = fake_stat(file=True, size=len(DATA),
                               device=s.st_dev, inode=s.st_ino,
                               modified=s.st_mtime_ns)
            with patch.object(m.os, "geteuid", return_value=0), \
                 patch.object(m.socket, "gethostname", return_value="glmdch"), \
                 patch.object(m, "source_path", return_value=source), \
                 patch.object(m, "trusted_chain", return_value=record), \
                 patch.object(m.os, "fstat", return_value=record):
                response = m.inspect_retained(SHA, DIGEST)
                self.assertEqual(DIGEST, response["operator_sha256"])
                self.assertFalse(response["signature_verified_by_inspector"])
                self.assertFalse(response["manifest_verified_by_inspector"])
                self.assertFalse(response["package_persisted_by_inspector"])
                self.assertFalse(response["may_apply"])
                self.assertFalse(response["may_install"])
                with self.assertRaisesRegex(m.Blocked,
                                            "RETAINED_SOURCE_BYTES_DIVERGED"):
                    m.inspect_retained(SHA, "f"*64)

    def test_modified_inode_size_and_permissions_block(self):
        with tempfile.TemporaryDirectory() as td:
            file = Path(td) / "source.py"
            file.write_bytes(DATA)
            real = file.stat()
            before = fake_stat(file=True, size=len(DATA),
                               device=real.st_dev, inode=real.st_ino,
                               modified=real.st_mtime_ns)
            for current in (
                fake_stat(file=True, inode=real.st_ino + 1, device=real.st_dev),
                fake_stat(file=True, inode=real.st_ino, device=real.st_dev,
                          size=len(DATA)+1),
                fake_stat(file=True, inode=real.st_ino, device=real.st_dev,
                          modified=real.st_mtime_ns+1),
                fake_stat(file=True, inode=real.st_ino, device=real.st_dev,
                          owner=1000),
                fake_stat(file=True, inode=real.st_ino, device=real.st_dev,
                          mode=stat.S_IFREG | 0o666),
            ):
                with self.subTest(current=current), \
                     patch.object(m.os, "fstat", return_value=current):
                    with self.assertRaises(m.Blocked):
                        m.read_exact(file, before)

    def test_expected_hash_validation_and_no_mutation(self):
        for value in (True, b"x"*64, None, "a"*63, "g"*64, "sha256:"+DIGEST):
            with patch.object(m.os, "geteuid", return_value=0), \
                 patch.object(m.socket, "gethostname", return_value="glmdch"), \
                 patch.object(m, "trusted_chain") as chain:
                with self.subTest(value=value), self.assertRaisesRegex(
                        m.Blocked, "RETAINED_SOURCE_EXPECTED_HASH_INVALID"):
                    m.inspect_retained(SHA, value)
                chain.assert_not_called()
        tree = ast.parse(SOURCE.read_text())
        imports = [a.name for node in ast.walk(tree) if isinstance(node, ast.Import)
                   for a in node.names]
        self.assertFalse(set(imports) & {"subprocess", "fcntl", "shutil",
                                         "urllib", "requests", "docker"})
        forbidden = {"system", "execv", "execve", "replace", "rename", "unlink",
                     "remove", "chown", "chmod", "write_bytes", "write_text",
                     "mkdir", "makedirs", "popen"}
        for node in ast.walk(tree):
            if isinstance(node, ast.Call) and isinstance(node.func, ast.Attribute):
                self.assertNotIn(node.func.attr, forbidden)
        self.assertNotIn("O_CREAT", SOURCE.read_text())
        self.assertNotIn("O_WRONLY", SOURCE.read_text())
        self.assertIn("LIBRARY_ONLY_NO_INSTALL_ENTRYPOINT", SOURCE.read_text())


if __name__ == "__main__":
    unittest.main()
