"""Candidate-cut comment is informational and bound to the exact SHA and digest."""
from pathlib import Path
import importlib.util
import unittest

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("cc", ROOT / "scripts/qa/qa-corte-candidato.py")
cc = importlib.util.module_from_spec(spec)
spec.loader.exec_module(cc)
SHA = "a" * 40
DIGEST = "sha256:" + "b" * 64


class CandidateTests(unittest.TestCase):
    def release(self, **kw):
        base = {"tag_name": "qa-signed-" + SHA, "assets": [{"name": "qa-signed-" + SHA + ".zip", "digest": DIGEST}]}
        base.update(kw)
        return base

    def test_digest_taken_from_exact_asset(self):
        self.assertEqual(cc.candidate(SHA, self.release()), DIGEST)

    def test_rejects_wrong_tag_missing_or_bad_digest(self):
        for rel in (self.release(tag_name="qa-signed-" + "c" * 40), self.release(assets=[]),
                    self.release(assets=[{"name": "qa-signed-" + SHA + ".zip", "digest": "sha256:xyz"}])):
            with self.assertRaises(SystemExit):
                cc.candidate(SHA, rel)
        with self.assertRaises(SystemExit):
            cc.candidate("main", self.release())

    def test_comment_is_not_a_review_and_has_marker(self):
        text = cc.body(SHA, DIGEST)
        self.assertIn(cc.MARKER.format(sha=SHA), text)
        self.assertIn("No es una revisión ni una autorización", text)
        self.assertNotIn("qa525-reviewed-cut-v1", text)
        self.assertIn("--source " + SHA + " --digest " + DIGEST, text)

    def test_workflow_step_cannot_fail_publication(self):
        wf = (ROOT / ".github/workflows/qa-signed-publish.yml").read_text()
        self.assertIn("scripts/qa/qa-corte-candidato.py", wf)
        self.assertIn("continue-on-error: true", wf)


if __name__ == "__main__":
    unittest.main()
