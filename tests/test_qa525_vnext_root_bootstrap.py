"""QA525 root bootstrap trust check: tampering must STOP before any module is compiled."""
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest
import unittest.mock

SRC = Path(__file__).resolve().parents[1] / "scripts/qa/qa525-vnext-root-bootstrap.py"
UID = os.getuid()


def rd(p):
    with open(p, "rb") as f:
        return f.read()


def pathlib_write(p, data):
    Path(p).write_bytes(data if isinstance(data, bytes) else data.encode())


def canon(obj):
    return json.dumps(obj, sort_keys=True, separators=(",", ":")).encode()


def sha(b):
    return hashlib.sha256(b).hexdigest()


def load():
    spec = importlib.util.spec_from_file_location("qa525_boot_test", SRC)
    m = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(m)
    return m


class BootstrapTests(unittest.TestCase):
    def setUp(self):
        self.m = load()
        self.base = os.path.realpath(tempfile.mkdtemp(prefix="bs"))
        os.chmod(self.base, 0o755)
        self.addCleanup(shutil.rmtree, self.base, True)
        self.inst = os.path.join(self.base, "inst")
        os.mkdir(self.inst, 0o755)
        self.write("recv.py", b"VALUE = 1\n")
        self.real = os.path.realpath(sys.executable)
        self.flags = dict(isolated=1, no_site=1, ignore_environment=1)
        self.build()

    def write(self, name, data, mode=0o644):
        p = os.path.join(self.inst, name)
        if os.path.lexists(p):
            os.unlink(p)
        with open(p, "wb") as f:
            f.write(data)
        os.chmod(p, mode)

    def build(self):
        files = {n: sha(rd(os.path.join(self.inst, n)))
                 for n in sorted(os.listdir(self.inst))}
        self.manifest = canon({"files": files, "interpreter": {
            "path": self.real, "sha256": sha(rd(self.real))}})
        self.pin = sha(self.manifest)

    def verify(self, **kw):
        m = self.m
        args = dict(install_dir=self.inst, manifest_bytes=self.manifest, external_pin=self.pin, uid=UID, anchor=self.base)
        args.update(kw)
        with unittest.mock.patch.object(m.sys, "flags", type("F", (), self.flags)()):
            return m.verify_install(**args)

    def stop(self, code, **kw):
        with self.assertRaises(self.m.Stop) as ctx:
            self.verify(**kw)
        self.assertEqual(str(ctx.exception), code)

    def test_happy_path_loads_from_verified_bytes_only(self):
        v = self.verify()
        self.assertEqual(self.m.load_module(v, "recv.py").VALUE, 1)
        self.assertEqual(v.record["manifest_pin"], self.pin)

    def test_pin_from_the_same_package_or_wrong_is_refused(self):
        self.stop("BOOT_MANIFEST_PIN_MISMATCH", external_pin="0" * 64)
        self.stop("BOOT_PIN_INVALID", external_pin="sha256:" + self.pin)

    def test_receiver_changed_after_manifest_stops_before_compile(self):
        self.write("recv.py", b"VALUE = 2\n")
        self.stop("BOOT_HASH_MISMATCH")

    def test_swap_after_verification_is_not_executed(self):
        v = self.verify()
        self.write("recv.py", b"import os; os._exit(9)\n")
        self.assertEqual(self.m.load_module(v, "recv.py").VALUE, 1)

    def test_interpreter_hash_or_path_mismatch_stops(self):
        bad = json.loads(self.manifest)
        bad["interpreter"]["sha256"] = "1" * 64
        raw = canon(bad)
        self.stop("BOOT_HASH_MISMATCH", manifest_bytes=raw, external_pin=sha(raw))
        bad["interpreter"]["path"] = "/usr/bin/true"
        raw = canon(bad)
        self.stop("BOOT_INTERPRETER_MISMATCH", manifest_bytes=raw, external_pin=sha(raw))

    def test_writable_dir_symlink_or_wrong_owner_stops(self):
        os.chmod(self.inst, 0o775)
        self.stop("BOOT_DIR_UNSAFE")
        os.chmod(self.inst, 0o755)
        link = os.path.join(self.base, "lnk")
        os.symlink(self.inst, link)
        self.stop("BOOT_DIR_UNSAFE", install_dir=link)
        self.stop("BOOT_DIR_UNSAFE", uid=UID + 1)

    def test_group_writable_symlinked_or_hardlinked_file_stops(self):
        os.chmod(os.path.join(self.inst, "recv.py"), 0o664)
        self.stop("BOOT_FILE_UNSAFE")
        os.chmod(os.path.join(self.inst, "recv.py"), 0o644)
        os.link(os.path.join(self.inst, "recv.py"), os.path.join(self.base, "other"))
        self.stop("BOOT_FILE_UNSAFE")
        os.unlink(os.path.join(self.base, "other"))
        os.unlink(os.path.join(self.inst, "recv.py"))
        os.symlink("/etc/hostname", os.path.join(self.inst, "recv.py"))
        with self.assertRaises(OSError):
            self.verify()

    def test_not_isolated_interpreter_stops(self):
        self.flags["isolated"] = 0
        self.stop("BOOT_NOT_ISOLATED")

    def test_manifest_with_extra_or_odd_names_is_invalid(self):
        for bad in ("../x.py", "a b.py", "x.txt"):
            raw = canon({"files": {bad: "1" * 64}, "interpreter": json.loads(self.manifest)["interpreter"]})
            self.stop("BOOT_MANIFEST_INVALID", manifest_bytes=raw, external_pin=sha(raw))

    def test_unknown_module_is_refused(self):
        v = self.verify()
        with self.assertRaises(self.m.Stop):
            self.m.load_module(v, "other.py")

    def test_forged_verified_dict_or_object_cannot_execute_anything(self):
        marker = os.path.join(self.base, "pwned")
        evil = ("open(%r,'w').write('x')\n" % marker).encode()
        for forged in ({"recv.py": evil}, ({}, "recv.py"), None, object()):
            with self.assertRaises(self.m.Stop) as ctx:
                self.m.load_module(forged, "recv.py")
            self.assertEqual(str(ctx.exception), "BOOT_VERIFIED_FORGED")
        with self.assertRaises(self.m.Stop):
            self.m.Verified(object(), {"recv.py": evil}, {})
        v = self.verify()
        with self.assertRaises(self.m.Stop):
            v.record = {}
        self.assertFalse(os.path.exists(marker))

    def test_manifest_must_be_canonical_exact_and_without_duplicate_keys(self):
        good = json.loads(self.manifest)
        variants = {
            "spaced": json.dumps(good).encode(),
            "extra_key": canon(dict(good, extra=1)),
            "dup_key": self.manifest.replace(b'"files"', b'"files":{},"files"', 1),
            "not_object": b"[]",
        }
        for label, raw in variants.items():
            self.stop("BOOT_MANIFEST_INVALID", manifest_bytes=raw, external_pin=sha(raw))

    def test_interpreter_is_not_injectable(self):
        import inspect
        self.assertNotIn("interpreter_real", inspect.signature(self.m.verify_install).parameters)

    def test_record_is_durable_and_excl(self):
        record = self.verify().record
        ev = os.path.join(self.base, "ev")
        os.mkdir(ev, 0o755)
        name = self.m.write_record(ev, record, uid=UID, anchor=self.base)
        with self.assertRaises(FileExistsError):
            self.m.write_record(ev, record, uid=UID, anchor=self.base)
        with unittest.mock.patch.object(os, "fsync", wraps=os.fsync) as fs:
            self.m.write_record(ev, dict(record, n=1), uid=UID, anchor=self.base)
        self.assertEqual(fs.call_count, 2)  # file AND parent directory
        self.assertEqual(json.loads(rd(os.path.join(ev, name)))["schema"], "qa525-pre-root-trust-v1")

    def test_real_isolated_process_runs_the_check(self):
        helper = os.path.join(self.base, "h.py")
        pathlib_write(helper, 
            "import importlib.util,sys,json\n"
            "s=importlib.util.spec_from_file_location('b',sys.argv[1]);m=importlib.util.module_from_spec(s);s.loader.exec_module(m)\n"
            "v=m.verify_install(sys.argv[2],open(sys.argv[3],'rb').read(),sys.argv[4],uid=int(sys.argv[5]),anchor=sys.argv[6])\n"
            "print(m.load_module(v,'recv.py').VALUE)\n")
        mf = os.path.join(self.base, "m.json")
        pathlib_write(mf, self.manifest)
        run = lambda *flags: subprocess.run([self.real, *flags, helper, str(SRC), self.inst, mf, self.pin, str(UID), self.base],
                                            capture_output=True, text=True, env={})
        ok = run("-I", "-S")
        self.assertEqual((ok.returncode, ok.stdout.strip()), (0, "1"), ok.stderr)
        self.assertNotEqual(run().returncode, 0)  # not isolated => STOP

    def test_check_cli_for_soporte_is_read_only_and_prints_fixed_codes(self):
        mf = os.path.join(self.base, "m2.json")
        pathlib_write(mf, self.manifest)
        run = lambda pin: subprocess.run([self.real, "-I", "-S", str(SRC), "check", self.inst, mf, pin],
                                         capture_output=True, text=True, env={})
        bad = run(self.pin)  # uid=0 owners required: a non-root-owned install must STOP
        if UID != 0:
            self.assertEqual((bad.returncode, bad.stdout.strip()), (1, "BOOT_DIR_UNSAFE"))
        self.assertEqual(run("0" * 64).stdout.strip(), "BOOT_MANIFEST_PIN_MISMATCH")
        self.assertEqual(subprocess.run([self.real, "-I", "-S", str(SRC)], capture_output=True,
                                        text=True, env={}).stdout.strip(), "BOOT_USAGE")

    # ---- `run` (root launch): v2 manifest in the install dir, pin by stdin only, record BEFORE the launcher ----
    LAUNCH_BODY = (b"CALLS = []\ndef launch(load, record):\n    CALLS.append(1)\n"
                   b"    return {'ok': True, 'files': sorted(record['files'])}\n")

    def _fake_helper(self):
        helper = os.path.join(self.base, "fakekeygen")
        pathlib_write(helper, b"#!/bin/sh\n")
        os.chmod(helper, 0o755)
        return helper

    def _v2(self, helper_sha=None, helpers=True):
        """Build a v2 manifest (helpers.ssh-keygen pinned) and store it INSIDE the install dir."""
        helper = self._fake_helper()
        files = {n: sha(rd(os.path.join(self.inst, n))) for n in sorted(os.listdir(self.inst))
                 if n.endswith(".py")}
        body = {"files": files, "interpreter": {"path": self.real, "sha256": sha(rd(self.real))}}
        if helpers:
            body["helpers"] = {"ssh-keygen": {"path": helper, "sha256": helper_sha or sha(rd(helper))}}
        self.manifest = canon(body)
        self.pin = sha(self.manifest)
        self.mf = os.path.join(self.inst, "manifest.json")
        if os.path.lexists(self.mf):
            os.unlink(self.mf)
        pathlib_write(self.mf, self.manifest)
        os.chmod(self.mf, 0o644)

    def _launcher_install(self, body=None):
        self.write(self.m.LAUNCHER, body or self.LAUNCH_BODY)
        self.ev = os.path.join(self.base, "ev-run")
        if not os.path.isdir(self.ev):
            os.mkdir(self.ev, 0o755)
        self._v2()

    def _launch(self, pin=None, argv=None, euid=0, stdin=None, **kw):
        import io
        pin = self.pin if pin is None else pin
        argv = argv or ["b", "run", self.inst, self.mf]
        args = dict(euid=euid, uid=UID, anchor=self.base, evidence_dir=self.ev, os_uid=UID)
        args.update(kw)
        with unittest.mock.patch.object(self.m.sys, "flags", type("F", (), self.flags)()):
            return self.m.launch(argv, stdin or io.StringIO(pin + "\n"), **args)

    def code(self, **kw):
        with self.assertRaises(self.m.Stop) as ctx:
            self._launch(**kw)
        return str(ctx.exception)

    def test_run_writes_the_v2_record_then_hands_the_loader_to_the_launcher(self):
        self._launcher_install()
        out = self._launch()
        self.assertTrue(out["ok"])
        (name,) = os.listdir(self.ev)  # pre-root-trust evidence exists
        rec = json.loads(rd(os.path.join(self.ev, name)))
        self.assertEqual(rec["schema"], "qa525-pre-root-trust-v2")
        self.assertIn("ssh-keygen", rec["helpers"])

    def test_run_refuses_non_root_bad_pin_extra_stdin_and_wrong_argv_before_any_module(self):
        import io
        self._launcher_install()
        self.assertEqual(self.code(euid=1000), "BOOT_NOT_ROOT")
        self.assertEqual(self.code(pin="0" * 64), "BOOT_MANIFEST_PIN_MISMATCH")
        self.assertEqual(self.code(argv=["b", "run", self.inst]), "BOOT_USAGE")
        self.assertEqual(self.code(argv=["b", "run", self.inst, self.mf, "x"]), "BOOT_USAGE")
        self.assertEqual(self.code(stdin=io.StringIO(self.pin + "\nextra\n")), "BOOT_PIN_INVALID")
        self.assertEqual(os.listdir(self.ev), [])  # no evidence, nothing loaded

    def test_run_stops_when_launcher_altered_or_missing_from_the_manifest(self):
        self._launcher_install()
        self.write(self.m.LAUNCHER, b"def launch(load, record):\n    return {'evil': True}\n")
        self.assertEqual(self.code(), "BOOT_HASH_MISMATCH")
        self.assertEqual(os.listdir(self.ev), [])
        os.unlink(os.path.join(self.inst, self.m.LAUNCHER))
        self._v2()  # manifest rebuilt WITHOUT the launcher
        self.assertEqual(self.code(), "BOOT_MODULE_UNKNOWN")
        self.assertEqual(os.listdir(self.ev), [])  # P2: no pre-root-trust receipt for a run that cannot start

    def test_v1_manifest_is_check_only_and_never_authorises_a_root_run(self):
        self._launcher_install()
        self._v2(helpers=False)  # v1 shape: still passes `check`...
        self.assertEqual(self.verify().record["schema"], "qa525-pre-root-trust-v1")
        self.assertEqual(self.code(), "BOOT_RUN_REQUIRES_V2")  # ...but `run` stops before record/loader
        self.assertEqual(os.listdir(self.ev), [])

    def test_manifest_must_sit_in_the_install_dir_and_be_a_plain_safe_file(self):
        self._launcher_install()
        outside = os.path.join(self.base, "m-outside.json")
        pathlib_write(outside, self.manifest)
        self.assertEqual(self.code(argv=["b", "run", self.inst, outside]), "BOOT_MANIFEST_PATH_INVALID")
        self.assertEqual(self.code(argv=["b", "run", self.inst, self.inst + "/../inst/manifest.json"]),
                         "BOOT_MANIFEST_PATH_INVALID")
        link = os.path.join(self.inst, "m-link.json")
        os.symlink(self.mf, link)
        with self.assertRaises(OSError):  # O_NOFOLLOW: refused at open, never read
            self._launch(argv=["b", "run", self.inst, link])
        fifo = os.path.join(self.inst, "m-fifo.json")
        os.mkfifo(fifo)  # would hang a blocking open/read as root
        self.assertEqual(self.code(argv=["b", "run", self.inst, fifo]), "BOOT_FILE_UNSAFE")
        big = os.path.join(self.inst, "m-big.json")
        pathlib_write(big, b"x" * (self.m.MAX_FILE + 1))
        self.assertEqual(self.code(argv=["b", "run", self.inst, big]), "BOOT_FILE_UNSAFE")
        os.chmod(self.mf, 0o666)  # group/other-writable manifest
        self.assertEqual(self.code(), "BOOT_FILE_UNSAFE")
        os.chmod(self.mf, 0o644)
        os.chmod(self.inst, 0o777)  # writable install dir
        self.assertEqual(self.code(), "BOOT_DIR_UNSAFE")
        os.chmod(self.inst, 0o755)
        self.assertEqual(os.listdir(self.ev), [])

    def test_launcher_loader_cannot_reach_disk_or_unknown_modules(self):
        self._launcher_install(b"def launch(load, record):\n    load('not-in-manifest.py')\n")
        self.assertEqual(self.code(), "BOOT_MODULE_UNKNOWN")

    def test_helpers_are_hashed_owned_by_the_os_and_never_symlinked(self):
        self._launcher_install()
        self.assertTrue(self._launch()["ok"])
        self._v2(helper_sha="0" * 64)
        self.assertEqual(self.code(), "BOOT_HASH_MISMATCH")
        self._v2()
        self.assertEqual(self.code(os_uid=UID + 12345), "BOOT_FILE_UNSAFE")  # not OS-owned
        helper = self._fake_helper()
        link = os.path.join(self.base, "linkkeygen")
        os.symlink(helper, link)
        files = {n: sha(rd(os.path.join(self.inst, n))) for n in sorted(os.listdir(self.inst)) if n.endswith(".py")}
        self.manifest = canon({"files": files, "helpers": {"ssh-keygen": {"path": link, "sha256": sha(rd(helper))}},
                               "interpreter": {"path": self.real, "sha256": sha(rd(self.real))}})
        self.pin = sha(self.manifest)
        pathlib_write(self.mf, self.manifest)
        self.assertEqual(self.code(), "BOOT_HELPER_PATH_UNSAFE")

    def test_helpers_key_shape_is_strict(self):
        files = {n: sha(rd(os.path.join(self.inst, n))) for n in sorted(os.listdir(self.inst))}
        interp = {"path": self.real, "sha256": sha(rd(self.real))}
        for helpers in ({"Bad Name": {"path": "/usr/bin/x", "sha256": "0" * 64}},
                        {"x": {"path": "usr/bin/x", "sha256": "0" * 64}},
                        {"x": {"path": "/usr/../bin/x", "sha256": "0" * 64}},
                        {"x": {"path": "/usr/bin/x", "sha256": "0" * 64, "extra": 1}},
                        {"x": "nope"}):
            self.manifest = canon({"files": files, "helpers": helpers, "interpreter": interp})
            self.pin = sha(self.manifest)
            self.stop("BOOT_MANIFEST_INVALID")

    def test_pin_is_read_without_blocking_on_a_real_terminal_and_without_echo(self):
        """P1-A: a pseudo-terminal that receives ONLY pin+ENTER must return (no second ENTER / CTRL-D)."""
        import pty
        import select
        import termios
        master, slave = pty.openpty()
        helper = os.path.join(self.base, "readpin.py")
        pathlib_write(helper,
            "import importlib.util,sys\n"
            "s=importlib.util.spec_from_file_location('b',sys.argv[1]);m=importlib.util.module_from_spec(s);s.loader.exec_module(m)\n"
            "print('PIN=' + m._read_pin(sys.stdin))\n")
        proc = subprocess.Popen([self.real, "-I", "-S", helper, str(SRC)], stdin=slave, stdout=subprocess.PIPE,
                                stderr=subprocess.PIPE, env={}, text=True)
        os.close(slave)
        self.addCleanup(os.close, master)
        try:
            for _ in range(100):  # wait until the child has switched echo off (it then blocks in readline)
                if not termios.tcgetattr(master)[3] & termios.ECHO:
                    break
                select.select([], [], [], 0.05)
            os.write(master, (self.pin + "\n").encode())
            out, err = proc.communicate(timeout=15)  # a hang here is the P1-A regression
        except subprocess.TimeoutExpired:
            proc.kill()
            proc.communicate()
            self.fail("pin read blocked on a real terminal")
        self.assertEqual(out.strip(), "PIN=" + self.pin, err)
        echoed = b""
        try:
            while select.select([master], [], [], 0.2)[0]:
                echoed += os.read(master, 4096)
        except OSError:  # EIO once the child side is closed
            pass
        self.assertNotIn(self.pin.encode(), echoed)  # not echoed back to the terminal

    def test_pin_from_a_pipe_never_blocks_waiting_for_eof_and_rejects_trailing_bytes(self):
        r, w = os.pipe()
        self.addCleanup(lambda: [os.close(x) for x in (r, w) if x >= 0])
        os.write(w, (self.pin + "\n").encode())  # writer stays OPEN: no EOF, no more data
        with os.fdopen(os.dup(r), "r") as pipe_in:
            self.assertEqual(self.m._read_pin(pipe_in), self.pin)
        os.write(w, (self.pin + "\nextra").encode())
        with os.fdopen(os.dup(r), "r") as pipe_in:
            with self.assertRaises(self.m.Stop):
                self.m._read_pin(pipe_in)
        os.write(w, b"x" * 120)  # no newline within 100 bytes
        with os.fdopen(os.dup(r), "r") as pipe_in:
            with self.assertRaises(self.m.Stop):
                self.m._read_pin(pipe_in)

    def test_main_run_is_wired_and_prints_fixed_codes(self):
        self._launcher_install()
        out = subprocess.run([self.real, "-I", "-S", str(SRC), "run", self.inst, self.mf], input="0" * 64 + "\n",
                             capture_output=True, text=True, env={})
        self.assertEqual(out.returncode, 1)
        self.assertEqual(out.stdout.strip(), "BOOT_NOT_ROOT" if UID != 0 else "BOOT_DIR_UNSAFE")


if __name__ == "__main__":
    unittest.main()
