#!/usr/bin/env python3
"""Fail closed on incomplete NuGet v1 JSON or high/critical direct/transitive advisories."""
import json
import sys
from pathlib import Path


def check_report(report):
    if not isinstance(report, dict) or report.get("version") != 1:
        raise ValueError("Unsupported or missing NuGet report version")
    if report.get("problems"):
        raise ValueError("NuGet reported problems; vulnerability lookup was not proven complete")
    parameters = str(report.get("parameters", "")).split()
    if not {"--vulnerable", "--include-transitive"}.issubset(parameters):
        raise ValueError("Report must include vulnerable and transitive lookup")
    if not isinstance(report.get("sources"), list) or not report["sources"]:
        raise ValueError("NuGet report contains no advisory sources")
    projects = report.get("projects")
    if not isinstance(projects, list) or not projects:
        raise ValueError("NuGet report contains no projects")
    findings = []
    for project in projects:
        if not isinstance(project.get("path"), str) or not project["path"].endswith(".csproj"):
            raise ValueError("NuGet report contains no project path")
        # With --vulnerable, NuGet omits frameworks when there are no findings.
        # Only accept that shape after verifying the audit mode, sources and project.
        frameworks = project.get("frameworks", [])
        if not isinstance(frameworks, list):
            raise ValueError("Invalid NuGet frameworks")
        for framework in frameworks:
            for package_kind in ("topLevelPackages", "transitivePackages"):
                packages = framework.get(package_kind, [])
                if not isinstance(packages, list):
                    raise ValueError("Invalid NuGet package list")
                for package in packages:
                    for advisory in package.get("vulnerabilities", []):
                        severity = str(advisory.get("severity", "")).lower()
                        if severity not in {"low", "moderate", "high", "critical"}:
                            raise ValueError("Missing or unknown advisory severity")
                        if severity in {"high", "critical"}:
                            findings.append((package["id"], severity, advisory.get("advisoryurl", "")))
    return findings


def main(paths):
    failed = False
    for path in paths:
        try:
            findings = check_report(json.loads(Path(path).read_text(encoding="utf-8-sig")))
            for package, severity, url in findings:
                print(f"{path}: {severity} vulnerability in {package}: {url}", file=sys.stderr)
            failed |= bool(findings)
        except (OSError, ValueError, KeyError, TypeError, AttributeError) as error:
            print(f"{path}: dependency audit failed: {error}", file=sys.stderr)
            failed = True
    return 1 if failed or not paths else 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
