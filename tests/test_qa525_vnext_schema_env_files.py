"""Lifecycle of ephemeral root-only env files: synthetic secrets in a private temp dir, no root, no host."""
import importlib.util
import os
from pathlib import Path
import stat
import tempfile
import unittest

BASE = Path(__file__).resolve().parents[1] / "scripts" / "qa"


def load(n, f):
    s = importlib.util.spec_from_file_location(n, BASE / f)
    m = importlib.util.module_from_spec(s)
    s.loader.exec_module(m)
    return m


ef = load("envfiles_t", "qa525-vnext-schema-env-files.py")
rd = load("envrender_t", "qa525-vnext-schema-env-render.py")
HISTORY = [f"20250101{i:06d}_Migration{i:03d}" for i in range(100)]
SECRET = b"synthetic-secret-123"
SOURCE = b"PMGM_QA_MIGRATOR_DB_PASSWORD=" + SECRET + b"\nPMGM_QA_DB_PASSWORD=ignored\n"
UID = os.geteuid()


class EnvFilesTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.base = self.tmp.name + "/qa525-env"

    def use(self, **kw):
        args = dict(base_dir=self.base, source_reader=lambda: SOURCE, history=HISTORY, render=rd.render,
                    verify_tmpfs=lambda p: p == self.tmp.name, uid=UID)
        args.update(kw)
        return ef.ephemeral_env_files(**args)

    def test_creates_0700_dir_0600_files_with_exact_content_and_removes_everything(self):
        with self.use() as (m, g):
            self.assertEqual(stat.S_IMODE(os.lstat(self.base).st_mode), 0o700)
            for p in (m, g):
                st = os.lstat(p)
                self.assertEqual((stat.S_IMODE(st.st_mode), st.st_nlink, st.st_uid), (0o600, 1, UID))
            self.assertEqual(Path(g).read_bytes(), b"PGPASSWORD=" + SECRET + b"\n")
            self.assertIn(b"QA525_DDL_CONNECTION=Host=postgres;", Path(m).read_bytes())
        self.assertFalse(os.path.exists(self.base))

    def test_cleanup_happens_when_the_body_fails_and_the_error_is_not_masked(self):
        with self.assertRaises(RuntimeError):
            with self.use():
                raise RuntimeError("boom")
        self.assertFalse(os.path.exists(self.base))

    def test_existing_directory_is_refused_and_never_deleted(self):
        os.mkdir(self.base, 0o700)
        marker = Path(self.base) / "keep"
        marker.write_bytes(b"x")
        with self.assertRaises(ef.Stop) as c:
            with self.use():
                self.fail("must not enter")
        self.assertEqual(str(c.exception), "SCHEMA_ENVFILES_DIR_EXISTS")
        self.assertTrue(marker.exists())

    def test_not_tmpfs_symlink_and_bad_inputs_are_refused_before_creating_anything(self):
        for kw in ({"verify_tmpfs": lambda p: False}, {"verify_tmpfs": lambda p: (_ for _ in ()).throw(OSError())},
                   {"base_dir": "relative/x"}, {"base_dir": self.tmp.name + "/a/../b"}, {"base_dir": self.base + "/"},
                   {"render": None}, {"uid": True}):
            with self.assertRaises(ef.Stop):
                with self.use(**kw):
                    self.fail("must not enter")
            self.assertFalse(os.path.exists(self.base))

    def test_hostile_source_or_history_creates_nothing_and_leaks_no_secret(self):
        for kw in ({"source_reader": lambda: b"PMGM_QA_MIGRATOR_DB_PASSWORD=bad;evil\n"},
                   {"source_reader": lambda: (_ for _ in ()).throw(OSError(SECRET.decode()))},
                   {"history": HISTORY[:99]}):
            with self.assertRaises(ef.Stop) as c:
                with self.use(**kw):
                    self.fail("must not enter")
            self.assertEqual(str(c.exception), "SCHEMA_ENVFILES_RENDER_FAILED")
            self.assertNotIn("synthetic", str(c.exception))
            self.assertFalse(os.path.exists(self.base))

    def test_wrong_owner_is_refused_and_cleaned(self):
        with self.assertRaises(ef.Stop):
            with self.use(uid=UID + 1):
                self.fail("must not enter")
        self.assertFalse(os.path.exists(self.base))

    def test_cleanup_failure_is_reported_not_silent(self):
        real_unlink = os.unlink
        try:
            os.unlink = lambda *a, **k: (_ for _ in ()).throw(PermissionError())
            with self.assertRaises(ef.Stop) as c:
                with self.use():
                    pass
            self.assertEqual(str(c.exception), "SCHEMA_ENVFILES_CLEANUP_FAILED")
        finally:
            os.unlink = real_unlink
        for name in ef.NAMES:  # contents were zero-truncated even though unlink failed
            p = Path(self.base) / name
            if p.exists():
                self.assertEqual(p.read_bytes(), b"")
                p.unlink()
        os.rmdir(self.base)


if __name__ == "__main__":
    unittest.main()
