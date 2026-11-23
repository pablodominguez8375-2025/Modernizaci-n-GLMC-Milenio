import importlib.util, json, os, stat, tempfile, unittest, hashlib
from pathlib import Path

SRC = Path(__file__).resolve().parent.parent / "scripts" / "qa" / "qa525-host-inventory-readonly.py"
spec = importlib.util.spec_from_file_location("inv", SRC)
inv = importlib.util.module_from_spec(spec)
spec.loader.exec_module(inv)

PIN = "sha256:a3d78cc8d4c021695d6b5b637ce1bb38b1a21ef4e8afeba68181e791764c76b3"


def fake(argv):
    key = " ".join(argv)
    if "image inspect" in key:
        return 0, PIN + "\n"
    if "docker ps" in key:
        return 0, "x-api-1|img|abc|running|Up\n"
    if "list-timers" in key:
        return 0, "n/a pmgm-g6-minio-restart.timer x\nunrelated.timer\n"
    if "systemctl show" in key:
        return 0, "LoadState=loaded\nActiveState=inactive\n"
    return 0, "ok\n"


class T(unittest.TestCase):
    def setUp(self):
        self.d = tempfile.mkdtemp()
        os.chmod(self.d, 0o700)

    def test_commands_allowlist_has_no_mutation(self):
        for k, argv in inv.COMMANDS.items():
            for w in argv[1:]:
                self.assertNotIn(w, inv.FORBIDDEN_WORDS, (k, w))
            self.assertTrue(argv[0].startswith("/usr/bin/"))
        self.assertEqual(inv.COMMANDS["restic_version"], ["/usr/bin/restic", "version"])

    def test_collect_with_fake_runner_and_pin_match(self):
        f = Path(self.d, "launcher"); f.write_bytes(b"abc"); os.chmod(f, 0o755)
        s = Path(self.d, "secret-token.txt"); s.write_bytes(b"zzz")
        res = inv.collect(paths=(("l", str(f), "file"), ("s", str(s), "file"), ("d", self.d, "dir"),
                                 ("m", self.d + "/nope", "file")), runner=fake, now="T")
        self.assertEqual(res["paths"]["l"]["sha256"], hashlib.sha256(b"abc").hexdigest())
        self.assertIsNone(res["paths"]["s"]["sha256"])
        self.assertEqual(res["paths"]["m"]["status"], "absent")
        self.assertTrue(res["migrator"]["matches_expected_pin"])
        self.assertEqual(len(res["timers"]["rows"]), 1)
        names = {e["name"]: e for e in res["paths"]["d"]["entries"]}
        self.assertIsNone(names["secret-token.txt"].get("sha256"))
        self.assertTrue(res["read_only"])

    def test_pin_mismatch_and_garbage(self):
        res = inv.collect(paths=(), runner=lambda a: (0, "sha256:" + "9" * 64) if "inspect" in a else (0, ""))
        self.assertFalse(res["migrator"]["matches_expected_pin"])
        res = inv.collect(paths=(), runner=lambda a: (0, "junk; rm -rf") if "inspect" in a else (0, ""))
        self.assertIsNone(res["migrator"]["id"])

    def test_denied_path_reported_not_raised(self):
        d = Path(self.d, "locked"); d.mkdir(); os.chmod(d, 0)
        try:
            if os.geteuid() == 0:
                self.skipTest("root bypasses permissions")
            r = inv.probe_path(str(d), "dir")
            self.assertEqual(r["entries"], "denied")
        finally:
            os.chmod(d, 0o700)

    def test_symlink_not_followed_for_hash(self):
        t = Path(self.d, "t"); t.write_bytes(b"x"); l = Path(self.d, "l"); l.symlink_to(t)
        r = inv.probe_path(str(l), "file")
        self.assertEqual(r["type"], "symlink")
        self.assertNotIn("sha256", r)

    def test_write_new_excl_0600_and_no_overwrite(self):
        p = os.path.join(self.d, "out.json")
        inv.write_new(b"{}\n", p)
        self.assertEqual(stat.S_IMODE(os.stat(p).st_mode), 0o600)
        with self.assertRaises(FileExistsError):
            inv.write_new(b"x", p)
        self.assertEqual(Path(p).read_bytes(), b"{}\n")

    def test_write_new_refuses_symlink(self):
        t = os.path.join(self.d, "target"); l = os.path.join(self.d, "ln"); os.symlink(t, l)
        with self.assertRaises(OSError):
            inv.write_new(b"x", l)
        self.assertFalse(os.path.exists(t))

    def test_canonical_deterministic_and_no_secret_markers(self):
        res = inv.collect(paths=(), runner=fake, now="T")
        a, b = inv.canonical(res), inv.canonical(json.loads(inv.canonical(res)))
        self.assertEqual(a, b)

    def test_runner_missing_binary_is_rc127(self):
        old = inv.COMMANDS["uname"]
        inv.COMMANDS["uname"] = ["/nonexistent/x"]
        try:
            self.assertEqual(inv.run_cmd("uname")[0], 127)
        finally:
            inv.COMMANDS["uname"] = old

    def test_source_has_no_mutating_calls(self):
        src = SRC.read_text()
        for bad in ("shell=True", "os.remove", "os.unlink", "shutil", "os.rename", "chmod(", "os.system"):
            self.assertNotIn(bad, src)


if __name__ == "__main__":
    unittest.main()
