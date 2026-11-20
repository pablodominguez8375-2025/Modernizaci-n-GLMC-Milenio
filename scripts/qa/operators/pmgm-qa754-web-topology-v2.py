#!/usr/bin/env python3
"""Read-only QA #754 WEB deployment topology audit; never reads/prints secrets.

Host: glmdch, project: centenario-qa-lab2. No deployment, restart,
Docker Compose config, token use, backup restoration, or host changes.
"""
import hashlib
import json
import os
from pathlib import Path
import re
import socket
import stat
import subprocess
import sys
from urllib.request import Request, urlopen

PROJECT='centenario-qa-lab2'
IDENTITY_STAGE=Path('/etc/pmgm/qa754-identity-overlay.stage.json')
G6=Path('/home/soporte/centenario-qa-g6-CIFRADO-20261027T115953Z.tar.gz')
G6_HASH='e4184cb798e3b44414c09d85735db6e6b2002e75027de67da8ed5b930a7ca8a6'
G6_SIZE=1027549
QA_LITE=Path('/opt/centenario/qa-tools/qa-lite-install.py')
WEB_STATE=Path('/opt/centenario/qa-auto-lite/installed.json')
RE_SHA=re.compile(r'[a-f0-9]{40}\Z')
RE_WEB_OVERRIDE=re.compile(r'web-[a-f0-9]{64}\.json\Z')
RE_API_OVERLAY=re.compile(r'api-[a-f0-9]{40}\.json\Z')

class Blocked(Exception): pass

def require(condition,code):
    if not condition: raise Blocked(code)

def subprocess_read(args,timeout=18,allow_nonzero=False):
    try:
        p=subprocess.run(args,stdin=subprocess.DEVNULL,stdout=subprocess.PIPE,stderr=subprocess.DEVNULL,
                         timeout=timeout,check=False)
    except (OSError,subprocess.TimeoutExpired): raise Blocked('READ_ONLY_COMMAND_UNAVAILABLE_OR_TIMEOUT') from None
    require(len(p.stdout)<=350000,'OUTPUT_TOO_LARGE')
    if p.returncode and not allow_nonzero: raise Blocked('READ_ONLY_COMMAND_FAILED')
    try: return p.stdout.decode('utf8').strip(),p.returncode
    except UnicodeDecodeError: raise Blocked('INVALID_UTF8_METADATA') from None

def docker(*args): return subprocess_read(('docker',*args))[0]

def container(service):
    listing=docker('ps','--filter',f'label=com.docker.compose.project={PROJECT}',
                   '--filter',f'label=com.docker.compose.service={service}',
                   '--format','{{.ID}}').splitlines()
    require(len(listing)==1 and re.fullmatch(r'[a-f0-9]{12,64}',listing[0]),'SERVICE_NOT_UNIQUE_'+service.upper())
    try: data=json.loads(docker('inspect',listing[0]))
    except ValueError: raise Blocked('DOCKER_INSPECT_INVALID') from None
    require(isinstance(data,list) and len(data)==1 and isinstance(data[0],dict),'DOCKER_INSPECT_INVALID')
    obj=data[0]
    cfg=obj.get('Config') or {}; labels=cfg.get('Labels') or {}
    require(labels.get('com.docker.compose.project')==PROJECT and
            labels.get('com.docker.compose.service')==service,'CONTAINER_WRONG_SCOPE')
    require((obj.get('State') or {}).get('Running') is True,'SERVICE_NOT_RUNNING_'+service.upper())
    return obj

def compose_paths(obj):
    raw=((obj.get('Config') or {}).get('Labels') or {}).get('com.docker.compose.project.config_files','')
    paths=[Path(v) for v in raw.split(',') if v]
    require(all(v.is_absolute() and v.is_file() and not v.is_symlink() for v in paths), 'COMPOSE_FILES_INVALID')
    return paths

