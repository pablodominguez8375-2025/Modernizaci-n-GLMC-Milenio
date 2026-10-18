#!/usr/bin/env python3
"""Centenario synthetic private QA lab2: encrypted LOCAL backup, deliberately NOT G6 certification.

Stages PostgreSQL (pmgm, pmgm_keycloak, global roles) and current Silo S3 objects,
then encrypts their contents in a *new* Restic repository and performs a local
restoration/hash round-trip. Only creates a 0600 encrypted export tar for SFTP.
Requires separate off-host upload, offline key custody, and real DB/S3 restore.

Never touches main, srv01, production, credentials, or existing lab volumes.
Requires root; prints only sanitized fixed status codes.
"""
from __future__ import annotations
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
from datetime import datetime, timezone

SHA = '4ef456fee3a578a9e6418f3f7072af5dd79eb045'
PROJECT = 'centenario-qa-lab2'
BASE = Path('/opt/centenario/qa-lab-firstboot/recovery-v2')
MODEL = BASE / 'compose.recovery.json'
REPORT = BASE / 'status.json'
ROOT = Path('/opt/centenario/qa-g6')
KEY = Path('/etc/pmgm/qa-g6-restic-password')
PG = PROJECT + '-postgres-1'
SILO = PROJECT + '-minio-1'
USER_HOME = Path('/home/soporte')
EXPECTED = {'postgres','minio','clamav','web','api','keycloak','gateway'}
JOBS = {'migrator','db-grants','minio-init'}

class Blocked(Exception):
    pass


def require(condition: bool, code: str):
    if not condition:
        raise Blocked(code)


def tell(code: str):
    print(code, flush=True)


def run(args: list[str], *, timeout=60, cwd=None, env=None, capture=False):
    # Never print subprocess stderr/stdout; Docker Compose model and SQL can hold secrets.
    r = subprocess.run(args, cwd=cwd, env=env, stdin=subprocess.DEVNULL,
                       stdout=subprocess.PIPE if capture else subprocess.DEVNULL,
                       stderr=subprocess.PIPE, timeout=timeout, check=False)
    if r.returncode:
        raise Blocked('COMMAND_FAILED_' + re.sub('[^A-Z0-9_]', '_', args[0].upper()))
    return r.stdout if capture else b''


def checksum(path: Path) -> str:
    with path.open('rb') as f:
        return hashlib.file_digest(f,'sha256').hexdigest()


def protected_file(path: Path, mode: int):
    require(path.is_file() and not path.is_symlink(), 'EXPECTED_FILE_MISSING')
    st = path.stat()
    require(st.st_uid == 0 and stat.S_IMODE(st.st_mode) == mode, 'EXPECTED_FILE_UNSAFE')


def secure_dir(path: Path):
    if not path.exists():
        path.mkdir(mode=0o700)
    st=path.stat()
    require(path.is_dir() and not path.is_symlink() and st.st_uid==0 and
            stat.S_IMODE(st.st_mode)==0o700,'PRIVATE_DIRECTORY_UNSAFE')


def inspect():
    require(os.geteuid()==0, 'ROOT_REQUIRED')
    os.umask(0o077)
    protected_file(MODEL,0o600)
    protected_file(REPORT,0o600)
    state=json.loads(REPORT.read_text())
    require(state.get('status')=='PRIVATE_LAB_SMOKE_PASS' and
            state.get('scope')=='PRIVATE_SYNTHETIC_LAB_NOT_G3_G6_UAT' and
            state.get('project')==PROJECT and state.get('source_sha')==SHA,
            'QA_LAB_SMOKE_NOT_VERIFIED')
    model=json.loads(MODEL.read_text())
    require(model.get('name') in (None,''),'MODEL_NAME_MISMATCH')
    services=model.get('services',{})
    require(set(services)==EXPECTED|JOBS|{'storage-backup-client'}, 'MODEL_SERVICES_MISMATCH')
    for key,spec in services.items():
        require(not spec.get('privileged',False) and spec.get('network_mode')!='host',
                'MODEL_PRIVILEGED_SERVICE')
        require(not spec.get('ports') or key=='gateway','PUBLIC_SERVICE_BIND')
    ports=services['gateway'].get('ports',[])
    require(len(ports)==2 and all(x.get('host_ip')=='127.0.0.1' for x in ports),
            'GATEWAY_NOT_PRIVATE')
    require(all(services[n].get('pull_policy')=='never' and
                re.fullmatch(r'sha256:[0-9a-f]{64}', services[n].get('image',''))
                for n in ('minio','minio-init','storage-backup-client')),
            'SILO_IMAGE_NOT_PINNED')
    require(all(not x.get('external',False) for x in model.get('volumes',{}).values()),
            'EXTERNAL_VOLUME_NOT_ALLOWED')
    out=run(['docker','ps','-a','--filter','label=com.docker.compose.project='+PROJECT,
             '--format','{{.Label "com.docker.compose.service"}}|{{.State}}|{{.Status}}'],capture=True)
    entries=[x.decode('utf-8','replace').split('|') for x in out.splitlines()]
    states={entry[0]:entry[1] for entry in entries if len(entry)==3}
    require(EXPECTED.issubset(states) and all(states[name]=='running' for name in EXPECTED),
            'QA_SERVICES_NOT_RUNNING')
    require(JOBS.issubset(states) and all(states[name]=='exited' for name in JOBS),
            'QA_JOBS_NOT_PRESENT')
    for name in (PG, SILO):
        health = run(['docker', 'inspect', '-f', '{{.State.Health.Status}}', name],
                     capture=True).decode().strip()
        require(health == 'healthy', 'QA_BACKUP_SOURCE_NOT_HEALTHY')
    require(shutil.disk_usage('/opt/centenario').free>=4*1024**3,
            'INSUFFICIENT_BACKUP_SPACE')
    for cmd in ('restic','docker','tar'):
        require(shutil.which(cmd) is not None,'MISSING_TOOL_'+cmd.upper())
    require(USER_HOME.is_dir(), 'OPERATOR_HOME_MISSING')
    tell('QA_LAB2_G6_PREFLIGHT: PASS')


