#!/usr/bin/env python3
"""G6 QA synthetic recovery rehearsal in disposable isolated Docker resources.

Verify the *same SHA256* encrypted archive independently checked from Google Drive;
restore Restic with the host's protected key, replay PostgreSQL globals and two
custom DB dumps in an offline disposable PostgreSQL server; recreate Silo bucket
in an internal Docker network and compare every restored object's SHA256.

On success OR failure clean up only the temporary labeled containers and network;
never touch QA LAB2, its volumes, credentials, production, main, or srv01.
This proves a SAME-HOST ISOLATED rehearsal, not cross-host disaster recovery,
atomic multi-store consistency, IAM reconstruction, or retention.
"""
from __future__ import annotations

import fcntl
import hashlib
import json
import os
from pathlib import Path
import re
import secrets
import shutil
import stat
import subprocess
import sys
import tarfile
import tempfile
import time

QA = 'centenario-qa-lab2'
SHA = '4ef456fee3a578a9e6418f3f7072af5dd79eb045'
WORK = Path('/opt/centenario/qa-g6')
MODEL = Path('/opt/centenario/qa-lab-firstboot/recovery-v2/compose.recovery.json')
QA_STATUS = Path('/opt/centenario/qa-lab-firstboot/recovery-v2/status.json')
KEY = Path('/etc/pmgm/qa-g6-restic-password')
ARCHIVE = Path('/home/soporte/centenario-qa-g6-CIFRADO-20261018T142458Z.tar.gz')
ARCHIVE_SHA = 'a6f6e2cc2347953f74f8916b755d0152ec9c63787c534be0d661bd231f8de1d8'
REPORT = WORK / 'isolated-restore.report.json'
LOCK = WORK / '.restore-isolated.lock'
TAG = 'centenario.qa.g6.restore'
PG_NAME = 'centenario-g6-restore-postgres'
SILO_NAME = 'centenario-g6-restore-silo'
NET_NAME = 'centenario-g6-restore-internal'
EXPECTED_ROLE_NAMES = ('pmgm_app', 'pmgm_keycloak', 'pmgm_migrator', 'pmgm_runtime', 'pmgm_reports')

class Blocked(Exception):
    pass


def require(condition, code):
    if not condition:
        raise Blocked(code)


def emit(message):
    print(message, flush=True)


def sha256(path):
    with path.open('rb') as handle:
        return hashlib.file_digest(handle, 'sha256').hexdigest()


def safe_file(path, uid=0, mode=0o600):
    require(path.is_file() and not path.is_symlink(), 'SOURCE_FILE_MISSING')
    st = path.stat()
    require(st.st_uid == uid and stat.S_IMODE(st.st_mode) == mode, 'SOURCE_FILE_UNSAFE')


def run(argv, *, timeout=180, capture=False, stdin_file=None, env=None):
    """Never print a subprocess's stdout/stderr, which may contain secrets."""
    infile = stdin_file.open('rb') if stdin_file is not None else subprocess.DEVNULL
    try:
        process = subprocess.run(
            argv, stdin=infile, stdout=subprocess.PIPE if capture else subprocess.DEVNULL,
            stderr=subprocess.PIPE, timeout=timeout, check=False, env=env,
        )
    except subprocess.TimeoutExpired:
        raise Blocked('COMMAND_TIMEOUT_' + re.sub('[^A-Z0-9]', '_', argv[0].upper()))
    finally:
        if stdin_file is not None:
            infile.close()
    require(process.returncode == 0, 'COMMAND_FAILED_' + re.sub('[^A-Z0-9]', '_', argv[0].upper()))
    return process.stdout if capture else b''


def docker(*args, **kwargs):
    return run(['docker', *args], **kwargs)


