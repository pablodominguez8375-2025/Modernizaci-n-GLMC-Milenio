"""Tests for the CI-side auto-web envelope producer and its workflow (inactive)."""
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]
_spec = importlib.util.spec_from_file_location("aw_ci_producer", ROOT / "scripts/qa/qa525-vnext-release-envelope-ci.py")
CI = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(CI)
WORKFLOW = ROOT / ".github/workflows/qa-release-envelope.yml"
PNG = b"\x89PNG\r\n\x1a\n" + b"\x00" * 16


def git(repo, *args):
    env = dict(os.environ, GIT_AUTHOR_NAME="t", GIT_AUTHOR_EMAIL="t@t", GIT_COMMITTER_NAME="t",
               GIT_COMMITTER_EMAIL="t@t", GIT_CONFIG_GLOBAL="/dev/null", GIT_CONFIG_SYSTEM="/dev/null")
    return subprocess.run(["git", "-C", repo, *args], check=True, capture_output=True, env=env).stdout.decode().strip()


class AutoWebCiProducerTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.repo = self.tmp.name
        git(self.repo, "init", "-q", "-b", "dev")
        self.write("frontend/src/App.tsx", b"export {}\n")
        self.write("frontend/src/assets/old.png", PNG)
        git(self.repo, "add", "-A")
        git(self.repo, "commit", "-q", "-m", "base")
        self.base = git(self.repo, "rev-parse", "HEAD")

    def tearDown(self):
        self.tmp.cleanup()

    def write(self, rel, data, mode=None):
        path = Path(self.repo, rel)
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)
        if mode:
            path.chmod(mode)

    def commit(self):
        git(self.repo, "add", "-A")
        git(self.repo, "commit", "-q", "-m", "c")
        return git(self.repo, "rev-parse", "HEAD")

    def test_lineage_sniffs_blobs_and_reports_modes(self):
        self.write("frontend/src/assets/new.png", PNG)
        self.write("frontend/src/assets/fake.png", b"<svg onload=x>")
        self.write("frontend/src/tool.png", PNG, mode=0o755)
        Path(self.repo, "frontend/src/assets/old.png").unlink()
        head = self.commit()
        lin = CI.lineage(self.repo, self.base, head)
        self.assertEqual((lin["merge_base"], lin["ahead_by"], lin["total_changed_files"]), (self.base, 1, 4))
        by = {e["path"]: e for e in lin["changed_files"]}
        self.assertEqual(by["frontend/src/assets/new.png"]["content_kind"], "png")
        self.assertEqual(by["frontend/src/assets/fake.png"]["content_kind"], "unknown")
        self.assertEqual(by["frontend/src/tool.png"]["mode"], "100755")
        self.assertEqual((by["frontend/src/assets/old.png"]["status"], by["frontend/src/assets/old.png"]["content_kind"]),
                         ("removed", "none"))
        verdict = CI.CLASSIFIER.classify(lin["changed_files"], complete=True, total=4)
        self.assertEqual(verdict["result"], "manual")
        self.assertTrue({"CONTENT_KIND_MISMATCH", "MODE_NOT_REGULAR_FILE", "F4A_REMOVAL_MANUAL"} <= set(verdict["reasons"]))

    def test_only_png_change_is_auto_web(self):
        self.write("frontend/src/assets/new.png", PNG)
        head = self.commit()
        lin = CI.lineage(self.repo, self.base, head)
        self.assertEqual(CI.CLASSIFIER.classify(lin["changed_files"], complete=True, total=1)["result"], "auto-web")

    def test_raw_diff_parser_rejects_malformed_and_renames(self):
        with self.assertRaises(CI.Fail):
            CI.parse_raw_diff(b":100644 100644 a b R100\0x\0")
        with self.assertRaises(CI.Fail):
            CI.parse_raw_diff(b":100644 100644 a b M\0")

    def test_main_refuses_outside_dev_head(self):
        env = dict(os.environ, GITHUB_REPOSITORY="o/r", GITHUB_REF="refs/heads/main", GITHUB_SHA="a" * 40)
        old = os.environ.copy()
        try:
            os.environ.clear()
            os.environ.update(env)
            with self.assertRaisesRegex(CI.Fail, "CI_REF_OR_SHA_NOT_DEV_HEAD"):
                CI.main(["--repo", self.repo, "--installed-json", "{}", "--candidate-sha", "a" * 40,
                         "--sequence", "1", "--issued-at", "x", "--not-before", "x", "--expires-at", "x",
                         "--image-inspect", "x", "--image-archive", "x", "--base-digests-json", "x",
                         "--build-args-json", "x", "--content-layer-count", "2", "--out-dir", self.repo])
        finally:
            os.environ.clear()
            os.environ.update(old)


