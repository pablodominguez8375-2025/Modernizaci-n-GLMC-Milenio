#!/usr/bin/env python3
"""Read-only supply verification and vulnerability inventory for the fixed QA image."""
import argparse
import datetime
import hashlib
import json
import os
from pathlib import Path
import subprocess
import tarfile
import tempfile
import urllib.request

IMAGE = 'docker.io/pgsty/silo@sha256:aaac6bce13eb99316b3c88bd20143fc3c38ab607585256a411a095c44ee57f62'
SOURCE = '7a3ca0f82100cce06289a465f54139da2639f642'
TRIVY_URL = 'https://github.com/aquasecurity/trivy/releases/download/v0.76.0/trivy_0.76.0_Linux-64bit.tar.gz'
TRIVY_SHA = 'e24b1c51293c1b15e4ba8d45bbc7891ba053f90278ec2698f3562c993e145fe7'
ACCEPT = ', '.join(['application/vnd.oci.image.index.v1+json', 'application/vnd.oci.image.manifest.v1+json',
                    'application/vnd.docker.distribution.manifest.list.v2+json', 'application/vnd.docker.distribution.manifest.v2+json'])


def fetch(url, headers=None, maximum=32 * 1024 * 1024):
    with urllib.request.urlopen(urllib.request.Request(url, headers=headers or {}), timeout=60) as response:
        value = response.read(maximum + 1)
    if len(value) > maximum:
        raise ValueError('oversized response')
    return value


def digest(value):
    return 'sha256:' + hashlib.sha256(value).hexdigest()