def preflight():
    require(os.geteuid() == 0, 'ROOT_REQUIRED')
    os.umask(0o077)
    for cmd in ('docker', 'restic'):
        require(shutil.which(cmd) is not None, 'REQUIRED_TOOL_MISSING')
    safe_file(MODEL)
    safe_file(QA_STATUS)
    safe_file(KEY)
    require(len(KEY.read_bytes().strip()) >= 36, 'KEY_MISSING_OR_SHORT')
    require(ARCHIVE.is_file() and not ARCHIVE.is_symlink(), 'ARCHIVE_MISSING')
    require(sha256(ARCHIVE) == ARCHIVE_SHA, 'ARCHIVE_SHA256_MISMATCH')
    require(not REPORT.exists() and not REPORT.is_symlink(),
            'RESTORE_RECEIPT_ALREADY_EXISTS')
    state = json.loads(QA_STATUS.read_text())
    require(state.get('status') == 'PRIVATE_LAB_SMOKE_PASS' and
            state.get('project') == QA and state.get('source_sha') == SHA,
            'QA_STATUS_NOT_MATCHING_SIGNED_SOURCE')
    model = json.loads(MODEL.read_text())
    services = model.get('services', {})
    require(services.get('postgres', {}).get('image') == 'postgres:17',
            'POSTGRES_IMAGE_UNEXPECTED')
    silo = services.get('minio', {}).get('image', '')
    require(re.fullmatch(r'sha256:[0-9a-f]{64}', silo) is not None and
            services.get('minio', {}).get('pull_policy') == 'never',
            'SILO_IMAGE_UNPINNED')
    require(model.get('networks') and
            all(not x.get('external', False) for x in model.get('networks', {}).values()),
            'MODEL_EXTERNAL_NETWORK')
    # Read-only Docker checks: no pre-existing resources with reserved names.
    out = docker('ps', '-a', '--format', '{{.Names}}', capture=True).decode().splitlines()
    require(PG_NAME not in out and SILO_NAME not in out, 'RESTORE_CONTAINER_NAME_IN_USE')
    nets = docker('network', 'ls', '--format', '{{.Name}}', capture=True).decode().splitlines()
    require(NET_NAME not in nets, 'RESTORE_NETWORK_NAME_IN_USE')
    statuses = docker('ps', '--filter', 'label=com.docker.compose.project='+QA,
                      '--format', '{{.Label "com.docker.compose.service"}}|{{.Status}}',
                      capture=True).decode().splitlines()
    running = {item.split('|', 1)[0] for item in statuses if '|' in item}
    require({'postgres','minio','keycloak','api','web','gateway','clamav'}.issubset(running),
            'QA_SOURCE_NOT_RUNNING')
    for name in ('postgres', 'minio'):
        state = docker('inspect', '-f', '{{.State.Health.Status}}', QA+'-'+name+'-1',
                       capture=True).decode().strip()
        require(state == 'healthy', 'QA_SOURCE_NOT_HEALTHY')
    require(shutil.disk_usage(WORK).free >= 2*1024**3, 'INSUFFICIENT_DISK')
    run(['docker', 'image', 'inspect', 'postgres:17'], timeout=20)
    run(['docker', 'image', 'inspect', silo], timeout=20)
    emit('G6_RESTORE_PREFLIGHT_AND_DRIVE_ARCHIVE_HASH: PASS')
    return silo


