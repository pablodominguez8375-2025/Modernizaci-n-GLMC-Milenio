#!/usr/bin/env python3
"""QA525 API_WEB host EFFECTS: the ONLY file of this flight that imports subprocess/network. INACTIVE library.

Each function does exactly one thing and takes no authority from its caller beyond an already allowlisted argv:
* docker_reader(): runs /usr/bin/docker with an argv that the read-only guard (host-adapters.read_only_docker) has
  ALREADY accepted; fixed environment, no shell, bounded time and output.
* executor(): runs an argv that the physical runner has ALREADY matched against its 4-command allowlist.
* pin_verifier(paths): re-hashes the five pinned root-owned files on every call (never cached).
* https_fetch(): GET without redirects, bounded body, system CA bundle.
Nothing here is authority, authenticates a host or proves a state.
"""
import hashlib
import os
import stat
import subprocess
import urllib.error
import urllib.request

DOCKER = "/usr/bin/docker"
ENV = {"PATH": "/usr/sbin:/usr/bin:/sbin:/bin", "LC_ALL": "C"}
READ_TIMEOUT, EXEC_TIMEOUT, HTTP_TIMEOUT = 60, 600, 10
MAX_OUT, MAX_BODY = 262144, 262144
ROOT_UID = 0  # tests may rebind
ABORT_PATH = "/root/.qa525-apiweb-abort"  # tests may rebind; the Sponsor creates it (root, 0600) to stop a flight
PIN_FILES = ("systemd_run_sha256", "docker_sha256", "python_sha256", "child_sha256", "compose_stack_sha256")


class Stop(Exception):
    """Fixed codes only."""


def abort_requested():
    """True when the root abort marker EXISTS (any file type counts). Absent is the only False; any other error is
    True (fail closed). Read-only: one lstat, nothing is created, opened or changed."""
    try:
        os.lstat(ABORT_PATH)
    except FileNotFoundError:
        return False
    except (OSError, ValueError):
        return True
    return True


def docker_reader():
    def run(args):
        try:
            done = subprocess.run([DOCKER] + list(args), stdout=subprocess.PIPE, stderr=subprocess.DEVNULL,
                                  stdin=subprocess.DEVNULL, env=dict(ENV), timeout=READ_TIMEOUT, check=False,
                                  shell=False)
        except Exception:
            raise Stop("EFFECT_READ_FAILED") from None
        if done.returncode != 0 or len(done.stdout) > MAX_OUT:
            raise Stop("EFFECT_READ_FAILED")
        try:
            return done.stdout.decode("utf-8")
        except UnicodeDecodeError:
            raise Stop("EFFECT_READ_FAILED") from None
    return run


def executor():
    def run(argv):
        if type(argv) is not list or not argv or any(type(a) is not str or "\0" in a for a in argv):
            raise Stop("EFFECT_ARGV_INVALID")
        try:
            done = subprocess.run(argv, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                                  stdin=subprocess.DEVNULL, env=dict(ENV), timeout=EXEC_TIMEOUT, check=False,
                                  shell=False)
        except Exception:
            raise Stop("EFFECT_EXEC_FAILED") from None
        return int(done.returncode)
    return run


def _hash_file(path):
    fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC)
    try:
        st = os.fstat(fd)
        if (not stat.S_ISREG(st.st_mode) or st.st_uid != ROOT_UID or st.st_mode & 0o022 or st.st_size > 512 * 1024 * 1024):
            raise Stop("EFFECT_PIN_FILE_UNSAFE")
        digest = hashlib.sha256()
        while True:
            chunk = os.read(fd, 1 << 20)
            if not chunk:
                break
            digest.update(chunk)
        return "sha256:" + digest.hexdigest()
    finally:
        os.close(fd)


def pin_verifier(paths):
    """`paths`: {pin_name: absolute path}. Returns verify_pins(expected) -> bool, hashing again on EVERY call."""
    if type(paths) is not dict or set(paths) != set(PIN_FILES) or any(
            type(p) is not str or not p.startswith("/") for p in paths.values()):
        raise Stop("EFFECT_PIN_PATHS_INVALID")
    frozen = dict(paths)

    def verify(expected):
        if type(expected) is not dict or set(expected) != set(PIN_FILES):
            return False
        try:
            return all(_hash_file(frozen[name]) == expected[name] for name in PIN_FILES)
        except (OSError, Stop):
            return False
    return verify


class _NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *args, **kwargs):
        return None


def https_fetch():
    opener = urllib.request.build_opener(_NoRedirect)

    def fetch(url):
        if type(url) is not str or not url.startswith("https://"):
            raise Stop("EFFECT_URL_INVALID")
        try:
            with opener.open(urllib.request.Request(url, method="GET"), timeout=HTTP_TIMEOUT) as resp:
                return resp.status, resp.geturl(), resp.read(MAX_BODY + 1)
        except urllib.error.HTTPError as exc:
            return exc.code, url, b""
        except Exception:
            raise Stop("EFFECT_FETCH_FAILED") from None
    return fetch


if __name__ == "__main__":
    raise SystemExit("QA525_APIWEB_EFFECTS_LIBRARY_ONLY_NO_GO")
