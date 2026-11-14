"""F4-B SHADOW: AST analyzer (node + TypeScript) and pure shadow verdict. INACTIVE.

The analyzer needs frontend/node_modules/typescript. CI installs it and sets QA525_REQUIRE_TS=1,
so a missing toolchain FAILS there instead of skipping.
"""
import importlib.util
import json
import os
from pathlib import Path
import shutil
import subprocess
import unittest

ROOT = Path(__file__).resolve().parents[1]
_spec = importlib.util.spec_from_file_location("aw_f4b", ROOT / "scripts/qa/qa525-vnext-autoweb-f4b.py")
B = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(B)
ANALYZER = ROOT / "scripts/qa/qa525-vnext-autoweb-f4b-analyze.cjs"
HAVE_TS = shutil.which("node") is not None and (ROOT / "frontend/node_modules/typescript").is_dir()
if os.environ.get("QA525_REQUIRE_TS") == "1" and not HAVE_TS:
    raise RuntimeError("QA525_REQUIRE_TS=1 but node/typescript is missing")


def analyze(path, old, new):
    out = subprocess.run(["node", str(ANALYZER)], input=json.dumps([{"path": path, "old": old, "new": new}]).encode(),
                         capture_output=True, check=True)
    return json.loads(out.stdout)[0]


BASE_TSX = """import { useState } from 'react'
import { listLodges } from './api/lodgeApi'
export function Page({ onSave }: { onSave: () => void }) {
  const [n, setN] = useState(0)
  return <div className="panel"><h2>Talleres</h2><button onClick={() => setN(n + 1)}>{n}</button></div>
}
"""


@unittest.skipUnless(HAVE_TS, "node/typescript not installed (CI enforces QA525_REQUIRE_TS=1)")
class AutoWebF4bAnalyzerTests(unittest.TestCase):
    def facts(self, new, old=BASE_TSX, path="frontend/src/Page.tsx"):
        return analyze(path, old, new)

    def test_text_and_markup_change_is_clean(self):
        f = self.facts(BASE_TSX.replace("<h2>Talleres</h2>", "<h2>Talleres de la Orden</h2>"))
        self.assertTrue(f["parse_ok"] and f["imports_equal"])
        self.assertEqual((f["new_sinks"], f["new_api_refs"], f["new_mutating_prop_calls"]), ({}, 0, 0))
        self.assertEqual(f["added_lines"], 1)

    def test_new_sinks_detected(self):
        cases = {
            "id:fetch": "fetch('/api/x')", "prop:localStorage": "window.localStorage.setItem('a','b')",
            "id:localStorage": "localStorage.clear()", "id:eval": "eval('1')", "new_Function": "new Function('x')",
            "dynamic_import": "import('./x')", "prop:innerHTML": "document.body.innerHTML = ''",
            "prop:cookie": "document.cookie = 'a=b'", "import_meta_env": "import.meta.env.VITE_X",
            "window.open": "window.open('https://x')", "prop:sendBeacon": "navigator.sendBeacon('/x')",
            "id:WebSocket": "new WebSocket('wss://x')", "location:href": "window.location.href = '/x'",
        }
        for kind, stmt in cases.items():
            with self.subTest(kind=kind):
                new = BASE_TSX.replace("  const [n, setN]", f"  {stmt}\n  const [n, setN]")
                self.assertIn(kind, self.facts(new)["new_sinks"])

    def test_dangerously_set_inner_html(self):
        new = BASE_TSX.replace('<div className="panel">', '<div className="panel" dangerouslySetInnerHTML={{ __html: "x" }}>')
        self.assertIn("dangerouslySetInnerHTML", self.facts(new)["new_sinks"])

    def test_new_api_reference_and_mutating_callback(self):
        f = self.facts(BASE_TSX.replace("useState(0)", "useState(0); void listLodges()"))
        self.assertEqual(f["new_api_refs"], 1)
        f = self.facts(BASE_TSX.replace("useState(0)", "useState(0); onSave()"))
        self.assertEqual(f["new_mutating_prop_calls"], 1)

    def test_import_changes_detected(self):
        f = self.facts("import { x } from './other'\n" + BASE_TSX)
        self.assertEqual((f["imports_equal"], f["imports_added"]), (False, ["./other"]))
        f = self.facts(BASE_TSX.replace("import { listLodges } from './api/lodgeApi'\n", ""))
        self.assertEqual(f["imports_removed"], ["./api/lodgeApi"])

    def test_moving_code_keeps_counts(self):
        old = BASE_TSX.replace("useState(0)", "useState(0); fetch('/a')")
        new = old.replace("<h2>Talleres</h2>", "<h2>Otro</h2>")
        self.assertEqual(self.facts(new, old=old)["new_sinks"], {})

    def test_css_sinks(self):
        f = analyze("frontend/src/a.css", ".a{color:red}", ".a{color:red}\n@import 'x.css';\n.b{background:url(https://evil/x.png)}")
        self.assertEqual(set(f["new_sinks"]), {"css:@import", "css:url_external"})

    def test_parse_error_flagged(self):
        self.assertFalse(self.facts(BASE_TSX + "\nexport const = ;\n")["parse_ok"])

    def test_bad_input_fails_closed(self):
        out = subprocess.run(["node", str(ANALYZER)], input=b"{}", capture_output=True)
        self.assertNotEqual(out.returncode, 0)
        self.assertIn(b"F4B_ANALYZER_FAILED", out.stdout)