def safe_extract_encrypted_archive(path, target):
    """Manually extract regular files and dirs only, rejecting tar traversal and links."""
    target.mkdir(mode=0o700, exist_ok=True)
    require(target.is_dir() and not target.is_symlink() and
            stat.S_IMODE(target.stat().st_mode) == 0o700,
            'ARCHIVE_EXTRACT_DIRECTORY_UNSAFE')
    count = 0
    total = 0
    with tarfile.open(path, 'r:gz') as archive:
        for member in archive:
            count += 1
            require(count < 5000, 'ARCHIVE_ENTRY_LIMIT')
            parts = Path(member.name).parts
            require(parts and parts[0] == 'restic-repository' and
                    all(part not in ('', '.', '..') for part in parts),
                    'ARCHIVE_ENTRY_PATH_INVALID')
            require(member.isdir() or member.isfile(), 'ARCHIVE_UNSAFE_ENTRY_TYPE')
            relative = Path(*parts)
            dest = target / relative
            require(dest.resolve().is_relative_to(target.resolve()), 'ARCHIVE_PATH_ESCAPE')
            if member.isdir():
                dest.mkdir(parents=True, exist_ok=True, mode=0o700)
            else:
                total += member.size
                require(total <= 2*1024**3, 'ARCHIVE_UNCOMPRESSED_SIZE_LIMIT')
                dest.parent.mkdir(parents=True, exist_ok=True, mode=0o700)
                require(not dest.exists(), 'ARCHIVE_DUPLICATE_ENTRY')
                with archive.extractfile(member) as source, dest.open('xb') as sink:
                    require(source is not None, 'ARCHIVE_FILE_UNREADABLE')
                    shutil.copyfileobj(source, sink)
                dest.chmod(0o600)
    require((target/'restic-repository'/'config').is_file(), 'RESTIC_REPO_CONFIG_MISSING')


def unpack_restic(stage):
    safe_extract_encrypted_archive(ARCHIVE, stage)
    recovered = stage/'recovered'
    repo = stage/'restic-repository'
    recovered.mkdir(mode=0o700)
    env = os.environ.copy()
    env.update({'RESTIC_REPOSITORY':str(repo),
                'RESTIC_PASSWORD_FILE':str(KEY),
                'RESTIC_CACHE_DIR':str(stage/'cache')})
    run(['restic', 'check', '--read-data'], timeout=900, env=env)
    run(['restic', 'restore', 'latest', '--target', str(recovered)], timeout=900, env=env)
    manifests = list(recovered.rglob('manifest.json'))
    require(len(manifests) == 1, 'RESTORE_MANIFEST_NOT_UNIQUE')
    src = manifests[0].parent
    manifest = json.loads(manifests[0].read_text())
    require(manifest.get('scope') == 'SYNTHETIC_PRIVATE_LAB2_LOCAL_RESTIC_ONLY' and
            manifest.get('source_sha') == SHA and
            manifest.get('databases') == ['pmgm', 'pmgm_keycloak'],
            'BACKUP_MANIFEST_SCOPE_MISMATCH')
    file_entries = manifest.get('files', {})
    require(isinstance(file_entries, dict) and len(file_entries) >= 3,
            'BACKUP_MANIFEST_EMPTY')
    actual = {}
    for item in src.rglob('*'):
        require(not item.is_symlink(), 'RESTIC_RECOVERY_SYMLINK')
        if item.is_file() and item.name != 'manifest.json':
            actual[item.relative_to(src).as_posix()] = {'bytes': item.stat().st_size,
                                                       'sha256': sha256(item)}
    require(actual == file_entries, 'RESTORED_FILES_HASH_MISMATCH')
    for name in ('globals.sql', 'pmgm.dump', 'pmgm_keycloak.dump'):
        require((src/'postgres'/name).is_file(), 'POSTGRES_BACKUP_FILE_MISSING')
    objects = src/'silo-current-objects'
    require(objects.is_dir(), 'SILO_CURRENT_OBJECTS_MISSING')
    entries = [p for p in objects.rglob('*') if p.is_file()]
    require(len(entries) == manifest.get('objects_count'), 'SILO_BACKUP_COUNT_MISMATCH')
    emit('G6_DRIVE_IDENTICAL_RESTIC_RESTORE_AND_MANIFEST_HASH: PASS')
    return src, manifest


def pg_user_ids():
    uid = int(docker('run', '--rm', '--pull', 'never', '--network', 'none',
                     '--entrypoint', 'id', 'postgres:17', '-u', 'postgres',
                     timeout=30, capture=True).decode().strip())
    gid = int(docker('run', '--rm', '--pull', 'never', '--network', 'none',
                     '--entrypoint', 'id', 'postgres:17', '-g', 'postgres',
                     timeout=30, capture=True).decode().strip())
    require(100 <= uid <= 65534 and 100 <= gid <= 65534, 'POSTGRES_USER_ID_UNEXPECTED')
    return uid, gid


