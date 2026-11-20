#!/usr/bin/env python3
"""Read-only local #604 inventory. Does not provision or change Keycloak."""
import hashlib, importlib.util, json, os, re, socket, stat, subprocess, sys
import urllib.parse, urllib.request
from pathlib import Path

KC='centenario-qa-lab2-keycloak-1'
ISSUER='https://sistema.granlogiamixta.cl/identity/realms/pmgm'
TOOL=Path('/opt/centenario/qa-auto-lite/releases/4d60a37be6d69ba4335a494ab7682fa06972da71/scripts/qa/prepare-master-qa.py')
TOOL_SHA='b21aa06bc32ac4e31236b2fa66603ff19716fe24e03a4ae54bf77fccec314559'
BACKUP=Path('/opt/centenario/qa-master-604/keycloak-before-master.dump')
BACKUP_SHA='616648f133ffaa3d98bfa124d729dfbf8b327ef47b5e1598023fe4e2c1d21c41'
AUTH='''set -eu
umask 077
u="${KC_BOOTSTRAP_ADMIN_USERNAME:-${KEYCLOAK_ADMIN:-}}"
export KC_CLI_PASSWORD="${KC_BOOTSTRAP_ADMIN_PASSWORD:-${KEYCLOAK_ADMIN_PASSWORD:-}}"
test -n "$u" && test -n "$KC_CLI_PASSWORD"
d=$(mktemp -d /tmp/pmgm604.XXXXXX)
if /opt/keycloak/bin/kcadm.sh config credentials --server http://127.0.0.1:8080/identity --realm master --user "$u" --config "$d/session" >/dev/null 2>&1; then
 printf '%s' "$d"
else
 rm -rf -- "$d"
 exit 1
fi
'''

def require(ok,code):
    if not ok:raise RuntimeError(code)

def run(args):
    result=subprocess.run(args,stdin=subprocess.DEVNULL,stdout=subprocess.PIPE,
                          stderr=subprocess.PIPE,text=True,timeout=45)
    require(result.returncode==0,'KEYCLOAK_LOCAL_QUERY_FAILED')
    return result.stdout

def trusted(path,expected,private=False):
    for p in [path,*path.parents]:
        s=p.lstat()
        require(not p.is_symlink() and s.st_uid==0 and not s.st_mode&0o022,'HOST_PATH_UNSAFE')
    require(path.is_file(),'HOST_FILE_UNSAFE')
    if private:require(stat.S_IMODE(path.stat().st_mode)==0o600,'BACKUP_NOT_PRIVATE')
    with path.open('rb') as f:require(hashlib.file_digest(f,'sha256').hexdigest()==expected,'HOST_FILE_HASH_CHANGED')

def get_resource(path):
    prefix='/admin/realms/pmgm/'
    require(path.startswith(prefix),'QUERY_NOT_ALLOWED')
    parts=urllib.parse.urlsplit(path[len(prefix):])
    require(parts.path and not parts.netloc and not parts.fragment,'QUERY_NOT_ALLOWED')
    args=[parts.path,'-r','pmgm']
    for k,v in urllib.parse.parse_qsl(parts.query):args+=['-q',k+'='+v]
    return args

def describe(account,master,module):
    if account:
        print('ADMIN_YA_EXISTE_NO_SE_MODIFICO')
    else:
        print('NOMBRE_ADMIN_DISPONIBLE')
    if not master:
        print('SIN_TITULAR_MASTER_EN_INVENTARIO')
    elif (master.get('id')==module.LEGACY_QA_SUBJECT and master.get('username')=='qa.admin'):
        print('ROL_MASTER_EN_FIXTURE_QA_HEREDADO')
    else:
        print('MASTER_EXISTENTE_REQUIERE_REVISION')
    print('CUENTAS_Y_ROLES_SIN_CAMBIOS')

def main():
    directory=None
    try:
        require(os.geteuid()==0 and socket.gethostname()=='glmdch','HOST_NOT_AUTHORIZED')
        trusted(BACKUP,BACKUP_SHA,True);trusted(TOOL,TOOL_SHA)
        inspected=json.loads(run(['docker','inspect',KC]))[0]
        require(inspected['State']['Running'] and inspected['Config']['Labels'].get('com.docker.compose.project')=='centenario-qa-lab2','KEYCLOAK_NOT_EXPECTED')
        spec=importlib.util.spec_from_file_location('reviewed_master',TOOL)
        module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
        print('RESPALDO_Y_UTILIDAD_VERIFICADOS',flush=True)
        directory=run(['docker','exec',KC,'sh','-c',AUTH]).strip()
        require(re.fullmatch(r'/tmp/pmgm604\.[A-Za-z0-9]{6,20}',directory) is not None,'SESSION_PATH_INVALID')
        print('ACCESO_ADMIN_LOCAL_CORRECTO',flush=True)
        with urllib.request.urlopen(ISSUER+'/.well-known/openid-configuration',timeout=15) as r:
            discovery=json.loads(r.read(100000))
        def get(path):
            if path=='/realms/pmgm/.well-known/openid-configuration':return discovery
            args=get_resource(path)
            return json.loads(run(['docker','exec',KC,'/opt/keycloak/bin/kcadm.sh','get',*args,'--config',directory+'/session']))
        account,master=module.inventory(get,'pmgm',ISSUER,allow_exact_legacy=True)
        describe(account,master,module)
        return 0
    except Exception as error:
        code=str(error)
        safe=code if re.fullmatch(r'[A-Z0-9_]{1,100}',code) else 'LOCAL_INVENTORY_NOT_CONFIRMED'
        print('BLOCKED '+safe)
        return 1
    finally:
        if directory and re.fullmatch(r'/tmp/pmgm604\.[A-Za-z0-9]{6,20}',directory):
            subprocess.run(['docker','exec',KC,'rm','-rf','--',directory],stdin=subprocess.DEVNULL,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL,timeout=15)

if __name__=='__main__':sys.exit(main())
