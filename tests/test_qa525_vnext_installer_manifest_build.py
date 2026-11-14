import copy
import hashlib
import importlib.util
import io
import json
import os
import stat
import tempfile
import unittest
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
QA = ROOT / "scripts" / "qa"


def load(path, name):
    spec = importlib.util.spec_from_file_location(name, path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


B = load(ROOT / "scripts/qa/operators/qa525-vnext-installer-manifest-build.py", "qa525_imb")
BOOT = load(QA / "qa525-vnext-root-bootstrap.py", "qa525_boot_t")
LCFG = load(QA / "qa525-vnext-root-launch-config.py", "qa525_lcfg_t")
SRC = "b" * 40
ROOTDIR = "Proyecto-Centenario-QA-srv01-" + SRC[:12]
H = "a" * 64
FP = "SHA256:" + "A" * 43


def good_config(asset_hex):
    return {
        "pins": {"ops_source_sha": "1" * 40, "app_source_sha": "2" * 40, "app_archive_sha256": "sha256:" + "3" * 64,
                 "plan_sha256": "sha256:" + "4" * 64, "admission_sha256": "sha256:" + "5" * 64,
                 "policy_hash": "sha256:" + "6" * 64},
        "launcher_pins": {"release_asset_sha256": "sha256:" + asset_hex, "old_launcher_sha256": "7" * 64,
                          "candidate_operator_sha256": "8" * 64},
        "allowed_signers": "ops ssh-ed25519 AAAA", "go_fingerprint": FP, "ops_v3_sha256": "sha256:" + "9" * 64,
        "app_subject_sha256": "sha256:" + "a" * 64, "inbox": "/var/lib/x/inbox", "temp_root": "/var/lib/x/tmp",
        "socket_path": "/run/x.sock", "journal_root": "/var/lib/x/j", "lease_dir": "/run/x",
        "peer_uid": 1000, "timeout": 5, "children": dict(B.CHILDREN), "g6_pins": None,
        "authenticity": {"ops_allowed_signers": "ops ssh-ed25519 BBBB", "ops_signature_sha256": "sha256:" + "b" * 64,
                         "app_bundle_sha256": "sha256:" + "c" * 64, "trusted_root_sha256": "sha256:" + "d" * 64,
                         "app_archive_path": "/var/lib/x/app.zip", "drop_uid": 65534, "drop_gid": 65534,
                         "source_repository_id": "123456"},
        "apiweb": None}


def make_release(mutate=None, extra=None, inner_names=None, outer_extra=None, manifest_override=None):
    files = {"BUILD-INFO.txt": ("SOURCE_SHA=%s\n%s\n" % (SRC, B.STORAGE_MARK)).encode(),
             "docs/readme.md": b"hello\n"}
    for p in sorted(QA.glob("qa525-vnext-*.py")):
        files["scripts/qa/" + p.name] = p.read_bytes()
    files.update(extra or {})
    if mutate:
        mutate(files)
    lines = "".join("%s  ./%s\n" % (hashlib.sha256(v).hexdigest(), k) for k, v in sorted(files.items()))
    files["MANIFEST.sha256"] = (manifest_override if manifest_override is not None else lines).encode()
    inner = io.BytesIO()
    with zipfile.ZipFile(inner, "w", zipfile.ZIP_DEFLATED) as z:
        for k, v in (inner_names(files) if inner_names else files).items():
            z.writestr(ROOTDIR + "/" + k, v)
    outer = io.BytesIO()
    with zipfile.ZipFile(outer, "w") as z:
        z.writestr(ROOTDIR + ".zip", inner.getvalue())
        z.writestr(ROOTDIR + ".zip.verification.json", b"{}")
        z.writestr("installable-provenance.sigstore.json", b"{}")
        for k, v in (outer_extra or {}).items():
            z.writestr(k, v)
    return outer.getvalue()


def kwargs(asset, cfg=None, **over):
    asset_hex = hashlib.sha256(asset).hexdigest()
    raw = json.dumps(cfg if cfg is not None else good_config(asset_hex)).encode()
    base = dict(asset_sha256=asset_hex, source_sha=SRC, config_name="launch-config.json", config_bytes=raw,
                interpreter={"path": "/usr/bin/python3.14", "sha256": H},
                helpers={"gh": {"path": "/usr/bin/gh", "sha256": "c" * 64},
                         "ssh-keygen": {"path": "/usr/bin/ssh-keygen", "sha256": "d" * 64}})
    base.update(over)
    return base


class BuildTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.asset = make_release()

    def stop(self, code, asset=None, **over):
        asset = asset if asset is not None else self.asset
        with self.assertRaises(B.Stop) as cm:
            B.build_manifest(asset, **kwargs(asset, **over))
        self.assertEqual(str(cm.exception), code)

    def test_positive_bootstrap_accepts_the_manifest_and_runs_repeat_identically(self):
        blob, info = B.build_manifest(self.asset, **kwargs(self.asset))
        blob2, _ = B.build_manifest(self.asset, **kwargs(self.asset))
        self.assertEqual(blob, blob2)
        pin = hashlib.sha256(blob).hexdigest()
        self.assertEqual(info["manifest_sha256"], pin)
        files, interp, helpers, config = BOOT.check_manifest_pin(blob, pin)  # the real PRE-4 gate
        self.assertIn(B.LAUNCHER, files)
        self.assertIn(B.CONFIG_MODULE, files)
        self.assertEqual(set(helpers), {"gh", "ssh-keygen"})
        self.assertNotIn(B.BOOTSTRAP, files)
        for child in B.CHILDREN.values():
            self.assertIn(child, files)
        self.assertEqual(files[B.LAUNCHER], hashlib.sha256((QA / B.LAUNCHER).read_bytes()).hexdigest())
        self.assertEqual(info["bootstrap_sha256"], hashlib.sha256((QA / B.BOOTSTRAP).read_bytes()).hexdigest())

    def test_closure_covers_every_module_the_launcher_loads_by_name(self):
        _, code = B.load_release(self.asset, hashlib.sha256(self.asset).hexdigest(), SRC)
        names = B.closure(code)
        for dep in ("qa525-vnext-root-locks.py", "qa525-vnext-root-go-verify.py", "qa525-vnext-root-authenticity.py"):
            self.assertIn(dep, names)

    def test_config_validator_matches_the_audited_launch_config(self):
        good = good_config("e" * 64)

        def audited(c):
            c = copy.deepcopy(c)
            for path in BOOT.BYTES_FIELDS:
                h = c
                for k in path[:-1]:
                    h = h[k]
                h[path[-1]] = h[path[-1]].encode("ascii")
            try:
                LCFG.materialize(preverified_config=c)
                return True
            except LCFG.Stop:
                return False

        def mine(c):
            try:
                B.validate_config(json.dumps(c).encode())
                return True
            except B.Stop:
                return False
        muts = [lambda c: None, lambda c: c.pop("apiweb"), lambda c: c.update(extra=1),
                lambda c: c["pins"].update(plan_sha256="x"), lambda c: c["pins"].update(ops_source_sha="1" * 39),
                lambda c: c.update(go_fingerprint="SHA256:short"), lambda c: c.update(peer_uid=0),
                lambda c: c.update(timeout=61), lambda c: c.update(timeout=0), lambda c: c.update(timeout=True),
                lambda c: c["authenticity"].update(drop_uid=1000), lambda c: c.update(inbox="rel/path"),
                lambda c: c.update(socket_path="/a/../b"), lambda c: c.update(children={}),
                lambda c: c["children"].update(LAUNCHER_ONLY="evil.py"),
                lambda c: c.update(g6_pins={"g6_command": "preflight", "g6_operation_sha256": "sha256:" + "1" * 64}),
                lambda c: c.update(g6_pins={"g6_command": "nope", "g6_operation_sha256": "sha256:" + "1" * 64}),
                lambda c: c.update(apiweb=[]), lambda c: c.update(allowed_signers=""),
                lambda c: c["authenticity"].update(source_repository_id="0"),
                lambda c: c["launcher_pins"].update(old_launcher_sha256="sha256:" + "7" * 64)]
        for i, m in enumerate(muts):
            c = copy.deepcopy(good)
            m(c)
            self.assertEqual(mine(c), audited(c), "mutation %d" % i)

    def test_config_adversarial(self):
        for raw in (b"", b"[]", b"{", b'{"a":1,"a":2}', b"\xff", b"x" * (B.MAX_CONFIG + 1), b'{"timeout":NaN}'):
            self.stop("BUILD_CONFIG_INVALID", config_bytes=raw)
        bad = good_config(hashlib.sha256(self.asset).hexdigest())
        bad["launcher_pins"]["release_asset_sha256"] = "sha256:" + "0" * 64
        self.stop("BUILD_CONFIG_ASSET_PIN_MISMATCH", cfg=bad)
        self.stop("BUILD_CONFIG_NAME_INVALID", config_name="../x.json")
        self.stop("BUILD_CONFIG_NAME_INVALID", config_name="x.py")

    def test_source_asset_and_manifest_forgeries(self):
        self.stop("BUILD_ASSET_DIGEST_MISMATCH", asset_sha256="0" * 64, cfg=good_config("0" * 64))
        self.stop("BUILD_INPUT_INVALID", source_sha="zz")
        other = make_release(mutate=lambda f: f.__setitem__("BUILD-INFO.txt", ("SOURCE_SHA=%s\n%s\n" % ("c" * 40, B.STORAGE_MARK)).encode()))
        self.stop("BUILD_SOURCE_BINDING_MISMATCH", asset=other)
        self.stop("BUILD_MANIFEST_NONCANONICAL", asset=make_release(manifest_override="junk\n"))
        injected = make_release(extra={"scripts/qa/qa525-vnext-evil.py": b"x=1\n"},
                                manifest_override=None)
        # injected file IS listed in a consistent manifest => drift is detected by the closure reference only if referenced
        B.build_manifest(injected, **kwargs(injected))
        omitted = make_release(inner_names=lambda f: {k: v for k, v in f.items() if k != "docs/readme.md"})
        self.stop("BUILD_MANIFEST_FILE_SET_MISMATCH", asset=omitted)
        drift = make_release(inner_names=lambda f: {**f, "docs/readme.md": b"tampered\n"})
        self.stop("BUILD_MANIFEST_CHECKSUM_MISMATCH", asset=drift)
        dup = make_release(manifest_override="%s  ./a\n%s  ./a\n" % (H, H))
        self.stop("BUILD_MANIFEST_PATH_INVALID", asset=dup)

    def test_archive_structure_attacks(self):
        self.stop("BUILD_OUTER_CONTENT_UNEXPECTED", asset=make_release(outer_extra={"extra.txt": b"x"}))
        trav = make_release(inner_names=lambda f: {**f, "../../etc/passwd": b"x"})
        self.stop("BUILD_UNSAFE_ARCHIVE_MEMBER", asset=trav)
        self.stop("BUILD_ARCHIVE_INVALID", asset=b"notazip")

    def test_symlink_member_refused(self):
        inner = io.BytesIO()
        with zipfile.ZipFile(inner, "w") as z:
            info = zipfile.ZipInfo(ROOTDIR + "/link")
            info.external_attr = (stat.S_IFLNK | 0o777) << 16
            z.writestr(info, "target")
        outer = io.BytesIO()
        with zipfile.ZipFile(outer, "w") as z:
            z.writestr(ROOTDIR + ".zip", inner.getvalue())
            z.writestr(ROOTDIR + ".zip.verification.json", b"{}")
            z.writestr("installable-provenance.sigstore.json", b"{}")
        a = outer.getvalue()
        self.stop("BUILD_ARCHIVE_SYMLINK", asset=a)

    def test_closure_missing_member_and_helper_gaps(self):
        missing = make_release(mutate=lambda f: f.pop("scripts/qa/qa525-vnext-root-locks.py"))
        self.stop("BUILD_CLOSURE_MEMBER_MISSING", asset=missing)
        self.stop("BUILD_HELPERS_INCOMPLETE", helpers={"ssh-keygen": {"path": "/usr/bin/ssh-keygen", "sha256": H}})
        no_boot = make_release(mutate=lambda f: f.pop("scripts/qa/qa525-vnext-root-bootstrap.py"))
        self.stop("BUILD_CLOSURE_MEMBER_MISSING", asset=no_boot)

    def test_host_pin_parsing(self):
        for bad in ("/usr/bin/x", "rel=%s" % H, "/usr/bin/x=zz", "/a/../b=%s" % H, "a=b=c=d"):
            with self.assertRaises(B.Stop):
                B._hostpin(bad, False)
        self.assertEqual(B._hostpin("gh=/usr/bin/gh=" + H, True), ("gh", {"path": "/usr/bin/gh", "sha256": H}))


class CliTests(unittest.TestCase):
    def test_missing_physical_inputs_listed_and_nothing_written(self):
        with tempfile.TemporaryDirectory() as d:
            out = os.path.join(d, "m.json")
            import contextlib
            buf = io.StringIO()
            with contextlib.redirect_stdout(buf):
                rc = B.main(["x", "--output", out])
            self.assertEqual(rc, 3)
            self.assertIn("interpreter", buf.getvalue())
            self.assertIn("helper:gh", buf.getvalue())
            self.assertFalse(os.path.exists(out))

    def test_end_to_end_writes_0600_exclusive_and_never_prints_config(self):
        asset = make_release()
        asset_hex = hashlib.sha256(asset).hexdigest()
        with tempfile.TemporaryDirectory() as d:
            ap, cp, out = (os.path.join(d, n) for n in ("a.zip", "launch-config.json", "m.json"))
            Path(ap).write_bytes(asset)
            Path(cp).write_text(json.dumps(good_config(asset_hex)))
            argv = ["x", "--asset", ap, "--asset-sha256", asset_hex, "--source-sha", SRC, "--config", cp,
                    "--interpreter", "/usr/bin/python3.14=" + H, "--helper", "gh=/usr/bin/gh=" + "c" * 64,
                    "--helper", "ssh-keygen=/usr/bin/ssh-keygen=" + "d" * 64, "--output", out]
            import contextlib
            buf = io.StringIO()
            with contextlib.redirect_stdout(buf):
                self.assertEqual(B.main(argv), 0)
                self.assertEqual(B.main(argv), 1)  # exclusive create: never overwrites
            text = buf.getvalue()
            self.assertIn("AUTHORITY=NONE", text)
            self.assertIn("SIGSTORE_VERIFIED_BY_BUILDER=false", text)
            for secret in ("ssh-ed25519", FP, "AAAA"):
                self.assertNotIn(secret, text)
            self.assertEqual(stat.S_IMODE(os.stat(out).st_mode), 0o600)
            pin = hashlib.sha256(Path(out).read_bytes()).hexdigest()
            self.assertIn(pin, text)
            BOOT.check_manifest_pin(Path(out).read_bytes(), pin)
            os.symlink(ap, os.path.join(d, "lnk.zip"))
            argv2 = list(argv)
            argv2[argv2.index("--asset") + 1] = os.path.join(d, "lnk.zip")
            argv2[argv2.index("--output") + 1] = os.path.join(d, "m2.json")
            with contextlib.redirect_stdout(io.StringIO()):
                self.assertEqual(B.main(argv2), 1)
            self.assertFalse(os.path.exists(os.path.join(d, "m2.json")))


if __name__ == "__main__":
    unittest.main()
