#!/usr/bin/env python3
"""Supervised #604 QA activation; no password handling or application upgrade."""
import contextlib, copy, fcntl, hashlib, importlib.util, json, os, re
import socket, stat, subprocess, sys, tempfile, time, urllib.request
from datetime import datetime, timezone
from pathlib import Path

BASE = Path('/opt/centenario/qa-master-604')
PREPARE = BASE/'prepare-local.py'
PREPARE_SHA = '1e47858b20f8cbe51befdefb716205d70ade431f3aab5d23565c8a0cc7055d91'
TARGET = '4d60a37be6d69ba4335a494ab7682fa06972da71'
IMAGE = 'sha256:98f90cc224524b36d7ee1b89f1c576da4bb0ac6bdccc4c9972b9ac871e25bce2'
API = 'centenario-qa-lab2-api-1'
PROJECT = 'centenario-qa-lab2'
COMPOSE = Path('/opt/centenario/qa-lab-firstboot/recovery-v2/compose.recovery.json')
OVERLAY = Path('/opt/centenario/qa-api-refresh')/('api-'+TARGET+'.json')
ENV_FILE = Path('/etc/pmgm/master-qa.env')
LOADER = Path('/opt/centenario/qa-auto-lite/run-with-backup-20261022.py')
LOADER_SHA = 'b31b67a2a0d35cecd8e12e8b5fbcc072814a7fbb308aa9bf41c6213af6a38ce7'
SMTP = 'SystemMail__EncryptionKey'
AUTHORITY = 'http://keycloak:8080/identity/realms/pmgm'

def require(ok, code):
    if not ok: raise RuntimeError(code)

def run(args, timeout=120):
    p = subprocess.run(args, stdin=subprocess.DEVNULL, capture_output=True,
                       text=True, timeout=timeout)
    require(p.returncode == 0, 'LOCAL_OPERATION_FAILED')
    return p.stdout

def private(path, digest=None):
    for parent in [path, *path.parents]:
        s = parent.lstat()
        require(not parent.is_symlink() and s.st_uid == 0 and
                not s.st_mode & 0o022, 'HOST_PATH_UNSAFE')
    s = path.lstat()
    require(stat.S_ISREG(s.st_mode) and stat.S_IMODE(s.st_mode) == 0o600
            and s.st_nlink == 1, 'HOST_FILE_NOT_PRIVATE')
    raw = path.read_bytes()
    if digest: require(hashlib.sha256(raw).hexdigest() == digest, 'CODE_OR_BACKUP_CHANGED')
    return raw