def F(path, status="modified", mode="100644", kind=None):
    if kind is None:
        kind = "none" if status == "removed" else ("png" if path.endswith(".png") else "unknown")
    return {"path": path, "status": status, "mode": mode, "content_kind": kind}


def FACT(path, **kw):
    base = {"path": path, "parse_ok": True, "imports_equal": True, "imports_added": [], "imports_removed": [],
            "new_sinks": {}, "new_api_refs": 0, "new_mutating_prop_calls": 0, "added_lines": 5}
    base.update(kw)
    return base


class AutoWebF4bVerdictTests(unittest.TestCase):
    P = "frontend/src/LodgePage.tsx"

    def verdict(self, files, facts, e2e=True, **kw):
        return B.shadow_verdict(files, facts, e2e_passed=e2e, **kw)

    def test_clean_screen_change_would_auto_but_never_applies(self):
        v = self.verdict([F(self.P)], [FACT(self.P)])
        self.assertEqual((v["result"], v["shadow"], v["may_apply"]), (B.WOULD_AUTO, True, False))

    def test_each_gate_makes_it_manual(self):
        cases = {
            "F4B_IMPORTS_CHANGED": FACT(self.P, imports_equal=False, imports_added=["./x"]),
            "F4B_NEW_SINK": FACT(self.P, new_sinks={"id:fetch": 1}),
            "F4B_NEW_API_REFERENCE": FACT(self.P, new_api_refs=1),
            "F4B_NEW_MUTATING_CALLBACK": FACT(self.P, new_mutating_prop_calls=1),
            "F4B_PARSE_FAILED": FACT(self.P, parse_ok=False),
            "F4B_TOO_LARGE": FACT(self.P, added_lines=401),
        }
        for reason, fact in cases.items():
            with self.subTest(reason=reason):
                self.assertIn(reason, self.verdict([F(self.P)], [fact])["reasons"])

    def test_structure_gates(self):
        self.assertIn("F4B_ONLY_MODIFIED_REGULAR_FILES", self.verdict([F(self.P, "added")], [])["reasons"])
        self.assertIn("F4B_ONLY_MODIFIED_REGULAR_FILES", self.verdict([F(self.P, mode="100755")], [FACT(self.P)])["reasons"])
        self.assertIn("F4B_FACTS_MISSING", self.verdict([F(self.P)], [])["reasons"])
        self.assertIn("F4B_E2E_NOT_PASSED", self.verdict([F(self.P)], [FACT(self.P)], e2e=False)["reasons"])
        many = [F("frontend/src/P%02d.tsx" % i) for i in range(16)]
        self.assertIn("F4B_FILE_COUNT", self.verdict(many, [FACT(e["path"]) for e in many])["reasons"])
        self.assertIn("COMPARISON_INCOMPLETE", self.verdict([F(self.P)], [FACT(self.P)], complete=False)["reasons"])
        self.assertIn("F4B_FACTS_INVALID", self.verdict([F(self.P)], [FACT(self.P), FACT(self.P)])["reasons"])

    def test_f4a_denylist_still_applies_to_screens(self):
        for p, why in (("frontend/src/api/lodgeApi.ts", "DENIED_DIRECTORY"), ("frontend/src/auth/session.ts", "DENIED_DIRECTORY"),
                       ("frontend/src/main.tsx", "DENIED_NAME"), ("frontend/src/runtimeConfig.ts", "DENIED_NAME"),
                       ("frontend/src/vite-env.d.ts", "DENIED_NAME"), ("frontend/index.html", "OUTSIDE_AUTO_WEB_ROOTS")):
            with self.subTest(path=p):
                self.assertIn(why, self.verdict([F(p)], [FACT(p)])["reasons"])

    def test_static_assets_reuse_f4a(self):
        v = self.verdict([F("frontend/src/assets/a.png", kind="png"), F(self.P)], [FACT(self.P)])
        self.assertEqual(v["result"], B.WOULD_AUTO)
        v = self.verdict([F("frontend/src/assets/a.png", kind="unknown")], [])
        self.assertIn("CONTENT_KIND_MISMATCH", v["reasons"])

    def test_f4a_classifier_untouched(self):
        """F4-B must not change the pinned F4-A classifier (policy hash)."""
        self.assertEqual(B.CLASSIFIER.VERSION, 3)
        self.assertEqual(B.CLASSIFIER.PROFILE, "F4A_STATIC_NON_EXECUTABLE")


if __name__ == "__main__":
    unittest.main()
