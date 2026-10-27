#!/usr/bin/env python3
"""PMGM #754 — activación reversible exclusiva API QA glmdch.

Por defecto es SOLO LECTURA (--check). --apply es una intervención explícita,
local y supervisada. No imprime Compose, entornos, secretos ni JWT. No modifica
DB, realm, mail vault, web, Keycloak, Postgres ni main. NO acredita restauración
independiente G6 ni permite crear usuarios por sí mismo.

Requiere instalar el script como root:root 0700 en /opt/centenario/qa-tools/.
"""
from __future__ import annotations
import argparse
import fcntl
import hashlib
import hmac
import json
import os
from pathlib import Path
import re
import stat
import subprocess
import sys
import time
from urllib.request import Request, urlopen

PROJECT = 'centenario-qa-lab2'
STAGE = Path('/etc/pmgm/qa754-identity-overlay.stage.json')
SECRET = Path('/etc/pmgm/qa-account-admin-secret')
SCRIPT = Path('/opt/centenario/qa-tools/pmgm-qa754-api-identity-activate.py')
G6 = Path('/home/soporte/centenario-qa-g6-CIFRADO-20261027T115953Z.tar.gz')
G6_SHA256 = 'e4184cb798e3b44414c09d85735db6e6b2002e75027de67da8ed5b930a7ca8a6'
G6_BYTES = 1027549
# Huella de respaldo cifrado comprobada fuera del host, sin publicar IDs privados de Drive.
AUTHORITY = 'http://keycloak:8080/identity/realms/pmgm'
ISSUER = 'https://sistema.granlogiamixta.cl/identity/realms/pmgm'
SERVER = 'http://keycloak:8080/identity'
CLIENT = 'pmgm-account-admin'
LOGIN = 'https://sistema.granlogiamixta.cl/'
HEALTH = 'https://sistema.granlogiamixta.cl/health/ready'
IDENTITY = {
    'UserAccounts__Identity__ServerUrl': SERVER,
    'UserAccounts__Identity__Issuer': ISSUER,
    'UserAccounts__Identity__ClientId': CLIENT,
    'UserAccounts__Mail__LoginUrl': LOGIN,
}
SECRET_KEY = 'UserAccounts__Identity__ClientSecret'
CHANGING = set(IDENTITY) | {SECRET_KEY}
ROOT_DIR = Path('/opt/centenario/qa-tools')
LOCK = Path('/run/lock/pmgm-qa754-api-activation.lock')

class Blocked(Exception):
    pass


def require(test, code):
    if not test:
        raise Blocked(code)


def cmd(args, timeout=40, allow_error=False):
    try:
        p = subprocess.run(args, stdin=subprocess.DEVNULL, stdout=subprocess.PIPE,
                           stderr=subprocess.DEVNULL, timeout=timeout, check=False)
    except (OSError, subprocess.TimeoutExpired) as e:
        raise Blocked('COMMAND_UNAVAILABLE_OR_TIMEOUT') from e
    if p.returncode and not allow_error:
        raise Blocked('COMMAND_FAILED')
    if len(p.stdout) > 4_000_000:
        raise Blocked('COMMAND_RESPONSE_TOO_LARGE')
    return p.stdout.decode('utf-8', errors='strict').strip(), p.returncode


def docker(*args, timeout=40):
    return cmd(('docker', *args), timeout=timeout)[0]


def parse_json(s):
    try:
        return json.loads(s)
    except (ValueError, TypeError) as e:
        raise Blocked('INVALID_JSON_METADATA') from e


def inspect(*args):
    a = parse_json(docker('inspect', *args))
    require(isinstance(a,list) and len(a)==1 and isinstance(a[0],dict), 'INVALID_INSPECT')
    return a[0]


def image_inspect(tag):
    a = parse_json(docker('image', 'inspect', tag))
    require(isinstance(a,list) and len(a)==1 and isinstance(a[0],dict), 'INVALID_IMAGE_INSPECT')
    return a[0]


def service(name):
    ids = docker('ps','--filter',f'label=com.docker.compose.project={PROJECT}',
                 '--filter',f'label=com.docker.compose.service={name}',
                 '--format','{{.ID}}').splitlines()
    require(len(ids)==1 and re.fullmatch(r'[a-f0-9]{12,64}',ids[0]), 'SERVICE_NOT_UNIQUE_'+name.upper())
    obj = inspect(ids[0])
    labels = (obj.get('Config') or {}).get('Labels') or {}
    require(labels.get('com.docker.compose.project')==PROJECT and
            labels.get('com.docker.compose.service')==name,'SERVICE_WRONG_LABEL')
    require((obj.get('State') or {}).get('Running') is True, 'SERVICE_NOT_RUNNING_'+name.upper())
    require(((obj.get('State') or {}).get('Health') or {}).get('Status') != 'unhealthy',
            'SERVICE_UNHEALTHY_'+name.upper())
    return obj


