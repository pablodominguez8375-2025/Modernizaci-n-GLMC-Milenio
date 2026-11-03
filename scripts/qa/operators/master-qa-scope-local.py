#!/usr/bin/env python3
"""#604: repair only the missing order scope on the registered QA Master."""
import copy, hashlib, importlib.util, json, os, re, socket, sys, urllib.request
from datetime import datetime, timezone
from pathlib import Path

BASE = Path('/opt/centenario/qa-master-604')
ACTIVATE = BASE/'activate-local.py'
ACTIVATE_SHA = 'd2982b1fcb212f87550237580cfef07fd2b3a5d99106797a04e992f6afeff713'

def require(ok, code):
    if not ok: raise RuntimeError(code)

def plan(user, subject):
    require(user.get('id') == subject and user.get('username') == 'admin' and
            user.get('enabled') is True, 'MASTER_IDENTITY_MISMATCH')
    attrs = user.get('attributes', {})
    require(isinstance(attrs, dict) and attrs.get('pmgm_master_qa_bootstrap') == ['issue-604'],
            'MASTER_MARKER_MISMATCH')
    require(not attrs.get('pmgm_org'), 'MASTER_HAS_ORGANIZATION_REQUIRES_REVIEW')
    require(attrs.get('pmgm_scope') in (None, [], ['order']), 'UNEXPECTED_EXISTING_SCOPE')
    result = copy.deepcopy(attrs)
    result['pmgm_scope'] = ['order']
    return result

def validate_mappers(mappers):
    require(isinstance(mappers,list), 'MAPPER_INVENTORY_INVALID')
    scopes = [m for m in mappers if m.get('config',{}).get('claim.name') == 'pmgm_scope']
    require(len(scopes) == 1, 'SCOPE_MAPPER_AMBIGUOUS')
    mapper = scopes[0]; config = mapper.get('config', {})
    require(mapper.get('protocolMapper') == 'oidc-usermodel-attribute-mapper' and
            config.get('user.attribute') == 'pmgm_scope' and
            config.get('access.token.claim') == 'true' and config.get('multivalued') == 'false',
            'SCOPE_MAPPER_NOT_EXPECTED')
    roles = [m for m in mappers if m.get('config',{}).get('claim.name') == 'pmgm_role']
    require(len(roles) == 1 and roles[0].get('protocolMapper') == 'oidc-usermodel-realm-role-mapper'
            and roles[0].get('config',{}).get('access.token.claim') == 'true',
            'ROLE_MAPPER_NOT_EXPECTED')

def verify_user(before, after, attributes):
    require(after.get('attributes') == attributes, 'SCOPE_WRITE_NOT_CONFIRMED')
    require({k:v for k,v in before.items() if k != 'attributes'} ==
            {k:v for k,v in after.items() if k != 'attributes'}, 'OTHER_USER_FIELD_CHANGED')

