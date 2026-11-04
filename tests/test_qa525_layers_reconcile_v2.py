"""Pure-logic and static-contract tests for the 6eed4 compose-layer reconciler (the live Docker path is exercised only on glmdch)."""
import re
import subprocess
import unittest
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
SCRIPT = REPO / "scripts/qa/operators/qa525-layers-reconcile-v2-6eed4-20261104.sh"
TEXT = SCRIPT.read_text()
PY = TEXT.split("<<'PY'\n", 1)[1].split("\nPY\n", 1)[0]
HELPERS = PY.split("def main():", 1)[0]
SRC = "6eed4f00b7917da3cca36bbda70e16d16328648d"
OLD = "41d9c490d138419c7286445f37e7aeddf0d1b8d5"
STATE = Path("/opt/centenario/qa525-signed-prepare")
IMG = "a" * 64


def ns():
    import sys
    old = sys.argv
    sys.argv = ["", "/x", SRC, "READ_ONLY"]
    try:
        g = {"__name__": "t"}
        exec(HELPERS, g)
    finally:
        sys.argv = old
    return g


def layer(name, sha, img=IMG):
    return str(STATE / ("qa525-%s-%s-%s.json" % (name, sha, img)))


class Plan(unittest.TestCase):
    def setUp(self):
        self.g = ns()
        self.api_base = ["/opt/centenario/qa-lab-firstboot/recovery-v2/compose.recovery.json", "/opt/centenario/qa-api-refresh/api-" + "b" * 40 + ".json", "/etc/pmgm/qa754-identity-overlay.stage.json"]

    def read(self, name, img=IMG):
        return lambda f: {"services": {name: {"image": "sha256:" + img}}}

    def test_drops_only_last_6eed4_layer(self):
        files = self.api_base + [layer("api", OLD), layer("api", SRC)]
        out = self.g["plan"]("api", files, "sha256:" + IMG, self.read("api"), STATE)
        self.assertEqual(out, files[:-1])

    def test_web_with_only_base_and_6eed4(self):
        files = ["/b/compose.recovery.json", "/b/web-" + "c" * 64 + ".json", layer("web", SRC)]
        self.assertEqual(self.g["plan"]("web", files, "sha256:" + IMG, self.read("web"), STATE), files[:-1])

    def test_rejects_when_layer_not_last_or_missing_or_duplicated(self):
        base = self.api_base
        for files in (base + [layer("api", SRC), layer("api", OLD)], base + [layer("api", OLD)]):
            with self.assertRaises(self.g["Stop"]):
                self.g["plan"]("api", files, "sha256:" + IMG, self.read("api"), STATE)

    def test_rejects_layer_not_image_only_or_other_image_than_running(self):
        files = self.api_base + [layer("api", SRC)]
        with self.assertRaises(self.g["Stop"]):
            self.g["plan"]("api", files, "sha256:" + IMG, lambda f: {"services": {"api": {"image": "sha256:" + IMG, "ports": ["1:1"]}}}, STATE)
        with self.assertRaises(self.g["Stop"]):
            self.g["plan"]("api", files, "sha256:" + "c" * 64, self.read("api"), STATE)

    def test_rejects_wrong_service_or_directory(self):
        with self.assertRaises(self.g["Stop"]):
            self.g["plan"]("api", self.api_base + [layer("web", SRC)], "sha256:" + IMG, self.read("api"), STATE)
        with self.assertRaises(self.g["Stop"]):
            self.g["plan"]("api", self.api_base + ["/tmp/" + Path(layer("api", SRC)).name], "sha256:" + IMG, self.read("api"), STATE)

    def test_args_match_dual_transaction_flags(self):
        core = type("C", (), {"PROJECT": "centenario-qa-lab2", "ENV_FILE": "/opt/centenario/qa-auto-lite/empty.env"})
        a = self.g["args_for"](core, "/w", ["/f1", "/f2"], "web")
        self.assertEqual(a[-11:], ["up", "--detach", "--no-deps", "--no-build", "--pull", "never", "--force-recreate", "--wait", "--wait-timeout", "90", "web"])
        self.assertEqual([a[i + 1] for i, x in enumerate(a) if x == "--file"], ["/f1", "/f2"])


