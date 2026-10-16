#!/usr/bin/env python3
"""Verify a same-source signed QA archive before loading it; never start services."""
import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import tarfile

ROOT = Path(__file__).resolve().parents[2]
REPO = 'pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio'
WORKFLOW = REPO + '/.github/workflows/qa-silo-rebuild.yml'
SERVICES = ('minio', 'minio-init', 'storage-backup-client')
SUBJECTS = ('rebuild-report.json', 'server-cve-triage.json',
            'server-fixed.openvex.json', 'silo-qa-review-only.tar')


def run(command, **kwargs):
    return subprocess.run(command, check=True, stdin=kwargs.pop('stdin', subprocess.DEVNULL),
                          stdout=kwargs.pop('stdout', subprocess.PIPE), stderr=subprocess.DEVNULL,
                          timeout=kwargs.pop('timeout', 180), **kwargs)


def module():
    spec = importlib.util.spec_from_file_location('silo_rebuild', ROOT / 'scripts/qa/rebuild-silo-candidate.py')
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result


def verify_bundle(directory, source_sha):
    if re.fullmatch('[0-9a-f]{40}', source_sha or '') is None:
        raise ValueError('full approved source SHA required')
    directory = Path(directory).resolve(strict=True)
    for name in SUBJECTS + ('project-provenance.sigstore.json', 'trivy-raw.json', 'trivy-reviewed.json'):
        path = directory / name
        if path.is_symlink() or not path.is_file():
            raise ValueError('missing or linked evidence')
    report = json.loads((directory / 'rebuild-report.json').read_text())
    workflow_sha = report.get('workflow_sha', '')
    if re.fullmatch('[0-9a-f]{40}', workflow_sha) is None or report.get('source_sha') != source_sha:
        raise ValueError('source/workflow binding required')
    # PR certificates bind the workflow merge SHA, while the signed report binds
    # the exact checkout HEAD. These are distinct and must never be conflated.
    # A saved verification.json is never trusted as a substitute for signature verification.
    for subject in SUBJECTS:
        run(['gh', 'attestation', 'verify', str(directory / subject), '--repo', REPO,
             '--bundle', str(directory / 'project-provenance.sigstore.json'),
             '--signer-workflow', WORKFLOW, '--source-digest', workflow_sha,
             '--deny-self-hosted-runners', '--format', 'json'])
    image = report.get('local_image_id', '')
    if (report.get('source_sha') != source_sha or
            report.get('status') != 'CANDIDATE_VALIDATED_NOT_DEPLOYED' or report.get('failures') != {} or
            report.get('scope') != 'SYNTHETIC_QA_RUNNER_ONLY' or
            report.get('effective_blocking_findings') != [] or
            report.get('real_iam_recovery_sdk', {}).get('status') != 'PASS' or
            report.get('vendor_signature_verified') is not True or
            re.fullmatch(r'sha256:[0-9a-f]{64}', image) is None):
        raise ValueError('candidate gates or source mismatch')
    rebuild = module()
    registry = report.get('vendor_registry', {})
    license_info = report.get('vendor_license', {})
    license_path = directory / 'Silo-LICENSE.txt'
    if (registry.get('image') != rebuild.VENDOR or registry.get('os') != 'linux' or
            registry.get('architecture') != 'amd64' or license_info.get('source_commit') != rebuild.SERVER_SOURCE or
            license_info.get('spdx') != 'AGPL-3.0-or-later' or license_path.is_symlink() or
            hashlib.sha256(license_path.read_bytes()).hexdigest() != license_info.get('sha256')):
        raise ValueError('vendor platform or license evidence mismatch')
    for name in ('trivy-raw.json', 'trivy-reviewed.json', 'sbom.cdx.json', 'runtime-binaries-and-rpms.txt'):
        path = directory / name
        if (path.is_symlink() or not path.is_file() or
                hashlib.sha256(path.read_bytes()).hexdigest() != report.get('evidence_sha256', {}).get(name)):
            raise ValueError('unsigned or changed scan/inventory evidence')
    evidence = json.loads((directory / 'server-cve-triage.json').read_text())
    if report.get('server_cve_triage') != evidence:
        raise ValueError('triage mismatch')
    expected_vex = rebuild.fixed_component_vex(evidence, image)
    actual_vex = json.loads((directory / 'server-fixed.openvex.json').read_text())
    # Timestamps and UUID differ between creations; compare the exact bound statement.
    if actual_vex.get('statements') != expected_vex['statements']:
        raise ValueError('fixed component binding mismatch')
    raw = rebuild.blocking_findings(json.loads((directory / 'trivy-raw.json').read_text()))
    reviewed = rebuild.blocking_findings(json.loads((directory / 'trivy-reviewed.json').read_text()))
    if raw != report.get('blocking_findings') or reviewed:
        raise ValueError('scan evidence mismatch')
    for finding in raw:
        if (finding.get('VulnerabilityID') != 'CVE-2026-39414' or
                finding.get('PkgName') != 'github.com/minio/minio' or
                finding.get('InstalledVersion') != 'v0.0.0-20261015052015-7a3ca0f82100' or
                finding.get('Target') != 'usr/bin/silo'):
            raise ValueError('unexpected fixed finding')
    archive = directory / 'silo-qa-review-only.tar'
    with archive.open('rb') as stream:
        if hashlib.file_digest(stream, 'sha256').hexdigest() != report.get('archive_sha256'):
            raise ValueError('archive checksum mismatch')
    # docker-save archive: exactly one config, with content ID bound to the signed report.
    # Inspect only; never extract tar paths onto the host.
    with tarfile.open(archive) as tar:
        manifest = json.load(tar.extractfile('manifest.json'))
        if len(manifest) != 1:
            raise ValueError('single image required')
        config = tar.extractfile(manifest[0]['Config']).read()
        if 'sha256:' + hashlib.sha256(config).hexdigest() != image:
            raise ValueError('archive config identity mismatch')
        metadata = json.loads(config)
        if metadata.get('architecture') != 'amd64' or metadata.get('os') != 'linux':
            raise ValueError('linux amd64 image required')
    return report


