"""Pure-logic and static-contract tests for the 6eed4 compose-layer reconciler (the live Docker path is exercised only on glmdch)."""
import re
import subprocess
import unittest
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
SCRIPT = REPO / "scripts/qa/operators/qa525-layers-reconcile-6eed4-20261104.sh"
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
        for bad in ("--review-and-run", "--apply-supervised", "dual-" + SRC + ".json\", \"w", "docker rm", "docker stop", "docker rmi", " rm ", "rmtree", "unlink", "chmod", "chown", "mv "):
            self.assertNotIn(bad, TEXT, bad)
        self.assertIn("EXECUTE_RECONCILE", TEXT)
        self.assertIn("build.exclusive()", PY)
        self.assertEqual(len(re.findall(r"os\.open\(", PY)), 1)  # solo el log de auditoría
        self.assertIn("MODE == \"READ_ONLY\"", PY)
        self.assertLess(PY.index('MODE == "READ_ONLY"'), PY.index("run(\"api\""))  # A0 y salida antes de cualquier recreate


if __name__ == "__main__":
    unittest.main()
