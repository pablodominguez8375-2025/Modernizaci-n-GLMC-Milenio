#!/usr/bin/env python3
"""QA525 off-host multiprocess nonce persistence probe (stdlib only).

This is a *probe*, not a production adapter or authorization verifier.
A real adapter MUST authenticate a signed GO independently and bind
nonce/release/plan before calling its durable spend implementation.
"""
import argparse
import fcntl
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile

def spend(directory: Path, nonce: str, release: str, plan: str) -> bool:
    """Atomic consumption across OS processes, durable before return."""
    if not all(len(x) == 64 and all(c in "0123456789abcdef" for c in x) for x in (nonce, release, plan)):
        raise ValueError("expected lowercase sha256 identifiers")
    directory.mkdir(mode=0o700, parents=True, exist_ok=True)
    lock = directory / ".lock"
    lockfd = os.open(str(lock), os.O_RDWR | os.O_CREAT | os.O_NOFOLLOW, 0o600)
    with os.fdopen(lockfd, "a+b") as handle:
        fcntl.flock(handle, fcntl.LOCK_EX)
        target = directory / ("spent-" + nonce)
        if os.path.lexists(target):
            return False
        fd = os.open(str(target), os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
        try:
            data = json.dumps({"nonce": nonce, "release_sha": release, "plan_sha256": plan}, sort_keys=True).encode() + b"\n"
            with os.fdopen(fd, "wb") as out:
                out.write(data)
                out.flush()
                os.fsync(out.fileno())
        except BaseException:
            # Never silently reuse a nonce whose write may have been interrupted.
            raise
        dirfd = os.open(str(directory), os.O_RDONLY | os.O_DIRECTORY)
        try:
            os.fsync(dirfd)
        finally:
            os.close(dirfd)
        return True

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--worker", action="store_true")
    parser.add_argument("--crash-after-spend", action="store_true")
    parser.add_argument("--dir")
    args = parser.parse_args()
    nonce, release, plan = ("a" * 64, "b" * 64, "c" * 64)
    if args.worker:
        result = spend(Path(args.dir), nonce, release, plan)
        if args.crash_after_spend and result:
            os._exit(73)  # abrupt process death after durable nonce, before mutation
        print("SPENT" if result else "REPLAY")
        return
    with tempfile.TemporaryDirectory(prefix="qa525-nonce-probe-") as tmp:
        cmd = [sys.executable, str(Path(__file__).resolve()), "--worker", "--dir", tmp]
        workers = [subprocess.Popen(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True) for _ in range(8)]
        results = [p.communicate(timeout=15) for p in workers]
        assert all(p.returncode == 0 for p in workers), results
        lines = [out.strip() for out, _ in results]
        assert lines.count("SPENT") == 1 and lines.count("REPLAY") == 7, lines
        assert not spend(Path(tmp), nonce, release, plan), "replay after restart accepted"
        record = json.loads((Path(tmp) / ("spent-" + nonce)).read_text())
        assert record == {"nonce": nonce, "release_sha": release, "plan_sha256": plan}
        crash_dir = Path(tmp) / "crash"
        crashed = subprocess.run(cmd[:-1] + [str(crash_dir), "--crash-after-spend"], capture_output=True, text=True, timeout=15)
        assert crashed.returncode == 73, (crashed.returncode, crashed.stderr)
        replay = subprocess.run(cmd[:-1] + [str(crash_dir)], capture_output=True, text=True, timeout=15)
        assert replay.returncode == 0 and replay.stdout.strip() == "REPLAY"
        assert not spend(Path(tmp), nonce, "d" * 64, plan), "nonce reused with different release"
        assert not spend(Path(tmp), nonce, release, "e" * 64), "nonce reused with different plan"
        print("QA525_NONCE_MULTIPROCESS_PROBE=PASS (8 processes, 1 spend, 7 replay, persistent restart)")
        print("LIMIT: no signature verification, power-loss fault injection or host adapter certification")

if __name__ == "__main__":
    main()
