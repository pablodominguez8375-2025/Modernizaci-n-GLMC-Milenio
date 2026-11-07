"""Hermetic byte-pin/TOCTOU regression tests for QA525 B7 Compose libraries."""
import copy
import hashlib
import importlib.util
from pathlib import Path
import stat
from types import SimpleNamespace
import unittest

FILE = Path(__file__).resolve().parents[1] / "scripts/qa/qa525-vnext-compose-byte-pins.py"
spec = importlib.util.spec_from_file_location("qa525_compose_byte_pins", FILE)
target = importlib.util.module_from_spec(spec)
spec.loader.exec_module(target)

BASE = "/opt/centenario/qa-lab-firstboot/recovery-v2/compose.recovery.json"
API = [BASE] + ["/opt/centenario/qa525-signed-prepare/api-" + str(i) + ".json" for i in range(5)]
WEB = [BASE] + ["/opt/centenario/qa525-signed-prepare/web-" + str(i) + ".json" for i in range(4)]


def paths(api=None, web=None):
    return {"api": list(API if api is None else api),
            "web": list(WEB if web is None else web)}


def content_for(layers):
    return {p: ("{\"path_number\":%d}\n" % i).encode()
            for i, p in enumerate(sorted(set(layers["api"] + layers["web"])))}


def stat_info(*, directory=False, ino=123, uid=0, gid=0, mode=None,
              links=1, size=0, mtime_ns=10, ctime_ns=12):
    return SimpleNamespace(st_dev=1, st_ino=ino, st_uid=uid, st_gid=gid,
                           st_mode=((stat.S_IFDIR | 0o755) if directory else
                                    (stat.S_IFREG | 0o600)) if mode is None else mode,
                           st_nlink=links, st_size=size,
                           st_mtime_ns=mtime_ns, st_ctime_ns=ctime_ns)


class FakeReadOnlyPort:
    def __init__(self, files, *, root=0):
        self.files = files
        self.root = root
        self.read_offsets = {}
        self.fd_paths = {}
        self.nextfd = 10
        self.calls = []
        self.file_stats = {p: stat_info(ino=100 + i, size=len(raw))
                           for i, (p, raw) in enumerate(sorted(files.items()))}
        self.changed_stat = {}
        self.bad_dirs = {}
        self.inject_read_failure = False
        self.replace_bytes = {}

    def euid(self):
        self.calls.append(("euid",))
        return self.root

    def lstat(self, path):
        self.calls.append(("lstat", path))
        if path in self.bad_dirs:
            return self.bad_dirs[path]
        if path in self.file_stats:
            return self.changed_stat.get(path, self.file_stats[path])
        return stat_info(directory=True, ino=len(path) + 200)

    def open_ro(self, path):
        self.calls.append(("open_ro", path))
        fd = self.nextfd
        self.nextfd += 1
        self.fd_paths[fd] = path
        self.read_offsets[fd] = 0
        return fd

    def fstat(self, fd):
        self.calls.append(("fstat", fd))
        return self.file_stats[self.fd_paths[fd]]

    def read(self, fd, length):
        self.calls.append(("read", fd, length))
        if self.inject_read_failure:
            raise RuntimeError("private path /secret")
        path = self.fd_paths[fd]
        raw = self.replace_bytes.get(path, self.files[path])
        offset = self.read_offsets[fd]
        self.read_offsets[fd] += length
        return raw[offset:offset + length]

    def close(self, fd):
        self.calls.append(("close", fd))
        self.fd_paths.pop(fd)
        self.read_offsets.pop(fd)