def await_postgres():
    for _ in range(90):
        proc = subprocess.run(['docker', 'exec', PG_NAME, 'pg_isready', '-U',
                               'g6_restore_admin', '-d', 'postgres'],
                              stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                              timeout=8, check=False)
        if proc.returncode == 0:
            return
        time.sleep(1)
    raise Blocked('POSTGRES_RESTORE_SERVER_NOT_READY')


def restore_postgres(src):
    uid, gid = pg_user_ids()
    docker('run', '-d', '--rm', '--pull', 'never', '--name', PG_NAME,
           '--label', TAG+'=true', '--network', 'none', '--read-only',
           '--user', f'{uid}:{gid}', '--cap-drop', 'ALL',
           '--security-opt', 'no-new-privileges', '--pids-limit', '256',
           '--memory', '1200m', '--cpus', '1',
           '--tmpfs', f'/var/lib/postgresql/data:rw,nosuid,nodev,size=700m,uid={uid},gid={gid},mode=0700',
           '--tmpfs', f'/var/run/postgresql:rw,nosuid,nodev,size=16m,uid={uid},gid={gid},mode=0770',
           '--tmpfs', '/tmp:rw,nosuid,nodev,size=64m,mode=1777',
           '-e', 'POSTGRES_HOST_AUTH_METHOD=trust',
           '-e', 'POSTGRES_USER=g6_restore_admin', '-e', 'POSTGRES_DB=postgres',
           '-e', 'PGDATA=/var/lib/postgresql/data/pgdata',
           'postgres:17', timeout=50)
    await_postgres()
    run(['docker', 'exec', '-i', PG_NAME, 'psql', '-X', '-v', 'ON_ERROR_STOP=1',
         '-U', 'g6_restore_admin', '-d', 'postgres'],
        stdin_file=src/'postgres'/'globals.sql', timeout=180)
    for db in ('pmgm','pmgm_keycloak'):
        run(['docker', 'exec', '-i', PG_NAME, 'pg_restore', '-e', '-C', '-U',
             'g6_restore_admin', '-d', 'postgres'],
            stdin_file=src/'postgres'/(db+'.dump'), timeout=600)
    db_count = docker('exec', PG_NAME, 'psql', '-X', '-Atq', '-U',
                      'g6_restore_admin', '-d', 'postgres', '-c',
                      "SELECT count(*) FROM pg_database WHERE datname IN ('pmgm','pmgm_keycloak')",
                      capture=True).decode().strip()
    require(db_count == '2', 'POSTGRES_DATABASE_COUNT_MISMATCH')
    roles = ','.join("'"+role+"'" for role in EXPECTED_ROLE_NAMES)
    role_count = docker('exec', PG_NAME, 'psql', '-X', '-Atq', '-U',
                       'g6_restore_admin', '-d', 'postgres', '-c',
                       f'SELECT count(*) FROM pg_roles WHERE rolname IN ({roles})',
                       capture=True).decode().strip()
    require(role_count == str(len(EXPECTED_ROLE_NAMES)), 'POSTGRES_ROLES_NOT_RESTORED')
    table_counts = {}
    for database in ('pmgm','pmgm_keycloak'):
        value = docker('exec', PG_NAME, 'psql', '-X', '-Atq', '-U',
                       'g6_restore_admin', '-d', database, '-c',
                       "SELECT count(*) FROM pg_class c JOIN pg_namespace n ON "
                       "c.relnamespace=n.oid WHERE c.relkind IN ('r','p') "
                       "AND n.nspname NOT IN ('pg_catalog','information_schema')",
                       capture=True).decode().strip()
        require(value.isdecimal() and int(value) > 0, 'POSTGRES_RESTORED_TABLES_MISSING')
        table_counts[database] = int(value)
    emit('G6_POSTGRES_2DB_GLOBAL_ROLES_AND_TABLES_RESTORED: PASS')
    return table_counts


