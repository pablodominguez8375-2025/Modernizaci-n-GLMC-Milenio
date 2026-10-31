"""Hermetic adversarial tests: launcher inspection cannot install or deploy."""
import ast
import importlib.util
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]
FILE = ROOT / "scripts/qa/operators/qa525-launcher-upgrade-inspect.py"
spec = importlib.util.spec_from_file_location("qa525_launcher_inspector_test", FILE)
inspector = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = inspector
spec.loader.exec_module(inspector)


def template(source="/opt/centenario/qa525-signed-stage/old/scripts/qa/operators/qa525-update.py"):
    return ("#!/usr/bin/python3\nimport os,sys\n" +
            'os.execv("/usr/bin/python3", ["/usr/bin/python3", "-B", ' +
            repr(source) + ', *sys.argv[1:]])\n')


class LauncherInspectionTests(unittest.TestCase):
    def test_exact_old_launcher_template_extracts_only_frozen_source(self):
        path = inspector.launcher_source_from_text(template())
        self.assertEqual("qa525-update.py", path.name)
        self.assertTrue(path.is_absolute())

    def test_reject_injected_code_and_untrusted_interpreter(self):
        variants = (
            template() + 'import subprocess\n',
            template().replace('import os,sys', 'import os,sys;print(1)'),
            template().replace('/usr/bin/python3', '/tmp/python3'),
            template().replace('"-B", ', '"-B", "-c", '),
            template().replace('sys.argv[1:]', 'sys.argv[:]'),
            template().replace('qa525-update.py', 'other-runner.py'),
            template().replace('/opt/centenario/', '../centenario/'),
            template().replace('qa525-update.py', 'qa525-update.py" + "x'),
        )
        for value in variants:
            with self.subTest(value=value[-65:]), self.assertRaises(inspector.Blocked):
                inspector.launcher_source_from_text(value)

    def test_strict_literal_rejects_eval_and_absolute_parent_escape(self):
        lines = (
            template().replace(
                "'/opt/centenario/qa525-signed-stage/old/scripts/qa/operators/qa525-update.py'",
                "__import__('os').system('echo bad')"),
            template('/opt/centenario/../tmp/qa525-update.py'),
            template('/tmp/qa525-update.py').replace('"/usr/bin/python3"', '"/bin/bash"'),
        )
        for body in lines:
            with self.subTest(body=body[-75:]), self.assertRaises(inspector.Blocked):
                inspector.launcher_source_from_text(body)

    def test_inspect_only_hashes_and_sets_all_write_capabilities_false(self):
        existing = template().encode()
        source = b"#!/usr/bin/env python3\nprint('not executed')\n"
        with patch.object(inspector.os, "geteuid", return_value=0), \
             patch.object(inspector.socket, "gethostname", return_value="glmdch"), \
             patch.object(inspector, "read_regular_file",
                          side_effect=[existing, source]) as loader:
            output = inspector.inspect()
        self.assertEqual(2, loader.call_count)
        self.assertEqual("LAUNCHER_OBSERVED_UPGRADE_NOT_AUTHORIZED", output["status"])
        self.assertFalse(output["may_install"])
        self.assertFalse(output["may_apply"])
        self.assertFalse(output["signed_release_verified"])
        self.assertFalse(output["rollback_validated"])
        self.assertEqual(64, len(output["current_operator_sha256"]))

    def test_wrong_host_never_reads_source(self):
        with patch.object(inspector.os, "geteuid", return_value=0), \
             patch.object(inspector.socket, "gethostname", return_value="srv01"), \
             patch.object(inspector, "read_regular_file") as loader:
            with self.assertRaises(inspector.Blocked):
                inspector.inspect()
            loader.assert_not_called()

    def test_reject_symlink_and_non_root_permissions_in_filesystem_guard(self):
        with tempfile.TemporaryDirectory() as td:
            folder = Path(td)
            target = folder / "qa525-update.py"
            target.write_text("not trusted")
            alias = folder / "alias"
            alias.symlink_to(target)
            with self.assertRaises(inspector.Blocked):
                inspector.owned_chain(alias)
            with self.assertRaises(inspector.Blocked):
                inspector.owned_chain(target, final_mode=0o700)

    def test_inspection_is_not_an_installer(self):
        content = FILE.read_text()
        # The inspector has no write-capable OS operations or Docker imports.
        for marker in ("os.replace(", "os.rename(", "subprocess.", "docker ",
                       "os.chmod(", "os.chown(", "shutil."):
            self.assertNotIn(marker, content)
        calls = [node.func for node in ast.walk(ast.parse(content))
                 if isinstance(node, ast.Call)]
        forbidden = {"execv", "execve", "system", "popen",
                     "rename", "unlink", "chmod", "chown", "write_text",
                     "write_bytes"}
        # String.replace is used only to sanitize a static error code;
        # actual os.replace/mutation remains forbidden by the text guard.
        self.assertFalse([node.attr for node in calls
                          if isinstance(node, ast.Attribute)
                          and node.attr in forbidden])
        self.assertIn('"may_install": False', content)
        self.assertIn('"may_apply": False', content)


if __name__ == "__main__":
    unittest.main()
