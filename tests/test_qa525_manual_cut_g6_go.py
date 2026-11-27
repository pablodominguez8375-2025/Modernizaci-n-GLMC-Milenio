"""Signed G6 GO (option A): Ed25519 via the pinned OpenSSH verifier, five canonical fields, TTL <= 900 s, fresh nonce.
Real `ssh-keygen -Y sign/verify` with an EPHEMERAL test key; the real OPS key / host binary hash are NOT pinned in the
driver, so production is STOP.  Hermetic: no host."""
import hashlib
import json
import os
import pathlib
import sys
import time
import unittest
from contextlib import redirect_stdout
import io

HERE = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import test_qa525_manual_cut_g6_adapter as ad  # noqa: E402
import g6_go_support as gs  # noqa: E402
g6t = ad.g6t
cut = ad.cut
AUTH = "b" * 64
DRIVER = hashlib.sha256(b"driver-final").hexdigest()
OP = hashlib.sha256(b"op").hexdigest()


class Case(ad.AdapterCycle):
    test_all_five_commands_through_the_adapter_each_with_its_own_marker = None  # inherited cycle test: not this suite's job

    def setUp(self):
        super().setUp()
        self.signer = gs.Signer(cut)
        self.addCleanup(self.signer.close)
        self.now = int(time.time())

    def check(self, go_sig, command="preflight", op=OP, driver=DRIVER, now=None, cfg=None):
        go, sig = go_sig
        return cut.g6_check_go(open(go, "rb").read(), open(sig, "rb").read(), command, op, driver,
                               self.now if now is None else now, cfg or self.signer.cfg())

    def stop(self, code, *a, **k):
        with self.assertRaises(cut.Stop) as cm:
            self.check(*a, **k)
        self.assertEqual(str(cm.exception), code)

    def go(self, **k):
        return self.signer.go(k.pop("command", "preflight"), k.pop("op", OP), k.pop("driver", DRIVER), self.now, **k)