class Static(unittest.TestCase):
    def test_contract(self):
        subprocess.run(["bash", "-n", str(SCRIPT)], check=True)
        for bad in ("--review-and-run", "--apply-supervised", "docker rm", "docker stop", "docker rmi", " rm ", "rmtree", "unlink", "chmod", "chown", "mv "):
            self.assertNotIn(bad, TEXT, bad)
        self.assertIn("EXECUTE_RECONCILE", TEXT)
        self.assertIn("build.exclusive()", PY)
        # sinks de escritura: journal (O_EXCL / .next), directorio fsync (RDONLY) y log de auditoría; nada más
        self.assertEqual(len(re.findall(r"os\.open\(", PY)), 4)
        self.assertIn("os.O_EXCL", PY)
        self.assertIn("os.fsync(dfd)", PY)
        self.assertLess(PY.index("jwrite(j, create=True)"), PY.index('run("api", cleaned["api"])'))  # journal durable antes de mutar
        self.assertIn("verify_state(j, j[\"original\"])", PY)  # rollback verificado antes de declarar PASS
        self.assertLess(PY.index("verify_state(j, j[\"original\"])"), PY.index('j["status"] = "ROLLED_BACK"'))
        for mode in ("RECOVER_READ_ONLY", "RECOVER_EXECUTE"):
            self.assertIn(mode, TEXT)
        self.assertNotIn("dual-%s" % SRC, PY.replace('"dual-" + SRC', ""))  # nunca crea dual-6eed4
        self.assertIn("MODE == \"READ_ONLY\"", PY)
        self.assertLess(PY.index('MODE == "READ_ONLY"'), PY.index('run("api", cleaned'))  # A0 y salida antes de cualquier recreate


if __name__ == "__main__":
    unittest.main()


FAKE_UPDATE = r'''
import json, os, contextlib, types
from pathlib import Path
CFG = json.loads(os.environ["FAKE_CFG"])
STATE = Path(CFG["state"])
LAYER_SRC = CFG["src"]
live = json.load(open(CFG["live"]))
def save(): json.dump(live, open(CFG["live"], "w"))
class Entry:
    pass
def load_entry():
    e = Entry()
    e.STATE = STATE; e.WORKDIR = Path("/w"); e.HERE = Path("/h")
    e.MODULES = [Path("/m%d" % i) for i in range(8)]
    e.trusted = lambda *a, **k: None
    e.private_json = lambda p, maximum=65536: json.load(open(p))
    e.single_operator_runtime = lambda d: "fp"
    e.health = lambda sha: live["health"] and not (CFG.get("health_fail_after") and live["web_clean"] and not live["rolled"] and CFG["health_fail_after"] == "clean")
    def reviewed_layers(core, records, root):
        for fs in records.values():
            if any(LAYER_SRC in f for f in fs): raise RuntimeError("COMPOSE_HISTORY_NOT_SUCCESSFULLY_APPLIED")
    e.reviewed_layers = reviewed_layers
    core = types.SimpleNamespace(TARGETS=("api", "web"), DEPENDENCIES=("keycloak",), PROJECT="p", ENV_FILE="/e")
    def docker(args, cwd=None, timeout=None):
        if args[0] == "image":
            return "a" * 40
        svc = args[-1]
        files = [args[i + 1] for i, x in enumerate(args) if x == "--file"]
        if CFG.get("fail_web_up") and svc == "web" and not any(LAYER_SRC in f for f in files):
            raise RuntimeError("compose failed")
        if CFG.get("fail_rollback") and any(LAYER_SRC in f for f in files):
            raise RuntimeError("rollback failed")
        live["labels"][svc] = files
        live["cid"][svc] += "x"
        if CFG.get("change_dep") and svc == "web" and not any(LAYER_SRC in f for f in files): live["dep"] += "z"
        save(); return ""
    build = types.SimpleNamespace(exclusive=lambda: contextlib.nullcontext(),
        snapshot=lambda d: {"api": {"container_fingerprint": live["cid"]["api"], "image_id": "sha256:" + "a" * 64, "running": True},
                            "web": {"container_fingerprint": live["cid"]["web"], "image_id": "sha256:" + "a" * 64, "running": True},
                            "keycloak": {"container_fingerprint": live["dep"], "image_id": "sha256:" + "d" * 64, "running": True}})
    adapter = types.SimpleNamespace(docker=docker, metadata=lambda: {n: [None, "sha256:" + "a" * 64, "running", None, None, None, None, ",".join(live["labels"][n])] for n in ("api", "web")})
    mods = [core, build, None, None, None, adapter, None, None]
    e.load = lambda name, p: mods[int(str(p)[2:])] if str(p).startswith("/m") else None
    return e
def require_reconciled_archive(entry): pass
'''


