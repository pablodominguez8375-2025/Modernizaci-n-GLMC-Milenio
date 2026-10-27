#!/usr/bin/env python3
"""PMGM #754: guarded WEB-only QA hotfix from the exact installed 6ff4 source.

--check is read-only. --prepare fetches pinned GitHub source and builds a local OCI
image, never restarts a service. --apply updates only WEB and attempts rollback on
failure. --rollback requires explicit confirmation. Run only on synthetic glmdch.
Never print compose configs, container env, tokens, private paths or credentials.

This is a reproducible *pinned-source* QA procedure, NOT an OCI signature, signed
release, a DR restore, or permission to deploy main/production.
"""
import argparse
import fcntl
import hashlib
import hmac
import json
import os
from pathlib import Path
import re
import shutil
import socket
import stat
import subprocess
import sys
import tempfile
import time
from urllib.request import Request, urlopen

PROJECT = 'centenario-qa-lab2'
BASE_COMMIT = '6ff4ae0d092d4214df4ea31af484efbc969fde54'
HOTFIX_COMMIT = '424153c3a06c2b0bc0f8c2e8599eed4bb9ac6786'
BASE_BRANCH = 'qa/baseline-web-6ff4-20261027'
HOTFIX_BRANCH = 'qa/hotfix-754-web-json-from-6ff4-20261027'
REPOSITORY = 'https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio.git'
OLD_CALL = "return this.request('/api/system/user-accounts',{method:'POST',body:JSON.stringify(request)})"
NEW_CALL = "return this.postJson<UserAccountCreated>('/api/system/user-accounts',request)"
PATCH_PATH = 'frontend/src/api/pmgmApi.ts'
ALLOWED_DIFF = frozenset({PATCH_PATH, 'frontend/src/api/qa754-hotfix415.test.ts',
                          'docs/handoffs/2026-10-27-chatgpt-qa754-web-hotfix-6ff4.md'})
PROJECT_DIR = Path('/opt/centenario/qa-lab-firstboot/recovery-v2')
IDENTITY_STAGE = Path('/etc/pmgm/qa754-identity-overlay.stage.json')
SCRIPT = Path('/opt/centenario/qa-tools/pmgm-qa754-web-hotfix.py')
STATE_DIR = Path('/opt/centenario/qa-tools/qa754-web-hotfix')
PREPARED = STATE_DIR/'prepared.json'
JOURNAL = STATE_DIR/'deployment.json'
LOCK = Path('/run/lock/pmgm-qa754-web-hotfix.lock')
BACKUP = Path('/home/soporte/centenario-qa-g6-CIFRADO-20261027T115953Z.tar.gz')
BACKUP_SHA = 'e4184cb798e3b44414c09d85735db6e6b2002e75027de67da8ed5b930a7ca8a6'
BACKUP_BYTES = 1027549
WEB_ORIGINAL = 'https://sistema.granlogiamixta.cl'
REVISION = 'org.opencontainers.image.revision'
SHA_IMAGE = re.compile(r'sha256:[a-f0-9]{64}\Z')
WEB_OVERRIDE = re.compile(r'web-[a-f0-9]{64}\.json\Z')
API_OVERRIDE = re.compile(r'api-[a-f0-9]{40}\.json\Z')
ALLOWED_BUILD_ARGS = frozenset({'VITE_API_BASE_URL','VITE_USE_MOCKS','VITE_OIDC_AUTHORITY',
 'VITE_OIDC_CLIENT_ID','VITE_OIDC_SCOPE','VITE_OIDC_ALLOW_HTTP_LOCAL','VITE_OIDC_ALLOW_HTTP_QA'})
EXPECTED_IDENTITY = {'UserAccounts__Identity__ServerUrl':'http://keycloak:8080/identity',
 'UserAccounts__Identity__Issuer':'https://sistema.granlogiamixta.cl/identity/realms/pmgm',
 'UserAccounts__Identity__ClientId':'pmgm-account-admin',
 'UserAccounts__Mail__LoginUrl':'https://sistema.granlogiamixta.cl/'}

class Blocked(Exception): pass

def require(condition,code):
    if not condition: raise Blocked(code)

def sha(path):
    h=hashlib.sha256()
    with Path(path).open('rb') as stream:
        for chunk in iter(lambda:stream.read(1024*1024),b''):h.update(chunk)
    return h.hexdigest()