class Qa525ComposeBytePinsContractTests(unittest.TestCase):
    def setUp(self):
        self.layers = paths()
        self.files = content_for(self.layers)
        self.pins = {p: "sha256:" + hashlib.sha256(raw).hexdigest()
                     for p, raw in self.files.items()}
        self.port = FakeReadOnlyPort(self.files)

    def audit(self):
        return target.inspect(layers=self.layers, pinned_sha256=self.pins,
                              port=self.port)

    def code(self, label):
        with self.assertRaises(target.Stop) as context:
            self.audit()
        self.assertEqual(str(context.exception), label)
        self.assertIsNone(context.exception.__cause__)

    def test_all_ten_unique_files_read_once_and_no_authority(self):
        result = self.audit()
        self.assertEqual((result["api_layers"], result["web_layers"]), (6, 5))
        self.assertEqual(result["files_verified"], 10)
        self.assertTrue(result["all_files_match_caller_pins"])
        for flag in ("pins_authenticated_here", "port_authenticated_here",
                     "may_apply", "may_install", "sponsor_approved_here"):
            self.assertIs(result[flag], False)
        names = [path for action, *rest in self.port.calls if action == "open_ro"
                 for path in rest]
        self.assertEqual(sorted(names), sorted(self.files))
        self.assertFalse(self.port.fd_paths)

    def test_future_api7_web6_full_pins_accepted(self):
        self.layers = paths(API + ["/opt/centenario/qa525-signed-prepare/api-next.json"],
                            WEB + ["/opt/centenario/qa525-signed-prepare/web-next.json"])
        self.files = content_for(self.layers)
        self.pins = {p: "sha256:" + hashlib.sha256(raw).hexdigest()
                     for p, raw in self.files.items()}
        self.port = FakeReadOnlyPort(self.files)
        result = self.audit()
        self.assertEqual((result["api_layers"], result["web_layers"]), (7, 6))
        self.assertEqual(result["files_verified"], 12)

    def test_missing_extra_and_malformed_pin_refused(self):
        for kind in ("missing", "extra", "invalid"):
            with self.subTest(kind=kind):
                pins = dict(self.pins)
                if kind == "missing":
                    del pins[BASE]
                elif kind == "extra":
                    pins["/etc/unknown.json"] = pins[BASE]
                else:
                    pins[BASE] = "BAD"
                with self.assertRaises(target.Stop):
                    target.inspect(layers=self.layers, pinned_sha256=pins, port=self.port)
                self.assertFalse(any(c[0] == "open_ro" for c in self.port.calls))

    def test_digest_changed_same_byte_length_refused(self):
        self.port.replace_bytes[BASE] = b"x" * len(self.files[BASE])
        self.code("COMPOSE_DIGEST_MISMATCH")
        self.assertFalse(self.port.fd_paths)

    def test_symlink_and_group_writable_parent_refused_before_open(self):
        parent = "/opt/centenario/qa-lab-firstboot/recovery-v2"
        for mode in (stat.S_IFLNK | 0o777, stat.S_IFDIR | 0o775):
            with self.subTest(mode=mode):
                self.port = FakeReadOnlyPort(self.files)
                self.port.bad_dirs[parent] = stat_info(directory=True, mode=mode)
                self.code("COMPOSE_ROOT_PATH_UNTRUSTED")
                self.assertFalse(any(c[0] == "open_ro" for c in self.port.calls))

    def test_file_hardlink_symlink_and_owner_change_rejected(self):
        for changes in ({"links": 2}, {"uid": 1000}, {"mode": stat.S_IFLNK | 0o777},
                        {"mode": stat.S_IFREG | 0o664}):
            with self.subTest(changes=changes):
                self.port = FakeReadOnlyPort(self.files)
                old = copy.copy(self.port.file_stats[BASE])
                for key, value in changes.items():
                    setattr(old, "st_" + ("nlink" if key == "links" else key), value)
                self.port.file_stats[BASE] = old
                self.code("COMPOSE_ROOT_PATH_UNTRUSTED" if "links" not in changes
                          and "mode" in changes or "uid" in changes
                          else "COMPOSE_FILE_UNTRUSTED")

    def test_descriptor_toctou_inode_swap_refused(self):
        self.port.changed_stat[BASE] = stat_info(ino=999, size=len(self.files[BASE]))
        self.code("COMPOSE_DESCRIPTOR_CHANGED")
        self.assertFalse(self.port.fd_paths)

    def test_size_changed_refused(self):
        self.port.replace_bytes[BASE] = self.files[BASE][:-1]
        self.code("COMPOSE_SIZE_CHANGED")
        self.assertFalse(self.port.fd_paths)

    def test_broken_port_untrusted_uid_and_exceptions_are_fixed_codes(self):
        self.port.root = 1000
        self.code("COMPOSE_PORT_UNTRUSTED")
        self.port = FakeReadOnlyPort(self.files)
        self.port.inject_read_failure = True
        self.code("COMPOSE_READ_FAILED")
        self.assertFalse(self.port.fd_paths)
        with self.assertRaisesRegex(target.Stop, "COMPOSE_PORT_INVALID"):
            target.inspect(layers=self.layers, pinned_sha256=self.pins, port=None)

    def test_reordered_layers_change_manifest_hash_without_changing_file_digest(self):
        normal = self.audit()["manifest_sha256"]
        self.layers = paths(API[:-2] + [API[-1], API[-2]], WEB)
        self.port = FakeReadOnlyPort(self.files)
        shifted = self.audit()
        self.assertNotEqual(shifted["manifest_sha256"], normal)
        self.assertIs(shifted["may_apply"], False)

    def test_duplicate_or_missing_layer_refused(self):
        for malformed in (paths(API[:-1], WEB), paths(API + [API[-1]], WEB),
                          paths(API, WEB + ["/opt/unexpected/../file"]),
                          paths(API, ["/wrong/base.json"] + WEB[1:])):
            with self.subTest(layers=malformed):
                with self.assertRaises(target.Stop):
                    target.inspect(layers=malformed, pinned_sha256=self.pins, port=self.port)


if __name__ == "__main__":
    unittest.main()
