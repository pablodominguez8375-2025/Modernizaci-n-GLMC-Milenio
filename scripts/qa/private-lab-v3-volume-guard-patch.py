#!/usr/bin/env python3
"""One-time, fail-closed patch for the QA synthetic laboratory recovery V2.

This does NOT run Docker, modify the signed candidate, read secrets or start QA.
Only creates /opt/centenario/qa-tools/repair-v3.py from the verified V2 file.
"""
import hashlib
import os
from pathlib import Path
import stat
import sys

SOURCE = Path('/opt/centenario/qa-tools/repair-v2.py')
DEST = Path('/opt/centenario/qa-tools/repair-v3.py')
V2_SHA256 = 'e8aafd3791decac164da88d17284b764870774d747cdb269b09b76e823dba58d'
V3_SHA256 = '835d5f671eef2acbe43b055b6ce37917d93a12754ba0da9874cbfbba08a82876'
OLD = """    volumes = r.stdout.decode().splitlines()
    guard(bool(volumes) and all(v.startswith(OLD_PROJECT + '_') for v in volumes),
          'UNRELATED_OR_MISSING_VOLUMES')
    guard(not any(v.startswith(NEW_PROJECT + '_') for v in volumes), 'NEW_LAB_VOLUMES_ALREADY_EXIST')
"""
NEW = """    volumes = r.stdout.decode().splitlines()
    # These named volumes are explicitly owned by the failed V1 lab. Docker may
    # additionally create 64-hex anonymous volumes without Compose labels.
    expected_named = {OLD_PROJECT + '_' + suffix for suffix in (
        'pmgm_qa_caddy_config', 'pmgm_qa_caddy_data',
        'pmgm_srv01_clamav_db', 'pmgm_srv01_minio_data',
        'pmgm_srv01_postgres_data')}
    anonymous = {v for v in volumes if re.fullmatch(r'[0-9a-f]{64}', v)}
    guard(len(volumes) == 7 and len(anonymous) == 2 and
          set(volumes) == expected_named | anonymous,
          'UNRELATED_OR_MISSING_VOLUMES')
    details = docker('volume', 'inspect', *volumes)
    guard(details.returncode == 0, 'VOLUME_INSPECTION_FAILED')
    inspected = json.loads(details.stdout)
    guard(len(inspected) == len(volumes), 'VOLUME_INSPECTION_INCOMPLETE')
    for item in inspected:
        name = item.get('Name')
        labels = item.get('Labels') or {}
        guard(name in expected_named or name in anonymous,
              'UNEXPECTED_VOLUME_IDENTITY')
        if name in expected_named:
            guard(labels.get('com.docker.compose.project') == OLD_PROJECT,
                  'LAB_VOLUME_LABEL_MISMATCH')
        else:
            guard('com.docker.compose.project' not in labels,
                  'ANONYMOUS_VOLUME_PROJECT_LABEL')
    guard(not any(v.startswith(NEW_PROJECT + '_') for v in volumes),
          'NEW_LAB_VOLUMES_ALREADY_EXIST')
"""


def digest(value):
    return hashlib.sha256(value).hexdigest()


def main():
    if os.geteuid() != 0:
        raise ValueError('ROOT_REQUIRED')
    if SOURCE.is_symlink() or DEST.exists() or DEST.is_symlink():
        raise ValueError('SCRIPT_PATH_UNSAFE')
    info = SOURCE.stat()
    if not stat.S_ISREG(info.st_mode) or info.st_uid != 0 or stat.S_IMODE(info.st_mode) != 0o500:
        raise ValueError('SOURCE_PERMISSIONS_MISMATCH')
    original = SOURCE.read_bytes()
    if digest(original) != V2_SHA256:
        raise ValueError('SOURCE_SHA256_MISMATCH')
    source = original.decode('utf-8')
    if source.count(OLD) != 1:
        raise ValueError('PATCH_CONTEXT_MISMATCH')
    revised = source.replace(OLD, NEW).encode('utf-8')
    compile(revised, str(DEST), 'exec')
    if digest(revised) != V3_SHA256:
        raise ValueError('V3_SHA256_MISMATCH')
    fd = os.open(DEST, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o500)
    try:
        with os.fdopen(fd, 'wb') as stream:
            os.fchmod(stream.fileno(), 0o500)
            stream.write(revised)
            stream.flush()
            os.fsync(stream.fileno())
        if digest(DEST.read_bytes()) != V3_SHA256:
            raise ValueError('WRITE_SHA256_MISMATCH')
    except Exception:
        DEST.unlink(missing_ok=True)
        raise
    print('V3_PATCH: PASS')
    print('V1_VOLUMES: PRESERVED')
    print('SERVICES_STARTED: NO')


if __name__ == '__main__':
    try:
        main()
    except Exception as error:
        print('V3_PATCH: BLOCKED_' + (error.args[0] if isinstance(error, ValueError) and error.args else type(error).__name__))
        sys.exit(1)
