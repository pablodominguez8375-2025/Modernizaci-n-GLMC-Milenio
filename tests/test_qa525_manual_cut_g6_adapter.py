"""G6 adapter (route C) end-to-end with the runner's own closed fakes: pinned modules, one nonce per command,
no host, no network, no MinIO.  Reuses the helpers of test_qa525_vnext_g6_v2_run (same fakes the runner is tested with)."""
import datetime as dt
import hashlib
import importlib.util
import json
import os
import pathlib
import shutil
import sys
import unittest

HERE = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import test_qa525_vnext_g6_v2_run as g6t  # noqa: E402

SRC = HERE.parent / "scripts/qa/operators/qa525-manual-cut-e5c40d3a.py"
spec = importlib.util.spec_from_file_location("cut_g6", SRC)
cut = importlib.util.module_from_spec(spec)
spec.loader.exec_module(cut)
NJ = "qa525-vnext-go-nonce-journal.py"


class Plane:
    def assert_held(self):
        pass


class AdapterCycle(g6t.G6RunBase):
    def setUp(self):
        super().setUp()
        pins = dict(cut.G6_PINS)
        pins[NJ] = cut.LIB_PINS[NJ]
        self.mods = cut.load_libs(str(HERE.parent / "scripts/qa"), pins)
        self.prun = self.mods[cut.G6_RUN]
        self.root = self.base + "/journal"
        os.mkdir(self.root, 0o700)
        self.n = 0
        self.outputs = []

    def env(self, runner, **over):
        args = dict(
            producer=g6t.prod, captor=g6t.cap, verifier=g6t.ver, uid=g6t.UID, now=lambda: self.clock,
            docker_runner=runner, make_restic=lambda sha: self.restic, read_key=lambda: self.key,
            emit=self.lines.append, state_root=self.state, silo_source=self.silo, restic=self.restic_bin,
            forbidden=[os.path.join(self.base, "prod")], module_hashes={"producer": "1" * 64, "entrypoint": "2" * 64},
            sleep=self.slept.append, monotonic=self.monotonic, systemctl_runner=g6t.SystemctlFake(self),
            systemctl="/usr/bin/systemctl")
        args.update(over)
        return self.prun.Env(**args)

    def step(self, command, env, **opts):
        """ONE command through the adapter with its own nonce and its own authorization."""
        options = list(opts.items())
        _, op = cut.g6_operation(self.prun, command, options)
        d = lambda b: "sha256:" + hashlib.sha256(b).hexdigest()  # noqa: E731
        auth = {"driver_sha256": d(b"driver"), "policy_sha256": d(b"policy"), "admission_sha256": d(b"go-" + command.encode()),
                "g6_operation_sha256": op, "g6_command": command}
        self.n += 1
        nonce = ("%064x" % (self.n * 7919))
        self.lines.clear()
        code = cut.run_g6(self.mods, Plane(), self.root, g6t.UID, int(__import__("time").time()), nonce, auth,
                          command, options, env)
        out = {l.split("=", 1)[0]: l.split("=", 1)[1] for l in self.lines if "=" in l}
        self.outputs.extend(self.lines)
        return code, out, auth, nonce

    def test_all_five_commands_through_the_adapter_each_with_its_own_marker(self):
        pre_env = self.env(g6t.RunFakeDocker(minio="false", image=(
            'sha256:%s ["%s"]\n' % ("a" * 64, g6t.vtest.IMAGE)).encode()))
        code, out, _, _ = self.step("preflight", pre_env)
        self.assertIn("PREFLIGHT_READY", out)
        env = self.env(g6t.RunFakeDocker(minio=["true", "false"]))
        code, init, _, _ = self.step("init", env, **{"restic-sha256": g6t.PIN})
        self.assertEqual(code, 0, self.lines)
        pins = {"restic-sha256": g6t.PIN, "repo-id": init["RESTIC_REPO_ID"], "key-epoch": init["KEY_EPOCH"]}
        code, backup, _, _ = self.step("backup", env, **pins)
        self.assertEqual(code, 0, self.lines)
        downloads = os.path.join(self.state, "downloads-v2")
        os.makedirs(downloads, mode=0o700, exist_ok=True)
        arch = backup["ARCHIVE_NAME"]
        shutil.copyfile(os.path.join(self.state, "exports-v2", arch), os.path.join(downloads, arch))
        os.chmod(os.path.join(downloads, arch), 0o600)
        self.clock = g6t.NOW + dt.timedelta(hours=2)
        renv = self.env(g6t.vtest.VerifierFakeDocker())
        code, rest, _, _ = self.step("restore", renv, **pins, **{
            "image": g6t.vtest.IMAGE, "archive-name": arch, "archive-sha256": backup["ARCHIVE_SHA256"],
            "snapshot-id": backup["SNAPSHOT_ID"], "manifest-sha256": backup["MANIFEST_SHA256"]})
        self.assertEqual(code, 0, self.lines)
        self.assertEqual(rest["RESULT"], "G6_SAME_HOST_ISOLATED_RESTORE_PASS_NOT_DISASTER_RECOVERY")
        ropts = {"archive-name": arch, "drive-file-id": g6t.DRIVE, "repo-id": pins["repo-id"], "key-epoch": pins["key-epoch"]}
        code, review, auth, nonce = self.step("review", renv, **ropts)
        self.assertEqual(code, 0, self.lines)
        # five commands => five distinct markers in the journal root
        self.assertEqual(len([n for n in os.listdir(self.root) if n.endswith('.spent')]), 5)
        # secrets never reach any output line
        self.assertNotIn(self.key.decode(), "\n".join(self.outputs))
        # post-review stage: params record bound to the same G6/GO/nonce, written exclusively
        rec = cut.build_a0_g6_params(self.mods, self.state, arch, pins["repo-id"], pins["key-epoch"], auth, nonce, g6t.UID)
        self.assertEqual(rec["command"], "review")
        out = self.base + "/params.json"
        cut.write_a0_g6_params(out, rec)
        self.assertEqual(oct(os.stat(out).st_mode & 0o777), "0o600")
        self.assertEqual(cut.load_a0_g6_params(out, g6t.UID), rec)
        with self.assertRaises(cut.Stop) as c:  # never overwritten
            cut.write_a0_g6_params(out, rec)
        self.assertEqual(str(c.exception), "A0_G6_PARAMS_EXISTS")
        # bound to ANOTHER operation/command => refused before writing anything
        with self.assertRaises(cut.Stop):
            cut.build_a0_g6_params(self.mods, self.state, arch, pins["repo-id"], pins["key-epoch"],
                                   dict(auth, g6_operation_sha256="0" * 64), nonce, g6t.UID)
        with self.assertRaises(cut.Stop):
            cut.build_a0_g6_params(self.mods, self.state, arch, "f" * 64, pins["key-epoch"], auth, nonce, g6t.UID)
        with open(out) as fh:
            self.assertNotIn(self.key.decode(), fh.read())

    def test_secret_key_is_never_in_exceptions_or_markers(self):
        env = self.env(g6t.RunFakeDocker(minio="false"))
        self.step("init", env, **{"restic-sha256": g6t.PIN})
        blob = ""
        for name in os.listdir(self.root):
            p = os.path.join(self.root, name)
            if os.path.isfile(p):
                with open(p, "rb") as fh:
                    blob += fh.read().decode("latin1")
            else:
                blob += name
        self.assertNotIn(self.key.decode(), blob)
        self.assertNotIn(self.key.decode(), "\n".join(self.outputs))

    def test_frontier_negatives_stop_before_dispatch(self):
        calls = []
        spy = lambda command, opts, env: calls.append(command)  # noqa: E731
        _, op = cut.g6_operation(self.prun, "preflight", [])
        d = lambda b: "sha256:" + hashlib.sha256(b).hexdigest()  # noqa: E731
        auth = {"driver_sha256": d(b"x"), "policy_sha256": d(b"p"), "admission_sha256": d(b"a"),
                "g6_operation_sha256": op, "g6_command": "preflight"}
        go = lambda mods, a=auth, nonce="3" * 64: cut.run_g6(mods, Plane(), self.root, g6t.UID, 1, nonce, a,  # noqa: E731
                                                              "preflight", [], object(), spy)
        # incomplete / fake admission objects
        for bad in ({}, {"g6_operation_sha256": op}, dict(auth, g6_command="init"), dict(auth, g6_operation_sha256="1" * 64)):
            with self.assertRaises(Exception):
                go(self.mods, bad)
        # modules map missing the runner, or with an unpinned stand-in, cannot even build an operation
        with self.assertRaises(Exception):
            go({})
        # pins that are inconsistent between the table and the bytes are refused by the loader
        wrong = dict(cut.G6_PINS)
        wrong[cut.G6_RUN] = "0" * 64
        with self.assertRaises(cut.Stop):
            cut.load_libs(str(HERE.parent / "scripts/qa"), wrong)
        # an injected loader/override cannot replace a pinned module: the table is fixed and unknown names are not loadable
        with self.assertRaises(cut.Stop):
            cut.load_libs(str(HERE.parent / "scripts/qa"), {"qa525-evil.py": "a" * 64})
        self.assertEqual(calls, [])
        self.assertEqual([n for n in os.listdir(self.root) if n.endswith('.spent')], [])


if __name__ == "__main__":
    unittest.main()
