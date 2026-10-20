import hashlib
import importlib.util
from pathlib import Path
import sys
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1] / "scripts/qa"
spec = importlib.util.spec_from_file_location("qa_bootstrap_test", ROOT / "qa-lite-bootstrap.py")
mod = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = mod
spec.loader.exec_module(mod)


class BootstrapTests(unittest.TestCase):
    def test_every_host_dependency_is_pinned_to_the_actual_reviewed_bytes(self):
        self.assertEqual(8, len(mod.PINNED_CODE))
        for filename, digest in mod.PINNED_CODE.items():
            self.assertEqual(digest, hashlib.sha256((ROOT / filename).read_bytes()).hexdigest(), filename)

    def test_wrong_host_cannot_download_install_packages_or_start_services(self):
        with patch.object(mod.os, "geteuid", return_value=0), \
                patch.object(mod.socket, "gethostname", return_value="production"), \
                patch.object(mod.sys, "argv", ["bootstrap", "--source", "a" * 40]), \
                patch.object(mod, "urlopen") as download, patch.object(mod.subprocess, "run") as run:
            with self.assertRaisesRegex(RuntimeError, "HOST_NOT_AUTHORIZED"):
                mod.main()
            download.assert_not_called()
            run.assert_not_called()


if __name__ == "__main__":
    unittest.main()
