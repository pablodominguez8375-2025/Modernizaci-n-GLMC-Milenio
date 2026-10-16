#!/usr/bin/env python3
"""Build an isolated, review-only QA image; never publish or deploy it."""
import argparse
import datetime
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


SERVER_SOURCE = '7a3ca0f82100cce06289a465f54139da2639f642'
SERVER_FIX = '3252d5b7f349e0ea41987761e4487972ea977e15'
SERVER_BLOBS = {
    'internal/s3select/select.go': 'a7abcbeb078224b3b35dbf750b840c2c2ca15221',
    'internal/s3select/json/preader.go': '9f982893a9524fbd856f10467cb29c2a90681e41',
    'internal/s3select/csv/reader.go': '161ecc16fd975eea4091ad9b4f83f017cb923902',
}


def server_patch_evidence(image, output):
    # Reuse the checksum-bounded downloader, never fetch an unpinned branch.
    spec = importlib.util.spec_from_file_location('silo_supply', ROOT / 'scripts/qa/verify-silo-supply.py')
    supply = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(supply)
    comparison = json.loads(supply.fetch('https://api.github.com/repos/pgsty/silo/compare/' +
                                        SERVER_FIX + '...' + SERVER_SOURCE))
    if comparison.get('status') not in ('ahead', 'identical') or comparison.get('behind_by') != 0:
        raise ValueError('server does not inherit the fix')
    if SERVER_SOURCE not in (output / 'vendor-attestation.json').read_text():
        raise ValueError('vendor attestation does not identify expected server source')
    files = {}
    for path, expected in SERVER_BLOBS.items():
        content = supply.fetch('https://raw.githubusercontent.com/pgsty/silo/' + SERVER_SOURCE + '/' + path)
        blob = hashlib.sha1(b'blob ' + str(len(content)).encode() + b'\0' + content).hexdigest()
        if blob != expected:
            raise ValueError('server source blob mismatch')
        text = content.decode()
        if path.endswith('select.go'):
            if 'json.NewPReader' not in text or 'simdj.NewReader' in text:
                raise ValueError('JSON Lines bounded routing not established')
        elif 'const maxCharsPerRecord = 1 << 20' not in text or 'errOverMaxRecordSize' not in text:
            raise ValueError('bounded input parsing not established')
        files[path] = {'git_blob': blob, 'sha256': hashlib.sha256(content).hexdigest()}
    hashes = []
    for identity in (VENDOR, image):
        result = subprocess.check_output(['docker', 'run', '--rm', '--entrypoint', '/usr/bin/sha256sum',
                                          identity, '/usr/bin/silo'], text=True).split()[0]
        if re.fullmatch(r'[0-9a-f]{64}', result) is None:
            raise ValueError('invalid server binary digest')
        hashes.append(result)
    if hashes[0] != hashes[1]:
        raise ValueError('rebuilt server differs from signed vendor binary')
    evidence = {'vulnerability': 'CVE-2026-39414', 'source_commit': SERVER_SOURCE,
                'fix_commit': SERVER_FIX, 'fix_ancestry_verified': True,
                'source_files': files, 'server_binary_sha256': hashes[0],
                'candidate_image_id': image, 'server_binary_preserved': True,
                'binary_regression': 'CSV_AND_JSON_LINES_COUNT_OVERSIZED_RECORDS_REJECTED',
                'status': 'FIX_EVIDENCE_FOR_COMPONENT_REVIEW',
                'scanner_exception_applied': False}
    (output / 'server-cve-triage.json').write_text(json.dumps(evidence, indent=2) + '\n')
    return evidence


SERVER_BINARY = '547a8181b6674d2b5c3ae2a3c515807f45e613764dfbbc82cf5ad9ad3800c4e4'
SERVER_PURL = 'pkg:golang/github.com/minio/minio@v0.0.0-20261015052015-7a3ca0f82100'


def fixed_component_vex(evidence, image):
    expected = {'vulnerability': 'CVE-2026-39414', 'source_commit': SERVER_SOURCE,
                'fix_commit': SERVER_FIX, 'fix_ancestry_verified': True,
                'server_binary_sha256': SERVER_BINARY, 'candidate_image_id': image,
                'server_binary_preserved': True,
                'binary_regression': 'CSV_AND_JSON_LINES_COUNT_OVERSIZED_RECORDS_REJECTED'}
    if re.fullmatch(r'sha256:[0-9a-f]{64}', image) is None:
        raise ValueError('invalid VEX image binding')
    if any(evidence.get(k) != v for k, v in expected.items()):
        raise ValueError('unverified VEX binding')
    blobs = evidence.get('source_files', {})
    if set(blobs) != set(SERVER_BLOBS) or any(
            blobs[k].get('git_blob') != v for k, v in SERVER_BLOBS.items()):
        raise ValueError('unverified VEX source')
    # This project-authored statement describes a patched fork, not an upstream
    # vendor VEX or a waiver. The pipeline verifies its binary/image binding.
    return {
        '@context': 'https://openvex.dev/ns/v0.2.0',
        '@id': 'https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/vex/' + image.split(':')[1],
        'author': 'Proyecto Centenario security engineering',
        'role': 'Document Creator',
        'timestamp': datetime.datetime.now(datetime.timezone.utc).isoformat(),
        'version': 1,
        'statements': [{
            'vulnerability': {'name': 'CVE-2026-39414'},
            'products': [{'@id': SERVER_PURL, 'hashes': {'sha-256': SERVER_BINARY}}],
            'status': 'fixed',
            'status_notes': ('Patched pgsty/silo fork at ' + SERVER_SOURCE + ' inherits ' + SERVER_FIX +
                             '; bounded CSV and JSON Lines routing confirmed by pinned blobs and real binary COUNT tests. '
                             'Valid only for server SHA256 ' + SERVER_BINARY + ' in candidate ' + image +
                             '. Project statement; not a vendor-authored VEX. See server-cve-triage.json.')
        }]
    }


