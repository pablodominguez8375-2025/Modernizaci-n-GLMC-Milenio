#!/usr/bin/env python3
"""#604 supervised QA Master preparation. Creates only a DISABLED admin."""
import getpass, hashlib, importlib.util, json, os, re, secrets, socket, stat
import subprocess, sys, urllib.request
from datetime import datetime, timezone
from pathlib import Path

BASE=Path('/opt/centenario/qa-master-604')
SUBJECT=Path('/etc/pmgm/master-qa-subject')
PASSWORD=Path('/etc/pmgm/master-qa-password')
INVENTORY=BASE/'inventory-local.py'
INVENTORY_SHA='b3a59e76513926fa40179ac36211d23942244f4230a7bbc9e4fdfcfcafa7f71d'
PG='centenario-qa-lab2-postgres-1'

def require(ok,code):
    if not ok:raise RuntimeError(code)

def execute(args,input=None,timeout=90):
    result=subprocess.run(args,input=input,stdin=None if input is not None else subprocess.DEVNULL,
                          stdout=subprocess.PIPE,stderr=subprocess.PIPE,text=True,timeout=timeout)
    require(result.returncode==0,'LOCAL_OPERATION_FAILED')
    return result.stdout

def write_private(path,text):
    for parent in path.parents:
        info=parent.lstat()
        require(not parent.is_symlink() and info.st_uid==0 and not info.st_mode&0o022,'HOST_PATH_UNSAFE')
    fd=os.open(path,os.O_CREAT|os.O_EXCL|os.O_NOFOLLOW|os.O_WRONLY,0o600)
    with os.fdopen(fd,'w') as stream:
        stream.write(text);stream.flush();os.fsync(stream.fileno())

def restore_probe(backup):
    name='qa604_restore_'+secrets.token_hex(6)
    created=False
    try:
        execute(['docker','exec',PG,'sh','-c','exec createdb -U "$POSTGRES_USER" -T template0 "$1"','qa604',name])
        created=True
        with backup.open('rb') as stream:
            result=subprocess.run(['docker','exec','-i',PG,'sh','-c',
                'exec pg_restore --exit-on-error --no-owner --no-privileges -U "$POSTGRES_USER" -d "$1"','qa604',name],
                stdin=stream,stdout=subprocess.PIPE,stderr=subprocess.PIPE,timeout=180)
        require(result.returncode==0,'KEYCLOAK_RESTORE_PROBE_FAILED')
        sql="SELECT count(*) FROM public.realm WHERE name='pmgm'"
        result=execute(['docker','exec',PG,'sh','-c',
            'exec psql -X -At -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$1" -c "$2"','qa604',name,sql])
        require(result.strip()=='1','KEYCLOAK_RESTORE_REALM_MISSING')
    finally:
        if created:
            execute(['docker','exec',PG,'sh','-c','exec dropdb -U "$POSTGRES_USER" "$1"','qa604',name])

class CLI:
    def __init__(self,helper,module,directory,discovery):
        self.h=helper;self.m=module;self.directory=directory;self.discovery=discovery
    def call(self,args,payload=None):
        return execute(['docker','exec','-i',self.h.KC,'/opt/keycloak/bin/kcadm.sh',
                        *args,'--config',self.directory+'/session'],
                       input=json.dumps(payload) if payload is not None else None)
    def get(self,path):
        if path=='/realms/pmgm/.well-known/openid-configuration':return self.discovery
        return json.loads(self.call(['get',*self.h.get_resource(path)]))
    def subject(self):return self.m.private_file(SUBJECT)
    def post(self,path,body):
        if path=='/admin/realms/pmgm/users':
            require(body.get('username')=='admin' and body.get('enabled') is False,'CREATE_MUST_BE_DISABLED_ADMIN')
            self.call(['create','users','-r','pmgm','-f','-'],body)
            return
        subject=self.subject()
        require(path=='/admin/realms/pmgm/users/'+subject+'/role-mappings/realm','WRITE_PATH_NOT_ALLOWED')
        require(isinstance(body,list) and len(body)==1 and body[0].get('name')==self.m.ROLE and isinstance(body[0].get('id'),str),'ROLE_WRITE_NOT_ALLOWED')
        self.call(['add-roles','-r','pmgm','--uid',subject,'--rolename',self.m.ROLE])
    def put(self,path,body):
        subject=self.subject()
        require(path=='/admin/realms/pmgm/users/'+subject+'/reset-password','ACTIVATION_NOT_ALLOWED_IN_THIS_PHASE')
        require(body.get('type')=='password' and body.get('temporary') is True,'PASSWORD_MUST_BE_TEMPORARY')
        self.call(['update','users/'+subject+'/reset-password','-r','pmgm','-n','-f','-'],body)
    def delete(self,path,body):
        require(path=='/admin/realms/pmgm/users/'+self.m.LEGACY_QA_SUBJECT+'/role-mappings/realm','DELETE_NOT_ALLOWED')
        require(isinstance(body,list) and len(body)==1 and body[0].get('name')==self.m.ROLE and isinstance(body[0].get('id'),str),'ROLE_WRITE_NOT_ALLOWED')
        self.call(['remove-roles','-r','pmgm','--uid',self.m.LEGACY_QA_SUBJECT,'--rolename',self.m.ROLE])

