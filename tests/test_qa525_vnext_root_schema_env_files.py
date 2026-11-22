"""Offline security tests for the root-only ephemeral schema env scope."""
import importlib.util
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

path = Path(__file__).resolve().parents[1] / "scripts/qa/qa525-vnext-root-schema-env-files.py"
spec = importlib.util.spec_from_file_location("qa525_schema_env_files", path)
mod = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mod)

class Plane:
    def assert_held(self):
        return True

class Renderer:
    def render(self, source, history):
        return b"QA525_DDL_CONNECTION=synthetic\\n", b"PGPASSWORD=synthetic\\n"

class SchemaEnvFilesTests(unittest.TestCase):
    def test_root_source_rejects_nonroot_or_unsafe_mode(self):
        with tempfile.TemporaryDirectory() as directory:
            path = directory + "/source"
            with open(path, "wb") as stream:
                stream.write(b"PMGM_QA_MIGRATOR_DB_PASSWORD=synthetic\\n")
            Path(path).chmod(0o644)  # unsafe even if tests run as root
            with patch.object(mod, "SOURCE", path):
                with self.assertRaises(mod.Stop):
                    mod._read_source()

    def test_scope_cleanup_on_success_and_exception(self):
        # Replace the root source and tmpfs guard only in this synthetic test.
        with tempfile.TemporaryDirectory() as root:
            real_stat = mod.os.stat
            def stat_mock(path, *args, **kwargs):
                result = real_stat(path, *args, **kwargs)
                if str(path) == root:
                    values = list(result)
                    values[4] = 0  # st_uid: root, synthetic only
                    return mod.os.stat_result(values)
                return result
            import builtins
            real_open = builtins.open
            class Mounts:
                def __enter__(self):
                    return iter(["tmpfs /run tmpfs rw 0 0"])
                def __exit__(self, *args):
                    return False
            def open_mock(path, *args, **kwargs):
                if path == "/proc/mounts":
                    return Mounts()
                return real_open(path, *args, **kwargs)
            with patch.object(mod, "TMP_ROOT", root), patch.object(mod, "_read_source", return_value=b"synthetic"), \
                 patch.object(mod.os, "stat", side_effect=stat_mock), patch.object(builtins, "open", side_effect=open_mock):
                for fail in (False, True):
                    with self.subTest(fail=fail):
                        try:
                            with mod.open_env(Plane(), Renderer(), []) as paths:
                                self.assertEqual(len(paths), 2)
                                self.assertTrue(all(Path(p).exists() for p in paths))
                                self.assertEqual(Path(paths[0]).stat().st_mode & 0o777, 0o600)
                                if fail:
                                    raise ValueError("synthetic")
                        except ValueError:
                            pass
                        self.assertEqual(list(Path(root).iterdir()), [])

if __name__ == "__main__":
    unittest.main()