def exec_fixed(args,*,cwd=None,timeout=35,allow_error=False):
    try:
        r=subprocess.run(tuple(str(x) for x in args),cwd=cwd,stdin=subprocess.DEVNULL,
                         stdout=subprocess.PIPE,stderr=subprocess.DEVNULL,timeout=timeout,check=False,
                         env={'PATH':'/usr/bin:/bin','HOME':'/root','GIT_CONFIG_NOSYSTEM':'1',
                              'GIT_TERMINAL_PROMPT':'0','GIT_CONFIG_GLOBAL':'/dev/null'})
    except (OSError,subprocess.TimeoutExpired): raise Blocked('LOCAL_COMMAND_UNAVAILABLE_OR_TIMEOUT') from None
    require(len(r.stdout)<=1_000_000,'COMMAND_OUTPUT_TOO_LARGE')
    if r.returncode and not allow_error: raise Blocked('LOCAL_COMMAND_FAILED')
    try: return r.stdout.decode('utf8').strip(),r.returncode
    except UnicodeDecodeError: raise Blocked('INVALID_COMMAND_OUTPUT') from None

def docker(*args,timeout=40): return exec_fixed(('docker',*args),timeout=timeout)[0]

def root_regular(path,mode=None):
    try: st=path.lstat()
    except OSError: raise Blocked('TRUSTED_FILE_MISSING') from None
    require(stat.S_ISREG(st.st_mode) and st.st_uid==0 and not st.st_mode&0o022,
            'TRUSTED_FILE_NOT_ROOT_CONTROLLED')
    if mode is not None:require(stat.S_IMODE(st.st_mode)==mode,'TRUSTED_FILE_MODE_WRONG')
    return st

def parse_json(text):
    try:return json.loads(text)
    except (TypeError,ValueError):raise Blocked('INVALID_LOCAL_METADATA') from None

def inspect(service):
    ids=docker('ps','--filter',f'label=com.docker.compose.project={PROJECT}',
               '--filter',f'label=com.docker.compose.service={service}',
               '--format','{{.ID}}').splitlines()
    require(len(ids)==1 and re.fullmatch(r'[0-9a-f]{12,64}',ids[0]),'SERVICE_NOT_UNIQUE_'+service.upper())
    value=parse_json(docker('inspect',ids[0]))
    require(isinstance(value,list) and len(value)==1 and isinstance(value[0],dict),'INVALID_SERVICE_INSPECT')
    obj=value[0]
    labels=(obj.get('Config') or {}).get('Labels') or {}
    require(labels.get('com.docker.compose.project')==PROJECT and
            labels.get('com.docker.compose.service')==service,'SERVICE_SCOPE_CHANGED')
    require((obj.get('State') or {}).get('Running') is True,'SERVICE_NOT_RUNNING_'+service.upper())
    require(((obj.get('State') or {}).get('Health') or {}).get('Status')!='unhealthy','SERVICE_UNHEALTHY_'+service.upper())
    return obj

def compose_paths(obj):
    labels=(obj.get('Config') or {}).get('Labels') or {}
    p=[Path(s) for s in labels.get('com.docker.compose.project.config_files','').split(',') if s]
    require(bool(p) and all(v.is_absolute() and v.is_file() and not v.is_symlink() for v in p),
            'COMPOSE_PATHS_INVALID')
    return p

def env_dict(obj):
    env=(obj.get('Config') or {}).get('Env') or []
    require(isinstance(env,list),'API_ENV_INVALID')
    result={}
    for item in env:
        require(isinstance(item,str) and '=' in item,'API_ENV_INVALID')
        k,v=item.split('=',1)
        require(k not in result,'API_ENV_DUPLICATE')
        result[k]=v
    return result

def identity_active(api):
    e=env_dict(api)
    require(all(e.get(k)==v for k,v in EXPECTED_IDENTITY.items()) and
            isinstance(e.get('UserAccounts__Identity__ClientSecret'),str) and
            bool(e['UserAccounts__Identity__ClientSecret']), 'API_IDENTITY_NOT_ACTIVE')

def expected_web_override(path,image):
    require(WEB_OVERRIDE.fullmatch(path.name) and path.parent==PROJECT_DIR,'WEB_OVERRIDE_UNEXPECTED')
    root_regular(path,0o600)
    j=parse_json(path.read_text())
    require(j=={'services':{'web':{'image':image}}},'WEB_OVERRIDE_CONTENT_CHANGED')

