"""Schema effects (offline): root-owned file gates, abort marker, closed runner, fail-closed wiring."""
import importlib.util
import os
from pathlib import Path
import tempfile
import unittest

HERE = Path(__file__).resolve().parent


def load(name, relative):
    spec = importlib.util.spec_from_file_location(name, HERE.parent / "scripts" / "qa" / relative)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


fx = load("sh_effects", "qa525-vnext-root-schema-effects.py")


class UATModule:
    @staticmethod
    def make(read_private, clock, expected_sha=None):
        return (lambda: True,)


class G6Module:
    @staticmethod
    def make(authority, claims, read_evidence, clock):
        return lambda data: True, 1


class SchemaCutEffectsTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        fx.ROOT_UID = os.getuid()
        self.addCleanup(setattr, fx, "ROOT_UID", 0)
        self.root = Path(self.tmp.name)
        os.chmod(self.root, 0o700)
        fx.ABORT_PATH = str(self.root / "abort")
        fx.UAT_CREDENTIAL_PATH = str(self.root / "uat")
        fx.bind_dependencies({"g6": G6Module, "uat": UATModule})

    def write(self, name, data=b"x", mode=0o600):
        p = self.root / name
        p.write_bytes(data)
        os.chmod(p, mode)
        return p

    def test_evidence_reads_only_safe_root_files(self):
        self.write("admission.json", b"{}")
        self.assertEqual(fx.read_evidence_factory(str(self.root))("admission"), b"{}")
        with self.assertRaises(fx.Stop):
            fx.read_evidence_factory(str(self.root))("unknown")
        self.write("approval.json", b"{}", 0o644)
        with self.assertRaises(fx.Stop):
            fx.read_evidence_factory(str(self.root))("approval")
        (self.root / "g6-review.json").symlink_to(self.root / "admission.json")
        with self.assertRaises(fx.Stop):
            fx.read_evidence_factory(str(self.root))("review")
        with self.assertRaises(fx.Stop):
            fx.read_evidence_factory("relative")

    def test_abort_marker_and_fail_closed_errors(self):
        self.assertFalse(fx.abort_requested())
        self.write("abort", b"")
        self.assertTrue(fx.abort_requested())
        fx.ABORT_PATH = "\0bad"
        self.assertTrue(fx.abort_requested())

    def test_schema_env_dir_exists_including_symlink_without_deletion(self):
        original = fx.SCHEMA_ENV_DIR
        self.addCleanup(setattr, fx, "SCHEMA_ENV_DIR", original)
        path = self.root / "schema-env"
        fx.SCHEMA_ENV_DIR = str(path)
        self.assertFalse(fx.schema_env_dir_exists())
        path.symlink_to(self.root / "nonexistent")
        self.assertTrue(fx.schema_env_dir_exists())
        self.assertTrue(path.is_symlink())
        path.unlink()
        path.mkdir()
        self.assertTrue(fx.schema_env_dir_exists())
        self.assertTrue(path.is_dir())

    def test_runner_is_closed_docker_only(self):
        for bad in (["/usr/bin/docker"], ("/bin/sh", "-c", "id"), (), ("/usr/bin/docker", "a\0"), "x"):
            with self.assertRaises(fx.Stop):
                fx.runner(bad, 10, None)
        with self.assertRaises(fx.Stop):
            fx.runner((fx.DOCKER, "version"), 0, None)

    def test_provide_stops_closed_until_every_reviewed_piece_is_wired(self):
        auth = {"host": {"evidence_dir": str(self.root)}}
        fx._DEPS.clear()
        with self.assertRaises(fx.Stop) as c:
            fx.provide(auth, {})
        self.assertEqual(str(c.exception), "SCHEMA_EFFECTS_NOT_WIRED")
        self.write("abort", b"")
        with self.assertRaises(fx.Stop) as c:
            fx.provide(auth, {})
        self.assertEqual(str(c.exception), "SCHEMA_EFFECTS_ABORT_REQUESTED")

    def test_provide_with_wiring_requires_credential_file_and_single_api_container(self):
        auth = {"host": {"evidence_dir": str(self.root)}}
        with self.assertRaises(fx.Stop) as c:  # credential absent
            fx.provide(auth, {}, run=lambda a, t, s: (0, ("a" * 64 + "\n").encode()))
        self.assertEqual(str(c.exception), "SCHEMA_EFFECTS_FILE_MISSING")
        self.write("uat", b"secret", 0o644)
        with self.assertRaises(fx.Stop) as c:  # group/other readable
            fx.provide(auth, {}, run=lambda a, t, s: (0, ("a" * 64 + "\n").encode()))
        self.assertEqual(str(c.exception), "SCHEMA_EFFECTS_FILE_UNSAFE")
        os.chmod(self.root / "uat", 0o600)
        eff = fx.provide(auth, {}, run=lambda a, t, s: (0, ("a" * 64 + "\n").encode()))
        self.assertEqual(eff.api_container_id, "a" * 64)
        self.assertNotIn("secret", repr(vars(eff)))
        for out in (b"", ("a" * 64 + "\n" + "b" * 64 + "\n").encode(), b"zz\n"):
            with self.assertRaises(fx.Stop):
                fx.provide(auth, {}, run=lambda a, t, s, o=out: (0, o))
        with self.assertRaises(fx.Stop):
            fx.provide(auth, {}, run=lambda a, t, s: (1, ("a" * 64 + "\n").encode()))


if __name__ == "__main__":
    unittest.main()