class AutoWebWorkflowStaticTests(unittest.TestCase):
    def setUp(self):
        self.text = WORKFLOW.read_text()

    def test_manual_dispatch_only_no_secrets_no_publish(self):
        head = self.text.split("permissions:")[0]
        self.assertIn("workflow_dispatch:", head)
        for word in ("push:", "schedule:", "pull_request", "workflow_run"):
            self.assertNotIn(word, head)
        for word in ("secrets.", "gh release", "contents: write", "ssh ", "glmdch"):
            self.assertNotIn(word, self.text)
        self.assertIn('test "$GITHUB_REF" = "refs/heads/dev"', self.text)
        self.assertIn("persist-credentials: false", self.text)

    def test_actions_pinned_by_sha_and_no_pull_in_build(self):
        for line in self.text.splitlines():
            if "uses:" in line:
                ref = line.split("@", 1)[1].split()[0]
                self.assertRegex(ref, r"^[0-9a-f]{40}$", line)
        self.assertIn("docker build --pull=false", self.text)
        self.assertIn("python3 -I scripts/qa/qa525-vnext-release-envelope-ci.py", self.text)
        self.assertIn('CONTENT_LAYER_COUNT: "2"', self.text)

    def test_content_layer_count_matches_dockerfile_final_stage(self):
        final = (ROOT / "frontend/Dockerfile").read_text().split("AS final", 1)[1]
        layer_steps = [l for l in final.splitlines() if l.startswith(("COPY ", "RUN ", "ADD "))]
        self.assertEqual(len(layer_steps), 3)  # conf (base, constant) + dist + chmod (2 content)
        self.assertTrue(layer_steps[0].startswith("COPY nginx.conf"))

    def test_workflow_is_valid_yaml(self):
        """The #1012 version had an unquoted ': ' in a step name: GitHub could not load it."""
        for number, line in enumerate(self.text.splitlines(), 1):
            stripped = line.strip()
            for key in ("- name: ", "name: ", "description: "):
                if stripped.startswith(key):
                    value = stripped[len(key):]
                    if not value.startswith(('"', "'")):
                        self.assertNotIn(": ", value, f"line {number}: unquoted ': ' in {key.strip()}")
        try:
            import yaml
        except ImportError:
            return
        doc = yaml.safe_load(self.text)
        self.assertEqual(set(doc["jobs"]), {"e2e", "envelope"})

    def test_untrusted_frontend_code_never_runs_in_the_signing_job(self):
        e2e, envelope = self.text.split("\n  envelope:\n", 1)
        self.assertIn("needs: e2e", envelope)
        self.assertNotIn("id-token", e2e.split("\n  e2e:\n", 1)[1])
        self.assertIn("id-token: write", envelope)
        for word in ("npm ", "npx ", "node .github", "setup-node", "--emit-f4b-facts"):
            self.assertNotIn(word, envelope, word)
        self.assertIn("--ignore-scripts", e2e)
        self.assertIn("--emit-f4b-facts", e2e)
        self.assertIn("f4b-shadow.json", envelope)

    def test_e2e_report_and_facts_loading(self):
        holder = tempfile.TemporaryDirectory()
        self.addCleanup(holder.cleanup)
        tmp = Path(holder.name)
        good = tmp / "ok.json"
        good.write_text(json.dumps({"schema": "pmgm-showcase-console-errors-v1", "walk_completed": True, "count": 0}))
        self.assertTrue(CI.e2e_passed(str(good)))
        for bad in ({"schema": "pmgm-showcase-console-errors-v1", "walk_completed": True, "count": 1},
                    {"schema": "pmgm-showcase-console-errors-v1", "walk_completed": False, "count": 0}, {}, []):
            p = tmp / "bad.json"
            p.write_text(json.dumps(bad))
            self.assertFalse(CI.e2e_passed(str(p)), bad)
        self.assertFalse(CI.e2e_passed(None))
        self.assertFalse(CI.e2e_passed(str(tmp / "missing.json")))
        self.assertEqual(CI.load_facts(None), [])
        (tmp / "f.json").write_text("{not json")
        self.assertEqual(CI.load_facts(str(tmp / "f.json")), [])
        (tmp / "deep.json").write_text("[" * 100000 + "]" * 100000)
        self.assertEqual(CI.load_facts(str(tmp / "deep.json")), [])
        self.assertFalse(CI.e2e_passed(str(tmp / "deep.json")))

    def test_showcase_walker_records_console_errors(self):
        walker = (ROOT / ".github/scripts/capture-showcase-views.mjs").read_text()
        for needle in ("Runtime.exceptionThrown", "Runtime.consoleAPICalled", "Log.entryAdded",
                       "console-errors.json", "PMGM_FAIL_ON_CONSOLE_ERRORS", "walk_completed"):
            self.assertIn(needle, walker)

    def test_build_args_file_is_valid(self):
        args = json.loads((ROOT / "ops/qa525/autoweb/web-build-args.json").read_text())
        self.assertTrue(CI.ENVELOPE.build_args_sha256(args).startswith("sha256:"))


if __name__ == "__main__":
    unittest.main()
