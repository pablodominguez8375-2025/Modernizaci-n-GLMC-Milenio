"""Read-only readiness for QA525. No build, apply, account or database writes."""
import base64
import hashlib
import hmac
import json
import os
import socket
import stat
import subprocess
import sys
import time
from pathlib import Path
from urllib.request import Request, urlopen

TARGET = '0bd927bc863d4c3321236b3a23eb15b748ed72dc'
BASE_API = '6ff4ae0d092d4214df4ea31af484efbc969fde54'
BASE_WEB = '424153c3a06c2b0bc0f8c2e8599eed4bb9ac6786'
PROJECT = 'centenario-qa-lab2'
ROOT = Path('/opt/centenario/qa-lab-firstboot/recovery-v2')
BASE = ROOT / 'compose.recovery.json'
API_LAYER = Path('/opt/centenario/qa-api-refresh') / ('api-' + BASE_API + '.json')
IDENTITY_LAYER = Path('/etc/pmgm/qa754-identity-overlay.stage.json')
ORIGIN = 'https://sistema.granlogiamixta.cl'
BACKUP = Path('/home/soporte/centenario-qa-g6-CIFRADO-20261027T115953Z.tar.gz')
BACKUP_SHA = 'e4184cb798e3b44414c09d85735db6e6b2002e75027de67da8ed5b930a7ca8a6'
BACKUP_TIME = 1793102393  # 2026-10-27T11:59:53Z, not file mtime.
API_URL = 'https://api.github.com/repos/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio'


class Blocked(Exception):
    pass


def require(ok, code):
    if not ok:
        raise Blocked(code)


def command(args, allowed=(0,)):
    result = subprocess.run(args, stdin=subprocess.DEVNULL, capture_output=True,
                            text=True, timeout=50)
    require(result.returncode in allowed, 'READ_COMMAND_FAILED')
    return result.stdout.strip()


def private(path):
    # Trust every ancestor as well as the file; reject symlinks and writable parents.
    for parent in path.parents:
        s = parent.lstat()
        require(stat.S_ISDIR(s.st_mode) and s.st_uid == 0 and not s.st_mode & 0o022,
                'PRIVATE_PARENT_UNTRUSTED')
    fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW)
    with os.fdopen(fd, 'rb') as stream:
        s = os.fstat(stream.fileno())
        require(stat.S_ISREG(s.st_mode) and s.st_uid == 0 and
                stat.S_IMODE(s.st_mode) == 0o600 and s.st_nlink == 1 and
                0 < s.st_size <= 1048576, 'PRIVATE_FILE_UNTRUSTED')
        data = stream.read(1048577)
        require(len(data) == s.st_size, 'PRIVATE_FILE_CHANGED')
        return data


def environment(rows):
    result = {}
    require(isinstance(rows, list), 'ENV_INVALID')
    for row in rows:
        require(isinstance(row, str) and '=' in row, 'ENV_INVALID')
        k, v = row.split('=', 1)
        require(k and k not in result, 'ENV_DUPLICATE')
        result[k] = v
    return result


def validate_env(configured, runtime, defaults):
    require(isinstance(configured, dict) and
            all(isinstance(v, str) for v in configured.values()), 'COMPOSE_ENV_INVALID')
    require(runtime == {**defaults, **configured}, 'ENV_EFFECTIVE_DRIFT')


