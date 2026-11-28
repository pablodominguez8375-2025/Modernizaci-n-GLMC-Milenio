"""GO-INSTALL: exclusive creation of GO+signature in the root-only dir; never overwrites, never deletes.  Hermetic (tmp dirs)."""
import ast
import hashlib
import importlib.util
import io
import os
import pathlib
import tempfile
import unittest

OPS = pathlib.Path(__file__).resolve().parent.parent / "scripts/qa/operators"
spec = importlib.util.spec_from_file_location("gi", OPS / "qa525-go-install.py")
gi = importlib.util.module_from_spec(spec)
spec.loader.exec_module(gi)
UID = os.getuid()
GO = b'{"command":"init","driver_sha256":"' + b"a" * 64 + b'","expires_at":1,"nonce":"' + b"b" * 32 + b'","op_sha256":"' + b"c" * 64 + b'"}'
SIG = b"-----BEGIN SSH SIGNATURE-----\nAAAA\n-----END SSH SIGNATURE-----\n"
H = lambda b: hashlib.sha256(b).hexdigest()


class Case(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.mkdtemp(prefix="gi-test-")
        self.addCleanup(__import__("shutil").rmtree, self.tmp, True)
        self.src = self.tmp + "/src"
        self.dest = self.tmp + "/dest"
        os.mkdir(self.src, 0o755)
        os.mkdir(self.dest, 0o700)
        os.chmod(self.src, 0o755)
        self.go, self.sig = self.src + "/go-abc12345.json", self.src + "/go-abc12345.json.sig"
        for p, d in ((self.go, GO), (self.sig, SIG)):
            with open(p, "wb") as fh:
                fh.write(d)

    def run_install(self, **kw):
        a = dict(go_src=self.go, go_sha=H(GO), sig_src=self.sig, sig_sha=H(SIG))
        a.update(kw)
        out = io.StringIO()
        try:
            gi.install(a["go_src"], a["go_sha"], a["sig_src"], a["sig_sha"], dest=kw.get("dest", self.dest), uid=UID, out=out)
            return 0, out.getvalue()
        except gi.Stop as exc:
            return 1, str(exc)

    def snap(self):
        return {n: open(self.dest + "/" + n, "rb").read() for n in sorted(os.listdir(self.dest))}


class Install(Case):
    def test_exclusive_install_writes_exact_bytes_0600_nlink1(self):
        code, out = self.run_install()
        self.assertEqual(code, 0, out)
        self.assertEqual(out.splitlines(), [
            "G6GI_GO=go-abc12345.json sha256=%s size=%d mode=0600 uid=%d nlink=1" % (H(GO), len(GO), UID),
            "G6GI_SIG=go-abc12345.json.sig sha256=%s size=%d mode=0600 uid=%d nlink=1" % (H(SIG), len(SIG), UID),
            "G6GI_INSTALLED=2"])
        self.assertEqual(self.snap(), {"go-abc12345.json": GO, "go-abc12345.json.sig": SIG})
        for n in os.listdir(self.dest):
            st = os.lstat(self.dest + "/" + n)
            self.assertEqual((oct(st.st_mode & 0o777), st.st_nlink), ("0o600", 1))
        self.assertEqual(open(self.go, "rb").read(), GO)  # source untouched

    def test_existing_destination_is_stop_and_never_overwritten(self):
        with open(self.dest + "/go-abc12345.json", "wb") as fh:
            fh.write(b"OLD")
        self.assertEqual(self.run_install(), (1, "DESTINATION_EXISTS"))
        self.assertEqual(self.snap(), {"go-abc12345.json": b"OLD"})
        os.unlink(self.dest + "/go-abc12345.json")
        with open(self.dest + "/go-abc12345.json.sig", "wb") as fh:
            fh.write(b"OLD")
        self.assertEqual(self.run_install(), (1, "DESTINATION_EXISTS"))
        self.assertEqual(self.snap(), {"go-abc12345.json.sig": b"OLD"})  # nothing created (GO not created either)

    def test_name_race_after_the_precheck_is_still_exclusive(self):
        real = gi._hook

        def racer(stage):
            if stage == "before":
                with open(self.dest + "/go-abc12345.json", "wb") as fh:
                    fh.write(b"RACE")
        gi._hook = racer
        try:
            code, out = self.run_install()
        finally:
            gi._hook = real
        self.assertEqual((code, out), (1, "DESTINATION_EXISTS"))
        self.assertEqual(self.snap(), {"go-abc12345.json": b"RACE"})

    def test_partial_failure_is_reported_and_nothing_is_deleted(self):
        real = gi._hook

        def racer(stage):
            if stage == "between":
                with open(self.dest + "/go-abc12345.json.sig", "wb") as fh:
                    fh.write(b"RACE")
        gi._hook = racer
        try:
            code, out = self.run_install()
        finally:
            gi._hook = real
        self.assertEqual((code, out), (1, "DESTINATION_EXISTS_GO_CREATED_SIGNATURE_NOT"))
        self.assertEqual(self.snap(), {"go-abc12345.json": GO, "go-abc12345.json.sig": b"RACE"})

    def test_hash_and_name_arguments_are_closed(self):
        self.assertEqual(self.run_install(go_sha="0" * 64), (1, "SOURCE_HASH_MISMATCH"))
        self.assertEqual(self.run_install(sig_sha="0" * 64), (1, "SOURCE_HASH_MISMATCH"))
        self.assertEqual(self.run_install(go_sha="XYZ"), (1, "HASH_ARGUMENT_INVALID"))
        other = self.src + "/other.sig"
        os.rename(self.sig, other)
        self.assertEqual(self.run_install(sig_src=other), (1, "SIGNATURE_NAME_MISMATCH"))
        self.assertEqual(self.snap(), {})

    def test_sources_must_be_plain_regular_single_link_small_files_in_a_safe_dir(self):
        link = self.src + "/lnk.json"
        os.symlink(self.go, link)
        self.assertEqual(self.run_install(go_src=link, sig_src=self.sig)[0], 1)
        os.link(self.go, self.src + "/hard.json")
        self.assertEqual(self.run_install(), (1, "SOURCE_NOT_ACCEPTABLE"))  # nlink 2
        os.unlink(self.src + "/hard.json")
        big = self.src + "/big.json"
        with open(big, "wb") as fh:
            fh.write(b"x" * 5000)
        self.assertEqual(self.run_install(go_src=big, go_sha=H(b"x" * 5000), sig_src=self.src + "/big.json.sig")[0], 1)
        os.chmod(self.src, 0o777)
        self.assertEqual(self.run_install(), (1, "SOURCE_PARENT_UNSAFE"))
        os.chmod(self.src, 0o755)
        self.assertEqual(self.run_install(go_src="relative.json")[0], 1)
        self.assertEqual(self.run_install(go_src=self.src + "/../src/go-abc12345.json")[0], 1)
        self.assertEqual(self.snap(), {})

    def test_destination_must_be_the_exact_private_directory(self):
        os.chmod(self.dest, 0o755)
        self.assertEqual(self.run_install(), (1, "DEST_NOT_ROOT_0700"))
        os.chmod(self.dest, 0o700)
        link = self.tmp + "/destlink"
        os.symlink(self.dest, link)
        self.assertEqual(self.run_install(dest=link), (1, "DEST_NOT_CANONICAL"))
        self.assertEqual(self.run_install(dest=self.tmp + "/missing"), (1, "DEST_OPEN_REFUSED"))
        self.assertEqual(self.snap(), {})

    def test_root_is_required_for_the_production_directory_and_argv_grammar(self):
        if UID != 0:
            with self.assertRaises(gi.Stop):
                gi.install(self.go, H(GO), self.sig, H(SIG), uid=UID)
        for argv in (["x"], ["x", "install"], ["x", "bogus", "a", "b", "c", "d"], ["x", "install", "a", "b", "c"]):
            buf = io.StringIO()
            self.assertEqual(gi.main(argv, out=buf), 1)
            self.assertEqual(buf.getvalue(), "G6GI_STOP:USAGE\n")

    def test_script_spawns_nothing_and_never_touches_the_driver(self):
        tree = ast.parse((OPS / "qa525-go-install.py").read_text())
        imports = {a.name.split(".")[0] for n in ast.walk(tree) if isinstance(n, ast.Import) for a in n.names}
        imports |= {n.module.split(".")[0] for n in ast.walk(tree) if isinstance(n, ast.ImportFrom) and n.module}
        self.assertLessEqual(imports, {"hashlib", "os", "re", "stat", "sys"})
        calls = {n.func.id for n in ast.walk(tree) if isinstance(n, ast.Call) and isinstance(n.func, ast.Name)}
        self.assertFalse(calls & {"exec", "eval", "compile", "__import__"})
        src = (OPS / "qa525-go-install.py").read_text()
        self.assertNotIn("os.unlink", src)
        self.assertNotIn("os.remove", src)
        self.assertNotIn("os.rename", src)


if __name__ == "__main__":
    unittest.main()