def main():
    directory=None;helper=None;lock=None;phase='PREFLIGHT'
    try:
        require(os.geteuid()==0 and socket.gethostname()=='glmdch','HOST_NOT_AUTHORIZED')
        os.umask(0o077)
        require(not INVENTORY.is_symlink() and INVENTORY.stat().st_uid==0 and
                stat.S_IMODE(INVENTORY.stat().st_mode)==0o600,'INVENTORY_NOT_PRIVATE')
        require(hashlib.sha256(INVENTORY.read_bytes()).hexdigest()==INVENTORY_SHA,'INVENTORY_CODE_CHANGED')
        spec=importlib.util.spec_from_file_location('master_local_inventory',INVENTORY)
        helper=importlib.util.module_from_spec(spec);spec.loader.exec_module(helper)
        helper.trusted(helper.BACKUP,helper.BACKUP_SHA,True);helper.trusted(helper.TOOL,helper.TOOL_SHA)
        spec=importlib.util.spec_from_file_location('reviewed_master_prepare',helper.TOOL)
        module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
        lock=module.lock_file()
        require(not SUBJECT.exists() and not SUBJECT.is_symlink() and not PASSWORD.exists() and not PASSWORD.is_symlink(),'PARTIAL_PREPARATION_REQUIRES_REVIEW')
        execute(['python3','/opt/centenario/qa-auto-lite/run-with-backup-20261022.py','--verify-backup-only'])
        fd=os.open('/dev/tty',os.O_RDWR)
        require(os.isatty(fd),'PRIVATE_TERMINAL_REQUIRED');os.close(fd)
        print('Introduce una clave temporal para admin: mínimo 14 caracteres. No se mostrará.',flush=True)
        password=getpass.getpass('Clave temporal: ')
        repeated=getpass.getpass('Repite la clave: ')
        require(password==repeated and 14<=len(password)<=256 and '\n' not in password and '\r' not in password,'PASSWORDS_INVALID_OR_DIFFERENT')
        phase='RESTORE_PROBE'
        restore_probe(helper.BACKUP)
        print('RESTAURACION_EN_BASE_TEMPORAL_CORRECTA_BASE_ELIMINADA',flush=True)
        directory=helper.run(['docker','exec',helper.KC,'sh','-c',helper.AUTH]).strip()
        require(re.fullmatch(r'/tmp/pmgm604\.[A-Za-z0-9]{6,20}',directory) is not None,'SESSION_PATH_INVALID')
        with urllib.request.urlopen(helper.ISSUER+'/.well-known/openid-configuration',timeout=15) as response:
            discovery=json.loads(response.read(100000))
        api=CLI(helper,module,directory,discovery)
        account,master=module.inventory(api.get,'pmgm',helper.ISSUER,allow_exact_legacy=True)
        require(account is None,'ADMIN_ALREADY_PRESENT')
        phase='LEGACY_ROLE_REVIEW'
        if master:
            require(master.get('id')==module.LEGACY_QA_SUBJECT,'FOREIGN_MASTER_ROLE')
            legacy='/admin/realms/pmgm/users/'+module.LEGACY_QA_SUBJECT
            before_user=api.get(legacy);before_roles=api.get(legacy+'/role-mappings/realm')
            module.retire_exact_legacy_master_role(api,'pmgm',helper.ISSUER)
            require(api.get(legacy)==before_user,'LEGACY_USER_CHANGED')
            expected={r['id'] for r in before_roles if r['name']!=module.ROLE}
            require({r['id'] for r in api.get(legacy+'/role-mappings/realm')}==expected,'LEGACY_OTHER_ROLES_CHANGED')
            print('ROL_MASTER_ANTIGUO_RETIRADO_USUARIO_Y_OTROS_ROLES_CONSERVADOS',flush=True)
        write_private(PASSWORD,password+'\n')
        phase='PREPARE_DISABLED'
        result=module.prepare(api,'pmgm',helper.ISSUER,password,SUBJECT)
        write_private(BASE/'prepared-disabled.json',json.dumps({
            'state':result,'backup_sha256':helper.BACKUP_SHA,
            'time_utc':datetime.now(timezone.utc).isoformat(),
            'restore_scope':'temporary_database_on_same_QA_instance_not_independent_DR'}))
        print('ADMIN_CREADO_DESHABILITADO_REGISTRO_PRIVADO_LISTO')
        print('PENDIENTE_CONFIGURAR_API_Y_ACTIVAR_NO_INTENTES_LOGIN_TODAVIA')
        return 0
    except (KeyboardInterrupt,EOFError):
        print('PREPARACION_INTERRUMPIDA_REVISAR_ESTADO_ANTES_DE_REPETIR');return 1
    except Exception as error:
        code=str(error)
        safe=code if re.fullmatch(r'[A-Z0-9_]{1,100}',code) else 'PREPARATION_NOT_CONFIRMED'
        print('BLOCKED '+safe+' ETAPA='+phase);return 1
    finally:
        if directory and helper and re.fullmatch(r'/tmp/pmgm604\.[A-Za-z0-9]{6,20}',directory):
            try:
                execute(['docker','exec',helper.KC,'rm','-rf','--',directory],timeout=15)
            except Exception:print('SESSION_CLEANUP_NOT_CONFIRMED')
        if lock is not None:os.close(lock)

if __name__=='__main__':sys.exit(main())