def validate_identity(env, identity, master, secret, smtp):
    require(all(env.get(k, 'false').lower() == 'false' for k in (
        'UserAccounts__Identity__RequireLiveTokenIntrospection',
        'UserAccounts__Identity__EnableLifecycleMutations')),
        'ACCOUNT_LIFECYCLE_FLAGS_REQUIRE_REVIEW')
    expected = {
        'UserAccounts__Identity__ServerUrl': 'http://keycloak:8080/identity',
        'UserAccounts__Identity__Issuer': ORIGIN + '/identity/realms/pmgm',
        'UserAccounts__Identity__ClientId': 'pmgm-account-admin',
        'UserAccounts__Mail__LoginUrl': ORIGIN + '/',
        'UserAccounts__Identity__ClientSecret': secret,
    }
    require(identity == expected, 'IDENTITY_LAYER_DRIFT')
    require(all(hmac.compare_digest(env.get(k, ''), v) for k, v in expected.items()),
            'IDENTITY_RUNTIME_DRIFT')
    require(set(master) == {'InitialLoad__MasterSubject', 'InitialLoad__MasterIssuer',
                            'Authentication__Authority'}, 'MASTER_FILE_INVALID')
    require(master['InitialLoad__MasterSubject'] and
            master['InitialLoad__MasterIssuer'] == ORIGIN + '/identity/realms/pmgm' and
            master['Authentication__Authority'] == 'http://keycloak:8080/identity/realms/pmgm',
            'MASTER_IDENTITY_INVALID')
    require(all(env.get(k) == v for k, v in master.items()), 'MASTER_RUNTIME_DRIFT')
    require(len(base64.b64decode(smtp, validate=True)) == 32 and
            hmac.compare_digest(env.get('SystemMail__EncryptionKey', ''), smtp),
            'SMTP_KEY_DRIFT')
    require(all(env.get(k, '').lower() == 'false' for k in
                ('Database__ApplyMigrationsOnStartup', 'Database__RunMigrationsAndExit')),
            'MIGRATIONS_ENABLED')


def get_json(url):
    with urlopen(Request(url, headers={'User-Agent': 'PMGM-QA525-ReadOnly'}), timeout=15) as response:
        require(response.status == 200 and response.geturl() == url, 'PUBLIC_REDIRECT_OR_STATUS')
        return json.load(response)


def backup_check(now):
    # Historical G6 is an integrity/off-host reference. Apply creates fresh dumps
    # of both mutable databases before changing either service.
    require(now >= BACKUP_TIME, 'BACKUP_CLOCK_INVALID')
    fd = os.open(BACKUP, os.O_RDONLY | os.O_NOFOLLOW)
    with os.fdopen(fd, 'rb') as stream:
        s = os.fstat(stream.fileno())
        require(stat.S_ISREG(s.st_mode) and s.st_size == 1027549, 'BACKUP_SIZE_DRIFT')
        require(hashlib.file_digest(stream, 'sha256').hexdigest() == BACKUP_SHA,
                'BACKUP_HASH_DRIFT')