def private_bytes(path, limit=65536):
    try:
        fd=os.open(path,os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC)
    except OSError as e:
        raise Blocked('PRIVATE_INPUT_UNAVAILABLE') from e
    try:
        st=os.fstat(fd)
        require(stat.S_ISREG(st.st_mode) and st.st_uid==0 and stat.S_IMODE(st.st_mode)==0o600,
                'PRIVATE_INPUT_PERMISSIONS')
        require(0<st.st_size<=limit, 'PRIVATE_INPUT_SIZE')
        data=os.read(fd,limit+1)
        require(len(data)<=limit and len(data)==st.st_size, 'PRIVATE_INPUT_READ')
        return data
    finally:
        os.close(fd)


def stage_env(raw,secret):
    doc=parse_json(raw.decode('utf-8'))
    require(isinstance(doc,dict) and set(doc)=={'services'} and
            isinstance(doc['services'],dict) and set(doc['services'])=={'api'},'STAGE_SCOPE')
    api=doc['services']['api']
    require(isinstance(api,dict) and set(api)=={'environment'} and
            isinstance(api['environment'],dict),'STAGE_SCOPE')
    env=api['environment']
    require(set(env)==CHANGING,'STAGE_ENV_SCOPE')
    require(all(env.get(k)==v for k,v in IDENTITY.items()),'STAGE_URL_MISMATCH')
    require(isinstance(env.get(SECRET_KEY),str) and
            hmac.compare_digest(env[SECRET_KEY],secret),'STAGE_SERVICE_SECRET_MISMATCH')
    return env


def env_list(items):
    require(isinstance(items,list),'ENV_LIST_BAD')
    d={}
    for item in items:
        require(isinstance(item,str) and '=' in item, 'ENV_ITEM_BAD')
        k,v=item.split('=',1)
        require(bool(k) and k not in d,'ENV_KEY_DUPLICATE')
        d[k]=v
    return d


def compose(paths):
    args=['docker','compose','--project-name',PROJECT]
    for p in paths:
        args.extend(('-f',str(p)))
    args.extend(('config','--format','json'))
    d=parse_json(cmd(args)[0])
    require(isinstance(d,dict) and isinstance(d.get('services'),dict) and 'api' in d['services'],
            'COMPOSE_BAD')
    return d


def paths_from_api(api):
    labels=(api.get('Config') or {}).get('Labels') or {}
    paths=[Path(s) for s in labels.get('com.docker.compose.project.config_files','').split(',') if s]
    require(len(paths)==2,'COMPOSE_LAYER_COUNT')
    require(paths[0].name=='compose.recovery.json' and
            re.fullmatch(r'api-[a-f0-9]{40}\.json',paths[1].name),'COMPOSE_LAYER_UNEXPECTED')
    for p in paths:
        require(p.is_absolute() and p.is_file() and not p.is_symlink(), 'COMPOSE_FILE_UNAVAILABLE')
    return paths


def baseline_match(cfg,api,image):
    configured=cfg['services']['api']
    tag=configured.get('image')
    require(isinstance(tag,str) and tag==(api.get('Config') or {}).get('Image'),'API_IMAGE_TAG_DRIFT')
    require(image.get('Id') and image['Id']==api.get('Image'),'API_IMAGE_ID_DRIFT')
    actual=env_list((api.get('Config') or {}).get('Env'))
    defaults=env_list((image.get('Config') or {}).get('Env') or [])
    expected=configured.get('environment')
    require(isinstance(expected,dict) and all(isinstance(k,str) and isinstance(v,str) for k,v in expected.items()),'API_COMPOSE_ENV_BAD')
    require(all(actual.get(k)==v for k,v in expected.items()),'API_ENV_DRIFT')
    require(all(defaults.get(k)==v for k,v in actual.items() if k not in expected),'API_RUNTIME_EXTRA_ENV')
    require(all(actual.get(k)==v for k,v in defaults.items() if k not in expected),'API_IMAGE_DEFAULT_ENV')
    for k in CHANGING - {'UserAccounts__Identity__ClientId'}:
        require(expected.get(k)=='' and actual.get(k)=='','BASELINE_IDENTITY_NOT_EMPTY')
    require(expected.get('UserAccounts__Identity__ClientId')==CLIENT,'CLIENT_ID_DRIFT')
    require(expected.get('Authentication__Authority')==AUTHORITY,'AUTHORITY_DRIFT')
    require(bool(expected.get('SystemMail__EncryptionKey')),'SMTP_KEY_MISSING')
    require(str(expected.get('Database__ApplyMigrationsOnStartup','')).lower()=='false',
            'API_MIGRATIONS_MUST_BE_DISABLED')
    return actual