@unittest.skipUnless(gs.have_keygen(), "ssh-keygen required")
class SignedGo(Case):
    def test_valid_signed_go_passes_and_returns_exactly_five_fields(self):
        go = self.check(self.go())
        self.assertEqual(set(go), {"command", "op_sha256", "nonce", "expires_at", "driver_sha256"})
        self.assertEqual(go["expires_at"], self.now + 600)

    def test_ttl_boundary_900_ok_901_rejected_and_past_rejected(self):
        self.check(self.go(ttl=900))
        self.stop("CUT_G6_GO_TTL_EXCEEDED", self.go(ttl=901))
        self.stop("CUT_G6_GO_TTL_EXCEEDED", self.go(ttl=3600))
        self.stop("CUT_G6_GO_EXPIRED", self.go(ttl=0))
        self.stop("CUT_G6_GO_EXPIRED", self.go(ttl=-5))
        self.stop("CUT_G6_GO_EXPIRED", self.go(ttl=600), now=self.now + 600)  # now == expires_at is already expired

    def test_every_field_altered_after_signing_breaks_the_signature(self):
        base = self.go()
        data = json.loads(open(base[0], "rb").read())
        alt = {"command": "init", "op_sha256": "1" * 64, "driver_sha256": "2" * 64, "nonce": "3" * 32,
               "expires_at": data["expires_at"] + 1}
        for field, value in alt.items():
            forged = dict(data)
            forged[field] = value
            path = os.path.join(self.signer.dir, "forged-%s.json" % field)
            with open(path, "wb") as fh:
                fh.write(cut.g6_go_canonical(forged))
            os.chmod(path, 0o600)
            self.stop("CUT_G6_GO_SIGNATURE_INVALID", (path, base[1]))

    def test_signed_but_for_another_command_operation_or_driver(self):
        self.stop("CUT_G6_GO_MISMATCH", self.go(command="init"))
        self.stop("CUT_G6_GO_MISMATCH", self.go(op="4" * 64))
        self.stop("CUT_G6_GO_MISMATCH", self.go(driver="5" * 64))

    def test_wrong_key_wrong_namespace_and_unsigned_garbage(self):
        self.stop("CUT_G6_GO_SIGNATURE_INVALID", self.go(key=self.signer.other_key()))
        self.stop("CUT_G6_GO_SIGNATURE_INVALID", self.go(namespace="qa525-go-cut-v1@pmgm"))  # a CUT GO cannot open G6
        go, sig = self.go()
        junk = os.path.join(self.signer.dir, "junk.sig")
        open(junk, "wb").write(b"not a signature")
        self.stop("CUT_G6_GO_SIGNATURE_INVALID", (go, junk))

    def test_signed_but_not_canonical_or_with_extra_missing_or_mistyped_fields(self):
        good = {"command": "preflight", "op_sha256": OP, "nonce": "a" * 32, "expires_at": self.now + 600,
                "driver_sha256": DRIVER}
        canon = cut.g6_go_canonical(good)
        spaced = json.dumps(good, sort_keys=True).encode()  # spaces after separators
        dup = canon[:-1] + b',"nonce":"' + b"b" * 32 + b'"}'
        bad = [("extra", {"extra": {"issued_at": 1}}, None), ("spaced", {}, spaced), ("dup", {}, dup),
               ("nl", {}, canon + b"\n"), ("nonce_short", {"nonce": "a" * 31}, None), ("nonce_upper", {"nonce": "A" * 32}, None),
               ("bool_exp", {"expires_at": True}, None), ("str_exp", {"expires_at": str(self.now + 600)}, None),
               ("float_exp", {"expires_at": float(self.now + 600)}, None), ("op_short", {"op_sha256": "a" * 63}, None),
               ("drv_prefixed", {"driver_sha256": "sha256:" + DRIVER}, None)]
        for name, over, raw in bad:
            go = self.signer.go("preflight", OP, DRIVER, self.now, nonce="a" * 32, extra=over, raw=raw)
            self.stop("CUT_G6_GO_FORMAT_INVALID", go)
        missing = dict(good)
        del missing["nonce"]
        go = self.signer.go("preflight", OP, DRIVER, self.now, raw=cut.g6_go_canonical(missing))
        self.stop("CUT_G6_GO_FORMAT_INVALID", go)

    def test_verifier_pins_are_checked_before_anything_runs(self):
        go = self.go()
        for over, code in (({"keygen_sha256": "sha256:" + "0" * 64}, "CUT_G6_GO_KEYGEN_PIN_MISMATCH"),
                           ({"fingerprint": "SHA256:" + "A" * 43}, "CUT_G6_GO_FINGERPRINT_MISMATCH"),
                           ({"keygen_path": "/usr/bin/../bin/ssh-keygen"}, "CUT_G6_GO_KEYGEN_INVALID"),
                           ({"required_euid": os.geteuid() + 1}, "CUT_G6_GO_ROOT_REQUIRED"),
                           ({"temp_root": self.signer.dir + "/absent"}, "CUT_G6_GO_TEMP_UNSAFE"),
                           ({"binary_uid": os.geteuid() + 12345}, "CUT_G6_GO_KEYGEN_UNTRUSTED")):
            self.stop(code, go, cfg=self.signer.cfg(**over))
        os.chmod(self.signer.work, 0o755)
        self.stop("CUT_G6_GO_TEMP_UNSAFE", go)
        os.chmod(self.signer.work, 0o700)
        self.assertEqual(os.listdir(self.signer.work), [])  # transient verifier dir always removed

    def test_signers_must_be_exactly_the_pinned_ops_key(self):
        other = gs.Signer(cut)
        self.addCleanup(other.close)
        go = self.go()
        self.stop("CUT_G6_GO_FINGERPRINT_MISMATCH", go, cfg=self.signer.cfg(signers=other.signers))  # key swap
        self.stop("CUT_G6_GO_SIGNERS_INVALID", go, cfg=self.signer.cfg(signers=self.signer.signers + b"x"))
        self.stop("CUT_G6_GO_SIGNERS_INVALID", go, cfg=self.signer.cfg(
            signers=self.signer.signers.replace(b"qa525-go-g6-v1@pmgm", b"qa525-go-cut-v1@pmgm")))

    def test_production_pins_are_the_proven_values_and_mode_is_still_off(self):
        self.assertIs(cut.G6_MODE_ENABLED, False)
        self.assertEqual(cut.G6_OPS_FINGERPRINT, "SHA256:DN64Bjj/7SMjP7LX9Nwrcp3lPRBpT2cKPs1NmaL1Y5k")
        self.assertEqual(cut._g6_signers_fingerprint(cut.G6_OPS_ALLOWED_SIGNERS), cut.G6_OPS_FINGERPRINT)
        self.assertEqual(cut.G6_OPS_ALLOWED_SIGNERS.count(b"\n"), 1)
        self.assertEqual(cut.G6_KEYGEN_SHA256, "sha256:2dd834413c9d3f4c0e3535b9fcf2e96e14c11f3d36fd23b6c5fc24e50411d051")
        self.assertEqual(cut.G6_KEYGEN_PATH, "/usr/bin/ssh-keygen")
        self.assertEqual(cut.G6_GO_NAMESPACE, "qa525-go-g6-v1@pmgm")
        self.assertEqual(cut.G6_GO_TTL_MAX, 900)
        cfg = cut._g6_go_cfg({})
        self.assertEqual((cfg["required_euid"], cfg["binary_uid"], cfg["temp_uid"]), (0, 0, 0))
        self.assertEqual(cfg["temp_root"], "/var/lib/pmgm-qa525-manual-cut-e5c40d3a")
        for bad in ({"signers": None}, {"keygen_sha256": None}):
            with self.assertRaises(cut.Stop) as cm:
                cut._g6_go_cfg({"go_cfg": dict(cfg, **bad)})
            self.assertEqual(str(cm.exception), "CUT_G6_GO_VERIFIER_NOT_PINNED")

    def test_a_foreign_key_or_a_foreign_keygen_binary_never_verifies_with_the_production_pins(self):
        go = self.go()
        # production signers + production fingerprint, but the GO was signed by the test key -> signature invalid
        cfg = self.signer.cfg(signers=cut.G6_OPS_ALLOWED_SIGNERS, fingerprint=cut.G6_OPS_FINGERPRINT)
        self.stop("CUT_G6_GO_SIGNATURE_INVALID", go, cfg=cfg)
        # production keygen hash pinned against the test host's binary -> mismatch unless it is glmdch's exact binary
        if gs.have_keygen() and cut.G6_KEYGEN_SHA256 != "sha256:" + hashlib.sha256(open(gs.KEYGEN, "rb").read()).hexdigest():
            self.stop("CUT_G6_GO_KEYGEN_PIN_MISMATCH", go, cfg=self.signer.cfg(keygen_sha256=cut.G6_KEYGEN_SHA256))


