#!/usr/bin/env python3
"""QA525 isolated journal durability probe; NOT a production adapter."""
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile

def durable_replace(directory: Path, payload: dict, crash_at: str = "") -> None:
    directory.mkdir(parents=True, exist_ok=True, mode=0o700)
    target = directory / "journal.json"
    tmp = directory / ".journal.tmp"
    fd = os.open(str(tmp), os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
    try:
        with os.fdopen(fd, "wb") as f:
            f.write((json.dumps(payload, sort_keys=True) + "\n").encode())
            f.flush()
            os.fsync(f.fileno())
        if crash_at == "before_rename":
            os._exit(71)
        os.replace(tmp, target)
        dirfd = os.open(str(directory), os.O_RDONLY | os.O_DIRECTORY)
        try:
            os.fsync(dirfd)
        finally:
            os.close(dirfd)
        if crash_at == "after_dirsync":
            os._exit(72)
    finally:
        if tmp.exists():
            tmp.unlink()

def main() -> None:
    if len(sys.argv) > 1 and sys.argv[1] == "worker":
        durable_replace(Path(sys.argv[2]), {"phase": "committed"}, sys.argv[3])
        return
    with tempfile.TemporaryDirectory(prefix="qa525-journal-") as root:
        directory = Path(root)
        durable_replace(directory, {"phase": "baseline"})
        assert json.loads((directory / "journal.json").read_text()) == {"phase": "baseline"}
        cmd = [sys.executable, str(Path(__file__).resolve()), "worker", root]
        before = subprocess.run(cmd + ["before_rename"], capture_output=True, timeout=15)
        assert before.returncode == 71
        assert json.loads((directory / "journal.json").read_text()) == {"phase": "baseline"}
        assert (directory / ".journal.tmp").exists()
        (directory / ".journal.tmp").unlink()  # supervised recovery cleanup
        after = subprocess.run(cmd + ["after_dirsync"], capture_output=True, timeout=15)
        assert after.returncode == 72
        assert json.loads((directory / "journal.json").read_text()) == {"phase": "committed"}
        print("QA525_JOURNAL_PROBE=PASS (pre-rename crash preserved baseline; post-dirsync crash preserved committed state)")
        print("LIMIT: no actual power-loss injection, signature validation or host integration")

if __name__ == "__main__":
    main()
