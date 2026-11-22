"""Guard: compiled Python bytecode must never be tracked (it hides stale code and bloats reviews)."""
from pathlib import Path
import shutil
import subprocess
import unittest

ROOT = Path(__file__).resolve().parents[1]


class NoTrackedBytecode(unittest.TestCase):
    @unittest.skipUnless(shutil.which("git") and (ROOT / ".git").exists(), "needs a git checkout")
    def test_no_tracked_bytecode(self):
        listed = subprocess.run(["git", "-C", str(ROOT), "ls-files", "-z"], capture_output=True, check=True).stdout
        bad = [name.decode("utf-8", "replace") for name in listed.split(b"\0")
               if name.endswith((b".pyc", b".pyo", b".pyd")) or b"__pycache__/" in name]
        self.assertEqual([], bad)

    def test_gitignore_blocks_bytecode(self):
        patterns = (ROOT / ".gitignore").read_text().splitlines()
        self.assertIn("__pycache__/", patterns)
        self.assertIn("*.py[cod]", patterns)


if __name__ == "__main__":
    unittest.main()