def get_sanitized_snapshot(*,after=False):
    require(os.geteuid()==0 and socket.gethostname()=='glmdch','HOST_NOT_AUTHORIZED')
    root_regular(IDENTITY_STAGE,0o600)
    api=inspect('api'); web=inspect('web')
    dependents={s:inspect(s) for s in ('postgres','keycloak','minio')}
    api_paths=compose_paths(api); web_paths=compose_paths(web)
    require(len(api_paths)==3 and api_paths[0].name=='compose.recovery.json' and
            API_OVERRIDE.fullmatch(api_paths[1].name) and api_paths[2]==IDENTITY_STAGE,
            'API_IDENTITY_LAYER_CHANGED')
    require(len(web_paths)==2 and web_paths[0]==api_paths[0] and
            web_paths[0].parent==PROJECT_DIR,'WEB_BASE_LAYOUT_CHANGED')
    require(web_paths[1].parent==PROJECT_DIR and WEB_OVERRIDE.fullmatch(web_paths[1].name),
            'WEB_IMAGE_LAYER_CHANGED')
    for p in api_paths[:2]+web_paths:
        root_regular(p)
    require((api.get('Config') or {}).get('Labels',{}).get('com.docker.compose.project.working_dir')==str(PROJECT_DIR),
            'API_COMPOSE_DIRECTORY_CHANGED')
    require((web.get('Config') or {}).get('Labels',{}).get('com.docker.compose.project.working_dir')==str(PROJECT_DIR),
            'WEB_COMPOSE_DIRECTORY_CHANGED')
    identity_active(api)
    require(SHA_IMAGE.fullmatch(web.get('Image','')) and
            SHA_IMAGE.fullmatch(api.get('Image','')),'RUNTIME_IMAGE_NOT_PINNED')
    expected_web_override(web_paths[1],web['Image'])
    # Only metadata, no secret output. Keep protected API's opaque container ID stable.
    return {'api':api,'web':web,'dependents':dependents,'api_paths':api_paths,
            'web_paths':web_paths,'boundary':{k:v.get('Id') for k,v in {'api':api,**dependents}.items()}}

def backup_check():
    require(BACKUP.is_file() and not BACKUP.is_symlink() and
            BACKUP.stat().st_size==BACKUP_BYTES,'G6_LOCAL_NOT_MATCHING')
    require(hmac.compare_digest(sha(BACKUP),BACKUP_SHA),'G6_LOCAL_HASH_CHANGED')

def timer_check():
    status,rc=exec_fixed(('systemctl','is-active','pmgm-qa-lite.timer'),allow_error=True,timeout=8)
    require(status=='inactive' and rc!=0,'WEB_TIMER_MUST_STAY_INACTIVE')

def public_request(path,*,require_json=False):
    req=Request(WEB_ORIGINAL+path,headers={'User-Agent':'PMGM-QA754-Hotfix/1.0'})
    try:
        with urlopen(req,timeout=9) as resp:
            require(resp.geturl()==WEB_ORIGINAL+path and resp.status==200,
                    'PUBLIC_HEALTH_OR_REDIRECT_FAILED')
            content=resp.read(2048)
    except (OSError,TimeoutError):raise Blocked('PUBLIC_ENDPOINT_UNAVAILABLE') from None
    return parse_json(content.decode('utf8')) if require_json else content

def public_check(*,expected_sha):
    require(public_request('/health/web').strip()==b'ok','PUBLIC_WEB_UNHEALTHY')
    ready=public_request('/health/ready',require_json=True)
    require(ready=={'status':'ready','database':'postgresql'},'PUBLIC_API_UNHEALTHY')
    version=public_request('/qa-version.json',require_json=True)
    require(version=={'environment':'QA_SYNTHETIC','source_sha':expected_sha},
            'PUBLIC_WEB_VERSION_UNEXPECTED')