class Behavior(unittest.TestCase):
    """End-to-end run of the embedded script against a fake installed launcher (no Docker, no root)."""

    def setUp(self):
        import json as _j, tempfile, os
        self.tmp = Path(tempfile.mkdtemp())
        self.state = self.tmp / "state"
        self.state.mkdir()
        self.stage = self.tmp / "stage"
        self.stage.mkdir()
        (self.stage / "qa525-update.py").write_text(FAKE_UPDATE)
        img = "a" * 64
        self.api_base = ["/b/compose.recovery.json", "/q/api-" + "b" * 40 + ".json", "/etc/identity.json"]
        self.web_base = ["/b/compose.recovery.json", "/b/web-" + "c" * 64 + ".json"]
        la, lw = str(self.state / ("qa525-api-%s-%s.json" % (SRC, img))), str(self.state / ("qa525-web-%s-%s.json" % (SRC, img)))
        for p, n in ((la, "api"), (lw, "web")):
            Path(p).write_text(_j.dumps({"services": {n: {"image": "sha256:" + img}}}))
        self.layers = {"api": self.api_base + [la], "web": self.web_base + [lw]}
        self.live = self.tmp / "live.json"
        self.live.write_text(_j.dumps({"labels": self.layers, "cid": {"api": "A", "web": "W"}, "dep": "K", "health": True, "web_clean": False, "rolled": False}))
        self.state_path = self.state

    def run_py(self, mode, **cfg):
        import json as _j, os, subprocess as sp
        env = dict(os.environ, FAKE_CFG=_j.dumps(dict(cfg, state=str(self.state), src=SRC, live=str(self.live))))
        code = PY.replace("def plan(", "import builtins\nSTATE_OVERRIDE = None\ndef plan(", 1)
        r = sp.run(["python3", "-B", "-", str(self.stage), SRC, mode], input=code, capture_output=True, text=True, env=env, timeout=60)
        return r.returncode, r.stdout + r.stderr

    def labels(self):
        import json as _j
        return _j.loads(self.live.read_text())["labels"]

    def test_success_journal_final_and_labels_clean(self):
        rc, out = self.run_py("EXECUTE_RECONCILE")
        self.assertEqual(rc, 0, out)
        self.assertIn("RECONCILED_LABELS_CLEAN", out)
        import json as _j
        j = _j.loads((self.state / "layers-reconcile-6eed4.json").read_text())
        self.assertEqual(j["status"], "RECONCILED")
        self.assertFalse(any(SRC in f for fs in self.labels().values() for f in fs))
        self.assertEqual(oct((self.state / "layers-reconcile-6eed4.json").stat().st_mode & 0o777), "0o600")
        self.assertFalse((self.state / ("dual-" + SRC + ".json")).exists())

    def test_failure_in_web_rolls_back_verified(self):
        rc, out = self.run_py("EXECUTE_RECONCILE", fail_web_up=True)
        self.assertEqual(rc, 3, out)
        self.assertIn("ROLLED_BACK_VERIFIED", out)
        self.assertEqual(self.labels(), self.layers)
        import json as _j
        self.assertEqual(_j.loads((self.state / "layers-reconcile-6eed4.json").read_text())["status"], "ROLLED_BACK")

    def test_dependency_changed_blocks_pass_and_rolls_back(self):
        rc, out = self.run_py("EXECUTE_RECONCILE", change_dep=True)
        self.assertNotIn("RECONCILED_LABELS_CLEAN", out)
        self.assertIn("ROLLBACK_NOT_VERIFIED", out)  # la dependencia ya cambió: el rollback NO puede declararse PASS
        import json as _j
        self.assertEqual(_j.loads((self.state / "layers-reconcile-6eed4.json").read_text())["status"], "RECOVERY_REQUIRED")

    def test_rollback_failure_marks_recovery_required_and_recover_mode_reports(self):
        rc, out = self.run_py("EXECUTE_RECONCILE", fail_web_up=True, fail_rollback=True)
        self.assertEqual(rc, 3, out)
        self.assertIn("MANUAL_RECOVERY_REQUIRED", out)
        rc2, out2 = self.run_py("RECOVER_READ_ONLY")
        self.assertIn("JOURNAL_STATUS=RECOVERY_REQUIRED", out2)
        self.assertIn("RECOVER_READ_ONLY_DONE_NO_CHANGES", out2)
        rc3, out3 = self.run_py("RECOVER_EXECUTE")
        self.assertEqual(rc3, 0, out3)  # sin la falla inyectada, la recuperación verifica y cierra
        self.assertIn("ROLLED_BACK_VERIFIED", out3)
        self.assertEqual(self.labels(), self.layers)
        rc4, out4 = self.run_py("RECOVER_EXECUTE", fail_rollback=True)
        self.assertIn("JOURNAL_FINAL_NOTHING_TO_DO", out4)

    def test_second_execute_refused_when_journal_exists(self):
        self.run_py("EXECUTE_RECONCILE")
        rc, out = self.run_py("EXECUTE_RECONCILE")
        self.assertIn("RECONCILE_JOURNAL_EXISTS_USE_RECOVER", out)

    def test_read_only_changes_nothing(self):
        before = self.labels()
        rc, out = self.run_py("READ_ONLY")
        self.assertEqual(rc, 0, out)
        self.assertIn("A0=PASS", out)
        self.assertEqual(self.labels(), before)
        self.assertEqual(list(self.state.glob("layers-reconcile*")), [])