@unittest.skipUnless(gs.have_keygen(), "ssh-keygen required")
class SignedGoThroughMain(Case):
    def deps(self, env_obj, **over):
        d = {"enabled": True, "lib_dir": str(HERE.parent / "scripts/qa"), "root": self.root, "uid": g6t.UID,
             "env": lambda mods: env_obj, "lock_paths": (), "state_root": self.state, "driver_sha256": DRIVER,
             "go_cfg": self.signer.cfg()}
        d.update(over)
        return d

    def run_main(self, argv, deps):
        out = io.StringIO()
        self.lines.clear()
        with redirect_stdout(out):
            code = cut.main(argv, deps)
        return code, out.getvalue()

    def argv(self, command="preflight", go=None, auth=AUTH, **kw):
        _, op = cut.g6_operation(self.prun, command, [])
        go = go or self.signer.go(command, op, DRIVER, int(time.time()), **kw)
        return ["--g6", command, "--authorization-sha256", auth, "--go", go[0], "--go-sig", go[1]]

    def env(self, *a, **k):
        return super().env(g6t.RunFakeDocker(minio="false", image=('sha256:%s ["%s"]\n' % ("a" * 64, g6t.vtest.IMAGE)).encode()))

    def test_valid_go_dispatches_and_its_nonce_is_what_the_journal_spends(self):
        nonce = "c" * 32
        self.run_main(self.argv(nonce=nonce), self.deps(self.env()))
        self.assertTrue(any(l.startswith("PREFLIGHT_READY=") for l in self.lines))
        spent = hashlib.sha256(nonce.encode()).hexdigest() + ".spent"
        self.assertIn(spent, os.listdir(self.root))

    def test_replayed_go_nonce_is_stop_even_with_a_new_authorization_text(self):
        nonce = "d" * 32
        self.run_main(self.argv(nonce=nonce), self.deps(self.env()))  # readiness is host-dependent; the dispatch ran
        self.assertTrue(any(l.startswith("PREFLIGHT_READY=") for l in self.lines), self.lines)
        code, out = self.run_main(self.argv(nonce=nonce, auth="e" * 64), self.deps(self.env()))
        self.assertEqual(code, 1)
        self.assertTrue(out.startswith("QA525_MANUAL_CUT_G6=STOP "), out)  # fixed code (the journal's own Stop is closed)
        self.assertNotIn("Traceback", out)
        self.assertEqual(self.lines, [])  # the second dispatch never ran

    def test_bad_go_stops_before_any_marker_lock_or_dispatch(self):
        for kw, code_expected in (({"ttl": 901}, "CUT_G6_GO_TTL_EXCEEDED"), ({"ttl": 0}, "CUT_G6_GO_EXPIRED"),
                                  ({"key": self.signer.other_key()}, "CUT_G6_GO_SIGNATURE_INVALID")):
            code, out = self.run_main(self.argv(**kw), self.deps(self.env()))
            self.assertEqual((code, out), (1, "QA525_MANUAL_CUT_G6=STOP %s\n" % code_expected))
        self.assertEqual(os.listdir(self.root), [])
        self.assertFalse(any(l.startswith("PREFLIGHT_READY=") for l in self.lines))

    def test_go_for_another_driver_hash_is_stop(self):
        code, out = self.run_main(self.argv(driver_sha256="9" * 64), self.deps(self.env()))
        self.assertEqual((code, out), (1, "QA525_MANUAL_CUT_G6=STOP CUT_G6_GO_MISMATCH\n"))
        self.assertEqual(os.listdir(self.root), [])

    def test_go_files_must_be_private_regular_and_present(self):
        go = self.signer.go("preflight", cut.g6_operation(self.prun, "preflight", [])[1], DRIVER, int(time.time()))
        os.chmod(go[0], 0o644)
        code, out = self.run_main(["--g6", "preflight", "--authorization-sha256", AUTH, "--go", go[0], "--go-sig", go[1]],
                                  self.deps(self.env()))
        self.assertEqual((code, out), (1, "QA525_MANUAL_CUT_G6=STOP CUT_G6_GO_FILE_INVALID\n"))
        code, out = self.run_main(["--g6", "preflight", "--authorization-sha256", AUTH, "--go", go[0] + ".none", "--go-sig", go[1]],
                                  self.deps(self.env()))
        self.assertEqual((code, out), (1, "QA525_MANUAL_CUT_G6=STOP CUT_G6_GO_FILE_INVALID\n"))
        self.assertEqual(os.listdir(self.root), [])

    def test_unpinned_config_is_stop_with_the_mode_enabled(self):
        d = self.deps(self.env(), go_cfg=dict(cut._g6_go_cfg({}), signers=None))
        code, out = self.run_main(self.argv(), d)
        self.assertEqual((code, out), (1, "QA525_MANUAL_CUT_G6=STOP CUT_G6_GO_VERIFIER_NOT_PINNED\n"))
        self.assertEqual(os.listdir(self.root), [])


if __name__ == "__main__":
    unittest.main()