def check(*,new_version=False):
    snapshot=get_sanitized_snapshot(after=new_version)
    backup_check(); timer_check()
    public_check(expected_sha=HOTFIX_COMMIT if new_version else BASE_COMMIT)
    if not new_version:
        require(not JOURNAL.exists(),'PRIOR_HOTFIX_DEPLOYMENT_STATE_EXISTS')
    print('QA754_WEB_SOURCE_BASE: PINNED_6FF4')
    print('QA754_API_IDENTITY: ACTIVE_THREE_LAYERS')
    print('QA754_WEB_LAYOUT: TWO_LAYERS_BASE_AND_IMAGE')
    print('QA754_OTHER_SERVICES: RUNNING')
    print('QA754_G6_OFFHOST_REFERENCE: LOCAL_SHA_MATCH')
    print('QA754_WEB_TIMER: INACTIVE')
    print('QA754_PUBLIC_HEALTH: PASS')
    print('RESULT: '+('POST_APPLY_HEALTH_VERIFIED' if new_version else 'READ_ONLY_WEB_HOTFIX_PRECHECK_PASS_NO_CHANGES'))
    return snapshot

def write_root_json(path,value):
    require(not path.exists() and path.parent.is_dir(),'STATE_ALREADY_EXISTS_OR_DIRECTORY_MISSING')
    with tempfile.NamedTemporaryFile(mode='w',dir=path.parent,prefix='.qa754-',delete=False) as out:
        tmp=Path(out.name)
        try:
            os.fchmod(out.fileno(),0o600)
            json.dump(value,out,sort_keys=True,separators=(',',':'))
            out.write('\n');out.flush();os.fsync(out.fileno())
        except Exception:
            tmp.unlink(missing_ok=True);raise
    os.replace(tmp,path)

def read_root_json(path):
    root_regular(path,0o600)
    return parse_json(path.read_text())

def git(*args,work=None,timeout=120):
    return exec_fixed(('git','-c','core.hooksPath=/dev/null',*args),cwd=work,timeout=timeout)[0]

def verify_git_tree(source):
    require(git('rev-parse','HEAD',work=source)==HOTFIX_COMMIT,'HOTFIX_COMMIT_MISMATCH')
    require(git('cat-file','-t',BASE_COMMIT,work=source)=='commit','BASE_COMMIT_MISSING')
    require(git('merge-base',BASE_COMMIT,HOTFIX_COMMIT,work=source)==BASE_COMMIT,
            'HOTFIX_BASE_NOT_ANCESTOR')
    diff=set(git('diff','--name-only',BASE_COMMIT,HOTFIX_COMMIT,work=source).splitlines())
    require(diff==ALLOWED_DIFF,'HOTFIX_SOURCE_DIFF_NOT_MINIMAL')
    old=git('show',BASE_COMMIT+':'+PATCH_PATH,work=source,timeout=30)
    new=git('show',HOTFIX_COMMIT+':'+PATCH_PATH,work=source,timeout=30)
    require(old.count(OLD_CALL)==1 and new==old.replace(OLD_CALL,NEW_CALL),
            'HOTFIX_PATCH_CONTENT_MISMATCH')
    require(git('status','--porcelain',work=source)=='','HOTFIX_SOURCE_NOT_CLEAN')
    return True

def public_build_args(base):
    # Parse raw compose privately; only bounded PUBLIC build args may leave memory
    # in docker build invocation. Never read/emit secrets from other services.
    cfg=parse_json(base.read_text())
    web=(cfg.get('services') or {}).get('web') or {}
    build=web.get('build') or {}
    args=build.get('args') or {}
    require(isinstance(args,dict) and set(args)<=ALLOWED_BUILD_ARGS and
            all(isinstance(v,str) and len(v)<250 and '\n' not in v for v in args.values()),
            'UNSAFE_WEB_BUILD_ARGS')
    require(args.get('VITE_USE_MOCKS')=='false' and args.get('VITE_API_BASE_URL','')==''
            and args.get('VITE_OIDC_AUTHORITY')==WEB_ORIGINAL+'/identity/realms/pmgm'
            and args.get('VITE_OIDC_CLIENT_ID')=='pmgm-web'
            and args.get('VITE_OIDC_SCOPE','openid profile')=='openid profile'
            and args.get('VITE_OIDC_ALLOW_HTTP_LOCAL','false')=='false',
            'WEB_BUILD_IDENTITY_CHANGED')
    return args