def registry_evidence():
    token = json.loads(fetch('https://auth.docker.io/token?service=registry.docker.io&scope=repository:pgsty/silo:pull'))['token']
    headers = {'Authorization': 'Bearer ' + token, 'Accept': ACCEPT}
    base = 'https://registry-1.docker.io/v2/pgsty/silo/'
    def get(kind, sha):
        raw = fetch(base + kind + '/' + sha, headers)
        if digest(raw) != sha:
            raise ValueError('digest mismatch')
        return json.loads(raw)
    index_sha = IMAGE.split('@', 1)[1]
    index = get('manifests', index_sha)
    candidates = [m for m in index['manifests'] if m.get('platform', {}).get('os') == 'linux'
                  and m.get('platform', {}).get('architecture') == 'amd64']
    if len(candidates) != 1:
        raise ValueError('ambiguous platform')
    child_sha = candidates[0]['digest']
    child = get('manifests', child_sha)
    config = get('blobs', child['config']['digest'])
    if config.get('architecture') != 'amd64' or config.get('os') != 'linux':
        raise ValueError('config platform mismatch')
    predicates = []
    for descriptor in index['manifests']:
        annotations = descriptor.get('annotations', {})
        if annotations.get('vnd.docker.reference.digest') != child_sha:
            continue
        attestation = get('manifests', descriptor['digest'])
        for layer in attestation.get('layers', []):
            if layer.get('mediaType') == 'application/vnd.in-toto+json':
                statement = get('blobs', layer['digest'])
                subjects = statement.get('subject', [])
                bound = any(s.get('digest', {}).get('sha256') == child_sha.split(':')[1] for s in subjects)
                if not bound:
                    raise ValueError('attestation subject mismatch')
                predicates.append({'type': statement.get('predicateType'), 'digest': layer['digest'],
                                   'source_commit_present': SOURCE in json.dumps(statement), 'subject_bound': bound})
    return {'image': IMAGE, 'index_sha256': index_sha, 'platform_manifest_sha256': child_sha,
            'config_sha256': child['config']['digest'], 'os': 'linux', 'architecture': 'amd64',
            'oci_predicates': predicates,
            'signature_note': 'Digest-bound OCI predicates alone do not authenticate the publisher.'}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', required=True)
    args = parser.parse_args()
    output = Path(args.output)
    output.mkdir(parents=True, exist_ok=True)
    report = {'timestamp_utc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
              'source_sha': os.environ.get('GITHUB_SHA', 'local'), 'image': IMAGE, 'status': 'BLOCKED'}
    stage = 'registry'
    try:
        report['registry'] = registry_evidence()
        stage = 'license'
        license_bytes = fetch('https://raw.githubusercontent.com/pgsty/silo/' + SOURCE + '/LICENSE')
        if b'GNU AFFERO GENERAL PUBLIC LICENSE' not in license_bytes or b'Version 3' not in license_bytes:
            raise ValueError('license unexpected')
        (output / 'Silo-LICENSE.txt').write_bytes(license_bytes)
        report['license'] = {'spdx': 'AGPL-3.0-or-later', 'source_commit': SOURCE, 'sha256': digest(license_bytes),
                             'scope': 'Technical identity check, not legal acceptance for production.'}
        stage = 'attestation'
        try:
            verification = subprocess.run(['gh', 'attestation', 'verify', 'oci://' + IMAGE,
                                           '--repo', 'pgsty/silo', '--format', 'json'],
                                          stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, timeout=90)
            report['publisher_signature_verified'] = verification.returncode == 0
            if verification.returncode == 0:
                (output / 'publisher-attestation.json').write_bytes(verification.stdout)
        except (OSError, subprocess.TimeoutExpired):
            report['publisher_signature_verified'] = False
        stage = 'scanner_download'
        with tempfile.TemporaryDirectory(prefix='qa-trivy-') as temporary:
            temporary = Path(temporary)
            binary_archive = fetch(TRIVY_URL, maximum=128 * 1024 * 1024)
            if hashlib.sha256(binary_archive).hexdigest() != TRIVY_SHA:
                raise ValueError('scanner checksum mismatch')
            archive = temporary / 'trivy.tgz'
            archive.write_bytes(binary_archive)
            with tarfile.open(archive) as tar:
                binary = tar.extractfile('trivy')
                if binary is None:
                    raise ValueError('scanner unavailable')
                scanner = temporary / 'trivy'
                scanner.write_bytes(binary.read())
                scanner.chmod(0o700)
            stage = 'vulnerability_scan'
            scan_path = output / 'trivy-image.json'
            scan = subprocess.run([str(scanner), 'image', '--image-src', 'remote', '--scanners', 'vuln',
                                   '--timeout', '6m', '--cache-dir', str(temporary / 'cache'),
                                   '--ignorefile', '/dev/null', '--format', 'json', '--output', str(scan_path), IMAGE],
                                  stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=420)
            if scan.returncode:
                raise ValueError('scanner incomplete')
            inventory = json.loads(scan_path.read_text())
            findings = [v for result in inventory.get('Results', []) for v in result.get('Vulnerabilities', [])]
            report['scanner'] = {'version': '0.76.0', 'binary_sha256': TRIVY_SHA,
                                 'severity_counts': {s: sum(v.get('Severity') == s for v in findings)
                                                     for s in ['UNKNOWN', 'LOW', 'MEDIUM', 'HIGH', 'CRITICAL']},
                                 'blocking_findings': [{k: v.get(k) for k in ['VulnerabilityID', 'PkgName', 'InstalledVersion', 'FixedVersion', 'Severity']}
                                                       for v in findings if v.get('Severity') in ('HIGH', 'CRITICAL')]}
            report['status'] = 'BLOCKED_VULNERABILITIES' if report['scanner']['blocking_findings'] else 'SCANNED_NO_HIGH_CRITICAL'
            if not report['publisher_signature_verified']:
                report['status'] = 'BLOCKED_PUBLISHER_SIGNATURE'
        stage = 'complete'
    except Exception:
        report['failed_stage'] = stage
    (output / 'supply-report.json').write_text(json.dumps(report, indent=2) + '\n')
    # Public metadata only. Never emit token, registry diagnostics or container env.
    print(json.dumps(report, separators=(',', ':')))
    return 0 if report['status'] == 'SCANNED_NO_HIGH_CRITICAL' else 1


if __name__ == '__main__':
    raise SystemExit(main())