def create_key():
    require(KEY.parent.is_dir() and not KEY.parent.is_symlink(), 'KEY_DIRECTORY_MISSING')
    if KEY.exists():
        protected_file(KEY,0o600)
        require(len(KEY.read_bytes().strip())>=36, 'RESTIC_KEY_TOO_SHORT')
    else:
        fd=os.open(KEY,os.O_CREAT|os.O_EXCL|os.O_NOFOLLOW|os.O_WRONLY,0o600)
        with os.fdopen(fd,'wb') as f:
            f.write(secrets.token_urlsafe(48).encode('ascii')+b'\n')
    tell('RESTIC_KEY_LOCAL_ROOT_ONLY: PASS')


def pg_stream(command: list[str], path: Path):
    fd=os.open(path,os.O_WRONLY|os.O_CREAT|os.O_EXCL|os.O_NOFOLLOW,0o600)
    with os.fdopen(fd,'wb') as out:
        proc=subprocess.Popen(['docker','exec',PG,*command],stdin=subprocess.DEVNULL,
                              stdout=out,stderr=subprocess.PIPE)
        try:
            _,stderr=proc.communicate(timeout=300)
        except subprocess.TimeoutExpired:
            proc.kill()
            proc.communicate()
            raise Blocked('POSTGRES_EXPORT_TIMEOUT')
        require(proc.returncode==0, 'POSTGRES_EXPORT_FAILED')
    require(path.stat().st_size>0,'POSTGRES_EXPORT_EMPTY')


def stage_postgres(work: Path):
    db=work/'postgres'
    db.mkdir(mode=0o700)
    # Bootstrap role is used locally via Docker socket; no DB password passed in argv.
    pg_stream(['pg_dumpall','--globals-only','-U','pmgm_app'],db/'globals.sql')
    for name in ('pmgm','pmgm_keycloak'):
        path=db/(name+'.dump')
        pg_stream(['pg_dump','-U','pmgm_app','-d',name,'--format=custom','--create'],path)
        with path.open('rb') as stream:
            proc=subprocess.run(['docker','exec','-i',PG,'pg_restore','--list'],
                                stdin=stream,stdout=subprocess.DEVNULL,
                                stderr=subprocess.PIPE,timeout=120)
        require(proc.returncode==0,'POSTGRES_DUMP_VALIDATION_FAILED')
    tell('POSTGRES_2BD_AND_GLOBALS_BACKUP: PASS')


def stage_objects(work:Path):
    objects=work/'silo-current-objects'
    objects.mkdir(mode=0o700)
    # Uses ONLY dedicated S3 backup identity from already protected LAB2 Compose,
    # never privileged MinIO admin credentials and never exposes env values.
    shell='''set -eu
umask 077
mcli alias set -- qa http://minio:9000 "$BACKUP_ACCESS_KEY" "$BACKUP_SECRET_KEY" >/dev/null 2>&1
mcli ls qa/pmgm-documents >/dev/null 2>&1
mcli mirror qa/pmgm-documents /export/silo-current-objects >/dev/null 2>&1
'''
    command=['docker','compose','-p',PROJECT,'-f',str(MODEL),'run',
             '--rm','--no-deps','-T','-u','0:0','-v',str(work)+':/export:rw',
             'storage-backup-client','-ec',shell]
    try:
        run(command,timeout=900)
    except Blocked:
        raise Blocked('SILO_BACKUP_MIRROR_FAILED')
    for f in objects.rglob('*'):
        require(not f.is_symlink(),'SILO_OBJECT_SYMLINK_NOT_ALLOWED')
    count=sum(1 for f in objects.rglob('*') if f.is_file())
    tell('SILO_CURRENT_OBJECTS_BACKUP: PASS')
    return count


def create_manifest(work:Path, count:int):
    files={}
    for p in work.rglob('*'):
        if p.is_file():
            files[p.relative_to(work).as_posix()]={'sha256':checksum(p),'bytes':p.stat().st_size}
    require(len(files)>=3, 'BACKUP_MANIFEST_TOO_SMALL')
    document={'scope':'SYNTHETIC_PRIVATE_LAB2_LOCAL_RESTIC_ONLY','source_sha':SHA,
              'databases':['pmgm','pmgm_keycloak'],'objects_count':count,
              'not_offhost_yet':True,'files':files}
    p=work/'manifest.json'
    p.write_text(json.dumps(document,sort_keys=True,indent=2)+'\n')
    os.chmod(p,0o600)
    return document