def prepare(snapshot):
    require(shutil.disk_usage('/').free>=8*1024**3,'INSUFFICIENT_BUILD_DISK_FREE')
    require(not PREPARED.exists() and not JOURNAL.exists(),'HOTFIX_ALREADY_PREPARED')
    source=STATE_DIR/'source'
    require(not source.exists(),'SOURCE_ALREADY_EXISTS')
    # Source is pinned to immutable 40-hex Git object. Neither release JSON nor
    # mutable branch contents are accepted as evidence, independent of CI claims.
    git('init',str(source),work=STATE_DIR)
    git('remote','add','origin',REPOSITORY,work=source)
    git('fetch','--depth=8','origin','refs/heads/'+HOTFIX_BRANCH,work=source,timeout=120)
    git('checkout','--detach',HOTFIX_COMMIT,work=source)
    # Fetch base if missing from shallow history: still immutable and verified.
    _,rc=exec_fixed(('git','-C',str(source),'cat-file','-e',BASE_COMMIT+'^{commit}'),allow_error=True)
    if rc:git('fetch','--depth=8','origin','refs/heads/'+BASE_BRANCH,work=source,timeout=120)
    verify_git_tree(source)
    public=public_build_args(snapshot['web_paths'][0])
    frontend=source/'frontend'
    require((frontend/'Dockerfile').is_file() and (frontend/'package-lock.json').is_file(),
            'FRONTEND_BUILD_SOURCE_MISSING')
    context=(frontend/'Dockerfile').read_text()
    require('FROM node:24-alpine AS build' in context and 'FROM nginx:stable-alpine AS final' in context
            and context.count('RUN npm run build\n')==1 and
            context.count('FROM nginx:stable-alpine AS final')==1,'UNEXPECTED_FRONTEND_DOCKERFILE')
    # An immutable build recipe addition publishes truthful hotfix source SHA.
    with tempfile.TemporaryDirectory(dir=STATE_DIR,prefix='.build-') as tmp:
        filename=Path(tmp)/'Dockerfile'
        mark=json.dumps({'environment':'QA_SYNTHETIC','source_sha':HOTFIX_COMMIT},
                        separators=(',',':'))
        context=context.replace('RUN npm run build\n', 'RUN npm run build\nRUN npm test -- src/api/qa754-hotfix415.test.ts\n')
        filename.write_text(context+"\nRUN printf '%s\\n' '"+mark+"' > /usr/share/nginx/html/qa-version.json && chmod 644 /usr/share/nginx/html/qa-version.json\n")
        iid=Path(tmp)/'image.id'
        command=['docker','build','--pull=false','--iidfile',str(iid),'-f',str(filename),
                 '--label',REVISION+'='+HOTFIX_COMMIT,
                 '--label','cl.pmgm.qa.hotfix-base='+BASE_COMMIT]
        for k,v in sorted(public.items()):command.extend(('--build-arg',k+'='+v))
        command.append(str(frontend))
        exec_fixed(command,cwd=source,timeout=1500)
        image=iid.read_text().strip()
    require(SHA_IMAGE.fullmatch(image),'BUILT_IMAGE_ID_INVALID')
    data=parse_json(docker('image','inspect',image))
    require(len(data)==1 and data[0].get('Id')==image and
            data[0].get('Config',{}).get('Labels',{}).get(REVISION)==HOTFIX_COMMIT,
            'BUILT_IMAGE_IDENTITY_INVALID')
    # Reverify working tree remains pinned after Docker build.
    verify_git_tree(source)
    write_root_json(PREPARED,{'old_image':snapshot['web']['Image'],
               'new_image':image,'old_web_override':str(snapshot['web_paths'][1]),
               'base':str(snapshot['web_paths'][0]),'source_sha':HOTFIX_COMMIT,
               'base_commit':BASE_COMMIT})
    print('QA754_WEB_LOCAL_IMAGE: BUILT_AND_PINNED_NO_DEPLOYMENT')
    print('QA754_WEB_IMAGE_BACKUP: ORIGINAL_IMAGE_RETAINED')
    print('RESULT: LOCAL_WEB_IMAGE_PREPARED_SERVICE_UNCHANGED')

def compose_up(base,overlay):
    # Only target WEB; never compose down, prune, pull, migrate, or recreate deps.
    exec_fixed(('docker','compose','--project-name',PROJECT,
                '--project-directory',str(PROJECT_DIR),'-f',str(base),'-f',str(overlay),
                'up','-d','--no-deps','--no-build','--pull','never',
                '--force-recreate','--wait','--wait-timeout','75','web'),
               cwd=PROJECT_DIR,timeout=170)

def verify_dependencies(boundary):
    current={s:inspect(s)['Id'] for s in boundary}
    require(current==boundary,'NON_WEB_CONTAINER_WAS_CHANGED')
    identity_active(inspect('api'))
    timer_check()