def prepare(directory, source_sha, output):
    report = verify_bundle(directory, source_sha)
    image = report['local_image_id']
    # No mutable tag or registry digest is inferred from a local config ID.
    present = subprocess.run(['docker', 'image', 'inspect', image], stdout=subprocess.DEVNULL,
                             stderr=subprocess.DEVNULL, timeout=30).returncode == 0
    if not present:
        run(['docker', 'load', '--input', str(Path(directory) / 'silo-qa-review-only.tar')])
    actual = run(['docker', 'image', 'inspect', '--format', '{{.Id}}', image], text=True).stdout.strip()
    if actual != image:
        raise ValueError('loaded image identity mismatch')
    binary = run(['docker', 'run', '--rm', '--network', 'none', '--entrypoint', 'sha256sum',
                  image, '/usr/bin/silo'], text=True).stdout.split()[0]
    if binary != report['server_cve_triage']['server_binary_sha256']:
        raise ValueError('loaded server binary mismatch')
    # Render without interpolation: no credentials are written to this temporary model.
    rendered = run(['docker', 'compose', '-f', str(ROOT / 'infrastructure/docker-compose.srv01.yml'),
                    '--profile', '*', 'config', '--no-interpolate', '--format', 'json'], text=True)
    model = json.loads(rendered.stdout)
    for name in SERVICES:
        service = model['services'][name]
        service['image'] = image
        service['pull_policy'] = 'never'
    # No generated name: caller supplies its isolated or operational project explicitly.
    model.pop('name', None)
    # `compose config` materializes default volume/network names from its own
    # project. Drop those defaults so a UUID laboratory cannot share resources.
    for section in ('volumes', 'networks'):
        for resource in model.get(section, {}).values():
            if not resource.get('external'):
                resource.pop('name', None)
    with Path(output).open('x') as stream:
        os.chmod(output, 0o600)
        json.dump(model, stream, indent=2)
        stream.write('\n')
    return image


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--bundle', type=Path, required=True)
    parser.add_argument('--source-sha', required=True)
    parser.add_argument('--compose-output', type=Path)
    args = parser.parse_args()
    try:
        if args.compose_output:
            image = prepare(args.bundle, args.source_sha, args.compose_output)
        else:
            image = verify_bundle(args.bundle, args.source_sha)['local_image_id']
        print(json.dumps({'status': 'VERIFIED_SYNTHETIC_QA_ARCHIVE', 'image': image,
                          'scope': 'NO_DEPLOYMENT_OR_UAT_CERTIFICATION'}))
        return 0
    except Exception as error:
        print(json.dumps({'status': 'BLOCKED_QA_ARCHIVE', 'error_type': type(error).__name__}))
        return 1


if __name__ == '__main__':
    sys.exit(main())