def restic_env():
    env=os.environ.copy()
    env.update({'RESTIC_PASSWORD_FILE':str(KEY),'RESTIC_CACHE_DIR':str(ROOT/'cache'),
                'RESTIC_REPOSITORY':str(ROOT/'repository')})
    return env


def restic_roundtrip(work:Path,manifest):
    env=restic_env()
    repo=Path(env['RESTIC_REPOSITORY'])
    if repo.exists():
        require(repo.is_dir() and not repo.is_symlink() and repo.stat().st_uid == 0,
                'RESTIC_REPOSITORY_UNSAFE')
        run(['restic','check'],env=env,timeout=120)
    else:
        run(['restic','init'],env=env,timeout=120)
    run(['restic','backup','--tag','centenario-qa-lab2-g6-local',str(work)],env=env,timeout=900)
    run(['restic','check','--read-data'],env=env,timeout=900)
    restored=Path(tempfile.mkdtemp(prefix='restore-',dir=ROOT))
    try:
        run(['restic','restore','latest','--target',str(restored)],env=env,timeout=900)
        matches=list(restored.rglob('manifest.json'))
        require(len(matches)==1,'RESTIC_RESTORE_MANIFEST_MISSING')
        target=matches[0].parent
        require(json.loads(matches[0].read_text())==manifest,'RESTIC_RESTORE_MANIFEST_MISMATCH')
        for name,record in manifest['files'].items():
            candidate=target/name
            require(candidate.is_file() and not candidate.is_symlink(),'RESTIC_RESTORE_FILE_MISSING')
            require(checksum(candidate)==record['sha256'],'RESTIC_RESTORE_FILE_HASH_MISMATCH')
    finally:
        shutil.rmtree(restored,ignore_errors=True)
    tell('RESTIC_ENCRYPTED_LOCAL_CHECK_AND_RESTORE: PASS')


def make_archive() -> Path:
    # The archive contains only the encrypted Restic repository, NEVER its key.
    stamp=datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%SZ')
    archive=USER_HOME/('centenario-qa-g6-CIFRADO-'+stamp+'.tar.gz')
    require(not archive.exists(),'BACKUP_ARCHIVE_ALREADY_EXISTS')
    with tarfile.open(archive,'w:gz') as tar:
        tar.add(ROOT/'repository',arcname='restic-repository',recursive=True)
    os.chmod(archive,0o600)
    import pwd
    owner=pwd.getpwnam('soporte')
    os.chown(archive,owner.pw_uid,owner.pw_gid)
    require(archive.stat().st_size>0,'ENCRYPTED_ARCHIVE_EMPTY')
    (ROOT/'archive.receipt.json').write_text(json.dumps({
        'status':'ENCRYPTED_LOCAL_ARCHIVE_READY_OFFHOST_PENDING',
        'sha256':checksum(archive),'bytes':archive.stat().st_size,
        'archive_name':archive.name,'source_sha':SHA,
        'key_offhost_custody':'PENDING',
        'restore_independent_host':'PENDING'},indent=2)+'\n')
    os.chmod(ROOT/'archive.receipt.json',0o600)
    tell('ENCRYPTED_ARCHIVE_LOCAL_READY: PASS')
    tell('SFTP_FILENAME: '+archive.name)
    tell('ENCRYPTED_ARCHIVE_SHA256: '+checksum(archive))
    tell('OFFHOST_UPLOAD_AND_KEY_CUSTODY: PENDING')
    tell('INDEPENDENT_POSTGRES_AND_SILO_RESTORE: PENDING')
    return archive


def main():
    inspect()
    secure_dir(ROOT)
    create_key()
    for d in ('cache',):
        secure_dir(ROOT/d)
    import fcntl
    lockfile = ROOT / '.backup.lock'
    fd = os.open(lockfile, os.O_RDWR | os.O_CREAT | os.O_NOFOLLOW, 0o600)
    try:
        fcntl.flock(fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
    except BlockingIOError:
        os.close(fd)
        raise Blocked('BACKUP_ALREADY_RUNNING')
    # The process owns fd for its whole lifetime; kernel releases flock on exit.
    path=Path(tempfile.mkdtemp(prefix='scratch-',dir=ROOT))
    try:
        stage_postgres(path)
        count=stage_objects(path)
        manifest=create_manifest(path,count)
        restic_roundtrip(path,manifest)
        make_archive()
        tell('=== G6 LOCAL BACKUP PREPARED - NOT G6 CERTIFIED ===')
    finally:
        shutil.rmtree(path,ignore_errors=True)


if __name__=='__main__':
    try:
        main()
    except Blocked as error:
        print('BLOQUEADO: '+str(error),flush=True)
        sys.exit(1)
    except Exception as error:
        print('BLOQUEADO: '+type(error).__name__,flush=True)
        sys.exit(1)