def apply(snapshot):
    prepared=read_root_json(PREPARED)
    require(prepared.get('old_image')==snapshot['web']['Image']
            and prepared.get('old_web_override')==str(snapshot['web_paths'][1])
            and prepared.get('base')==str(snapshot['web_paths'][0])
            and prepared.get('source_sha')==HOTFIX_COMMIT
            and prepared.get('base_commit')==BASE_COMMIT,
            'PREPARED_BASELINE_MISMATCH')
    image=prepared.get('new_image')
    require(isinstance(image,str) and SHA_IMAGE.fullmatch(image),'PREPARED_IMAGE_BAD')
    meta=parse_json(docker('image','inspect',image))
    require(len(meta)==1 and meta[0].get('Id')==image and
            meta[0].get('Config',{}).get('Labels',{}).get(REVISION)==HOTFIX_COMMIT,
            'PREPARED_IMAGE_CHANGED')
    new_layer=PROJECT_DIR/('web-'+image[7:]+'.json')
    # Could have been created by an earlier safe attempt; any unexpected file blocks.
    if new_layer.exists():
        expected={'services':{'web':{'image':image}}}
        require(root_regular(new_layer,0o600) and parse_json(new_layer.read_text())==expected,
                'CANDIDATE_OVERRIDE_CONFLICT')
    else:
        write_root_json(new_layer,{'services':{'web':{'image':image}}})
    old_layer=snapshot['web_paths'][1]
    write_root_json(JOURNAL,{'old_image':prepared['old_image'],'new_image':image,
                 'old_layer':str(old_layer),'new_layer':str(new_layer),
                 'base':str(snapshot['web_paths'][0]),'status':'started'})
    print('QA754_WEB_APPLY: START_WEB_ONLY')
    try:
        compose_up(snapshot['web_paths'][0],new_layer)
        updated=inspect('web')
        require(updated.get('Image')==image and
                compose_paths(updated)==[snapshot['web_paths'][0],new_layer],
                'WEB_IMAGE_OR_OVERLAY_NOT_APPLIED')
        verify_dependencies(snapshot['boundary'])
        for _ in range(12):
            try:
                public_check(expected_sha=HOTFIX_COMMIT)
                break
            except Blocked:
                time.sleep(4)
        else:raise Blocked('PUBLIC_HEALTH_FAILED_AFTER_WEB_CHANGE')
        print('QA754_WEB_APPLY: HOTFIX_HEALTH_PASS')
        print('QA754_UAT_SYNTHETIC_ACCOUNT_AND_EMAIL: PENDING')
        print('RESULT: APPLIED_WEB_HOTFIX_ONLY_UAT_PENDING')
        return 0
    except Exception:
        print('QA754_WEB_APPLY: FAILURE_ATTEMPT_WEB_ONLY_ROLLBACK')
        try:
            compose_up(snapshot['web_paths'][0],old_layer)
            restored=inspect('web')
            require(restored.get('Image')==prepared['old_image'] and
                    compose_paths(restored)==snapshot['web_paths'],'WEB_ROLLBACK_IMAGE_FAILED')
            verify_dependencies(snapshot['boundary'])
            public_check(expected_sha=BASE_COMMIT)
            JOURNAL.unlink()
            print('QA754_WEB_ROLLBACK: PREVIOUS_WEB_HEALTH_PASS')
            print('RESULT: WEB_HOTFIX_ROLLED_BACK_TO_ORIGINAL')
            return 3
        except Exception:
            print('QA754_WEB_ROLLBACK: FAILED_MANUAL_RECOVERY_REQUIRED')
            print('RESULT: CRITICAL_WEB_ROLLBACK_FAILED_STOP')
            return 4