def load(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module

def atomic(path, raw):
    private(path)
    fd, temp = tempfile.mkstemp(prefix='.qa604-', dir=path.parent)
    try:
        with os.fdopen(fd, 'wb') as stream:
            stream.write(raw); stream.flush(); os.fsync(stream.fileno())
        os.replace(temp, path)
        d = os.open(path.parent, os.O_DIRECTORY)
        try: os.fsync(d)
        finally: os.close(d)
    finally:
        if os.path.exists(temp): os.unlink(temp)

def inspect_api():
    return json.loads(run(['docker', 'inspect', API]))[0]

def environment(api):
    rows = api['Config']['Env']
    pairs = [row.split('=', 1) for row in rows]
    require(len({k for k, v in pairs}) == len(pairs), 'DUPLICATE_API_ENV')
    return dict(pairs)

def compose(*args):
    return ['docker', 'compose', '--project-name', PROJECT,
            '--project-directory', str(COMPOSE.parent), '--env-file',
            '/opt/centenario/qa-auto-lite/empty.env', '-f', str(COMPOSE),
            '-f', str(OVERLAY), *args]

def dependencies():
    ids = run(['docker', 'ps', '-aq', '--filter',
               'label=com.docker.compose.project='+PROJECT]).split()
    require(bool(ids), 'QA_CONTAINERS_MISSING')
    values = json.loads(run(['docker', 'inspect', *ids]))
    return {v['Name']:v['Id'] for v in values if v['Name'] != '/'+API}

def health():
    for route in ('/health/web', '/health/ready'):
        with urllib.request.urlopen('https://sistema.granlogiamixta.cl'+route,
                                    timeout=12) as response:
            require(response.status == 200, 'PUBLIC_HEALTH_FAILED')

def wait_health():
    deadline = time.monotonic()+90
    while True:
        try: health(); return
        except Exception:
            require(time.monotonic() < deadline, 'PUBLIC_HEALTH_FAILED')
            time.sleep(3)

def up():
    run(compose('up', '--detach', '--no-deps', '--no-build', '--pull',
                'never', '--force-recreate', 'api'))

def recover_api(old_raw, before_env, others):
    atomic(OVERLAY, old_raw)
    up()
    wait_health()
    verify_runtime(before_env, others)

def expected_environment(before, subject, issuer):
    require(before.get('Authentication__Authority') == AUTHORITY, 'AUTHORITY_CHANGED')
    require(bool(before.get(SMTP)), 'SMTP_KEY_MISSING')
    for k in ('Database__ApplyMigrationsOnStartup', 'Database__RunMigrationsAndExit'):
        require(before.get(k, '').lower() == 'false', 'MIGRATIONS_MUST_STAY_DISABLED')
    require(not before.get('InitialLoad__MasterSubject') and
            not before.get('InitialLoad__MasterIssuer'), 'MASTER_CONFIG_ALREADY_PRESENT')
    return {**before, 'InitialLoad__MasterSubject':subject,
            'InitialLoad__MasterIssuer':issuer}

def verify_runtime(expected, other_containers):
    api = inspect_api()
    require(api['Image'] == IMAGE and api['State']['Running'] and
            api['Config']['Labels'].get('org.opencontainers.image.revision') == TARGET,
            'API_IMAGE_OR_STATE_CHANGED')
    require(environment(api) == expected, 'API_RUNTIME_ENV_MISMATCH')
    require(dependencies() == other_containers, 'OTHER_CONTAINER_CHANGED')

def require_account(api, module, helper, subject):
    account, master = module.inventory(api.get, 'pmgm', helper.ISSUER)
    require(account and master and account['id'] == subject == master['id'] and
            account.get('enabled') is False and
            account.get('attributes', {}).get('pmgm_master_qa_bootstrap') == ['issue-604'] and
            'UPDATE_PASSWORD' in account.get('requiredActions', []), 'PREPARED_ACCOUNT_MISMATCH')

def activate_transport(prep):
    class ActivationCLI(prep.CLI):
        armed = False
        def post(self, path, body): raise RuntimeError('CREATE_OR_ROLE_WRITE_FORBIDDEN')
        def delete(self, path, body): raise RuntimeError('DELETE_FORBIDDEN')
        def put(self, path, body):
            require(self.armed and path == '/admin/realms/pmgm/users/'+self.subject() and
                    isinstance(body, dict) and body == {'enabled':True} and
                    type(body['enabled']) is bool, 'ACTIVATION_WRITE_FORBIDDEN')
            self.call(['update', 'users/'+self.subject(), '-r', 'pmgm', '-n', '-f', '-'], body)
    return ActivationCLI

def main():
    phase = 'PREFLIGHT'; directory = None; helper = None; timer = False
    changed = False; activation_attempted = False; completed = False; old_raw = None
    with contextlib.ExitStack() as stack:
        try:
            require(os.geteuid() == 0 and socket.gethostname() == 'glmdch', 'HOST_NOT_AUTHORIZED')
            os.umask(0o077)
            private(PREPARE, PREPARE_SHA)
            prep = load('qa604_prepare', PREPARE)
            private(prep.INVENTORY, prep.INVENTORY_SHA)
            helper = load('qa604_inventory', prep.INVENTORY)
            helper.trusted(helper.TOOL, helper.TOOL_SHA)
            helper.trusted(helper.BACKUP, helper.BACKUP_SHA, True)
            module = load('qa604_signed_source', helper.TOOL)
            fd = module.lock_file(); stack.callback(os.close, fd)
            for path in (Path('/opt/centenario/qa-api-refresh/operation.lock'),
                         Path('/opt/centenario/qa-auto-lite/web-update.lock')):
                private(path)
                handle = stack.enter_context(path.open('a'))
                fcntl.flock(handle, fcntl.LOCK_EX | fcntl.LOCK_NB)
            receipt = json.loads(private(BASE/'prepared-disabled.json'))
            require(receipt.get('state') == 'PREPARED_DISABLED_FOR_QA_CONFIG' and
                    receipt.get('backup_sha256') == helper.BACKUP_SHA, 'PREPARATION_RECEIPT_INVALID')
            require(not ENV_FILE.exists() and not ENV_FILE.is_symlink() and
                    not (BASE/'overlay-before-master.json').exists() and
                    not (BASE/'activated.json').exists(), 'PARTIAL_OPERATION_REQUIRES_REVIEW')
            subject = module.private_file(prep.SUBJECT)
            require(re.fullmatch(r'[A-Za-z0-9_-]{8,320}', subject) is not None, 'SUBJECT_INVALID')
            private(LOADER, LOADER_SHA)
            run(['python3', str(LOADER), '--verify-backup-only'])
            health()
            before_api = inspect_api(); before_env = environment(before_api)
            labels = before_api['Config']['Labels']
            require(before_api['Image'] == IMAGE and before_api['State']['Running'] and
                    labels.get('org.opencontainers.image.revision') == TARGET and
                    labels.get('com.docker.compose.project.config_files') == str(COMPOSE)+','+str(OVERLAY),
                    'API_BASELINE_CHANGED')
            expected = expected_environment(before_env, subject, helper.ISSUER)
            old_raw = private(OVERLAY); private(COMPOSE)
            overlay = json.loads(old_raw)
            require(overlay['services']['api']['image'] == IMAGE, 'OVERLAY_IMAGE_CHANGED')
            planned = json.loads(run(compose('config', '--format', 'json')))['services']['api']
            require(planned['image'] == IMAGE and all(before_env.get(k) == str(v)
                    for k,v in planned.get('environment', {}).items()), 'COMPOSE_RUNTIME_MISMATCH')
            others = dependencies()
            directory = helper.run(['docker', 'exec', helper.KC, 'sh', '-c', helper.AUTH]).strip()
            require(re.fullmatch(r'/tmp/pmgm604\.[A-Za-z0-9]{6,20}', directory) is not None, 'SESSION_PATH_INVALID')
            with urllib.request.urlopen(helper.ISSUER+'/.well-known/openid-configuration', timeout=15) as response:
                discovery = json.loads(response.read(100000))
            cli = activate_transport(prep)(helper, module, directory, discovery)
            require_account(cli, module, helper, subject)
            print('MASTER_DESHABILITADO_Y_RESPALDO_VERIFICADOS', flush=True)
            phase = 'CONFIGURE_API'
            timer = subprocess.run(['systemctl','is-active','--quiet','pmgm-qa-lite.timer'],
                                   stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL).returncode == 0
            if timer: run(['systemctl','stop','pmgm-qa-lite.timer'])
            prep.write_private(BASE/'overlay-before-master.json', old_raw.decode())
            config = ('InitialLoad__MasterSubject='+subject+'\nInitialLoad__MasterIssuer='+helper.ISSUER+
                      '\nAuthentication__Authority='+AUTHORITY+'\n')
            module.validate_config(config, subject, helper.ISSUER)
            prep.write_private(ENV_FILE, config)
            new_overlay = copy.deepcopy(overlay)
            env = new_overlay['services']['api'].setdefault('environment', {})
            require(isinstance(env, dict) and env.get(SMTP) == before_env[SMTP], 'SMTP_OVERLAY_CHANGED')
            env.update({k:expected[k] for k in ('InitialLoad__MasterSubject',
                       'InitialLoad__MasterIssuer','Authentication__Authority')})
            changed = True
            atomic(OVERLAY, (json.dumps(new_overlay)+'\n').encode())
            run(compose('config','--quiet'))
            print('CONFIGURANDO_API_MISMA_IMAGEN_SIN_MIGRACIONES', flush=True)
            up(); wait_health(); verify_runtime(expected, others)
            prep.write_private(BASE/'api-configured.json', json.dumps({
                'state':'RUNTIME_MASTER_CONFIG_VERIFIED','source_sha':TARGET,
                'image':IMAGE,'time_utc':datetime.now(timezone.utc).isoformat()}))
            print('API_SANA_MASTER_Y_CORREO_VERIFICADOS_EN_CONTENEDOR', flush=True)
            phase = 'ACTIVATE_ADMIN'
            # Recheck runtime at the write boundary; source.activate repeats realm inventory.
            verify_runtime(expected, others); health()
            cli.armed = True; activation_attempted = True
            module.activate(cli,'pmgm',helper.ISSUER,subject,config)
            module.status(cli,'pmgm',helper.ISSUER,subject,config)
            verify_runtime(expected, others); health()
            prep.write_private(BASE/'activated.json', json.dumps({
                'state':'ENABLED_FOR_AUTHENTICATED_UAT_NOT_CERTIFIED',
                'source_sha':TARGET,'time_utc':datetime.now(timezone.utc).isoformat(),
                'future_api_updaters_must_preserve_master_config':True}))
            completed = True
            print('ADMIN_ACTIVADO_CONFIGURACION_Y_UNICIDAD_VERIFICADAS')
            print('PENDIENTE_LOGIN_ADMIN_CAMBIO_DE_CLAVE_Y_UAT')
            return 0
        except Exception as error:
            if changed and not activation_attempted:
                try:
                    recover_api(old_raw, before_env, others)
                    print('API_ANTERIOR_RECUPERADA_ADMIN_SIGUE_DESHABILITADO')
                except Exception:
                    print('RECUPERACION_API_NO_CONFIRMADA_REQUIERE_REVISION')
            if activation_attempted and not completed:
                print('ACTIVACION_NO_CONFIRMADA_API_CONFIGURADA_CONSERVADA_NO_REPETIR')
            code = str(error)
            safe = code if re.fullmatch(r'[A-Z0-9_]{1,100}',code) else 'OPERATION_NOT_CONFIRMED'
            print('BLOCKED '+safe+' ETAPA='+phase)
            return 1
        finally:
            if directory and helper and re.fullmatch(r'/tmp/pmgm604\.[A-Za-z0-9]{6,20}',directory):
                try: run(['docker','exec',helper.KC,'rm','-rf','--',directory])
                except Exception: print('SESSION_CLEANUP_NOT_CONFIRMED')
            if timer:
                try: run(['systemctl','start','pmgm-qa-lite.timer'])
                except Exception: print('WEB_TIMER_RESTART_NOT_CONFIRMED')

if __name__ == '__main__': sys.exit(main())
