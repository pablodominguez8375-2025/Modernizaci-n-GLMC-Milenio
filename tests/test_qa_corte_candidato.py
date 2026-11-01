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

    def test_marker_on_page_four_is_found_and_no_duplicate_post(self):
        marker = cc.MARKER.format(sha=SHA)
        pages = {1: [{"body": "x"}] * 100, 2: [{"body": "x"}] * 100, 3: [{"body": "x"}] * 100,
                 4: [{"body": "x"}] * 40 + [{"body": marker + " corte"}], 5: []}
        posts = []
        self.assertEqual(cc.run(SHA, lambda s: self.release(), lambda p: pages.get(p, []), posts.append), 0)
        self.assertEqual(posts, [])

    def test_posts_once_when_marker_absent_across_many_pages(self):
        pages = {p: [{"body": "x"}] * 100 for p in range(1, 6)}
        pages[6] = [{"body": "x"}] * 3
        posts = []
        cc.run(SHA, lambda s: self.release(), lambda p: pages.get(p, []), posts.append)
        self.assertEqual(len(posts), 1)
        self.assertIn(cc.MARKER.format(sha=SHA), posts[0])

    def test_scan_bound_fails_closed_without_posting(self):
        posts = []
        with self.assertRaises(SystemExit):
            cc.run(SHA, lambda s: self.release(), lambda p: [{"body": "x"}] * 100, posts.append)
        self.assertEqual(posts, [])

    def test_body_warns_not_to_execute_and_distinguishes_signature(self):
        text = cc.body(SHA, DIGEST)
        self.assertIn("NO EJECUTAR", text)
        self.assertIn("Sigstore", text)
        self.assertIn("plantilla de referencia", text.lower())

    def test_workflow_step_cannot_fail_publication(self):
        wf = (ROOT / ".github/workflows/qa-signed-publish.yml").read_text()
        self.assertIn("scripts/qa/qa-corte-candidato.py", wf)
        self.assertIn("continue-on-error: true", wf)
        self.assertNotIn("workflow_dispatch", wf)
        self.assertNotIn("--review-and-run", wf)


if __name__ == "__main__":
    unittest.main()
