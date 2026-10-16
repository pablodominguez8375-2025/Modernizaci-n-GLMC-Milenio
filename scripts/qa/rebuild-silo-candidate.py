#!/usr/bin/env python3
"""Build an isolated, review-only QA image; never publish or deploy it."""
import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import subprocess
import tarfile
import tempfile
import time

ROOT = Path(__file__).resolve().parents[2]
VENDOR = 'docker.io/pgsty/silo@sha256:aaac6bce13eb99316b3c88bd20143fc3c38ab607585256a411a095c44ee57f62'
CLIENT_SOURCE = '0e24a70a0a428de4ae2b7853db3499ba1cb34f37'


def run(args, **kwargs):
    return subprocess.run(args, check=True, timeout=kwargs.pop('timeout', 180), **kwargs)


def check_source(source):
    actual = subprocess.check_output(['git', '-C', str(source), 'rev-parse', 'HEAD'], text=True).strip()
    if actual != CLIENT_SOURCE:
        raise ValueError('client source mismatch')
    dirty = subprocess.check_output(['git', '-C', str(source), 'status', '--porcelain', '--untracked-files=all'])
    if dirty:
        raise ValueError('dirty client source')
    archive = subprocess.check_output(['git', '-C', str(source), 'archive', CLIENT_SOURCE])
    return hashlib.sha256(archive).hexdigest()


def scanner_tools(output):
    spec = importlib.util.spec_from_file_location('silo_supply', ROOT / 'scripts/qa/verify-silo-supply.py')
    supply = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(supply)
    data = supply.fetch(supply.TRIVY_URL, maximum=128 * 1024 * 1024)
    if hashlib.sha256(data).hexdigest() != supply.TRIVY_SHA:
        raise ValueError('scanner checksum mismatch')
    archive = output / 'trivy.tgz'
    archive.write_bytes(data)
    with tarfile.open(archive) as tar:
        member = tar.extractfile('trivy')
        if member is None:
            raise ValueError('scanner unavailable')
        scanner = output / 'trivy'
        scanner.write_bytes(member.read())
        scanner.chmod(0o700)
    archive.unlink()
    return scanner, supply.TRIVY_SHA


def blocking_findings(inventory):
    results = inventory.get('Results')
    if not isinstance(results, list) or not results:
        raise ValueError('empty scan inventory')
    return [{key: finding.get(key) for key in
             ('VulnerabilityID', 'PkgName', 'InstalledVersion', 'FixedVersion', 'Severity')} |
            {'Target': result.get('Target')}
            for result in results for finding in result.get('Vulnerabilities', [])
            if finding.get('Severity') in ('HIGH', 'CRITICAL')]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    (output / 'NOT-A-DEPLOYMENT-RELEASE.txt').write_text(
        'Review-only candidate. No registry push or institutional deployment.\n'
        'Raw scan findings and real runtime results must be reviewed separately.\n')
    report = {'source_sha': os.environ.get('QA_VALIDATED_SOURCE_SHA'),
              'vendor_image': VENDOR, 'client_source': CLIENT_SOURCE,
              'status': 'BLOCKED', 'failures': {}, 'scope': 'SYNTHETIC_QA_RUNNER_ONLY'}
    stage = 'source'
    image = None
    try:
        report['client_git_archive_sha256'] = check_source(args.source)
        stage = 'vendor_signature'
        with (output / 'vendor-attestation.json').open('w') as stream:
            run(['gh', 'attestation', 'verify', 'oci://' + VENDOR, '--repo', 'pgsty/silo',
                 '--format', 'json'], stdout=stream, stderr=subprocess.DEVNULL)
        report['vendor_signature_verified'] = True
        stage = 'build'
        tag = 'pmgm-silo-qa-rebuild:' + report['source_sha'][:12]
        with (output / 'build.log').open('w') as stream:
            run(['docker', 'buildx', 'build', '--load', '--no-cache', '--platform', 'linux/amd64',
                 '--build-context', 'mcli_source=' + str(args.source.resolve()),
                 '-f', str(ROOT / 'infrastructure/silo/Dockerfile.qa-rebuild'),
                 '-t', tag, str(ROOT)], stdout=stream, stderr=subprocess.STDOUT, timeout=1200)
        image = subprocess.check_output(['docker', 'image', 'inspect', '--format', '{{.Id}}', tag], text=True).strip()
        if re.fullmatch(r'sha256:[0-9a-f]{64}', image) is None:
            raise ValueError('invalid image identity')
        report['local_image_id'] = image
        stage = 'binary_inventory'
        with (output / 'runtime-binaries-and-rpms.txt').open('w') as stream:
            run(['docker', 'run', '--rm', '--entrypoint', '/bin/sh', image, '-c',
                 'set -eu; sha256sum /usr/bin/silo /usr/bin/mcli; '
                 'cat /licenses/mcli-buildinfo.txt /licenses/qa-runtime-rpms.txt'], stdout=stream)
        stage = 'save'
        archive = output / 'silo-qa-review-only.tar'
        run(['docker', 'save', '-o', str(archive), image], timeout=180)
        report['archive_sha256'] = hashlib.file_digest(archive.open('rb'), 'sha256').hexdigest()
        stage = 'scan'
        # Temporary tools/cache are not uploaded; no inherited Trivy ignore/VEX.
        with tempfile.TemporaryDirectory(prefix='qa-rebuild-scan-') as tmp:
            scanner, scanner_sha = scanner_tools(Path(tmp))
            common = [str(scanner), 'image', '--input', str(archive), '--timeout', '8m',
                      '--cache-dir', str(Path(tmp) / 'cache'), '--ignorefile', '/dev/null']
            run(common + ['--scanners', 'vuln', '--format', 'json', '--output', str(output / 'trivy-raw.json')],
                stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=540)
            inventory = json.loads((output / 'trivy-raw.json').read_text())
            report['scanner_binary_sha256'] = scanner_sha
            report['blocking_findings'] = blocking_findings(inventory)
            run(common + ['--format', 'cyclonedx', '--output', str(output / 'sbom.cdx.json')],
                stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=180)
    except Exception as error:
        report['failures'][stage] = {'type': type(error).__name__}
        if isinstance(error, subprocess.CalledProcessError):
            report['failures'][stage]['exit'] = error.returncode
    if image:
        try:
            environment = dict(os.environ, PMGM_SILO_REBUILD_LAB='1',
                               PMGM_SILO_REBUILD_IMAGE_ID=image, PYTHONPATH=str(ROOT / 'tests'))
            started = time.monotonic()
            with (output / 'runtime-tests.log').open('w') as stream:
                run(['python3', '-m', 'unittest', 'test_silo_real_iam', 'test_silo_real_recovery',
                     'test_silo_dotnet_sdk', '-v'], cwd=ROOT, env=environment,
                    stdout=stream, stderr=subprocess.STDOUT, timeout=900)
            report['real_iam_recovery_sdk'] = {'status': 'PASS', 'seconds': round(time.monotonic() - started, 3)}
        except Exception as error:
            report['failures']['real_iam_recovery_sdk'] = {'type': type(error).__name__}
    if report['failures']:
        report['status'] = 'BLOCKED_INCOMPLETE_VALIDATION'
    elif report.get('blocking_findings'):
        report['status'] = 'BLOCKED_VULNERABILITIES'
    else:
        report['status'] = 'CANDIDATE_VALIDATED_NOT_DEPLOYED'
    (output / 'rebuild-report.json').write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps(report, separators=(',', ':')))
    return 0 if report['status'] == 'CANDIDATE_VALIDATED_NOT_DEPLOYED' else 1


if __name__ == '__main__':
    raise SystemExit(main())