def start_silo(silo_image, stage):
    docker('network', 'create', '--internal', '--label', TAG+'=true', NET_NAME,
           timeout=40)
    user = 'qa_restore_' + secrets.token_hex(5)
    password = secrets.token_urlsafe(35)
    creds = stage/'silo-restore-test.env'
    creds.write_text('G6_TEST_USER='+user+'\nG6_TEST_PASSWORD='+password+'\n'
                     'MINIO_ROOT_USER='+user+'\nMINIO_ROOT_PASSWORD='+password+'\n')
    creds.chmod(0o600)
    docker('run', '-d', '--rm', '--pull', 'never', '--name', SILO_NAME,
           '--label', TAG+'=true', '--network', NET_NAME,
           '--cap-drop', 'ALL', '--security-opt', 'no-new-privileges',
           '--pids-limit', '256', '--memory', '750m', '--cpus', '1',
           '--tmpfs', '/data:rw,nosuid,nodev,size=500m,mode=0777',
           '--tmpfs', '/tmp:rw,nosuid,nodev,size=64m,mode=1777',
           '--env-file', str(creds), silo_image, 'server', '/data',
           '--console-address', ':9001', timeout=50)
    for _ in range(90):
        proc = subprocess.run(['docker', 'exec', SILO_NAME, 'silo', 'healthcheck', 'ready'],
                              stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                              timeout=8, check=False)
        if proc.returncode == 0:
            return creds
        time.sleep(1)
    raise Blocked('SILO_RESTORE_SERVER_NOT_READY')


def restore_silo(src, stage, silo_image, creds, count):
    verify = stage/'silo-restored-verification'
    verify.mkdir(mode=0o700)
    shell = '''set -eu
export MC_CONFIG_DIR=/tmp/g6mc
mcli alias set -- qa http://'''+SILO_NAME+''':9000 "$G6_TEST_USER" "$G6_TEST_PASSWORD" >/dev/null 2>&1
mcli mb --ignore-existing qa/pmgm-documents >/dev/null 2>&1
mcli mirror /source qa/pmgm-documents >/dev/null 2>&1
mcli mirror qa/pmgm-documents /verified >/dev/null 2>&1
'''
    # Silo is internally networked only; mounts point exclusively into a root-0700 scratch directory.
    docker('run', '--rm', '--pull', 'never', '--network', NET_NAME,
           '--cap-drop', 'ALL', '--security-opt', 'no-new-privileges',
           '--pids-limit', '128', '--memory', '400m',
           '--tmpfs', '/tmp:rw,nosuid,nodev,size=32m,mode=1777',
           '--mount', 'type=bind,source='+str(src/'silo-current-objects')+',target=/source,readonly',
           '--mount', 'type=bind,source='+str(verify)+',target=/verified',
           '--env-file', str(creds), '--entrypoint', '/bin/sh',
           silo_image, '-ec', shell, timeout=900)
    source = {p.relative_to(src/'silo-current-objects').as_posix(): sha256(p)
              for p in (src/'silo-current-objects').rglob('*') if p.is_file()}
    actual = {p.relative_to(verify).as_posix(): sha256(p)
              for p in verify.rglob('*') if p.is_file()}
    require(source == actual and len(actual) == count, 'SILO_OBJECTS_SHA256_MISMATCH')
    emit('G6_SILO_CURRENT_OBJECTS_RESTORED_AND_HASHED: PASS')
    if count == 0:
        emit('G6_SILO_BACKUP_OBJECT_COUNT: ZERO - EMPTY_BUCKET_RESTORED')
    return count