def diff_only_identity(old,new,stage):
    require(set(old)==set(new) and
            {k:v for k,v in old.items() if k!='services'} == {k:v for k,v in new.items() if k!='services'},
            'TOPLEVEL_CHANGED')
    a,b=old['services'],new['services']
    require(set(a)==set(b),'SERVICE_SET_CHANGED')
    require(all(a[k]==b[k] for k in a if k!='api'), 'OTHER_SERVICE_CHANGED')
    old_api,new_api=a['api'],b['api']
    require({k:v for k,v in old_api.items() if k!='environment'}==
            {k:v for k,v in new_api.items() if k!='environment'},'API_NON_ENV_CHANGED')
    e1,e2=old_api.get('environment'),new_api.get('environment')
    require(isinstance(e1,dict) and isinstance(e2,dict) and set(e1)==set(e2),'API_ENV_KEYS_CHANGED')
    require(all((e2[k]==stage[k]) if k in CHANGING else (e2[k]==e1[k]) for k in e1),
            'API_UNRELATED_ENV_CHANGED')
    require(all(e1.get(k)=='' for k in CHANGING - {'UserAccounts__Identity__ClientId'}),
            'API_ALREADY_CONFIGURED')
    return e1,e2


def sha256_file(path):
    require(path.is_file() and not path.is_symlink(),'BACKUP_NOT_FOUND')
    require(path.stat().st_size==G6_BYTES,'BACKUP_SIZE_CHANGED')
    h=hashlib.sha256()
    with path.open('rb') as f:
        for chunk in iter(lambda:f.read(262144),b''):
            h.update(chunk)
    require(h.hexdigest()==G6_SHA256,'BACKUP_SHA_MISMATCH')


def timer_paused():
    state,rc=cmd(('systemctl','is-active','pmgm-qa-lite.timer'),timeout=8,allow_error=True)
    require(state=='inactive' and rc!=0,'WEB_TIMER_NOT_INACTIVE')


def public_health():
    req=Request(HEALTH,headers={'User-Agent':'PMGM-QA754-Health/1.0'})
    try:
        with urlopen(req,timeout=9) as res:
            return res.status==200
    except (OSError,TimeoutError):
        return False


def check(other_service_ids=None):
    require(os.geteuid()==0,'ROOT_REQUIRED')
    api=service('api')
    other_names=('keycloak','postgres','web','minio')
    other={name:service(name).get('Id') for name in other_names}
    paths=paths_from_api(api)
    secret=private_bytes(SECRET,8192).decode('utf-8').strip('\r\n')
    require(len(secret)>=8 and not any(x in secret for x in '\x00\r\n'),'SECRET_BAD_FORMAT')
    stage=stage_env(private_bytes(STAGE),secret)
    old,new=compose(paths),compose(paths+[STAGE])
    previous,candidate=diff_only_identity(old,new,stage)
    image=image_inspect(old['services']['api']['image'])
    actual=baseline_match(old,api,image)
    require(all(actual.get(k)==previous.get(k) for k in previous),'ENV_BASELINE_CHANGED')
    timer_paused()
    sha256_file(G6)
    require(public_health(),'PUBLIC_HEALTH_NOT_READY')
    if other_service_ids is not None:
        require(other==other_service_ids,'DEPENDENT_SERVICE_CHANGED')
    print('QA754_BUNDLE_STAGE: VERIFIED_ROOT_0600')
    print('QA754_COMPOSE_DELTA: API_IDENTITY_ONLY')
    print('QA754_API_ORIGINAL_IMAGE_AND_ENV: VERIFIED')
    print('QA754_API_DEPENDENCIES: RUNNING')
    print('QA754_WEB_TIMER: INACTIVE')
    print('QA754_G6_LOCAL_SHA256: MATCHES_OFFHOST_VERIFIED_SHA')
    print('QA754_G6_ISOLATED_DB_SILO_RESTORE: NOT_CERTIFIED')
    print('QA754_PUBLIC_HEALTH_BEFORE: HTTP_200')
    return {'paths':paths,'old_env':previous,'new_env':candidate,'old_image':api.get('Image'),
            'old_api_id':api.get('Id'),'others':other}


def change_compose(paths,enable):
    args=['docker','compose','--project-name',PROJECT]
    for p in (paths+[STAGE] if enable else paths):
        args.extend(('-f',str(p)))
    args.extend(('up','--detach','--no-deps','--no-build','--force-recreate','api'))
    cmd(args,timeout=180)