def main():
    helper = None; directory = None; lock = None
    try:
        require(os.geteuid() == 0 and socket.gethostname() == 'glmdch', 'HOST_NOT_AUTHORIZED')
        os.umask(0o077)
        # Verify before importing code. This reviewed module validates every path and dependency.
        s = ACTIVATE.lstat()
        require(not ACTIVATE.is_symlink() and s.st_uid == 0 and s.st_mode & 0o777 == 0o600,
                'ACTIVATION_CODE_UNSAFE')
        require(hashlib.sha256(ACTIVATE.read_bytes()).hexdigest() == ACTIVATE_SHA, 'ACTIVATION_CODE_CHANGED')
        spec = importlib.util.spec_from_file_location('qa604_activation',ACTIVATE)
        a = importlib.util.module_from_spec(spec); spec.loader.exec_module(a)
        a.private(ACTIVATE,ACTIVATE_SHA); a.private(a.PREPARE,a.PREPARE_SHA)
        prep = a.load('qa604_prepare',a.PREPARE)
        a.private(prep.INVENTORY,prep.INVENTORY_SHA)
        helper = a.load('qa604_inventory',prep.INVENTORY)
        helper.trusted(helper.TOOL,helper.TOOL_SHA)
        module = a.load('qa604_source',helper.TOOL)
        lock = module.lock_file()
        receipt = json.loads(a.private(BASE/'activated.json'))
        require(receipt.get('state') == 'ENABLED_FOR_AUTHENTICATED_UAT_NOT_CERTIFIED' and
                receipt.get('source_sha') == a.TARGET, 'ACTIVATION_RECEIPT_MISSING')
        subject = module.private_file(prep.SUBJECT)
        require(re.fullmatch(r'[A-Za-z0-9_-]{8,320}',subject) is not None, 'SUBJECT_INVALID')
        current = a.inspect_api(); env = a.environment(current)
        require(current['State']['Running'] and env.get('InitialLoad__MasterSubject') == subject and
                env.get('InitialLoad__MasterIssuer') == helper.ISSUER and
                env.get('Authentication__Authority') == a.AUTHORITY, 'API_MASTER_CONFIG_MISMATCH')
        a.health()
        print('COMPROBANDO_ATRIBUTOS_Y_MAPPERS_MASTER',flush=True)
        directory = helper.run(['docker','exec',helper.KC,'sh','-c',helper.AUTH]).strip()
        require(re.fullmatch(r'/tmp/pmgm604\.[A-Za-z0-9]{6,20}',directory) is not None,'SESSION_PATH_INVALID')
        with urllib.request.urlopen(helper.ISSUER+'/.well-known/openid-configuration',timeout=15) as response:
            discovery = json.loads(response.read(100000))
        cli = prep.CLI(helper,module,directory,discovery)
        account, master = module.inventory(cli.get,'pmgm',helper.ISSUER)
        require(account and master and account['id'] == subject == master['id'], 'MASTER_NOT_UNIQUE')
        endpoint = '/admin/realms/pmgm/users/'+subject
        before = cli.get(endpoint); attributes = plan(before,subject)
        clients = cli.get('/admin/realms/pmgm/clients?clientId=pmgm-web')
        require(len(clients) == 1 and clients[0].get('clientId') == 'pmgm-web' and
                re.fullmatch(r'[A-Za-z0-9_-]{8,320}',clients[0].get('id','')) is not None,
                'WEB_CLIENT_NOT_UNIQUE')
        validate_mappers(cli.get('/admin/realms/pmgm/clients/'+clients[0]['id']+'/protocol-mappers/models'))
        roles_before = cli.get(endpoint+'/role-mappings')
        if before.get('attributes') == attributes:
            print('SCOPE_ORDER_YA_PRESENTE_SIN_CAMBIOS_REQUIERE_REVISAR_SESION')
            return 0
        backup = BASE/'user-before-scope.json'
        require(not backup.exists() and not backup.is_symlink(), 'PREVIOUS_REPAIR_REQUIRES_REVIEW')
        prep.write_private(backup,json.dumps(before))
        # Exact registered user; merge every existing attribute, no password or role writes.
        cli.call(['update','users/'+subject,'-r','pmgm','-n','-f','-'],{'attributes':attributes})
        verify_user(before,cli.get(endpoint),attributes)
        require(cli.get(endpoint+'/role-mappings') == roles_before,'ROLE_MAPPING_CHANGED')
        module.status(cli,'pmgm',helper.ISSUER,subject,module.private_file(a.ENV_FILE))
        a.health()
        prep.write_private(BASE/'scope-repaired.json',json.dumps({
            'state':'ORDER_SCOPE_ATTRIBUTE_VERIFIED_NEW_LOGIN_REQUIRED',
            'time_utc':datetime.now(timezone.utc).isoformat()}))
        print('MASTER_SCOPE_ORDER_CORREGIDO_DATOS_Y_ROLES_CONSERVADOS')
        print('CIERRA_SESION_Y_VUELVE_A_ENTRAR_ADMIN_PARA_RENOVAR_PERMISOS')
        return 0
    except Exception as error:
        code = str(error)
        print('BLOCKED '+(code if re.fullmatch(r'[A-Z0-9_]{1,100}',code) else 'SCOPE_REPAIR_NOT_CONFIRMED'))
        return 1
    finally:
        if directory and helper and re.fullmatch(r'/tmp/pmgm604\.[A-Za-z0-9]{6,20}',directory):
            try: helper.run(['docker','exec',helper.KC,'rm','-rf','--',directory])
            except Exception: print('SESSION_CLEANUP_NOT_CONFIRMED')
        if lock is not None: os.close(lock)

if __name__ == '__main__': sys.exit(main())
