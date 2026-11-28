"""DRIVER-INSTALL gate: one embedded file, FD-relative preflight/apply/rollback, exact HOST-PREP shape required, never
overwrites or deletes.  Hermetic: tmp roots only, the real HOST-PREP script builds the prerequisite tree."""
import ast
import importlib.util
import io
import os
import pathlib
import re
import stat
import tempfile
import unittest

OPS = pathlib.Path(__file__).resolve().parent.parent / "scripts/qa/operators"


def load(path, name):
    spec = importlib.util.spec_from_file_location(name, path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


di = load(OPS / "qa525-driver-install.py", "di")
dib = load(OPS / "qa525-driver-install-build.py", "dib")
hp = load(OPS / "qa525-host-prep.py", "hp2")
driver = load(OPS / "qa525-manual-cut-e5c40d3a.py", "drv2")
UID = os.getuid()
NAME = di.DRIVER_NAME


def snap(root):
    out = {}
    for dp, dn, fn in os.walk(root):
        for n in dn + fn:
            p = os.path.join(dp, n)
            st = os.lstat(p)
            out[os.path.relpath(p, root)] = (st.st_mode, st.st_size, st.st_ino)
    return out


class Manifest(unittest.TestCase):
    def test_embedded_driver_is_the_repo_driver_and_script_is_reproducible(self):
        data = (OPS / NAME).read_bytes()
        self.assertEqual(di.driver_bytes(), data)
        self.assertEqual(dib.build((OPS / "qa525-driver-install.template.py").read_text(), OPS / NAME),
                         (OPS / "qa525-driver-install.py").read_text())

    def test_lib_table_equals_driver_pins_and_host_prep_bundle(self):
        self.assertEqual(di.LIBS, {**driver.LIB_PINS, **driver.G6_PINS})
        self.assertEqual(di.LIBS, {n: v[0] for n, v in hp.BUNDLE.items()} if False else {n: v[0] for n, v in hp.BUNDLE.items()})
        self.assertEqual(len(di.LIBS), 11)

    def test_installed_driver_has_the_mode_enabled_constant_and_pins(self):
        self.assertIs(driver.G6_MODE_ENABLED, True)
        self.assertIsNotNone(driver.G6_OPS_ALLOWED_SIGNERS)
        self.assertIsNotNone(driver.G6_KEYGEN_SHA256)

    def test_tampered_embedded_driver_is_stop(self):
        old = di.DRIVER
        try:
            di.DRIVER = (old[0], old[1], ("AAAA",) + tuple(old[2][1:]))
            with self.assertRaises(di.Stop) as c:
                di.driver_bytes()
            self.assertIn(str(c.exception), ("DRIVER_HASH_MISMATCH", "DRIVER_DECODE_FAILED"))
            di.DRIVER = ("", 0, ())
            with self.assertRaises(di.Stop) as c:
                di.driver_bytes()
            self.assertEqual(str(c.exception), "DRIVER_NOT_EMBEDDED")
        finally:
            di.DRIVER = old

    def test_script_spawns_nothing_and_never_executes_the_driver(self):
        tree = ast.parse((OPS / "qa525-driver-install.py").read_text())
        imports = {a.name.split(".")[0] for n in ast.walk(tree) if isinstance(n, ast.Import) for a in n.names}
        imports |= {n.module.split(".")[0] for n in ast.walk(tree) if isinstance(n, ast.ImportFrom) and n.module}
        self.assertLessEqual(imports, {"base64", "ctypes", "errno", "hashlib", "io", "os", "re", "stat", "sys"})
        calls = {n.func.id for n in ast.walk(tree) if isinstance(n, ast.Call) and isinstance(n.func, ast.Name)}
        self.assertFalse(calls & {"exec", "eval", "compile", "__import__"})


class Gate(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.mkdtemp(prefix="di-test-")
        self.addCleanup(__import__("shutil").rmtree, self.tmp, True)
        self.opt, self.var = self.tmp + "/opt", self.tmp + "/var"
        for p in (self.opt, self.var):
            os.mkdir(p, 0o755)
        out = io.StringIO()
        pre = io.StringIO()
        hp.run("preflight", opt=self.opt, var=self.var, uid=UID, out=pre)
        m = re.search(r"G6HP_BIND=(\d+):(\d+),(\d+):(\d+)", pre.getvalue())
        bind = ((int(m[1]), int(m[2])), (int(m[3]), int(m[4])))
        self.assertEqual(hp.run("apply", opt=self.opt, var=self.var, uid=UID, out=out, bind=bind), 0, out.getvalue())
        self.base = self.opt + "/" + di.BASE
        self.target = self.base + "/" + NAME

    def run_mode(self, mode, bind=None):
        out = io.StringIO()
        code = di.run(mode, opt=self.opt, var=self.var, uid=UID, out=out, bind=bind)
        return code, out.getvalue()

    def bind(self):
        code, out = self.run_mode("preflight")
        self.assertEqual(code, 0, out)
        m = re.search(r"G6DI_BIND=(\d+):(\d+)\n", out)
        return (int(m[1]), int(m[2]))

    def test_preflight_pass_is_exactly_six_lines_and_read_only(self):
        before = snap(self.tmp)
        code, out = self.run_mode("preflight")
        self.assertEqual(code, 0)
        lines = out.splitlines()
        self.assertEqual(len(lines), 6, out)
        self.assertTrue(lines[0].startswith("G6DI_BIND="))
        self.assertEqual(lines[1], "G6DI_PARENTS=uid=%d no-group-world-write" % UID)
        self.assertEqual(lines[2], "G6DI_HOSTPREP=base=0755 lib=files=11 hash=OK journal=0700 entries=0")
        self.assertEqual(lines[3], "G6DI_DRIVER=sha256=%s size=%d" % (di.DRIVER[0], di.DRIVER[1]))
        self.assertEqual(lines[4:], ["G6DI_TARGET=ABSENT", "G6DI_PREFLIGHT=PASS"])
        self.assertEqual(snap(self.tmp), before)

    def test_apply_installs_exactly_the_embedded_bytes_and_nothing_else(self):
        bind = self.bind()
        before = snap(self.tmp)
        code, out = self.run_mode("apply", bind)
        self.assertEqual(code, 0, out)
        self.assertEqual(out.splitlines(), ["G6DI_APPLIED=1", "G6DI_DRIVER_FILE=regular uid=%d mode=0644 nlink=1 size=%d" % (UID, di.DRIVER[1]),
                                            "G6DI_DRIVER_SHA256=" + di.DRIVER[0]])
        st = os.lstat(self.target)
        self.assertTrue(stat.S_ISREG(st.st_mode) and stat.S_IMODE(st.st_mode) == 0o644 and st.st_nlink == 1 and st.st_uid == UID)
        self.assertEqual(open(self.target, "rb").read(), (OPS / NAME).read_bytes())
        after = snap(self.tmp)
        self.assertEqual(set(after) - set(before), {os.path.relpath(self.target, self.tmp)})
        for k, v in before.items():
            self.assertEqual(after[k][2], v[2], k)  # no pre-existing inode replaced
        code, out = self.run_mode("preflight")
        self.assertEqual((code, out), (1, "G6DI_STOP:TARGET_EXISTS\n"))

    def test_apply_requires_the_exact_bound_base_and_a_bind(self):
        code, out = self.run_mode("apply", None)
        self.assertEqual((code, out), (1, "G6DI_STOP:BIND_REQUIRED\n"))
        st = os.stat(self.base)
        code, out = self.run_mode("apply", (st.st_dev, st.st_ino + 1))
        self.assertEqual((code, out), (1, "G6DI_STOP:BASE_NOT_THE_BOUND_ONE\n"))
        self.assertFalse(os.path.lexists(self.target))

    def test_host_prep_must_be_exactly_in_place(self):
        def stop(code):
            c, out = self.run_mode("preflight")
            self.assertEqual((c, out), (1, "G6DI_STOP:%s\n" % code))
            self.assertFalse(os.path.lexists(self.target))
        lib = self.base + "/lib"
        name = sorted(os.listdir(lib))[0]
        path = lib + "/" + name
        data = open(path, "rb").read()
        open(path, "ab").write(b"x")
        stop("LIB_HASH_MISMATCH")
        open(path, "wb").write(data)
        os.chmod(path, 0o600)
        stop("INSTALLED_STATE_UNEXPECTED")
        os.chmod(path, 0o644)
        open(lib + "/extra.py", "wb").write(b"")
        stop("LIB_STATE_UNEXPECTED")
        os.unlink(lib + "/extra.py")
        os.rename(path, path + ".away")
        stop("LIB_STATE_UNEXPECTED")
        os.rename(path + ".away", path)
        os.mkdir(self.base + "/other")
        stop("BASE_STATE_UNEXPECTED")
        os.rmdir(self.base + "/other")
        os.chmod(self.base, 0o775)
        stop("BASE_STATE_UNEXPECTED")
        os.chmod(self.base, 0o755)
        jroot = self.var + "/" + di.JBASE + "/" + di.JOURNAL
        open(jroot + "/used.spent", "wb").write(b"")
        stop("JOURNAL_NOT_EMPTY")
        os.unlink(jroot + "/used.spent")
        os.chmod(jroot, 0o755)
        stop("JOURNAL_STATE_UNEXPECTED")
        os.chmod(jroot, 0o700)
        self.assertEqual(self.run_mode("preflight")[0], 0)

    def test_existing_target_or_leftover_names_block_everything(self):
        for n in (NAME, NAME + di.SUFFIX_RB, NAME + di.SUFFIX_FAIL):
            p = self.base + "/" + n
            os.symlink("/nonexistent", p)
            code, out = self.run_mode("preflight")
            self.assertEqual((code, out), (1, "G6DI_STOP:TARGET_EXISTS\n"), n)
            os.unlink(p)

    def test_symlinked_base_is_not_followed(self):
        os.rename(self.base, self.tmp + "/real-base")
        os.symlink(self.tmp + "/real-base", self.base)
        code, out = self.run_mode("preflight")
        self.assertEqual((code, out), (1, "G6DI_STOP:BASE_STATE_UNEXPECTED\n"))

    def test_failed_apply_renames_never_deletes_and_reports_rolled_back(self):
        bind = self.bind()
        real = di._hook

        def boom(stage):
            if stage == "written":
                raise OSError("disk")
        di._hook = boom
        try:
            code, out = self.run_mode("apply", bind)
        finally:
            di._hook = real
        self.assertEqual((code, out), (1, "G6DI_STOP:APPLY_FAILED_ROLLED_BACK\n"))
        self.assertFalse(os.path.lexists(self.target))
        self.assertTrue(os.path.isfile(self.target + di.SUFFIX_FAIL))  # evidence kept
        self.assertEqual(sorted(os.listdir(self.base)), sorted(["lib", NAME + di.SUFFIX_FAIL]))

    def test_verification_failure_after_write_is_renamed_too(self):
        bind = self.bind()
        real = di._hook

        def corrupt(stage):
            if stage == "verify":
                with open(self.target, "r+b") as fh:
                    fh.write(b"X")
        di._hook = corrupt
        try:
            code, out = self.run_mode("apply", bind)
        finally:
            di._hook = real
        self.assertEqual((code, out), (1, "G6DI_STOP:INSTALLED_STATE_UNEXPECTED_ROLLED_BACK\n"))
        self.assertFalse(os.path.lexists(self.target))
        self.assertTrue(os.path.isfile(self.target + di.SUFFIX_FAIL))

    def test_rename_failure_during_failed_apply_is_reported_not_hidden(self):
        bind = self.bind()
        real_h, real_r = di._hook, di._noreplace_rename

        def boom(stage):
            if stage == "written":
                raise OSError("disk")

        def no_rename(*a, **k):
            raise di.Stop("RENAME_REFUSED_ERRNO_EXDEV")
        di._hook, di._noreplace_rename = boom, no_rename
        try:
            code, out = self.run_mode("apply", bind)
        finally:
            di._hook, di._noreplace_rename = real_h, real_r
        self.assertEqual((code, out), (1, "G6DI_STOP:APPLY_FAILED_ROLLBACK_FAILED\n"))

    def test_rollback_renames_exact_bytes_only_and_never_deletes(self):
        bind = self.bind()
        self.assertEqual(self.run_mode("apply", bind)[0], 0)
        self.assertEqual(self.run_mode("rollback", None), (1, "G6DI_STOP:BIND_REQUIRED\n"))
        with open(self.target, "r+b") as fh:
            fh.write(b"X")
        code, out = self.run_mode("rollback", bind)
        self.assertEqual((code, out), (1, "G6DI_STOP:INSTALLED_STATE_UNEXPECTED\n"))  # altered file is not touched
        self.assertTrue(os.path.isfile(self.target))
        with open(self.target, "wb") as fh:
            fh.write((OPS / NAME).read_bytes())
        code, out = self.run_mode("rollback", bind)
        self.assertEqual((code, out), (0, "G6DI_ROLLED_BACK=1\n"))
        self.assertFalse(os.path.lexists(self.target))
        self.assertEqual(open(self.target + di.SUFFIX_RB, "rb").read(), (OPS / NAME).read_bytes())

    def test_modes_and_root_are_closed(self):
        out = io.StringIO()
        self.assertEqual(di.run("nope", opt=self.opt, var=self.var, uid=UID, out=out), 1)
        self.assertEqual(out.getvalue(), "G6DI_STOP:MODE_INVALID\n")
        for argv in (["x"], ["x", "apply"], ["x", "apply", "1:2:3"], ["x", "preflight", "1:2"], ["x", "bogus"], ["x", "rollback", "a:b"]):
            buf = io.StringIO()
            real, __import__("sys").stdout = __import__("sys").stdout, buf
            try:
                self.assertEqual(di.main(argv), 1, argv)
            finally:
                __import__("sys").stdout = real
            self.assertEqual(buf.getvalue(), "G6DI_STOP:MODE_INVALID\n", argv)
        if UID != 0:
            out = io.StringIO()
            self.assertEqual(di.run("apply", uid=UID, out=out, bind=(1, 2)), 1)
            self.assertEqual(out.getvalue(), "G6DI_STOP:ROOT_REQUIRED\n")


class Upgrade(unittest.TestCase):
    """TRANSITION: replace the previously installed (never executed) driver; rename-only, never delete, restore on failure."""
    run_mode = Gate.run_mode

    def setUp(self):
        Gate.setUp(self)
        self.oldb = b"#!/usr/bin/env python3\n# previous driver\n" * 40
        self._old = di.OLD_DRIVER
        di.OLD_DRIVER = (__import__("hashlib").sha256(self.oldb).hexdigest(), len(self.oldb))
        self.addCleanup(setattr, di, "OLD_DRIVER", self._old)
        with open(self.target, "wb") as fh:
            fh.write(self.oldb)
        os.chmod(self.target, 0o644)
        self.old_ino = os.lstat(self.target).st_ino

    def bind(self):
        code, out = self.run_mode("upgrade-preflight")
        self.assertEqual(code, 0, out)
        m = re.search(r"G6DI_BIND=(\d+):(\d+)\n", out)
        return (int(m[1]), int(m[2]))

    def test_production_pin_is_the_driver_installed_in_glmdch(self):
        self.assertEqual(self._old, ("c9cd98c7d2d0ab588655550e8a841f01f65f1887673ff585e7f6ad5221e91962", 51347))
        self.assertNotEqual(self._old[0], di.DRIVER[0])

    def test_upgrade_preflight_is_seven_lines_and_read_only(self):
        before = snap(self.tmp)
        code, out = self.run_mode("upgrade-preflight")
        self.assertEqual(code, 0, out)
        lines = out.splitlines()
        self.assertEqual(len(lines), 7, out)
        self.assertTrue(lines[0].startswith("G6DI_BIND="))
        self.assertEqual(lines[3], "G6DI_INSTALLED=sha256=%s size=%d" % di.OLD_DRIVER)
        self.assertEqual(lines[4], "G6DI_NEWDRIVER=sha256=%s size=%d" % (di.DRIVER[0], di.DRIVER[1]))
        self.assertEqual(lines[5:], ["G6DI_SUPERSEDED=ABSENT", "G6DI_UPGRADE_PREFLIGHT=PASS"])
        self.assertEqual(snap(self.tmp), before)
        # the fresh-install preflight still refuses a present driver
        self.assertEqual(self.run_mode("preflight"), (1, "G6DI_STOP:TARGET_EXISTS\n"))

    def test_upgrade_renames_old_never_deletes_and_installs_new(self):
        bind = self.bind()
        code, out = self.run_mode("upgrade", bind)
        self.assertEqual(code, 0, out)
        self.assertEqual(out.splitlines()[:3], ["G6DI_UPGRADED=1", "G6DI_DRIVER_FILE=regular uid=%d mode=0644 nlink=1 size=%d" % (UID, di.DRIVER[1]),
                                                "G6DI_DRIVER_SHA256=" + di.DRIVER[0]])
        self.assertEqual(open(self.target, "rb").read(), (OPS / NAME).read_bytes())
        sup = self.target + di.SUFFIX_SUPERSEDED
        self.assertEqual(open(sup, "rb").read(), self.oldb)
        self.assertEqual(os.lstat(sup).st_ino, self.old_ino)  # the very same inode, renamed
        self.assertEqual(sorted(os.listdir(self.base)), sorted(["lib", NAME, NAME + di.SUFFIX_SUPERSEDED]))
        # not repeatable
        self.assertEqual(self.run_mode("upgrade", bind)[0], 1)

    def test_bind_is_required_and_exact(self):
        bind = self.bind()
        self.assertEqual(self.run_mode("upgrade", None), (1, "G6DI_STOP:BIND_REQUIRED\n"))
        self.assertEqual(self.run_mode("upgrade", (bind[0], bind[1] + 1)), (1, "G6DI_STOP:BASE_NOT_THE_BOUND_ONE\n"))
        self.assertEqual(open(self.target, "rb").read(), self.oldb)

    def test_only_the_exact_previous_driver_can_be_superseded(self):
        bind = self.bind()
        with open(self.target, "r+b") as fh:
            fh.write(b"X")
        self.assertEqual(self.run_mode("upgrade", bind), (1, "G6DI_STOP:OLD_DRIVER_HASH_MISMATCH\n"))
        os.chmod(self.target, 0o755)
        self.assertEqual(self.run_mode("upgrade-preflight")[0], 1)
        os.chmod(self.target, 0o644)
        os.link(self.target, self.base + "/hardlink")
        self.assertEqual(self.run_mode("upgrade-preflight")[0], 1)
        os.unlink(self.base + "/hardlink")
        with open(self.target, "wb") as fh:  # restored exact bytes -> works again
            fh.write(self.oldb)
        self.assertEqual(self.run_mode("upgrade-preflight")[0], 0)

    def test_absent_old_driver_or_leftovers_or_symlink_stop(self):
        os.rename(self.target, self.base + "/moved-away")
        self.assertEqual(self.run_mode("upgrade-preflight")[0], 1)
        os.rename(self.base + "/moved-away", self.target)
        for leftover in (di.SUFFIX_SUPERSEDED, di.SUFFIX_RB, di.SUFFIX_FAIL):
            open(self.target + leftover, "wb").close()
            self.assertEqual(self.run_mode("upgrade-preflight")[0], 1, leftover)
            os.unlink(self.target + leftover)
        os.rename(self.target, self.base + "/real")
        os.symlink(self.base + "/real", self.target)
        self.assertEqual(self.run_mode("upgrade-preflight")[0], 1)

    def test_host_prep_must_still_be_exact(self):
        with open(self.base + "/lib/" + sorted(di.LIBS)[0], "ab") as fh:
            fh.write(b"x")
        self.assertEqual(self.run_mode("upgrade-preflight")[0], 1)

    def _fail_at(self, stage, mutate=None):
        bind = self.bind()
        real = di._hook

        def boom(st):
            if st == stage:
                if mutate:
                    mutate()
                else:
                    raise OSError("disk")
        di._hook = boom
        try:
            return self.run_mode("upgrade", bind)
        finally:
            di._hook = real

    def test_failure_after_rename_restores_the_previous_driver_and_keeps_evidence(self):
        for stage in ("renamed", "written"):
            for n in os.listdir(self.base):
                if n.endswith((di.SUFFIX_FAIL, di.SUFFIX_SUPERSEDED)):
                    os.rename(self.base + "/" + n, self.base + "/zz-" + n.replace(".", "_"))
            code, out = self._fail_at(stage)
            self.assertEqual((code, out), (1, "G6DI_STOP:UPGRADE_FAILED_RESTORED\n"), stage)
            self.assertEqual(open(self.target, "rb").read(), self.oldb)  # previous driver is back under its name
            self.assertEqual(os.lstat(self.target).st_ino, self.old_ino)
            self.assertFalse(os.path.lexists(self.target + di.SUFFIX_SUPERSEDED))
            self.assertEqual(os.path.exists(self.target + di.SUFFIX_FAIL), stage == "written")
            for n in os.listdir(self.base):  # never deleted: keep everything for the next iteration
                if n.startswith("zz-") or n.endswith(di.SUFFIX_FAIL):
                    os.rename(self.base + "/" + n, self.base + "/keep-" + str(stage) + n[-6:])

    def test_corruption_detected_at_verify_restores_too(self):
        def corrupt():
            with open(self.target, "r+b") as fh:
                fh.write(b"X")
        code, out = self._fail_at("verify", corrupt)
        self.assertEqual((code, out), (1, "G6DI_STOP:INSTALLED_STATE_UNEXPECTED_RESTORED\n"))
        self.assertEqual(open(self.target, "rb").read(), self.oldb)
        self.assertTrue(os.path.isfile(self.target + di.SUFFIX_FAIL))

    def test_restore_failure_is_reported_not_hidden(self):
        bind = self.bind()
        real_h, real_r = di._hook, di._noreplace_rename
        calls = []

        def boom(stage):
            if stage == "written":
                raise OSError("disk")

        def flaky(pfd, src, dst, loader=None):
            calls.append(dst)
            if len(calls) >= 3:
                raise di.Stop("RENAME_REFUSED_ERRNO_EXDEV")
            return real_r(pfd, src, dst, loader)
        di._hook, di._noreplace_rename = boom, flaky
        try:
            code, out = self.run_mode("upgrade", bind)
        finally:
            di._hook, di._noreplace_rename = real_h, real_r
        self.assertEqual((code, out), (1, "G6DI_STOP:UPGRADE_FAILED_RESTORE_FAILED\n"))
        self.assertTrue(os.path.exists(self.target + di.SUFFIX_SUPERSEDED))  # old driver still preserved

    def test_argv_grammar_for_the_new_modes(self):
        for argv in (["x", "upgrade"], ["x", "upgrade", "1:2:3"], ["x", "upgrade-preflight", "1:2"], ["x", "upgrade", "a:b"]):
            buf = io.StringIO()
            real, __import__("sys").stdout = __import__("sys").stdout, buf
            try:
                self.assertEqual(di.main(argv), 1, argv)
            finally:
                __import__("sys").stdout = real
            self.assertEqual(buf.getvalue(), "G6DI_STOP:MODE_INVALID\n", argv)


if __name__ == "__main__":
    unittest.main()