def check():
    require(os.geteuid() == 0 and socket.gethostname() == 'glmdch', 'HOST_NOT_AUTHORIZED')
    services = {}
    for name in ('api', 'web', 'keycloak', 'postgres', 'minio', 'gateway', 'clamav'):
        c = json.loads(command(['docker', 'inspect', PROJECT + '-' + name + '-1']))[0]
        require(c['State']['Status'] == 'running', 'SERVICE_NOT_RUNNING')
        labels = c['Config'].get('Labels', {})
        require(labels.get('com.docker.compose.project') == PROJECT and
                labels.get('com.docker.compose.service') == name, 'PROJECT_LABEL_DRIFT')
        services[name] = c
    api, web = services['api'], services['web']
    require(api['Config']['Labels'].get('org.opencontainers.image.revision') == BASE_API and
            web['Config']['Labels'].get('org.opencontainers.image.revision') == BASE_WEB,
            'BASELINE_SOURCE_CHANGED')
    paths = api['Config']['Labels']['com.docker.compose.project.config_files'].split(',')
    require(paths == list(map(str, (BASE, API_LAYER, IDENTITY_LAYER))), 'API_THREE_LAYERS_CHANGED')
    identity_doc = json.loads(private(IDENTITY_LAYER))
    require(set(identity_doc) == {'services'} and set(identity_doc['services']) == {'api'} and
            set(identity_doc['services']['api']) == {'environment'}, 'IDENTITY_LAYER_SCOPE')
    identity = identity_doc['services']['api']['environment']
    master = environment([row for row in private(Path('/etc/pmgm/master-qa.env')).decode().splitlines()
                          if row.strip() and not row.lstrip().startswith('#')])
    secret = private(Path('/etc/pmgm/qa-account-admin-secret')).decode().strip('\r\n')
    smtp = private(Path('/etc/pmgm/qa-smtp-encryption-key')).decode().strip()
    validate_identity(environment(api['Config']['Env']), identity, master, secret, smtp)
    for c, name, count in ((api, 'api', 3), (web, 'web', 2)):
        labels = c['Config']['Labels']
        require(labels.get('com.docker.compose.project.working_dir') == str(ROOT), 'WORKDIR_CHANGED')
        layers = labels['com.docker.compose.project.config_files'].split(',')
        require(len(layers) == count and layers[0] == str(BASE), 'COMPOSE_LAYER_DRIFT')
        if name == 'web':
            require(Path(layers[1]).parent in (Path('/opt/centenario/qa-auto-lite'), ROOT) and
                    Path(layers[1]).name == 'web-' + c['Image'].removeprefix('sha256:') + '.json',
                    'WEB_LAYER_UNEXPECTED')
        for p in layers:
            private(Path(p))
        args = ['docker', 'compose', '--project-name', PROJECT, '--project-directory', str(ROOT),
                '--env-file', '/opt/centenario/qa-auto-lite/empty.env']
        for p in layers:
            args += ['-f', p]
        cfg = json.loads(command(args + ['config', '--format', 'json']))['services'][name]
        image = json.loads(command(['docker', 'image', 'inspect', cfg['image']]))[0]
        require(image['Id'] == c['Image'], 'COMPOSE_IMAGE_DRIFT')
        validate_env(cfg.get('environment', {}), environment(c['Config']['Env']),
                     environment(image['Config'].get('Env') or []))
    require(command(['systemctl', 'is-active', 'pmgm-qa-lite.timer'], (0, 3, 4)) == 'inactive',
            'WEB_TIMER_NOT_PAUSED')
    backup_check(time.time())
    receipt = json.loads(private(Path('/opt/centenario/qa-api-refresh/installed.json')))
    require(receipt.get('source_sha') == BASE_API and receipt.get('api_image_id') == api['Image'],
            'API_RECEIPT_DRIFT')
    query = 'BEGIN READ ONLY; SELECT "MigrationId" FROM public."__EFMigrationsHistory" ORDER BY 1; COMMIT;'
    rows = command(['docker', 'exec', PROJECT + '-postgres-1', 'sh', '-c',
                    'exec psql -X -qAt -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d pmgm -c "$1"',
                    'qa525-read-only', query]).splitlines()
    expected = receipt.get('database_migrations', [])
    require(len(rows) == len(set(rows)) == len(expected) == 98 and set(rows) == set(expected),
            'DATABASE_HISTORY_DRIFT')
    version = get_json(ORIGIN + '/qa-version.json')
    require(version.get('environment') == 'QA_SYNTHETIC' and version.get('source_sha') == BASE_WEB,
            'PUBLIC_WEB_SOURCE_DRIFT')
    for endpoint in ('/health/ready', '/health/web'):
        with urlopen(ORIGIN + endpoint, timeout=15) as response:
            require(response.status == 200, 'PUBLIC_HEALTH_FAILED')
    print('QA525_BASELINE: API_6FF4_WEB_424153_VERIFIED')
    print('QA525_CONFIG: API_THREE_LAYERS_WEB_TWO_LAYERS_RUNTIME_MATCH')
    print('QA525_PRESERVED: IDENTITY_MASTER_SMTP_VERIFIED')
    print('QA525_DATABASE: 98_MIGRATIONS_NO_WRITES')
    print('QA525_BACKUP: REFERENCE_G6_INTEGRITY_VERIFIED_FRESH_APPLY_DUMPS_REQUIRED')
    print('QA525_PUBLIC_HEALTH: PASS')
    print('QA525_SIGNED_TARGET_PACKAGE: NOT_VERIFIED_BY_THIS_PREFLIGHT')
    print('QA525_INDEPENDENT_RESTORE: NOT_CERTIFIED')
    print('RESULT: READ_ONLY_INPUTS_PASS_DEPLOYMENT_NOT_IMPLEMENTED')


if __name__ == '__main__':
    try:
        require(len(sys.argv) == 1, 'READ_ONLY_NO_OPTIONS')
        check()
    except Blocked as error:
        print('RESULT: BLOCKED_' + str(error))
        sys.exit(1)
    except Exception:
        # Never print subprocess output, environment, URLs with data or exception text.
        print('RESULT: BLOCKED_READ_ONLY_CHECK_FAILED')
        sys.exit(1)