def classify_layer(path, api_paths):
    """Metadata classification only; do not trust this as deploy permission."""
    if path == api_paths[0]:
        return 'RECOVERY_BASE_SHARED'
    if path == api_paths[1]:
        return 'API_IMAGE_LAYER_SHARED'
    if path == api_paths[2]:
        return 'API_IDENTITY_STAGE_SHARED'
    if RE_WEB_OVERRIDE.fullmatch(path.name):
        return 'WEB_IMAGE_OVERRIDE'
    if RE_API_OVERLAY.fullmatch(path.name):
        return 'DIFFERENT_API_IMAGE_LAYER'
    if path.name == 'compose.recovery.json':
        return 'DIFFERENT_RECOVERY_BASE'
    return 'UNRECOGNIZED_LOCAL_COMPOSE_LAYER'


def classify_layers(api,web):
    """Describe differences without assuming WEB and API share two layers.

    Some deployments recreate WEB with an image-specific override while API
    uses its own api-hash overlay. A mismatch is not proof of a broken state.
    No unknown layer is treated as authorization for a future deployment.
    """
    api_paths,web_paths=compose_paths(api),compose_paths(web)
    require(len(api_paths)==3,'API_EXPECTED_THREE_LAYERS')
    require(api_paths[0].name=='compose.recovery.json' and RE_API_OVERLAY.fullmatch(api_paths[1].name)
            and api_paths[2]==IDENTITY_STAGE,'API_IDENTITY_LAYER_MISSING')
    require(1<=len(web_paths)<=5,'WEB_LAYER_COUNT_OUTSIDE_DISCOVERY_LIMITS')
    st=IDENTITY_STAGE.stat()
    require(st.st_uid==0 and stat.S_IMODE(st.st_mode)==0o600 and stat.S_ISREG(st.st_mode)
            and not IDENTITY_STAGE.is_symlink(),'API_IDENTITY_STAGE_NOT_PRIVATE')
    web_layers=[classify_layer(path,api_paths) for path in web_paths]
    canonical=(len(web_layers) in (2,3) and web_layers[0]=='RECOVERY_BASE_SHARED'
               and web_layers[1]=='API_IMAGE_LAYER_SHARED'
               and (len(web_layers)==2 or web_layers[2]=='WEB_IMAGE_OVERRIDE'))
    return web_layers,canonical

def identity_active(obj):
    env=(obj.get('Config') or {}).get('Env') or []
    require(isinstance(env,list),'API_ENV_INVALID')
    vars={}
    for line in env:
        require(isinstance(line,str) and '=' in line,'API_ENV_INVALID')
        key,value=line.split('=',1)
        require(key not in vars, 'API_ENV_DUPLICATE')
        vars[key]=value
    # Never print values from this dictionary; it includes secrets.
    require(vars.get('UserAccounts__Identity__ServerUrl')=='http://keycloak:8080/identity'
            and vars.get('UserAccounts__Identity__Issuer')=='https://sistema.granlogiamixta.cl/identity/realms/pmgm'
            and vars.get('UserAccounts__Identity__ClientId')=='pmgm-account-admin'
            and bool(vars.get('UserAccounts__Identity__ClientSecret'))
            and vars.get('UserAccounts__Mail__LoginUrl')=='https://sistema.granlogiamixta.cl/',
            'API_IDENTITY_RUNTIME_NOT_VERIFIED')
    return True

def verify_g6():
    require(G6.is_file() and not G6.is_symlink() and G6.stat().st_size==G6_SIZE,'G6_LOCAL_NOT_MATCHING')
    h=hashlib.sha256()
    with G6.open('rb') as f:
        for part in iter(lambda:f.read(1024*1024),b''): h.update(part)
    require(h.hexdigest()==G6_HASH,'G6_LOCAL_SHA_CHANGED')

def timer():
    state,code=subprocess_read(('systemctl','is-active','pmgm-qa-lite.timer'),allow_nonzero=True)
    require(state=='inactive' and code!=0,'WEB_UPDATE_TIMER_NOT_INACTIVE')