def evaluate_fixed_component(archive, image, evidence, output):
    statement = fixed_component_vex(evidence, image)
    vex_path = output / 'server-fixed.openvex.json'
    vex_path.write_text(json.dumps(statement, indent=2) + '\n')
    with tempfile.TemporaryDirectory(prefix='qa-rebuild-reviewed-scan-') as tmp:
        scanner, _ = scanner_tools(Path(tmp))
        run([str(scanner), 'image', '--input', str(archive), '--scanners', 'vuln',
             '--timeout', '8m', '--cache-dir', str(Path(tmp) / 'cache'),
             '--ignorefile', '/dev/null', '--vex', str(vex_path), '--format', 'json',
             '--output', str(output / 'trivy-reviewed.json')],
            stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=540)
    return blocking_findings(json.loads((output / 'trivy-reviewed.json').read_text()))


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
              'workflow_sha': os.environ.get('GITHUB_SHA'),
              'vendor_image': VENDOR, 'client_source': CLIENT_SOURCE,
              'status': 'BLOCKED', 'failures': {}, 'scope': 'SYNTHETIC_QA_RUNNER_ONLY'}
    stage = 'source'
    image = None
    try:
        if subprocess.check_output(['git', '-C', str(ROOT), 'rev-parse', 'HEAD'], text=True).strip() != report['source_sha']:
            raise ValueError('checkout source mismatch')
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
        with archive.open('rb') as stream:
            report['archive_sha256'] = hashlib.file_digest(stream, 'sha256').hexdigest()
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
                     'test_silo_dotnet_sdk', 'test_silo_select_record_limit', '-v'], cwd=ROOT, env=environment,
                    stdout=stream, stderr=subprocess.STDOUT, timeout=900)
            report['real_iam_recovery_sdk'] = {'status': 'PASS', 'seconds': round(time.monotonic() - started, 3)}
            report['server_cve_triage'] = server_patch_evidence(image, output)
            with archive.open('rb') as stream:
                if hashlib.file_digest(stream, 'sha256').hexdigest() != report['archive_sha256']:
                    raise ValueError('candidate archive changed after scan')
            effective = evaluate_fixed_component(archive, image, report['server_cve_triage'], output)
            # Prevent any unintended filtering beyond this one reviewed component.
            removed = [v for v in report['blocking_findings'] if v not in effective]
            if any(v['VulnerabilityID'] != 'CVE-2026-39414' or v['PkgName'] != 'github.com/minio/minio' or
                   v['InstalledVersion'] != 'v0.0.0-20261015052015-7a3ca0f82100' or
                   v['Target'] != 'usr/bin/silo' for v in removed):
                raise ValueError('unexpected VEX filtering')
            report['effective_blocking_findings'] = effective
            report['fixed_component_statement'] = 'PROJECT_VEX_BOUND_TO_VERIFIED_SERVER_BINARY'

        except Exception as error:
            report['failures']['real_iam_recovery_sdk'] = {'type': type(error).__name__}
    if report['failures']:
        report['status'] = 'BLOCKED_INCOMPLETE_VALIDATION'
    elif report.get('effective_blocking_findings', report.get('blocking_findings')):
        report['status'] = 'BLOCKED_VULNERABILITIES'
    else:
        report['status'] = 'CANDIDATE_VALIDATED_NOT_DEPLOYED'
    report['evidence_sha256'] = {}
    for name in ('trivy-raw.json', 'trivy-reviewed.json', 'sbom.cdx.json', 'runtime-binaries-and-rpms.txt'):
        if (output / name).is_file():
            report['evidence_sha256'][name] = hashlib.sha256((output / name).read_bytes()).hexdigest()
    (output / 'rebuild-report.json').write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps(report, separators=(',', ':')))
    return 0 if report['status'] == 'CANDIDATE_VALIDATED_NOT_DEPLOYED' else 1


if __name__ == '__main__':
    raise SystemExit(main())
