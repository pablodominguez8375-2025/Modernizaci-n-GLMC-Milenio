#!/usr/bin/env python3
"""Build an isolated, review-only QA image; never publish or deploy it."""
import argparse
import datetime
import hashlib
import importlib.util
import json
import io
import os
from pathlib import Path
import re
import subprocess
import tarfile
import tempfile
import time
import urllib.error

ROOT = Path(__file__).resolve().parents[2]
VENDOR = 'docker.io/pgsty/silo@sha256:aaac6bce13eb99316b3c88bd20143fc3c38ab607585256a411a095c44ee57f62'
CLIENT_SOURCE = '0e24a70a0a428de4ae2b7853db3499ba1cb34f37'


def run(args, **kwargs):
    return subprocess.run(args, check=True, timeout=kwargs.pop('timeout', 180), **kwargs)


def check_source(source, expected=CLIENT_SOURCE):
    actual = subprocess.check_output(['git', '-C', str(source), 'rev-parse', 'HEAD'], text=True).strip()
    if actual != expected:
        raise ValueError('client source mismatch')
    dirty = subprocess.check_output(['git', '-C', str(source), 'status', '--porcelain', '--untracked-files=all'])
    if dirty:
        raise ValueError('dirty client source')
    archive = subprocess.check_output(['git', '-C', str(source), 'archive', expected])
    return hashlib.sha256(archive).hexdigest()


def export_source(source, expected, destination):
    digest = check_source(source, expected)
    raw = subprocess.check_output(['git', '-C', str(source), 'archive', expected])
    if hashlib.sha256(raw).hexdigest() != digest:
        raise ValueError('source archive changed')
    destination.mkdir()
    with tarfile.open(fileobj=io.BytesIO(raw)) as archive:
        archive.extractall(destination, filter='data')
    return digest


def failure_details(error):
    # Record useful, bounded diagnostics without URLs, headers or exception text.
    details = {'type': type(error).__name__}
    if isinstance(error, subprocess.CalledProcessError):
        details['exit'] = error.returncode
    if isinstance(error, urllib.error.HTTPError):
        details['http_status'] = error.code
    return details


VENDOR_SOURCE = '7a3ca0f82100cce06289a465f54139da2639f642'
SERVER_SOURCE = 'dd905b29f45d29bcef6aa894acef10f8071eb602'
SERVER_FIX = '3252d5b7f349e0ea41987761e4487972ea977e15'
SERVER_BLOBS = {
    'internal/s3select/select.go': 'a7abcbeb078224b3b35dbf750b840c2c2ca15221',
    'internal/s3select/json/preader.go': '9f982893a9524fbd856f10467cb29c2a90681e41',
    'internal/s3select/csv/reader.go': '161ecc16fd975eea4091ad9b4f83f017cb923902',
}


def server_patch_evidence(image, output, source):
    # Evidence comes from the already pinned checkout, not another API call.
    check_source(source, SERVER_SOURCE)
    ancestor = subprocess.run(['git', '-C', str(source), 'merge-base', '--is-ancestor',
                               SERVER_FIX, SERVER_SOURCE], stdout=subprocess.DEVNULL,
                              stderr=subprocess.DEVNULL, timeout=30)
    if ancestor.returncode != 0:
        raise ValueError('server does not inherit the fix')
    # The OCI attestation authenticates the retained vendor rootfs. The newer
    # rebuilt server source is bound separately by its immutable Git archive
    # and the project provenance emitted by this workflow.
    if VENDOR_SOURCE not in (output / 'vendor-attestation.json').read_text():
        raise ValueError('vendor attestation does not identify expected server source')
    files = {}
    for path, expected in SERVER_BLOBS.items():
        content = subprocess.check_output(['git', '-C', str(source), 'show', SERVER_SOURCE + ':' + path],
                                          timeout=30)
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
    if hashes[0] != VENDOR_SERVER_BINARY:
        raise ValueError('signed vendor server binary changed')
    evidence = {'vulnerability': 'CVE-2026-39414', 'source_commit': SERVER_SOURCE,
                'fix_commit': SERVER_FIX, 'fix_ancestry_verified': True,
                'source_files': files, 'server_binary_sha256': hashes[1],
                'vendor_server_binary_sha256': hashes[0],
                'server_build_toolchain': 'go1.27.3',
                'server_source_rebuilt': True,
                'candidate_image_id': image, 'server_binary_preserved': False,
                'binary_regression': 'CSV_AND_JSON_LINES_COUNT_OVERSIZED_RECORDS_REJECTED',
                'status': 'FIX_EVIDENCE_FOR_COMPONENT_REVIEW',
                'scanner_exception_applied': False}
    (output / 'server-cve-triage.json').write_text(json.dumps(evidence, indent=2) + '\n')
    return evidence


