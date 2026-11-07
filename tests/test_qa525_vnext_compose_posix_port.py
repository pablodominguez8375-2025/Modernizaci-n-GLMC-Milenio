"""Offline tests for the strict POSIX fd port of QA525 B7."""
import hashlib
import importlib.util
from pathlib import Path
import stat
from types import SimpleNamespace
import unittest
from unittest.mock import patch

HERE = Path(__file__).resolve().parents[1] / "scripts/qa"


def module(name, file):
    spec = importlib.util.spec_from_file_location(name, HERE / file)
    m = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(m)
    return m


posix = module("qa525_compose_posix_port_test", "qa525-vnext-compose-posix-port.py")
checker = module("qa525_compose_byte_pins_posix_test", "qa525-vnext-compose-byte-pins.py")
BASE = "/opt/centenario/qa-lab-firstboot/recovery-v2/compose.recovery.json"
API = [BASE] + ["/opt/centenario/qa525-signed-prepare/api-" + str(i) + ".json"
                for i in range(5)]
WEB = [BASE] + ["/opt/centenario/qa525-signed-prepare/web-" + str(i) + ".json"
                for i in range(4)]


def node(directory=False, ino=11, size=0):
    return SimpleNamespace(st_dev=1, st_ino=ino, st_uid=0, st_gid=0,
                           st_mode=(stat.S_IFDIR | 0o755 if directory
                                    else stat.S_IFREG | 0o600),
                           st_nlink=1, st_size=size, st_mtime_ns=5, st_ctime_ns=6)


