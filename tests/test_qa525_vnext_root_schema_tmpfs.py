"""Root schema mountinfo and source gates: synthetic, no host changes."""
import importlib.util
from pathlib import Path
from unittest.mock import patch
import io
import unittest

source = Path(__file__).resolve().parents[1] / "scripts/qa/qa525-vnext-root-schema-effects.py"
spec = importlib.util.spec_from_file_location("qa525_schema_effects_mount", source)
effects = importlib.util.module_from_spec(spec)
spec.loader.exec_module(effects)

class SchemaEffectsTmpfsTests(unittest.TestCase):
    def test_mountinfo_requires_exact_run_tmpfs(self):
        good = "51 20 0:24 / /run rw,nosuid - tmpfs tmpfs rw\\n"
        wrong = "51 20 0:24 / /run rw,nosuid - ext4 /dev/vda rw\\n"
        class RootStat:
            st_mode = 0o40755
            st_uid = 0
        for text, expected in ((good, True), (wrong, False)):
            with self.subTest(expected=expected), patch("builtins.open", return_value=io.StringIO(text)), \
                 patch.object(effects.os, "stat", return_value=RootStat()):
                self.assertIs(effects.verify_schema_tmpfs("/run"), expected)
        self.assertFalse(effects.verify_schema_tmpfs("/tmp"))

    def test_missing_mount_or_unsafe_permissions_fail_closed(self):
        with patch("builtins.open", side_effect=OSError):
            self.assertFalse(effects.verify_schema_tmpfs("/run"))
        class Unsafe:
            st_mode = 0o40777
            st_uid = 0
        with patch("builtins.open", return_value=io.StringIO(
                "51 20 0:24 / /run rw - tmpfs tmpfs rw\\n")), \
             patch.object(effects.os, "stat", return_value=Unsafe()):
            self.assertFalse(effects.verify_schema_tmpfs("/run"))

if __name__ == "__main__":
    unittest.main()