def rollback():
    # Manual rollback is allowed only from a fully pinned journal created by us.
    state=read_root_json(JOURNAL)
    prepared=read_root_json(PREPARED)
    required={'old_image','new_image','old_layer','new_layer','base','status'}
    require(isinstance(state,dict) and set(state)==required and state['status']=='started',
            'ROLLBACK_JOURNAL_INVALID')
    require(state['base']==str(PROJECT_DIR/'compose.recovery.json') and
            state['old_layer']==prepared.get('old_web_override') and
            state['old_image']==prepared.get('old_image') and
            state['new_image']==prepared.get('new_image'),
            'ROLLBACK_JOURNAL_BASELINE_MISMATCH')
    require(isinstance(state['old_image'],str) and SHA_IMAGE.fullmatch(state['old_image']) and
            isinstance(state['new_image'],str) and SHA_IMAGE.fullmatch(state['new_image']),
            'ROLLBACK_IMAGE_INVALID')
    old_layer=Path(state['old_layer']);new_layer=Path(state['new_layer'])
    require(old_layer.parent==PROJECT_DIR and WEB_OVERRIDE.fullmatch(old_layer.name) and
            new_layer==PROJECT_DIR/('web-'+state['new_image'][7:]+'.json'),
            'ROLLBACK_OVERRIDE_PATH_INVALID')
    expected_web_override(old_layer,state['old_image'])
    expected_web_override(new_layer,state['new_image'])
    snapshot=get_sanitized_snapshot(after=True)
    require(snapshot['web']['Image']==state['new_image'] and
            snapshot['web_paths']==[Path(state['base']),new_layer],
            'ROLLBACK_CURRENT_IMAGE_UNEXPECTED')
    backup_check();timer_check()
    old_image_info=parse_json(docker('image','inspect',state['old_image']))
    require(len(old_image_info)==1 and old_image_info[0].get('Id')==state['old_image'],
            'ROLLBACK_PREVIOUS_IMAGE_MISSING')
    print('QA754_WEB_MANUAL_ROLLBACK: START_WEB_ONLY')
    compose_up(Path(state['base']),old_layer)
    restored=inspect('web')
    require(restored.get('Image')==state['old_image'] and
            compose_paths(restored)==[Path(state['base']),old_layer],
            'ROLLBACK_PREVIOUS_IMAGE_NOT_RESTORED')
    verify_dependencies(snapshot['boundary'])
    public_check(expected_sha=BASE_COMMIT)
    JOURNAL.unlink()
    print('QA754_WEB_MANUAL_ROLLBACK: PREVIOUS_WEB_HEALTH_PASS')
    print('RESULT: MANUAL_WEB_ROLLBACK_OK')
    return 0


def install_guard():
    st=root_regular(SCRIPT,0o700)
    require(Path(__file__).resolve()==SCRIPT,'OPERATOR_MUST_BE_INSTALLED_IN_ROOT_TOOLS')
    require(STATE_DIR.is_dir() and STATE_DIR.stat().st_uid==0 and
            stat.S_IMODE(STATE_DIR.stat().st_mode)==0o700,
            'STATE_DIRECTORY_NOT_PRIVATE')

def main(argv=None):
    p=argparse.ArgumentParser(description='QA #754 hotfix web-only, root-protected')
    m=p.add_mutually_exclusive_group(required=True)
    for k in ('check','prepare','apply','rollback'):m.add_argument('--'+k,action='store_true')
    p.add_argument('--confirm',default='')
    args=p.parse_args(argv)
    install_guard()
    fd=os.open(LOCK,os.O_CREAT|os.O_RDWR|os.O_NOFOLLOW,0o600)
    try:
        st=os.fstat(fd)
        require(st.st_uid==0 and stat.S_IMODE(st.st_mode)==0o600,'LOCK_FILE_UNSAFE')
        try:fcntl.flock(fd,fcntl.LOCK_EX|fcntl.LOCK_NB)
        except BlockingIOError:raise Blocked('ANOTHER_HOTFIX_OPERATOR_RUNNING') from None
        if args.apply:
            require(args.confirm=='QA754-APLICAR-SOLO-WEB','EXPLICIT_CONFIRMATION_REQUIRED')
        elif args.rollback:
            require(args.confirm=='QA754-REVERTIR-SOLO-WEB','EXPLICIT_CONFIRMATION_REQUIRED')
        else:require(not args.confirm,'CONFIRM_ONLY_APPLY')
        if args.rollback:return rollback()
        snapshot=check()
        if args.check:return 0
        if args.prepare:
            print('QA754_WEB_PREPARE: BUILD_PINNED_SOURCE_NO_SERVICE_CHANGES')
            prepare(snapshot);return 0
        return apply(snapshot)
    finally:os.close(fd)

if __name__=='__main__':
    print('PMGM_QA754_WEB_HOTFIX_GUARDED_V1')
    try:sys.exit(main())
    except Blocked as e:
        print('RESULT: BLOCKED_'+str(e))
        sys.exit(2)
    except Exception:
        print('RESULT: BLOCKED_UNEXPECTED_NO_DETAILS')
        sys.exit(2)