class Qa525ComposePosixPortContractTests(unittest.TestCase):
    def test_paths_are_exact_and_root_scoped(self):
        for value in ("/etc/shadow", "/tmp/unsafe", "/opt/centenario/../etc/shadow",
                      "/opt/centenario//bad", "/opt/centenario/file/",
                      "/opt/centenario/./file", "/opt/centenario/file\x00junk",
                      "opt/centenario/a", 1, ""):
            with self.subTest(path=value):
                with self.assertRaises(posix.Stop):
                    posix.PosixReadPort([value])
        with self.assertRaises(posix.Stop):
            posix.PosixReadPort([])
        with self.assertRaises(posix.Stop):
            posix.PosixReadPort([BASE, BASE])
        with self.assertRaises(posix.Stop):
            posix.PosixReadPort([BASE] * 129)

    def test_only_allowed_files_are_opened(self):
        p = posix.PosixReadPort([BASE])
        with patch.object(posix.os, "open", return_value=123) as opening:
            self.assertEqual(p.open_ro(BASE), 123)
            opening.assert_called_once_with(BASE, posix.FLAGS)
            self.assertTrue(posix.FLAGS & posix.os.O_NOFOLLOW)
            self.assertTrue(posix.FLAGS & posix.os.O_CLOEXEC)
            self.assertTrue(posix.FLAGS & posix.os.O_NONBLOCK)
            for forbidden in ("/etc/shadow", "/opt/centenario/qa.json",
                              "/opt/centenario", "/"):
                with self.assertRaisesRegex(posix.Stop, "COMPOSEPOSIX_OPEN_REFUSED"):
                    p.open_ro(forbidden)
            self.assertEqual(opening.call_count, 1)
        with patch.object(posix.os, "close", return_value=None) as close:
            p.close(123)
            close.assert_called_once_with(123)

    def test_stat_only_exact_files_and_direct_ancestors(self):
        p = posix.PosixReadPort([BASE])
        with patch.object(posix.os, "lstat", return_value=node(directory=True)) as inspect:
            for trusted in ("/", "/opt", "/opt/centenario",
                            "/opt/centenario/qa-lab-firstboot",
                            "/opt/centenario/qa-lab-firstboot/recovery-v2", BASE):
                p.lstat(trusted)
            self.assertEqual(inspect.call_count, 6)
            for bad in ("/etc", "/etc/shadow", "/opt/centenario/other.json"):
                with self.assertRaisesRegex(posix.Stop, "COMPOSEPOSIX_STAT_REFUSED"):
                    p.lstat(bad)
            self.assertEqual(inspect.call_count, 6)

    def test_unknown_descriptor_read_limit_and_no_reuse_after_close(self):
        p = posix.PosixReadPort([BASE])
        for action in (lambda: p.fstat(3), lambda: p.read(3, 5),
                       lambda: p.close(3)):
            with self.assertRaisesRegex(posix.Stop, "COMPOSEPOSIX_FD_REFUSED"):
                action()
        with patch.object(posix.os, "open", return_value=20):
            p.open_ro(BASE)
        with patch.object(posix.os, "fstat", return_value=node(size=3)):
            self.assertEqual(p.fstat(20).st_size, 3)
        with patch.object(posix.os, "read", return_value=b"ok") as read:
            self.assertEqual(p.read(20, 3), b"ok")
            read.assert_called_once_with(20, 3)
            for length in (0, -1, 65537, True, "x"):
                with self.assertRaisesRegex(posix.Stop, "COMPOSEPOSIX_READ_LIMIT"):
                    p.read(20, length)
            self.assertEqual(read.call_count, 1)
        with patch.object(posix.os, "close"):
            p.close(20)
        with self.assertRaisesRegex(posix.Stop, "COMPOSEPOSIX_FD_REFUSED"):
            p.read(20, 1)

    def test_syscall_failures_never_echo_errors(self):
        p = posix.PosixReadPort([BASE])
        with patch.object(posix.os, "open", side_effect=OSError("secret name")):
            with self.assertRaises(posix.Stop) as c:
                p.open_ro(BASE)
            self.assertEqual(str(c.exception), "COMPOSEPOSIX_OPEN_FAILED")
            self.assertNotIn("secret", str(c.exception))
        with patch.object(posix.os, "lstat", side_effect=OSError("secret name")):
            with self.assertRaisesRegex(posix.Stop, "COMPOSEPOSIX_STAT_FAILED"):
                p.lstat(BASE)

    def test_end_to_end_full_pinned_stack_with_mock_posix_syscalls(self):
        layers = {"api": list(API), "web": list(WEB)}
        files = sorted(set(API + WEB))
        bytes_map = {p: ('{"layer":%d}\n' % i).encode() for i, p in enumerate(files)}
        pins = {p: "sha256:" + hashlib.sha256(b).hexdigest()
                for p, b in bytes_map.items()}
        file_stats = {p: node(ino=i+100, size=len(bytes_map[p]))
                      for i, p in enumerate(files)}
        by_fd = {}
        offset = {}
        calls = []

        def fake_lstat(path):
            return file_stats[path] if path in file_stats else node(directory=True)

        def fake_open(path, flags):
            self.assertEqual(flags, posix.FLAGS)
            fd = files.index(path) + 50
            by_fd[fd] = path
            offset[fd] = 0
            calls.append(("open", path))
            return fd

        def fake_fstat(fd):
            return file_stats[by_fd[fd]]

        def fake_read(fd, amount):
            path = by_fd[fd]
            raw = bytes_map[path]
            a = offset[fd]
            offset[fd] += amount
            return raw[a:a+amount]

        def fake_close(fd):
            calls.append(("close", fd))
            del by_fd[fd]

        with (patch.object(posix.os, "geteuid", return_value=0),
              patch.object(posix.os, "lstat", side_effect=fake_lstat),
              patch.object(posix.os, "open", side_effect=fake_open),
              patch.object(posix.os, "fstat", side_effect=fake_fstat),
              patch.object(posix.os, "read", side_effect=fake_read),
              patch.object(posix.os, "close", side_effect=fake_close)):
            p = posix.PosixReadPort(files)
            result = checker.inspect(layers=layers, pinned_sha256=pins, port=p)
        self.assertEqual((result["files_verified"], result["api_layers"],
                          result["web_layers"]), (10, 6, 5))
        self.assertEqual(len([x for x in calls if x[0] == "open"]), 10)
        self.assertEqual(len([x for x in calls if x[0] == "close"]), 10)
        self.assertFalse(by_fd)
        self.assertIs(result["may_apply"], False)
        self.assertIs(result["pins_authenticated_here"], False)


if __name__ == "__main__":
    unittest.main()