def post_verify(snapshot,activated):
    api=service('api')
    live_others={name:service(name).get('Id') for name in snapshot['others']}
    require(api.get('Id')!=snapshot['old_api_id'],'API_NOT_RECREATED')
    require(api.get('Image')==snapshot['old_image'],'API_IMAGE_CHANGED')
    require(live_others==snapshot['others'],'DEPENDENT_SERVICE_RECREATED')
    labels=(api.get('Config') or {}).get('Labels') or {}
    require(len(labels.get('com.docker.compose.project.config_files','').split(','))==
            (3 if activated else 2),'DEPLOY_LAYER_COUNT_WRONG')
    env=env_list((api.get('Config') or {}).get('Env'))
    expected=snapshot['new_env'] if activated else snapshot['old_env']
    require(all(env.get(k)==v for k,v in expected.items()),'API_POST_ENV_MISMATCH')
    require(all(env.get(k)==snapshot['old_env'].get(k) for k in snapshot['old_env'] if k not in CHANGING),
            'UNRELATED_ENV_CHANGED')
    require(all(env.get(k) == expected[k] for k in CHANGING),'IDENTITY_POST_ENV_MISMATCH')
    timer_paused()
    for _ in range(15):
        if public_health():
            return True
        time.sleep(4)
    raise Blocked('PUBLIC_HEALTH_NOT_RECOVERED')


def self_protected():
    p=SCRIPT
    try:
        st=p.stat()
    except OSError as e:
        raise Blocked('SCRIPT_INSTALL_REQUIRED') from e
    require(p.is_file() and not p.is_symlink() and st.st_uid==0 and
            stat.S_IMODE(st.st_mode)==0o700,'SCRIPT_NOT_ROOT_0700')
    require(Path(__file__).resolve()==p,'SCRIPT_WRONG_LOCATION')


def apply(snapshot):
    changed=False
    try:
        print('QA754_APPLY: START_API_ONLY')
        changed=True
        change_compose(snapshot['paths'], True)
        post_verify(snapshot,True)
        print('QA754_APPLY: API_ONLY_HEALTH_PASS')
        print('QA754_UAT_AUTHENTICATED_ACCOUNT_CREATION: PENDING')
        print('QA754_SMTP_KEYCLOAK_RESET: PENDING')
        print('RESULT: APPLIED_PRELIMINARY_QA_ONLY_UAT_PENDING')
        return 0
    except (Blocked, KeyboardInterrupt, Exception):
        print('QA754_APPLY: CHECK_FAILED_ATTEMPT_ROLLBACK')
        if changed:
            try:
                change_compose(snapshot['paths'],False)
                post_verify(snapshot,False)
                print('QA754_ROLLBACK: BASELINE_API_HEALTH_PASS')
                print('RESULT: RECOVERED_BASELINE_API_AFTER_ACTIVATION_FAILURE')
                return 3
            except Exception:
                print('QA754_ROLLBACK: FAILED_MANUAL_INTERVENTION_REQUIRED')
                print('RESULT: CRITICAL_ROLLBACK_FAILED_NO_SECRETS_SHOWN')
                return 4
        return 3


def main(argv=None):
    parser=argparse.ArgumentParser(description='PMGM #754 — operador supervisado SOLO API sintética')
    parser.add_argument('--check',action='store_true',help='Solo lectura, opción predeterminada')
    parser.add_argument('--apply',action='store_true',help='Recrea exclusivamente contenedor API, con rollback')
    parser.add_argument('--confirm',default='',help='Confirmación explícita de QA, no contraseña')
    args=parser.parse_args(argv)
    if args.check and args.apply:
        raise Blocked('CONFLICTING_OPTIONS')
    if args.apply and args.confirm!='QA754-ACTIVAR-SOLO-API':
        raise Blocked('CONFIRMATION_REQUIRED')
    self_protected()
    lock_fd=os.open(LOCK,os.O_CREAT | os.O_RDWR | os.O_NOFOLLOW | os.O_CLOEXEC,0o600)
    st=os.fstat(lock_fd)
    require(stat.S_ISREG(st.st_mode) and st.st_uid==0 and
            stat.S_IMODE(st.st_mode)==0o600, 'LOCK_FILE_UNSAFE')
    try:
        fcntl.flock(lock_fd,fcntl.LOCK_EX | fcntl.LOCK_NB)
    except BlockingIOError as e:
        os.close(lock_fd)
        raise Blocked('ANOTHER_ACTIVATION_IN_PROGRESS') from e
    try:
        snapshot=check()
        if not args.apply:
            print('RESULT: READ_ONLY_READY_FOR_OPERATOR_DECISION_NO_CHANGES')
            return 0
        return apply(snapshot)
    finally:
        os.close(lock_fd)

if __name__=='__main__':
    print('PMGM_QA754_API_ACTIVATION_GUARDED_V1')
    try:
        sys.exit(main())
    except Blocked as e:
        print('RESULT: BLOCKED_'+str(e))
        sys.exit(2)
    except Exception:
        print('RESULT: BLOCKED_UNEXPECTED_ERROR_NO_DETAILS')
        sys.exit(2)
