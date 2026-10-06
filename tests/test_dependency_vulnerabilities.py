import importlib.util
import unittest
from pathlib import Path

spec = importlib.util.spec_from_file_location(
    "audit", Path(__file__).resolve().parents[1] / "scripts/check-dependency-vulnerabilities.py")
audit = importlib.util.module_from_spec(spec)
spec.loader.exec_module(audit)


def report(severity=None, transitive=False):
    framework = {"framework": "net10.0"}
    if severity:
        framework["transitivePackages" if transitive else "topLevelPackages"] = [
            {"id": "Synthetic.Package", "vulnerabilities": [
                {"severity": severity, "advisoryurl": "https://example.invalid/advisory"}]}]
    return {"version": 1, "parameters": "--vulnerable --include-transitive",
            "sources": ["https://api.nuget.org/v3/index.json"],
            "projects": [{"path": "api.csproj", "frameworks": [framework]}]}


class DependencyAuditTests(unittest.TestCase):
    def test_clean_report(self):
        self.assertEqual([], audit.check_report(report()))

    def test_real_nuget_clean_shape_omits_frameworks(self):
        value = report()
        del value["projects"][0]["frameworks"]
        self.assertEqual([], audit.check_report(value))
        value["projects"][0]["frameworks"] = []
        self.assertEqual([], audit.check_report(value))

    def test_high_and_critical_direct_and_transitive(self):
        for severity in ("High", "Critical"):
            for transitive in (False, True):
                self.assertEqual(1, len(audit.check_report(report(severity, transitive))))

    def test_lower_severity_does_not_block(self):
        for severity in ("Low", "Moderate"):
            self.assertEqual([], audit.check_report(report(severity)))

    def test_incomplete_or_unavailable_audit_fails_closed(self):
        for value in ({}, {"version": 1, "projects": []},
                      {**report(), "problems": [{"severity": "warning", "message": "source unavailable"}]},
                      report("Unknown"), {**report(), "sources": []},
                      {**report(), "parameters": "--include-transitive"},
                      {**report(), "projects": [{}]}):
            with self.assertRaises(ValueError):
                audit.check_report(value)


if __name__ == "__main__":
    unittest.main()
