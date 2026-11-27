"""main() in g6 mode: OFF by default; closed grammar; one command per process; authorization text spent once;
params stage only after a successful review.  Hermetic: the runner's fake docker/restic, no host."""
import hashlib
import io
import os
import pathlib
import shutil
import sys
import unittest
from contextlib import redirect_stdout

HERE = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import test_qa525_manual_cut_g6_adapter as ad  # noqa: E402
import g6_go_support as gs  # noqa: E402
g6t = ad.g6t
cut = ad.cut
AUTH = "a" * 64


DRIVER_HEX = hashlib.sha256(b"d").hexdigest()


@unittest.skipUnless(gs.have_keygen(), "ssh-keygen required")
class G6Main(ad.AdapterCycle):
    def setUp(self):
        super().setUp()
        self.signer = gs.Signer(cut)
        self.addCleanup(self.signer.close)

    def deps(self, env_obj, **over):
        d = {"enabled": True, "lib_dir": str(HERE.parent / "scripts/qa"), "root": self.root, "uid": g6t.UID,
             "env": lambda mods: env_obj, "lock_paths": (), "state_root": self.state, "driver_sha256": DRIVER_HEX,
             "go_cfg": self.signer.cfg()}
        d.update(over)
        return d

    def call(self, argv, deps):
        out = io.StringIO()
        self.lines.clear()
        with redirect_stdout(out):
            code = cut.main(argv, deps)
        return code, out.getvalue()

    def argv(self, command, auth=AUTH, **opts):
        _, op = cut.g6_operation(self.prun, command, list(opts.items()))
        go, sig = self.signer.go(command, op, DRIVER_HEX, int(__import__("time").time()))
        a = ["--g6", command, "--authorization-sha256", auth, "--go", go, "--go-sig", sig]
        for k, v in opts.items():
            a += ["--" + k, v]
        return a

    def test_off_by_default_and_flag_is_false(self):
        self.assertIs(cut.G6_MODE_ENABLED, False)
        code, out = self.call(self.argv("preflight"), None)
        self.assertEqual((code, out), (1, "QA525_MANUAL_CUT_G6=STOP G6_MODE_DISABLED\n"))
        self.assertEqual(os.listdir(self.root), [])

    def test_grammar_is_closed(self):
        env = self.env(g6t.RunFakeDocker(minio="false"))
        g = ["--go", "/x/go.json", "--go-sig", "/x/go.json.sig"]
        bad = [["--g6"], ["--g6", "preflight"], ["--g6", "nope", "--authorization-sha256", AUTH, *g],
               ["--g6", "preflight", "--authorization-sha256", "zz", *g], ["--g6", "preflight", "--auth", AUTH, *g],
               ["--g6", "preflight", "--authorization-sha256", AUTH],
               ["--g6", "preflight", "--authorization-sha256", AUTH, "--go", "rel/go.json", "--go-sig", "/x/s"],
               ["--g6", "preflight", "--authorization-sha256", AUTH, "--go", "/x/../go.json", "--go-sig", "/x/s"],
               ["--g6", "init", "--authorization-sha256", AUTH, *g, "--restic-sha256"],
               ["--g6", "init", "--authorization-sha256", AUTH, *g, "positional"],
               ["--g6", "init", "--authorization-sha256", AUTH, *g, "--restic-sha256", "--other"],
               ["--g6", "init", "--authorization-sha256", AUTH, *g, "--bogus", "x"]]
        for argv in bad:
            code, out = self.call(argv, self.deps(env))
            self.assertEqual(code, 1, argv)
            self.assertTrue(out.startswith("QA525_MANUAL_CUT_G6=STOP CUT_G6_"), (argv, out))
        self.assertEqual(os.listdir(self.root), [])

    def test_full_cycle_one_authorization_per_command_and_params_stage(self):
        pre = self.env(g6t.RunFakeDocker(minio="false", image=('sha256:%s ["%s"]\n' % ("a" * 64, g6t.vtest.IMAGE)).encode()))
        self.call(self.argv("preflight", auth="1" * 64), self.deps(pre))
        self.assertTrue(any(l.startswith("PREFLIGHT_READY=") for l in self.lines), self.lines)  # readiness is host-dependent
        env = self.env(g6t.RunFakeDocker(minio=["true", "false"]))
        code, _ = self.call(self.argv("init", auth="2" * 64, **{"restic-sha256": g6t.PIN}), self.deps(env))
        self.assertEqual(code, 0, self.lines)
        init = {l.split("=", 1)[0]: l.split("=", 1)[1] for l in self.lines if "=" in l}
        pins = {"restic-sha256": g6t.PIN, "repo-id": init["RESTIC_REPO_ID"], "key-epoch": init["KEY_EPOCH"]}
        code, _ = self.call(self.argv("backup", auth="3" * 64, **pins), self.deps(env))
        self.assertEqual(code, 0, self.lines)
        backup = {l.split("=", 1)[0]: l.split("=", 1)[1] for l in self.lines if "=" in l}
        arch = backup["ARCHIVE_NAME"]
        os.makedirs(self.state + "/downloads-v2", mode=0o700, exist_ok=True)
        shutil.copyfile(self.state + "/exports-v2/" + arch, self.state + "/downloads-v2/" + arch)
        os.chmod(self.state + "/downloads-v2/" + arch, 0o600)
        import datetime as dt
        self.clock = g6t.NOW + dt.timedelta(hours=2)
        renv = self.env(g6t.vtest.VerifierFakeDocker())
        code, _ = self.call(self.argv("restore", auth="4" * 64, **pins, **{
            "image": g6t.vtest.IMAGE, "archive-name": arch, "archive-sha256": backup["ARCHIVE_SHA256"],
            "snapshot-id": backup["SNAPSHOT_ID"], "manifest-sha256": backup["MANIFEST_SHA256"]}), self.deps(renv))
        self.assertEqual(code, 0, self.lines)
        params = self.state + "/state-v2/" + cut.G6_PARAMS_NAME
        self.assertFalse(os.path.exists(params))  # params only after a successful REVIEW
        ropts = {"archive-name": arch, "drive-file-id": g6t.DRIVE, "repo-id": pins["repo-id"], "key-epoch": pins["key-epoch"]}
        code, _ = self.call(self.argv("review", auth="5" * 64, **ropts), self.deps(renv))
        self.assertEqual(code, 0, self.lines)
        self.assertEqual(oct(os.stat(params).st_mode & 0o777), "0o600")
        rec = cut.load_a0_g6_params(params, g6t.UID)
        self.assertEqual((rec["command"], rec["restic_repo_id"], rec["key_epoch"]), ("review", pins["repo-id"], pins["key-epoch"]))
        spent = sorted(n for n in os.listdir(self.root) if n.startswith("auth-"))
        self.assertEqual(len(spent), 5)  # one spent authorization per command
        self.assertEqual(len([n for n in os.listdir(self.root) if n.endswith(".spent") and not n.startswith("auth-")]), 5)

    def test_same_authorization_text_cannot_be_reused(self):
        pre = self.env(g6t.RunFakeDocker(minio="false", image=('sha256:%s ["%s"]\n' % ("a" * 64, g6t.vtest.IMAGE)).encode()))
        self.call(self.argv("preflight"), self.deps(pre))
        self.assertTrue(any(l.startswith("PREFLIGHT_READY=") for l in self.lines))
        code, out = self.call(self.argv("preflight"), self.deps(pre))
        self.assertEqual((code, out), (1, "QA525_MANUAL_CUT_G6=STOP CUT_G6_AUTHORIZATION_SPENT\n"))

    def test_host_root_and_pin_and_unexpected_failures_never_traceback(self):
        pre = self.env(g6t.RunFakeDocker(minio="false"))
        code, out = self.call(self.argv("preflight"), self.deps(pre, lib_dir=self.base))  # no pinned bytes there
        self.assertEqual(code, 1)
        self.assertTrue(out.startswith("QA525_MANUAL_CUT_G6=STOP "))
        self.assertNotIn("Traceback", out)
        code, out = self.call(self.argv("preflight"), self.deps(pre, **{"env": lambda m: 1 / 0}))
        self.assertEqual((code, out), (1, "QA525_MANUAL_CUT_G6=STOP CUT_G6_UNEXPECTED\n"))
        self.assertEqual([n for n in os.listdir(self.root) if n.startswith("auth-")], [])


if __name__ == "__main__":
    unittest.main()