# Reviewed from authenticated Actions artifact12469593117; independently hashed
# the sole image layer and matched scan/runtime evidence of source93d85516.
SERVER_BINARY = None  # Phase 1: pin only after reviewing exact-head build evidence.
VENDOR_SERVER_BINARY = '547a8181b6674d2b5c3ae2a3c515807f45e613764dfbbc82cf5ad9ad3800c4e4'
SERVER_VERSION = 'v0.0.0-20261027000322-dd905b29f45d'
SERVER_PURL = 'pkg:golang/github.com/minio/minio@' + SERVER_VERSION
SERVER_GO_JOSE_VERSION = 'v4.1.6'


def fixed_component_vex(evidence, image):
    if not isinstance(SERVER_BINARY, str) or re.fullmatch(r'[0-9a-f]{64}', SERVER_BINARY) is None:
        raise ValueError('rebuilt server binary review pending')
    expected = {'vulnerability': 'CVE-2026-39414', 'source_commit': SERVER_SOURCE,
                'fix_commit': SERVER_FIX, 'fix_ancestry_verified': True,
                'server_binary_sha256': SERVER_BINARY, 'candidate_image_id': image,
                'server_binary_preserved': False,
                'vendor_server_binary_sha256': VENDOR_SERVER_BINARY,
                'server_build_toolchain': 'go1.27.3', 'server_source_rebuilt': True,
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


def verify_go_inventory(inventory):
    results = inventory.get('Results', [])
    server = [r for r in results if r.get('Target') == 'usr/bin/silo']
    if len(server) != 1 or not any(p.get('Name') == 'github.com/minio/minio' and
            p.get('Version') == SERVER_PURL.rsplit('@', 1)[1] for p in server[0].get('Packages', [])):
        raise ValueError('server main module version missing from scan')
    go_jose = [p.get('Version') for p in server[0].get('Packages', [])
               if p.get('Name') == 'github.com/go-jose/go-jose/v4']
    if go_jose != [SERVER_GO_JOSE_VERSION]:
        raise ValueError('server go-jose version missing or incorrect in scan')
    for target in ('usr/bin/silo', 'usr/bin/mcli'):
        rows = [r for r in results if r.get('Target') == target]
        if len(rows) != 1 or not any(p.get('Name') == 'stdlib' and p.get('Version') == 'v1.27.3'
                for p in rows[0].get('Packages', [])):
            raise ValueError('Go toolchain missing or incorrect in scan')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path, required=True)
    parser.add_argument('--server-source', type=Path, required=True)
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
        report['server_git_archive_sha256'] = check_source(args.server_source, SERVER_SOURCE)
        stage = 'vendor_signature'
        with (output / 'vendor-attestation.json').open('w') as stream:
            run(['gh', 'attestation', 'verify', 'oci://' + VENDOR, '--repo', 'pgsty/silo',
                 '--format', 'json'], stdout=stream, stderr=subprocess.DEVNULL)
        report['vendor_signature_verified'] = True
        stage = 'vendor_platform_and_license'
        spec = importlib.util.spec_from_file_location('silo_vendor', ROOT / 'scripts/qa/verify-silo-supply.py')
        supplier = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(supplier)
        report['vendor_registry'] = supplier.registry_evidence()
        license_bytes = supplier.fetch('https://raw.githubusercontent.com/pgsty/silo/' + SERVER_SOURCE + '/LICENSE')
        license_sha = hashlib.sha256(license_bytes).hexdigest()
        if license_sha != '0d96a4ff68ad6d4b6f1f30f713b18d5184912ba8dd389f86aa7710db079abcb0':
            raise ValueError('vendor license mismatch')
        (output / 'Silo-LICENSE.txt').write_bytes(license_bytes)
        report['vendor_license'] = {'source_commit': SERVER_SOURCE, 'spdx': 'AGPL-3.0-or-later',
                                    'sha256': license_sha, 'scope': 'TECHNICAL_IDENTITY_QA_ONLY'}
        stage = 'build'
        tag = 'pmgm-silo-qa-rebuild:' + report['source_sha'][:12]
        with tempfile.TemporaryDirectory(prefix='qa-pinned-go-sources-') as temporary:
            exported = Path(temporary)
            for path, commit, name, field in (
                (args.source, CLIENT_SOURCE, 'mcli', 'client_git_archive_sha256'),
                (args.server_source, SERVER_SOURCE, 'silo', 'server_git_archive_sha256'),
            ):
                if export_source(path, commit, exported/name) != report[field]:
                    raise ValueError('source changed before build')
            with (output / 'build.log').open('w') as stream:
                run(['docker', 'buildx', 'build', '--load', '--no-cache', '--platform', 'linux/amd64',
                     '--build-context', 'mcli_source=' + str(exported/'mcli'),
                     '--build-context', 'silo_source=' + str(exported/'silo'),
                     '-f', str(ROOT / 'infrastructure/silo/Dockerfile.qa-rebuild'),
                     '-t', tag, str(ROOT)], stdout=stream, stderr=subprocess.STDOUT, timeout=1800)
        image = subprocess.check_output(['docker', 'image', 'inspect', '--format', '{{.Id}}', tag], text=True).strip()
        if re.fullmatch(r'sha256:[0-9a-f]{64}', image) is None:
            raise ValueError('invalid image identity')
        report['local_image_id'] = image
        stage = 'binary_inventory'
        with (output / 'runtime-binaries-and-rpms.txt').open('w') as stream:
            run(['docker', 'run', '--rm', '--entrypoint', '/bin/sh', image, '-c',
                 'set -eu; sha256sum /usr/bin/silo /usr/bin/mcli; '
                 'cat /licenses/mcli-buildinfo.txt /licenses/silo-buildinfo.txt /licenses/qa-runtime-rpms.txt'], stdout=stream)
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
                      '--cache-dir', str(Path(tmp) / 'cache'), '--ignorefile', '/dev/null', '--list-all-pkgs']
            run(common + ['--scanners', 'vuln', '--format', 'json', '--output', str(output / 'trivy-raw.json')],
                stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=540)
            inventory = json.loads((output / 'trivy-raw.json').read_text())
            verify_go_inventory(inventory)
            report['scanner_binary_sha256'] = scanner_sha
            report['blocking_findings'] = blocking_findings(inventory)
            run(common + ['--format', 'cyclonedx', '--output', str(output / 'sbom.cdx.json')],
                stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=180)
    except Exception as error:
        report['failures'][stage] = failure_details(error)
    if image:
        stage = 'real_iam_recovery_sdk'
        try:
            environment = dict(os.environ, PMGM_SILO_REBUILD_LAB='1',
                               PMGM_SILO_REBUILD_IMAGE_ID=image, PYTHONPATH=str(ROOT / 'tests'))
            started = time.monotonic()
            with (output / 'runtime-tests.log').open('w') as stream:
                run(['python3', '-m', 'unittest', 'test_silo_real_iam', 'test_silo_real_recovery',
                     'test_silo_dotnet_sdk', 'test_silo_select_record_limit', '-v'], cwd=ROOT, env=environment,
                    stdout=stream, stderr=subprocess.STDOUT, timeout=900)
            report['real_iam_recovery_sdk'] = {'status': 'PASS', 'seconds': round(time.monotonic() - started, 3)}
            stage = 'server_cve_triage'
            report['server_cve_triage'] = server_patch_evidence(image, output, args.server_source)
            stage = 'candidate_archive_recheck'
            with archive.open('rb') as stream:
                if hashlib.file_digest(stream, 'sha256').hexdigest() != report['archive_sha256']:
                    raise ValueError('candidate archive changed after scan')
            stage = 'fixed_component_scan'
            effective = evaluate_fixed_component(archive, image, report['server_cve_triage'], output)
            # Prevent any unintended filtering beyond this one reviewed component.
            removed = [v for v in report['blocking_findings'] if v not in effective]
            if any(v['VulnerabilityID'] != 'CVE-2026-39414' or v['PkgName'] != 'github.com/minio/minio' or
                   v['InstalledVersion'] != SERVER_VERSION or
                   v['Target'] != 'usr/bin/silo' for v in removed):
                raise ValueError('unexpected VEX filtering')
            report['effective_blocking_findings'] = effective
            report['fixed_component_statement'] = 'PROJECT_VEX_BOUND_TO_VERIFIED_SERVER_BINARY'

        except Exception as error:
            report['failures'][stage] = failure_details(error)
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
