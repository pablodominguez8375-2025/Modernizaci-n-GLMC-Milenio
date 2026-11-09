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

    # ---- `run` (root launch): pin by stdin only, record BEFORE the launcher, helpers hashed ----
    def _launcher_install(self, body=b"CALLS = []\ndef launch(load, record):\n    CALLS.append(1)\n    return {'ok': True, 'files': sorted(record['files'])}\n"):
        self.write(self.m.LAUNCHER, body)
        self.build()
        self.ev = os.path.join(self.base, "ev-run")
        os.mkdir(self.ev, 0o755)
        self.mf = os.path.join(self.base, "mrun.json")
        pathlib_write(self.mf, self.manifest)

    def _launch(self, pin=None, argv=None, euid=0, **kw):
        import io
        pin = self.pin if pin is None else pin
        argv = argv or ["b", "run", self.inst, self.mf]
        args = dict(euid=euid, uid=UID, anchor=self.base, evidence_dir=self.ev)
        args.update(kw)
        with unittest.mock.patch.object(self.m.sys, "flags", type("F", (), self.flags)()):
            return self.m.launch(argv, io.StringIO(pin + "\n"), **args)

    def test_run_writes_the_record_then_hands_the_loader_to_the_launcher(self):
        self._launcher_install()
        out = self._launch()
        self.assertTrue(out["ok"])
        self.assertEqual(len(os.listdir(self.ev)), 1)  # pre-root-trust evidence exists

    def test_run_refuses_non_root_bad_pin_extra_stdin_and_wrong_argv_before_any_module(self):
        import io
        self._launcher_install()
        for kw, code in ((dict(euid=1000), "BOOT_NOT_ROOT"), (dict(pin="0" * 64), "BOOT_MANIFEST_PIN_MISMATCH"),
                         (dict(argv=["b", "run", self.inst]), "BOOT_USAGE"),
                         (dict(argv=["b", "run", self.inst, self.mf, "x"]), "BOOT_USAGE")):
            with self.assertRaises(self.m.Stop) as ctx:
                self._launch(**kw)
            self.assertEqual(str(ctx.exception), code)
        self.assertEqual(os.listdir(self.ev), [])  # no evidence, nothing loaded
        with unittest.mock.patch.object(self.m.sys, "flags", type("F", (), self.flags)()):
            with self.assertRaises(self.m.Stop) as ctx:
                self.m.launch(["b", "run", self.inst, self.mf], io.StringIO(self.pin + "\nextra\n"),
                              euid=0, uid=UID, anchor=self.base, evidence_dir=self.ev)
        self.assertEqual(str(ctx.exception), "BOOT_PIN_INVALID")

    def test_run_stops_when_launcher_altered_or_missing_from_the_manifest(self):
        self._launcher_install()
        self.write(self.m.LAUNCHER, b"def launch(load, record):\n    return {'evil': True}\n")
        with self.assertRaises(self.m.Stop) as ctx:
            self._launch()
        self.assertEqual(str(ctx.exception), "BOOT_HASH_MISMATCH")
        self.assertEqual(os.listdir(self.ev), [])
        os.unlink(os.path.join(self.inst, self.m.LAUNCHER))
        self.build()
        pathlib_write(self.mf, self.manifest)
        with self.assertRaises(self.m.Stop) as ctx:
            self._launch()
        self.assertEqual(str(ctx.exception), "BOOT_MODULE_UNKNOWN")

    def test_launcher_loader_cannot_reach_disk_or_unknown_modules(self):
        self._launcher_install(b"def launch(load, record):\n    load('not-in-manifest.py')\n")
        with self.assertRaises(self.m.Stop) as ctx:
            self._launch()
        self.assertEqual(str(ctx.exception), "BOOT_MODULE_UNKNOWN")

    def test_helpers_are_hashed_and_a_changed_or_symlinked_helper_stops(self):
        helper = os.path.join(self.base, "fakekeygen")
        pathlib_write(helper, b"#!/bin/sh\n")
        os.chmod(helper, 0o755)
        files = {n: sha(rd(os.path.join(self.inst, n))) for n in sorted(os.listdir(self.inst))}
        hm = lambda sh: canon({"files": files, "helpers": {"ssh-keygen": {"path": helper, "sha256": sh}},
                               "interpreter": {"path": self.real, "sha256": sha(rd(self.real))}})
        real_safe = self.m._safe_dir
        skip_base = lambda path, uid, anchor="/": None if path == self.base and uid == 0 else real_safe(path, uid, anchor)
        self.manifest = hm(sha(rd(helper)))
        self.pin = sha(self.manifest)
        self.stop("BOOT_DIR_UNSAFE")  # the helper lives in an unsafe (non-OS-owned) dir => STOP
        with unittest.mock.patch.object(self.m, "_safe_dir", skip_base):
            if UID == 0:
                self.assertEqual(self.verify().record["helpers"]["ssh-keygen"]["sha256"], sha(rd(helper)))
            else:
                self.stop("BOOT_FILE_UNSAFE")  # the OS owns helpers: a non-root-owned file is refused
            self.manifest = hm("0" * 64)
            self.pin = sha(self.manifest)
            self.stop("BOOT_HASH_MISMATCH" if UID == 0 else "BOOT_FILE_UNSAFE")
        link = os.path.join(self.base, "linkkeygen")
        os.symlink(helper, link)
        self.manifest = canon({"files": files, "helpers": {"ssh-keygen": {"path": link, "sha256": sha(rd(helper))}},
                               "interpreter": {"path": self.real, "sha256": sha(rd(self.real))}})
        self.pin = sha(self.manifest)
        self.stop("BOOT_HELPER_PATH_UNSAFE")

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

    def test_main_run_is_wired_and_prints_fixed_codes(self):
        mf = os.path.join(self.base, "m3.json")
        pathlib_write(mf, self.manifest)
        out = subprocess.run([self.real, "-I", "-S", str(SRC), "run", self.inst, mf], input="0" * 64 + "\n",
                             capture_output=True, text=True, env={})
        self.assertEqual(out.returncode, 1)
        self.assertEqual(out.stdout.strip(), "BOOT_NOT_ROOT" if UID != 0 else "BOOT_MANIFEST_PIN_MISMATCH")


if __name__ == "__main__":
    unittest.main()