def cleanup():
    """Stop ONLY own labeled disposable resources, irrespective of failures."""
    for name in (PG_NAME, SILO_NAME):
        try:
            response = subprocess.run(['docker', 'inspect', '-f', '{{index .Config.Labels "'+TAG+'"}}', name],
                                      stdout=subprocess.PIPE, stderr=subprocess.DEVNULL,
                                      timeout=10)
            if response.returncode == 0 and response.stdout.strip() == b'true':
                subprocess.run(['docker', 'rm', '-f', name], stdout=subprocess.DEVNULL,
                               stderr=subprocess.DEVNULL, timeout=35)
        except Exception:
            pass
    try:
        response = subprocess.run(['docker','network','inspect','-f',
                                   '{{index .Labels "'+TAG+'"}}', NET_NAME],
                                  stdout=subprocess.PIPE, stderr=subprocess.DEVNULL,
                                  timeout=10)
        if response.returncode == 0 and response.stdout.strip() == b'true':
            subprocess.run(['docker', 'network', 'rm', NET_NAME], stdout=subprocess.DEVNULL,
                           stderr=subprocess.DEVNULL, timeout=30)
    except Exception:
        pass


def write_report(table_counts, object_count):
    require(not REPORT.exists(), 'REPORT_EXISTS')
    data = {'result': 'G6_SAME_HOST_ISOLATED_RESTORE_PASS_NOT_DISASTER_RECOVERY',
            'archive_filename': ARCHIVE.name, 'archive_sha256': ARCHIVE_SHA,
            'postgres_tables_by_database': table_counts,
            'postgres_global_roles_verified': list(EXPECTED_ROLE_NAMES),
            'silo_objects_verified': object_count,
            'silo_empty_bucket_only': object_count == 0,
            'offhost_archive_byte_identity': 'PASS_BY_KNOWN_DRIVE_SHA256',
            'password_offhost_custody': 'USER_ATTESTED_NOT_CRYPTOGRAPHICALLY_TESTED',
            'disaster_restore_other_host': 'PENDING',
            'iam_restore': 'NOT_INCLUDED', 'atomic_cross_resource_snapshot': 'NOT_PROVEN',
            'backup_retention': 'PENDING', 'production_touched': False}
    REPORT.write_text(json.dumps(data, indent=2, sort_keys=True)+'\n')
    REPORT.chmod(0o600)


def main():
    silo_image = preflight()
    require(WORK.is_dir() and not WORK.is_symlink() and WORK.stat().st_uid == 0
            and stat.S_IMODE(WORK.stat().st_mode) == 0o700,
            'G6_WORK_DIRECTORY_UNSAFE')
    fd = os.open(LOCK, os.O_WRONLY|os.O_CREAT|os.O_NOFOLLOW, 0o600)
    fcntl.flock(fd, fcntl.LOCK_EX|fcntl.LOCK_NB)
    stage = Path(tempfile.mkdtemp(prefix='restore-isolated-', dir=WORK))
    try:
        src, manifest = unpack_restic(stage)
        table_counts = restore_postgres(src)
        creds = start_silo(silo_image, stage)
        objects = restore_silo(src, stage, silo_image, creds, manifest['objects_count'])
        cleanup()
        # Fail closed if any ephemeral resource remains.
        active = docker('ps', '-a', '--format', '{{.Names}}', capture=True).decode().splitlines()
        networks = docker('network', 'ls', '--format', '{{.Name}}', capture=True).decode().splitlines()
        require(PG_NAME not in active and SILO_NAME not in active and NET_NAME not in networks,
                'EPHEMERAL_RESOURCE_CLEANUP_INCOMPLETE')
        write_report(table_counts, objects)
        emit('G6_EPHEMERAL_RESOURCE_CLEANUP: PASS')
        emit('=== G6 SAME-HOST ISOLATED RESTORE PASS - OTHER-HOST DR STILL PENDING ===')
    finally:
        cleanup()
        shutil.rmtree(stage, ignore_errors=True)
        os.close(fd)


if __name__ == '__main__':
    try:
        main()
    except Blocked as exc:
        print('BLOQUEADO: '+str(exc), flush=True)
        sys.exit(1)
    except Exception as exc:
        print('BLOQUEADO: '+type(exc).__name__, flush=True)
        sys.exit(1)