def historical_installer():
    if not QA_LITE.is_file() or QA_LITE.is_symlink(): return 'NOT_INSTALLED'
    try: src=QA_LITE.read_text()
    except (OSError,UnicodeDecodeError): return 'UNREADABLE'
    # Local source is only examined to classify its fixed backup policy; never execute/import it.
    if ('centenario-qa-g6-CIFRADO-20261019T175218Z.tar.gz' in src and
        'bed4ee196074a1919234da44c6bbd66c8a7cd16bdce70fba97ed1e34df8f9cae' in src):
        return 'HISTORICAL_OCT19_PIN_UNSUITABLE'
    return 'UNKNOWN_REQUIRE_OPERATOR_REVIEW'

def installed_state(web):
    if not WEB_STATE.is_file() or WEB_STATE.is_symlink(): return 'NO_TRUSTED_BASELINE'
    try:
        o=json.loads(WEB_STATE.read_text())
        sha=o.get('source_sha');image=o.get('web_image_id')
        if not (isinstance(sha,str) and RE_SHA.fullmatch(sha)
                and isinstance(image,str) and re.fullmatch(r'sha256:[a-f0-9]{64}',image)):
            return 'INVALID_FORMAT'
        return 'IMAGE_MATCH' if web.get('Image')==image else 'IMAGE_MISMATCH'
    except (ValueError,UnicodeDecodeError,OSError): return 'INVALID_FORMAT'

def version():
    req=Request('https://sistema.granlogiamixta.cl/qa-version.json',headers={'User-Agent':'PMGM-QA754-WEB-ReadOnly/1.0'})
    try:
        with urlopen(req,timeout=8) as resp:
            if resp.geturl()!='https://sistema.granlogiamixta.cl/qa-version.json': return 'UNEXPECTED_REDIRECT'
            if resp.status!=200:return 'NON_200'
            data=resp.read(2048)
        obj=json.loads(data)
        sha=obj.get('source_sha')
        if obj.get('environment')!='QA_SYNTHETIC' or not isinstance(sha,str) or not RE_SHA.fullmatch(sha):
            return 'UNKNOWN_VERSION'
        return 'SOURCE_SHA_'+sha[:12]
    except (OSError,ValueError,TimeoutError): return 'UNAVAILABLE'

def main():
    require(os.geteuid()==0 and socket.gethostname()=='glmdch','QA_ROOT_HOST_REQUIRED')
    api=container('api');web=container('web')
    for name in ('postgres','keycloak','minio'):container(name)
    web_layers,canonical=classify_layers(api,web)
    identity_active(api)
    verify_g6();timer()
    print('API_IDENTITY: ACTIVE_RUNTIME_VERIFIED')
    print('API_COMPOSE_LAYERS: THREE_WITH_PRIVATE_IDENTITY')
    print('WEB_COMPOSE_LAYER_COUNT: '+str(len(web_layers)))
    for index,kind in enumerate(web_layers,1):
        print(f'WEB_LAYER_{index}_CLASS: {kind}')
    print('WEB_API_COMPOSE_RELATION: '+('CANONICAL_SHARED_BASE' if canonical
                                      else 'DIFFERENT_REQUIRES_SUPERVISED_PLAN'))
    print('API_WEB_DEPENDENCIES: RUNNING')
    print('G6_ENCRYPTED_LOCAL: SHA_MATCHES_VERIFIED_OFFHOST_REFERENCE')
    print('WEB_TIMER: INACTIVE')
    print('QA_LITE_INSTALLER: '+historical_installer())
    print('QA_LITE_INSTALLED_STATE: '+installed_state(web))
    print('PUBLIC_WEB_VERSION: '+version())
    print('RESULT: READ_ONLY_TOPOLOGY_CLASSIFIED_NO_DEPLOYMENT_NO_CHANGES')

if __name__=='__main__':
    print('PMGM_QA754_WEB_TOPOLOGY_READ_ONLY_V2')
    try:main()
    except Blocked as e:
        print('RESULT: BLOCKED_'+str(e));sys.exit(2)
    except Exception:
        print('RESULT: BLOCKED_UNEXPECTED_NO_DETAILS');sys.exit(2)