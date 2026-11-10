"""QA525 launcher composition: real ssh-keygen GO, real receiver/entry/cut; plane, journal and child are stubs.

Nothing here is authority: the production `launch()` builds its own environment; `_compose` is the private seam."""
import array
import fcntl
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import shutil
import socket
import subprocess
import sys
import tempfile
import types
import unittest
import unittest.mock
from contextlib import contextmanager

BASE = Path(__file__).resolve().parents[1] / "scripts" / "qa"
KEYGEN = "/usr/bin/ssh-keygen"
UID = os.geteuid()
NS = "qa525-go-cut-v1@pmgm"


def load_file(name):
    spec = importlib.util.spec_from_file_location("qa525_t_" + name[:-3].replace("-", "_"), BASE / name)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


launcher = load_file("qa525-vnext-root-launcher.py")
sheet = load_file("qa525-vnext-sponsor-signing.py")
REAL = {n: load_file(n) for n in (
    "qa525-vnext-go-cut-contract.py", "qa525-vnext-root-go-verify.py",
    "qa525-vnext-root-entry.py", "qa525-vnext-root-boundary-recv.py", "qa525-vnext-ops-signature.py")}
D = lambda c: "sha256:" + c * 64
DATA = b"zip-inner-archive" * 100
ARCHIVE = "sha256:" + hashlib.sha256(DATA).hexdigest()
OPS_SHA, APP_SHA = "a" * 40, "b" * 40
NONCE = "e" * 64
NOW = 1_800_000_000  # 2027-01-15T08:00:00Z
ISSUED, EXPIRES = "2027-01-15T07:55:00Z", "2027-01-15T08:05:00Z"
LAUNCHER_PINS = {"release_asset_sha256": D("9"), "old_launcher_sha256": "1" * 64,
                 "candidate_operator_sha256": "2" * 64}
PINS = {"ops_source_sha": OPS_SHA, "app_source_sha": APP_SHA, "app_archive_sha256": ARCHIVE,
        "plan_sha256": D("6"), "admission_sha256": D("7"), "policy_hash": D("4")}


G6_OPERATION = json.dumps({
    "schema": "qa525-g6-operation-v1", "g6_command": "backup",
    "argv": ["--restic-sha256", "a" * 64, "--repo-id", "b" * 64, "--key-epoch", "c" * 32],
}, sort_keys=True, separators=(",", ":")).encode()
G6_OP = "sha256:" + hashlib.sha256(G6_OPERATION).hexdigest()
CHILDREN = {"LAUNCHER_ONLY": "qa525-vnext-root-launcher-child.py"}
CHILDREN.update({"G6:" + c: "qa525-vnext-root-g6-%s.py" % c for c in launcher.G6_COMMANDS})


def run(*argv):
    return subprocess.run(argv, check=True, capture_output=True)


