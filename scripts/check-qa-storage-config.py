#!/usr/bin/env python3
"""Offline configuration guard. Does not certify supplier, IAM or deployment."""
import argparse
import json
import re
import subprocess
import sys
from urllib.parse import urlsplit

PIN = re.compile(r'^[a-zA-Z0-9][a-zA-Z0-9._:/-]*@sha256:[a-f0-9]{64}$')
ROOT_KEYS = {'MINIO_ROOT_USER', 'MINIO_ROOT_PASSWORD', 'MINIO_ACCESS_KEY', 'MINIO_SECRET_KEY'}
STORAGE_NAMES = {'minio', 'minio-init', 'silo', 'storage', 'storage-init'}
PREFIX = 'DocumentStorage__'


def evaluate(config):
    errors = set()
    services = config.get('services', {})
    if not isinstance(services, dict) or not isinstance(services.get('api'), dict):
        return ['INVALID_COMPOSE_MODEL']
    env = services['api'].get('environment', {})
    if not isinstance(env, dict):
        return ['INVALID_API_ENVIRONMENT']
    access, secret = (env.get(PREFIX + k) for k in ('AccessKey', 'SecretKey'))
    if not all(isinstance(v, str) and v and 'REEMPLAZAR' not in v for v in (access, secret)):
        errors.add('DOCUMENT_CREDENTIALS_MISSING')
    if ROOT_KEYS.intersection(env):
        errors.add('ADMIN_CREDENTIAL_IN_API')
    if env.get(PREFIX + 'Bucket') != 'pmgm-documents':
        errors.add('DOCUMENT_BUCKET_REQUIRED')
    if not env.get(PREFIX + 'Region'):
        errors.add('REGION_REQUIRED')
    if str(env.get(PREFIX + 'ForcePathStyle')).lower() not in ('true', 'false'):
        errors.add('PATH_STYLE_REQUIRED')
    try:
        url = urlsplit(env.get(PREFIX + 'ServiceUrl', ''))
        if (url.scheme not in ('http', 'https') or not url.hostname or url.username or url.password
                or url.query or url.fragment or url.path not in ('', '/') or not url.port and url.netloc.endswith(':')):
            raise ValueError()
        if url.scheme != 'https' and url.hostname not in services:
            errors.add('EXTERNAL_S3_REQUIRES_TLS')
    except (ValueError, TypeError):
        errors.add('INVALID_S3_ENDPOINT')
        url = None
    for name, service in services.items():
        if not isinstance(service, dict):
            errors.add('INVALID_COMPOSE_MODEL')
            continue
        server_env = service.get('environment', {})
        if not isinstance(server_env, dict):
            errors.add('INVALID_COMPOSE_MODEL')
            continue
        admin = {v for k, v in server_env.items() if k in ROOT_KEYS and isinstance(v, str) and v}
        if name != 'api' and any(v in admin for v in (access, secret) if isinstance(v, str) and v):
            errors.add('ADMIN_CREDENTIAL_REUSED_BY_API')
        if (name in STORAGE_NAMES or ROOT_KEYS.intersection(server_env)
                or url and url.hostname == name):
            image = service.get('image', '')
            if not isinstance(image, str) or not PIN.fullmatch(image) or ':latest' in image:
                errors.add('STORAGE_IMAGE_DIGEST_REQUIRED')
            # Upstream distribution is blocked regardless of a historical digest.
            if isinstance(image, str) and any(image.startswith(p) for p in (
                'minio/minio', 'quay.io/minio/minio', 'docker.io/minio/minio')):
                errors.add('HISTORICAL_MINIO_SERVER_BLOCKED')
    return sorted(errors)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--stdin', action='store_true', help='Private rendered Compose JSON via stdin')
    parser.add_argument('--compose-file')
    parser.add_argument('--env-file')
    args = parser.parse_args()
    try:
        if args.stdin and not args.compose_file and not args.env_file:
            raw = sys.stdin.buffer.read(8 * 1024 * 1024 + 1)
        elif not args.stdin and args.compose_file and args.env_file:
            result = subprocess.run(['docker', 'compose', '-p', 'pmgm-srv01', '--env-file', args.env_file,
                                     '-f', args.compose_file, 'config', '--format', 'json'],
                                    stdin=subprocess.DEVNULL, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL,
                                    timeout=30, check=False)
            if result.returncode:
                raise ValueError()
            raw = result.stdout
        else:
            raise ValueError()
        if len(raw) > 8 * 1024 * 1024:
            raise ValueError()
        errors = evaluate(json.loads(raw))
    except Exception:
        errors = ['INVALID_OR_UNAVAILABLE_COMPOSE_CONFIG']
    print(json.dumps({'status': 'BLOCKED' if errors else 'CONFIG_CHECK_ONLY',
                      'scope': 'NO_IAM_OR_DEPLOYMENT_CERTIFICATION', 'errors': errors}))
    return 1 if errors else 0


if __name__ == '__main__':
    sys.exit(main())