@unittest.skipUnless(os.path.exists(KEYGEN) and hasattr(os, "memfd_create"), "ssh-keygen and memfd required")
@unittest.skipIf(UID == 0, "the production peer is never root: run these as an unprivileged user")
class LauncherComposeTests(unittest.TestCase):
    def setUp(self):
        self.dir = tempfile.mkdtemp(prefix="lnch")
        self.addCleanup(shutil.rmtree, self.dir, True)
        os.chmod(self.dir, 0o700)
        for sub in ("inbox", "tmp"):
            os.mkdir(os.path.join(self.dir, sub), 0o700)
        self.keygen = os.path.join(self.dir, "ssh-keygen")
        shutil.copy(KEYGEN, self.keygen)
        os.chmod(self.keygen, 0o755)
        self.keygen_sha = hashlib.sha256(Path(self.keygen).read_bytes()).hexdigest()  # bare hex, as #978 stores it
        key = os.path.join(self.dir, "k")
        run(KEYGEN, "-q", "-t", "ed25519", "-N", "", "-f", key)
        pub = Path(key + ".pub").read_text().split()
        self.key = key
        self.fp = run(KEYGEN, "-lf", key + ".pub").stdout.decode().split()[1]
        self.allowed = ('qa525-ops namespaces="%s" ssh-ed25519 %s\n' % (NS, pub[1])).encode()
        self.ops = dict(schema="qa525-ops-v3", status="OPS_SIGNED_VERIFIED", ops_source_sha=OPS_SHA,
                        program_sha256=D("1"), readiness_sha256=D("2"), staging_sha256=D("3"),
                        policy_hash=PINS["policy_hash"], app_archive_sha256=ARCHIVE)
        cut = REAL["qa525-vnext-go-cut-contract.py"]
        self.subject = dict(source_sha=APP_SHA, subject_sha256=ARCHIVE,
                            signer_workflow=cut.APP_WORKFLOW, oidc_issuer=cut.ISSUER)
        self.gh = os.path.join(self.dir, "gh")
        Path(self.gh).write_bytes(b"#!/bin/sh\nexit 0\n")
        os.chmod(self.gh, 0o755)
        self.gh_sha = hashlib.sha256(Path(self.gh).read_bytes()).hexdigest()
        self.archive_path = os.path.join(self.dir, "inner.zip")
        Path(self.archive_path).write_bytes(DATA)
        os.chmod(self.archive_path, 0o644)
        self.spent, self.acted, self.fds = [], [], []
        self.write_inbox()

    # ---- fixtures
    def go_bytes(self, **over):
        values = dict(ops_source_sha=OPS_SHA, app_source_sha=APP_SHA, app_archive_sha256=ARCHIVE,
                      plan_sha256=PINS["plan_sha256"], admission_sha256=PINS["admission_sha256"],
                      policy_hash=PINS["policy_hash"], issued_at=ISSUED, expires_at=EXPIRES, nonce=NONCE,
                      purpose="LAUNCHER_ONLY", **{k: v for k, v in LAUNCHER_PINS.items()})
        return sheet.build_go_cut(**dict(values, **over))

    def sign(self, data, ns=NS):
        doc = os.path.join(self.dir, "doc")
        Path(doc).write_bytes(data)
        if os.path.exists(doc + ".sig"):
            os.unlink(doc + ".sig")
        run(KEYGEN, "-Y", "sign", "-f", self.key, "-n", ns, doc)
        return Path(doc + ".sig").read_bytes()

    def write_inbox(self, go=None, sig=None, ops=None, subject=None):
        go = self.go_bytes() if go is None else go
        files = {launcher.G6_OPERATION_FILE: G6_OPERATION,
                 "go.json": go, "go.json.sig": self.sign(go) if sig is None else sig,
                 "ops-v3.json": json.dumps(ops or self.ops, sort_keys=True).encode(),
                 "app-subject.json": json.dumps(subject or self.subject, sort_keys=True).encode(),
                 "ops-v3.json.sig": b"-----BEGIN SSH SIGNATURE-----\nX\n-----END SSH SIGNATURE-----\n",
                 "app-bundle.json": b'{"mediaType":"bundle"}', "trusted-root.jsonl": b'{"mediaType":"root"}\n'}
        for name, data in files.items():
            path = os.path.join(self.dir, "inbox", name)
            Path(path).write_bytes(data)
            os.chmod(path, 0o600)
        self.files = files

    def config(self, **over):
        sha = lambda b: "sha256:" + hashlib.sha256(b).hexdigest()
        cfg = {"pins": dict(PINS), "launcher_pins": dict(LAUNCHER_PINS), "allowed_signers": self.allowed,
               "go_fingerprint": self.fp, "ops_v3_sha256": sha(self.files["ops-v3.json"]),
               "app_subject_sha256": sha(self.files["app-subject.json"]),
               "inbox": os.path.join(self.dir, "inbox"), "temp_root": os.path.join(self.dir, "tmp"),
               "socket_path": os.path.join(self.dir, "s"), "journal_root": os.path.join(self.dir, "j"),
               "lease_dir": os.path.join(self.dir, "l"), "peer_uid": UID, "timeout": 5,
               "children": dict(CHILDREN), "g6_pins": None,
               "authenticity": {"ops_allowed_signers": self.allowed.replace(NS.encode(), b"qa525-ops-v2@pmgm"),
                                "ops_signature_sha256": sha(self.files["ops-v3.json.sig"]),
                                "app_bundle_sha256": sha(self.files["app-bundle.json"]),
                                "trusted_root_sha256": sha(self.files["trusted-root.jsonl"]),
                                "app_archive_path": self.archive_path, "source_repository_id": "424242", "drop_uid": UID + 1, "drop_gid": UID + 1}}
        cfg.update(over)
        return cfg

    def loader(self, cfg=None, held=lambda: True, child_ok=True, **extra):
        spent, acted, tests = self.spent, self.acted, self

        @contextmanager
        def flight(rl, port, lease_dir, *, uid=0, anchor="/", view=None):
            tests.flight_args = (lease_dir, uid, view)
            yield types.SimpleNamespace(assert_held=lambda: None if held() else (_ for _ in ()).throw(RuntimeError()))
        journal = types.SimpleNamespace(consume=lambda root, nonce, binding, *, uid, now, expires_at:
                                        (spent.append((nonce, binding, now, expires_at)),
                                         {"record_sha256": D("c")})[1])
        tests.act_for_args = []
        act = lambda fd: (acted.append(os.fstat(fd).st_size), "done")[1]
        child = types.SimpleNamespace(
            act=act, act_for=lambda **kw: (tests.act_for_args.append(kw), act)[1]) if child_ok \
            else types.SimpleNamespace()
        mods = dict(REAL)
        tests.release_args = {}
        auth = tests.auth if hasattr(tests, "auth") else (lambda **kw: True)
        mods.update({
            "qa525-vnext-root-authenticity.py": types.SimpleNamespace(
                port_from_release=lambda **kw: (tests.release_args.update(kw), auth)[1]),
            "qa525-vnext-root-launch-config.py": types.SimpleNamespace(CONFIG=cfg or self.config()),
            "qa525-vnext-root-locks.py": types.SimpleNamespace(),
            "qa525-vnext-posix-port.py": types.SimpleNamespace(PosixPort=lambda: object()),
            "qa525-vnext-root-plane.py": types.SimpleNamespace(launcher_flight=flight),
            "qa525-vnext-go-nonce-journal.py": journal,
            "qa525-vnext-root-launcher-child.py": child})
        g6_child = types.SimpleNamespace(
            act_for=lambda **kw: (tests.act_for_args.append(kw),
                                  lambda *, claims, fd: act(fd))[1])
        mods.update({"qa525-vnext-root-g6-%s.py" % c: g6_child for c in launcher.G6_COMMANDS})
        g6_real = load_file("qa525-vnext-g6-v2-run.py")
        mods.update({
            launcher.G6_RUN: types.SimpleNamespace(
                parse=g6_real.parse, ENTRYPOINT=launcher.G6_RUN, SIBLINGS={},
                main=lambda *a, **kw: 0),
            launcher.G6_CHILD_CORE: types.SimpleNamespace(
                build=lambda **kw: lambda *, claims, fd: act(fd)),
        })
        mods.update(extra)

        def load(name):
            if name not in mods:
                raise RuntimeError("BOOT_MODULE_UNKNOWN")
            return mods[name]
        # Test-double only: production supplies the genuine PRE-4 Verified token.
        load._qa525_verified = types.SimpleNamespace(record=tests.record())
        load._qa525_load_module = lambda verified, name: mods[name]
        return load

    def sender(self, frame_nonce=NONCE, data=DATA, sealed=True):
        """A connected peer that sends the sealed memfd as soon as the launcher 'accepts' it."""
        recv = REAL["qa525-vnext-root-boundary-recv.py"]
        a, b = socket.socketpair(socket.AF_UNIX, socket.SOCK_SEQPACKET)
        self.addCleanup(a.close)
        fd = os.memfd_create("t", os.MFD_CLOEXEC | os.MFD_ALLOW_SEALING)
        self.fds.append(fd)
        os.write(fd, data)
        if sealed:
            fcntl.fcntl(fd, fcntl.F_ADD_SEALS, recv.SEALS)
        b.setsockopt(socket.SOL_SOCKET, socket.SO_PASSCRED, 1)
        frame = recv.MAGIC + frame_nonce.encode() + b":" + hashlib.sha256(DATA).hexdigest().encode()
        a.sendmsg([frame], [(socket.SOL_SOCKET, socket.SCM_RIGHTS, array.array("i", [fd]))])
        return b

    def env(self, conn=None, **over):
        env = {"euid": os.geteuid, "root_euid": UID, "os_uid": UID, "anchor": self.dir, "now": lambda: NOW,
               "accept": lambda path, uid, timeout: conn or self.sender(),
               "sha256": lambda b: "sha256:" + hashlib.sha256(b).hexdigest()}
        env.update(over)
        return env

    def record(self, **over):
        rec = {"helpers": {"ssh-keygen": {"path": self.keygen, "sha256": self.keygen_sha},
                           "gh": {"path": self.gh, "sha256": self.gh_sha}}}
        rec.update(over)
        return rec

    def compose(self, load=None, record=None, env=None):
        return launcher._compose(load or self.loader(), record or self.record(), env or self.env())

    def stop(self, code, **kw):
        with self.assertRaises(Exception) as err:
            self.compose(**kw)
        self.assertEqual(str(err.exception), code)

    def tearDown(self):
        for fd in self.fds:
            try:
                os.close(fd)
            except OSError:
                pass

    # ---- the happy path and what it proves
    def test_full_flight_spends_the_nonce_once_and_runs_only_the_fixed_child(self):
        out = self.compose()
        self.assertEqual(out["child_outcome"], "done")
        self.assertEqual(len(self.spent), 1)
        nonce, binding, now, expires = self.spent[0]
        self.assertEqual((nonce, now, expires), (NONCE, NOW, NOW + 300))
        self.assertEqual(binding, {"app": ARCHIVE, "ops": OPS_SHA, "policy": PINS["policy_hash"],
                                   "plan": PINS["plan_sha256"], "admission": PINS["admission_sha256"]})
        self.assertEqual(self.acted, [len(DATA)])
        (call,) = self.act_for_args  # the launcher child is built from the AUTHENTICATED claims + signed pins only
        self.assertEqual(set(call), {"claims", "launcher_pins"})
        self.assertEqual(call["claims"], {"purpose": "LAUNCHER_ONLY", "nonce": NONCE,
                                          "app_source_sha": PINS["app_source_sha"], "app_archive_sha256": ARCHIVE})
        self.assertEqual(call["launcher_pins"], LAUNCHER_PINS)
        self.assertFalse(out["may_apply"] or out["may_install"])
        self.assertEqual(self.flight_args[2], None, "the lease liveness view is never injected by the launcher")

    # ---- cross-scope and dispatch
    def g6_go(self, command="backup", op=G6_OP, **over):
        return self.go_bytes(purpose="G6_V2_ONLY", g6_command=command, g6_operation_sha256=op,
                             **{k: None for k in LAUNCHER_PINS}, **over)

    def g6_cfg(self, command="backup", op=G6_OP):
        return self.config(g6_pins={"g6_command": command, "g6_operation_sha256": op})

    def test_unknown_schema_never_reaches_a_child(self):
        for schema in ("qa525-go-cut-v1", "x"):
            go = json.dumps(dict(json.loads(self.go_bytes()), schema=schema), sort_keys=True,
                            separators=(",", ":")).encode()
            self.write_inbox(go=go)
            self.stop("LAUNCH_SCOPE_UNKNOWN", load=self.loader(cfg=self.config()))
        self.assertEqual((self.spent, self.acted), ([], []))

    def test_g6_go_runs_only_the_one_command_the_release_pins(self):
        self.write_inbox(go=self.g6_go())
        out = self.compose(load=self.loader(cfg=self.g6_cfg()))
        self.assertEqual((out["child_outcome"], len(self.spent), self.acted), ("done", 1, [len(DATA)]))

    def test_g6_go_is_refused_when_the_release_pins_no_g6_command(self):
        self.write_inbox(go=self.g6_go())
        self.stop("LAUNCH_G6_NOT_CONFIGURED", load=self.loader(cfg=self.config()))
        self.assertEqual((self.spent, self.acted), ([], []))

    def test_g6_go_for_another_command_or_operation_is_refused(self):
        for go in (self.g6_go(command="restore"), self.g6_go(op=D("a"))):
            self.write_inbox(go=go)
            with self.assertRaises(Exception) as err:
                self.compose(load=self.loader(cfg=self.g6_cfg("backup")))
            self.assertIn(str(err.exception), ("GO_CUT_MISMATCH", "GO_CUT_G6_BINDING_MISMATCH"))
        self.assertEqual((self.spent, self.acted), ([], []))

    def test_g6_operation_digest_mismatch_never_consumes_nonce(self):
        self.write_inbox(go=self.g6_go())
        path = os.path.join(self.dir, "inbox", launcher.G6_OPERATION_FILE)
        Path(path).write_bytes(G6_OPERATION + b" ")
        self.stop("LAUNCH_G6_OPERATION_PIN_MISMATCH", load=self.loader(cfg=self.g6_cfg()))
        self.assertEqual((self.spent, self.acted), ([], []))

    def test_g6_operation_wrong_command_is_refused_before_nonce(self):
        bad = json.dumps({"schema": "qa525-g6-operation-v1",
                          "g6_command": "restore", "argv": []},
                         sort_keys=True, separators=(",", ":")).encode()
        op = "sha256:" + hashlib.sha256(bad).hexdigest()
        self.write_inbox(go=self.g6_go(op=op))
        path = os.path.join(self.dir, "inbox", launcher.G6_OPERATION_FILE)
        Path(path).write_bytes(bad)
        self.stop("LAUNCH_G6_OPERATION_INVALID", load=self.loader(cfg=self.g6_cfg(op=op)))
        self.assertEqual((self.spent, self.acted), ([], []))

    def test_g6_missing_pre4_capability_stops_before_nonce(self):
        self.write_inbox(go=self.g6_go())
        load = self.loader(cfg=self.g6_cfg())
        del load._qa525_verified
        self.stop("LAUNCH_G6_VERIFIED_REQUIRED", load=load)
        self.assertEqual(self.spent, [])

    def test_g6_command_without_its_child_module_stops_before_the_nonce(self):
        self.write_inbox(go=self.g6_go())
        load = self.loader(cfg=self.g6_cfg())

        def gone(name):
            if name == "qa525-vnext-root-g6-backup.py":
                raise RuntimeError("BOOT_MODULE_UNKNOWN")
            return load(name)
        self.stop("BOOT_MODULE_UNKNOWN", load=gone)
        self.assertEqual(self.spent, [])

    def test_a_launcher_go_cannot_run_a_g6_child_and_a_g6_go_cannot_run_the_launcher_child(self):
        ran = []
        load = self.loader(cfg=self.g6_cfg())
        base = load

        def spy(name):
            mod = base(name)
            if name.startswith("qa525-vnext-root-g6-") and name != launcher.G6_CHILD_CORE:
                ran.append(name)
            return mod
        spy._qa525_verified = base._qa525_verified
        spy._qa525_load_module = base._qa525_load_module
        self.compose(load=spy)  # launcher GO with a G6-pinned config
        self.assertNotIn("qa525-vnext-root-g6-backup.py", ran)
        self.write_inbox(go=self.g6_go())
        ran.clear()
        self.compose(load=spy)
        self.assertEqual(ran, ["qa525-vnext-root-g6-backup.py"])

    def test_g6_go_missing_schema_or_not_json_is_refused(self):
        for raw in (b"not json", b'{"a":1}', b'[1]', b'{"schema":1}'):
            self.write_inbox(go=raw, sig=b"-----BEGIN SSH SIGNATURE-----\n-----END SSH SIGNATURE-----")
            with self.assertRaises(Exception):
                self.compose(load=self.loader(cfg=self.config()))
        self.assertEqual(self.spent, [])

    # ---- authenticity: claims are not enough
    def test_consistent_but_wrongly_signed_go_spends_nothing(self):
        other = os.path.join(self.dir, "k2")
        run(KEYGEN, "-q", "-t", "ed25519", "-N", "", "-f", other)
        good = self.go_bytes()
        doc = os.path.join(self.dir, "doc2")
        Path(doc).write_bytes(good)
        run(KEYGEN, "-Y", "sign", "-f", other, "-n", NS, doc)
        self.write_inbox(go=good, sig=Path(doc + ".sig").read_bytes())
        self.stop("GO_CUT_SIGNATURE_DENIED", load=self.loader(cfg=self.config()))
        self.assertEqual(self.spent, [])

    def test_signature_for_another_namespace_is_refused(self):
        go = self.go_bytes()
        self.write_inbox(go=go, sig=self.sign(go, ns="qa525-other@pmgm"))
        self.stop("GO_CUT_SIGNATURE_DENIED", load=self.loader(cfg=self.config()))
        self.assertEqual(self.spent, [])

    def test_go_that_differs_from_the_pinned_cut_is_refused(self):
        self.write_inbox(go=self.go_bytes(plan_sha256=D("8")))
        self.stop("GO_CUT_MISMATCH", load=self.loader(cfg=self.config()))
        self.assertEqual(self.spent, [])

    def test_expired_go_is_refused(self):
        self.stop("GO_CUT_EXPIRED_OR_FUTURE", env=self.env(now=lambda: NOW + 3600))
        self.assertEqual(self.spent, [])

    # ---- pins and inputs
    def test_receipts_must_match_the_digests_pinned_in_the_config(self):
        self.stop("LAUNCH_RECEIPT_PIN_MISMATCH",
                  load=self.loader(cfg=self.config(ops_v3_sha256=D("0"))))
        self.stop("LAUNCH_RECEIPT_PIN_MISMATCH",
                  load=self.loader(cfg=self.config(app_subject_sha256=D("0"))))
        for key in ("ops_signature_sha256", "app_bundle_sha256", "trusted_root_sha256"):
            cfg = self.config()
            cfg["authenticity"][key] = D("0")
            self.stop("LAUNCH_RECEIPT_PIN_MISMATCH", load=self.loader(cfg=cfg))
        self.assertEqual((self.spent, self.acted), ([], []))

    def test_helper_hash_is_converted_once_and_a_bad_helper_pin_stops(self):
        self.assertEqual(launcher._helper_pin(self.record()), (self.keygen, "sha256:" + self.keygen_sha))
        for bad in ({}, {"ssh-keygen": {"path": "relative", "sha256": self.keygen_sha}},
                    {"ssh-keygen": {"path": self.keygen, "sha256": "sha256:" + self.keygen_sha}},
                    {"ssh-keygen": {"path": self.keygen, "sha256": "AB" * 32}}):
            self.stop("LAUNCH_HELPER_PIN_INVALID", record={"helpers": bad})
        wrong = self.record()
        wrong["helpers"]["ssh-keygen"]["sha256"] = "0" * 64
        with self.assertRaises(Exception):  # real #981: the binary does not match the pinned hash
            self.compose(record=wrong)

    def test_config_must_have_exactly_the_expected_shape(self):
        cfg = self.config()
        for bad in (dict(cfg, extra=1), {k: v for k, v in cfg.items() if k != "peer_uid"},
                    dict(cfg, peer_uid=0), dict(cfg, timeout=0), dict(cfg, timeout=61),
                    dict(cfg, children=dict(CHILDREN, **{"LAUNCHER_ONLY": "../evil.py"})),
                    dict(cfg, children=dict(CHILDREN, **{"LAUNCHER_ONLY": "qa525-vnext-other-child.py"})),
                    dict(cfg, children=dict(CHILDREN, extra="qa525-vnext-y.py")),
                    dict(cfg, children={"LAUNCHER_ONLY": "qa525-vnext-x.py"}),
                    dict(cfg, g6_pins={"g6_command": "wipe", "g6_operation_sha256": D("5")}),
                    dict(cfg, g6_pins={"g6_command": "backup"}),
                    dict(cfg, g6_pins={"g6_command": "backup", "g6_operation_sha256": "5" * 64}),
                    dict(cfg, inbox="relative"), dict(cfg, go_fingerprint="SHA256:short"),
                    {k: v for k, v in cfg.items() if k != "authenticity"},
                    dict(cfg, authenticity=dict(cfg["authenticity"], extra=1)),
                    dict(cfg, authenticity=dict(cfg["authenticity"], drop_uid=0)),
                    dict(cfg, authenticity=dict(cfg["authenticity"], drop_gid=True)),
                    dict(cfg, authenticity=dict(cfg["authenticity"], drop_uid=UID)),  # the peer's own uid
                    dict(cfg, authenticity=dict(cfg["authenticity"], app_archive_path="relative.zip")),
                    dict(cfg, authenticity=dict(cfg["authenticity"], ops_allowed_signers=b"")),
                    dict(cfg, authenticity=dict(cfg["authenticity"], app_bundle_sha256="x")),
                    dict(cfg, authenticity=dict(cfg["authenticity"], source_repository_id="0")),
                    dict(cfg, authenticity=dict(cfg["authenticity"], source_repository_id=424242)),
                    dict(cfg, authenticity={k: v for k, v in cfg["authenticity"].items() if k != "source_repository_id"})):
            self.stop("LAUNCH_CONFIG_INVALID", load=self.loader(cfg=bad))

    def test_children_mapping_is_exact_so_no_swap_duplicate_or_cross_scope_child_is_accepted(self):
        swaps = []
        for a, b in (("G6:backup", "G6:restore"), ("LAUNCHER_ONLY", "G6:backup"), ("G6:init", "G6:review")):
            swapped = dict(CHILDREN)
            swapped[a], swapped[b] = CHILDREN[b], CHILDREN[a]
            swaps.append(swapped)
        same = dict(CHILDREN, **{"G6:backup": CHILDREN["G6:restore"]})  # two keys -> the same child
        to_launcher = dict(CHILDREN, **{"G6:backup": CHILDREN["LAUNCHER_ONLY"]})
        from_g6 = dict(CHILDREN, **{"LAUNCHER_ONLY": CHILDREN["G6:restore"]})
        for go, cfg_over in ((self.go_bytes(), {}), (self.g6_go(), {"g6_pins": {
                "g6_command": "backup", "g6_operation_sha256": G6_OP}})):
            self.write_inbox(go=go)
            for children in swaps + [same, to_launcher, from_g6]:
                self.stop("LAUNCH_CONFIG_INVALID", load=self.loader(cfg=self.config(children=children, **cfg_over)))
        self.assertEqual((self.spent, self.acted), ([], []))

    def test_children_mapping_is_the_code_constant(self):
        self.assertEqual(launcher.CHILD_MODULES["G6:backup"], "qa525-vnext-root-g6-backup.py")
        self.assertEqual(len(set(launcher.CHILD_MODULES.values())), 6)
        self.assertEqual(self.config()["children"], launcher.CHILD_MODULES)

    def test_inbox_files_must_be_plain_owned_files(self):
        path = os.path.join(self.dir, "inbox", "go.json")
        os.chmod(path, 0o666)
        self.stop("LAUNCH_INPUT_UNSAFE")
        os.chmod(path, 0o600)
        os.unlink(path)
        os.symlink(os.path.join(self.dir, "inbox", "go.json.sig"), path)
        self.stop("LAUNCH_INPUT_MISSING")

    def test_must_be_root_in_production_semantics(self):
        self.stop("LAUNCH_NOT_ROOT", env=self.env(root_euid=UID + 1))

    # ---- the child is resolved before anything is spent
    def test_missing_or_invalid_child_stops_before_the_nonce_is_spent(self):
        self.stop("LAUNCH_CHILD_INVALID", load=self.loader(child_ok=False))
        self.assertEqual(self.spent, [])
        load = self.loader()
        mods_missing = lambda name: (_ for _ in ()).throw(RuntimeError("BOOT_MODULE_UNKNOWN")) \
            if name == "qa525-vnext-root-launcher-child.py" else load(name)
        self.stop("BOOT_MODULE_UNKNOWN", load=mods_missing)
        self.assertEqual(self.spent, [])

    def test_launcher_child_without_act_for_is_refused_before_the_nonce(self):
        mods_child = types.SimpleNamespace(act=lambda fd: "done")  # the old stub shape is no longer enough
        load = self.loader()
        legacy = lambda name: mods_child if name == "qa525-vnext-root-launcher-child.py" else load(name)
        self.stop("LAUNCH_CHILD_INVALID", load=legacy)
        self.assertEqual(self.spent, [])

    # ---- sender and plane failures
    def test_wrong_frame_nonce_or_unsealed_fd_never_reaches_the_journal_or_child(self):
        self.stop("BND_FRAME_MISMATCH", env=self.env(conn=self.sender(frame_nonce="f" * 64)))
        self.stop("BND_FD_UNSAFE", env=self.env(conn=self.sender(sealed=False)))
        self.assertEqual((self.spent, self.acted), ([], []))

    def test_wrong_archive_bytes_do_not_match_the_signed_digest(self):
        self.stop("BND_DIGEST_MISMATCH", env=self.env(conn=self.sender(data=DATA + b"x")))
        self.assertEqual((self.spent, self.acted), ([], []))

    def test_plane_not_held_means_no_go_check_no_nonce_no_child(self):
        self.stop("ENTRY_PLANE_NOT_HELD", load=self.loader(held=lambda: False))
        self.assertEqual((self.spent, self.acted), ([], []))

    def test_plane_lost_during_the_flight_is_not_reported_as_success(self):
        state = {"n": 0}

        def held():
            state["n"] += 1
            return state["n"] < 3  # held at entry and before the nonce, lost after the child
        self.stop("ENTRY_PLANE_LOST_DURING_FLIGHT", load=self.loader(held=held))
        self.assertEqual(len(self.spent), 1, "a lost plane after the nonce leaves it spent, never reusable")

    def test_accept_takes_one_peer_keeps_credentials_and_leaves_no_socket_file(self):
        import threading
        path = os.path.join(self.dir, "sock")
        got = {}

        def peer():
            for _ in range(200):
                if os.path.exists(path):
                    break
                import time
                time.sleep(0.01)
            c = socket.socket(socket.AF_UNIX, socket.SOCK_SEQPACKET)
            got["mode"] = os.stat(path).st_mode & 0o777
            c.connect(path)
            c.send(b"hello")  # sent BEFORE accept() returns to the caller
            got["c"] = c
        t = threading.Thread(target=peer)
        t.start()
        conn = launcher._accept(path, UID, 5, owner=UID, anchor=self.dir)
        t.join()
        self.addCleanup(conn.close)
        self.addCleanup(got["c"].close)
        msg, anc, _f, _a = conn.recvmsg(64, socket.CMSG_SPACE(12))
        self.assertEqual((msg, got["mode"], os.path.exists(path)), (b"hello", 0o600, False))
        self.assertTrue(any(k == socket.SCM_CREDENTIALS for _l, k, _v in anc))

    def test_accept_times_out_and_cleans_up(self):
        path = os.path.join(self.dir, "sock2")
        with self.assertRaises(launcher.Stop) as err:
            launcher._accept(path, UID, 0.2, owner=UID, anchor=self.dir)
        self.assertEqual(str(err.exception), "LAUNCH_SOCKET_FAILED")
        self.assertFalse(os.path.exists(path))

    def test_preexisting_socket_is_never_deleted_by_a_failed_attempt(self):
        path = os.path.join(self.dir, "other")
        other = socket.socket(socket.AF_UNIX, socket.SOCK_SEQPACKET)
        self.addCleanup(other.close)
        other.bind(path)
        before = os.lstat(path)
        with self.assertRaises(launcher.Stop) as err:
            launcher._accept(path, UID, 0.2, owner=UID, anchor=self.dir)
        self.assertEqual(str(err.exception), "LAUNCH_SOCKET_FAILED")
        after = os.lstat(path)
        self.assertEqual((before.st_dev, before.st_ino), (after.st_dev, after.st_ino))

    def test_preexisting_regular_file_or_symlink_at_the_path_is_left_alone(self):
        victim = os.path.join(self.dir, "victim")
        Path(victim).write_text("keep")
        os.chmod(victim, 0o640)
        link = os.path.join(self.dir, "lnk")
        os.symlink(victim, link)
        for path in (victim, link):
            with self.assertRaises(launcher.Stop):
                launcher._accept(path, UID, 0.2, owner=UID, anchor=self.dir)
        self.assertEqual((Path(victim).read_text(), os.stat(victim).st_mode & 0o777), ("keep", 0o640))
        self.assertTrue(os.path.islink(link))

    def test_a_swapped_name_is_not_unlinked_or_chowned(self):
        path = os.path.join(self.dir, "sw")
        real_chown, real_listen = os.chown, socket.socket.listen
        stray = os.path.join(self.dir, "stray")
        Path(stray).write_text("x")
        os.chmod(stray, 0o640)

        def swap_then_fail(sock, n):  # the name is replaced after our bind, then the attempt fails
            os.unlink(path)
            os.symlink(stray, path)
            raise OSError("boom")
        with unittest.mock.patch.object(socket.socket, "listen", swap_then_fail):
            with self.assertRaises(launcher.Stop):
                launcher._accept(path, UID, 0.2, owner=UID, anchor=self.dir)
        self.assertTrue(os.path.islink(path), "the foreign entry that replaced our socket must survive")
        self.assertEqual(os.stat(stray).st_mode & 0o777, 0o640, "no chmod/chown reached the foreign target")

    def test_unsafe_or_linked_socket_directory_is_refused_before_bind(self):
        loose = os.path.join(self.dir, "loose")
        os.mkdir(loose, 0o700)
        os.chmod(loose, 0o777)
        linked = os.path.join(self.dir, "linked")
        os.symlink(os.path.join(self.dir, "tmp"), linked)
        for directory in (loose, linked):
            with self.assertRaises(launcher.Stop) as err:
                launcher._accept(os.path.join(directory, "s"), UID, 0.2, owner=UID, anchor=self.dir)
            self.assertEqual(str(err.exception), "LAUNCH_DIR_UNSAFE")
        self.assertEqual(os.listdir(os.path.join(self.dir, "tmp")), [])
        self.assertEqual(os.listdir(loose), [])

    def test_failure_after_bind_removes_only_our_own_socket(self):
        path = os.path.join(self.dir, "own")
        with unittest.mock.patch.object(os, "chown", side_effect=OSError("denied")):
            with self.assertRaises(launcher.Stop):
                launcher._accept(path, UID, 0.2, owner=UID, anchor=self.dir)
        self.assertFalse(os.path.lexists(path))

    def test_inbox_with_an_unsafe_ancestor_is_refused_before_any_read(self):
        outer = os.path.join(self.dir, "outer")
        os.mkdir(outer, 0o700)
        os.chmod(outer, 0o777)
        inner = os.path.join(outer, "in")
        os.mkdir(inner, 0o700)
        shutil.copy(os.path.join(self.dir, "inbox", "go.json"), os.path.join(inner, "go.json"))
        with self.assertRaises(launcher.Stop) as err:
            launcher._read_regular(inner, "go.json", UID, self.dir)
        self.assertEqual(str(err.exception), "LAUNCH_INBOX_UNSAFE")

    # ---- the receipts are claims until the authenticity port says otherwise
    def test_without_an_authenticity_port_nothing_runs(self):
        load = self.loader()

        def missing(name):
            if name == "qa525-vnext-root-authenticity.py":
                raise RuntimeError("BOOT_MODULE_UNKNOWN")
            return load(name)
        self.stop("BOOT_MODULE_UNKNOWN", load=missing)
        self.assertEqual((self.spent, self.acted), ([], []))

    def test_port_without_verify_or_denying_or_raising_or_truthy_only_stops_before_the_nonce(self):
        for auth, code in ((lambda **kw: False, "LAUNCH_AUTHENTICITY_DENIED"),
                           (lambda **kw: 1, "LAUNCH_AUTHENTICITY_DENIED"),
                           (lambda **kw: None, "LAUNCH_AUTHENTICITY_DENIED"),
                           (lambda **kw: (_ for _ in ()).throw(ValueError("x")), "LAUNCH_AUTHENTICITY_DENIED")):
            self.auth = auth
            self.stop(code)
        self.auth = None
        self.stop("LAUNCH_AUTHENTICITY_PORT_MISSING")
        self.assertEqual((self.spent, self.acted), ([], []))

    def test_release_material_reaches_the_authenticity_builder_only_from_pinned_sources(self):
        self.compose()
        got = self.release_args
        self.assertEqual(got["keygen"], (self.keygen, "sha256:" + self.keygen_sha))
        self.assertEqual(got["gh"], (self.gh, "sha256:" + self.gh_sha))
        self.assertEqual(got["policy_hash"], PINS["policy_hash"])
        self.assertEqual(got["go_fingerprint"], self.fp)
        self.assertEqual(got["ops_signature_bytes"], self.files["ops-v3.json.sig"])
        self.assertEqual(got["app_bundle"], self.files["app-bundle.json"])
        self.assertEqual(got["trusted_root"], self.files["trusted-root.jsonl"])
        self.assertEqual(got["authenticity"]["drop_uid"], UID + 1)
        fd, size = got["open_archive"]()
        self.addCleanup(os.close, fd)
        self.assertEqual((size, os.read(fd, size + 1)), (len(DATA), DATA))
        self.assertEqual(got["required_euid"], UID)

    def test_a_missing_gh_helper_or_inbox_file_stops_before_authentication(self):
        rec = self.record()
        del rec["helpers"]["gh"]
        self.stop("LAUNCH_HELPER_PIN_INVALID", record=rec)
        for name in ("ops-v3.json.sig", "app-bundle.json", "trusted-root.jsonl"):
            path = os.path.join(self.dir, "inbox", name)
            data = Path(path).read_bytes()
            os.unlink(path)
            self.stop("LAUNCH_INPUT_MISSING")
            Path(path).write_bytes(data)
            os.chmod(path, 0o600)
        self.assertEqual((self.spent, self.acted), ([], []))

    def test_bundle_and_root_are_read_with_their_own_larger_limits_and_no_more(self):
        path = os.path.join(self.dir, "inbox", "app-bundle.json")
        Path(path).write_bytes(b"x" * 200_000)
        os.chmod(path, 0o600)
        self.assertEqual(len(launcher._read_regular(os.path.join(self.dir, "inbox"), "app-bundle.json", UID, self.dir)), 200_000)
        Path(path).write_bytes(b"x" * 1_000_001)
        os.chmod(path, 0o600)
        with self.assertRaises(launcher.Stop) as err:
            launcher._read_regular(os.path.join(self.dir, "inbox"), "app-bundle.json", UID, self.dir)
        self.assertEqual(str(err.exception), "LAUNCH_INPUT_UNSAFE")
        big = os.path.join(self.dir, "inbox", "go.json")  # the small files keep the 64 KiB limit
        Path(big).write_bytes(b"x" * 70_000)
        os.chmod(big, 0o600)
        with self.assertRaises(launcher.Stop):
            launcher._read_regular(os.path.join(self.dir, "inbox"), "go.json", UID, self.dir)

    def test_archive_opener_refuses_links_writable_hardlinked_foreign_or_oversized_files(self):
        def stop(path, anchor=None):
            with self.assertRaises(launcher.Stop) as err:
                launcher._open_archive(path, UID, anchor or self.dir)
            self.assertEqual(str(err.exception), "LAUNCH_ARCHIVE_UNSAFE")
        link = os.path.join(self.dir, "link.zip")
        os.symlink(self.archive_path, link)
        stop(link)
        os.chmod(self.archive_path, 0o664)
        stop(self.archive_path)
        os.chmod(self.archive_path, 0o644)
        os.link(self.archive_path, os.path.join(self.dir, "twin.zip"))
        stop(self.archive_path)
        stop(os.path.join(self.dir, "absent.zip"))
        stop("relative.zip")
        stop(self.archive_path, anchor="/nonexistent")
        os.unlink(os.path.join(self.dir, "twin.zip"))
        with unittest.mock.patch.object(launcher, "MAX_ARCHIVE", 4):
            stop(self.archive_path)
        fd, size = launcher._open_archive(self.archive_path, UID, self.dir)
        os.close(fd)
        self.assertEqual(size, len(DATA))

    def test_fabricated_receipts_with_a_coherent_digest_are_still_denied_by_the_port(self):
        fake_ops = dict(self.ops, status="OPS_SIGNED_VERIFIED")  # coherent claims, no signature behind them
        self.write_inbox(ops=fake_ops)
        seen = {}

        def auth(**kw):
            seen.update(kw)
            return False
        self.auth = auth
        self.stop("LAUNCH_AUTHENTICITY_DENIED", load=self.loader(cfg=self.config()))
        self.assertEqual(json.loads(seen["ops_bytes"])["status"], "OPS_SIGNED_VERIFIED")
        self.assertEqual(seen["claims"]["purpose"], "LAUNCHER_ONLY")
        self.assertEqual(self.spent, [])

    # ---- production entry point: one fixed code, no traceback
    def test_launch_prints_one_fixed_code_and_exits_nonzero(self):
        import contextlib
        import io
        out = io.StringIO()
        with contextlib.redirect_stdout(out), self.assertRaises(SystemExit) as err:
            launcher.launch(self.loader(), self.record())  # not root in the test => LAUNCH_NOT_ROOT, or an I/O STOP
        self.assertEqual(err.exception.code, 1)
        self.assertRegex(out.getvalue().strip(), r"\A[A-Z0-9_]{3,64}\Z")

    def test_unexpected_errors_are_masked(self):
        import contextlib
        import io
        out = io.StringIO()
        boom = lambda name: (_ for _ in ()).throw(ValueError("secret detail /etc/x"))
        with contextlib.redirect_stdout(out), self.assertRaises(SystemExit):
            launcher.launch(boom, self.record())
        self.assertNotIn("secret", out.getvalue())


if __name__ == "__main__":
    unittest.main()
