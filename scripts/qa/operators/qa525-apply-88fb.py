"""Apply already prepared QA525 API and WEB images as one reversible transaction."""
import contextlib
import fcntl
import hashlib
import importlib.util
import io
import json
import os
import re
import socket
import stat
import subprocess
import sys
import time
from pathlib import Path
from urllib.request import Request, urlopen

TARGET = '88fb9049de22e15da8b54cab6bc56d806cac1477'
PROJECT = 'centenario-qa-lab2'
ROOT = Path('/opt/centenario/qa-lab-firstboot/recovery-v2')
EMPTY_ENV = Path('/opt/centenario/qa-auto-lite/empty.env')
TOOLS = Path('/opt/centenario/qa-tools')
STATE = Path('/opt/centenario/qa-api-refresh/prepare-88fb')
CODE = Path('/opt/centenario/qa-auto-lite/code-4c0101fa439acd52e2f1e78a9f4bfb3b1eb49c4b')
ORIGIN = 'https://sistema.granlogiamixta.cl'
PREFLIGHT_SHA = 'fd02788fbf8f2a62d41c1304b37c2206b98210f733adbd57511ba7c06e97fd74'
PREPARE_SHA = '01b5473d560da91d01f3a3ec1f1e962a58a4bf9088fd9d1f5342956fdf8aac91'
DEPENDENCIES = {
    'qa-agent-readiness.py': 'afc1d54a90696a4a33bdfe689c47406b5dc0aaf8838426f907761adab20ddd55',
    'qa-signed-staging.py': 'ebc9acd2845b4185f8362a81d3eb1657e9a83b1c2cd3d53e4d3dc739fd911e88',
}


class Blocked(Exception):
    pass


def require(ok, code):
    if not ok:
        raise Blocked(code)


def digest(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def trusted(path, expected=None, directory=False):
    for p in (path, *path.parents):
        s = p.lstat()
        require(not p.is_symlink() and s.st_uid == 0 and not s.st_mode & 0o022,
                'LOCAL_PATH_OR_PARENT_UNTRUSTED')
    require(path.is_dir() if directory else path.is_file(), 'LOCAL_PATH_TYPE')
    if expected:
        require(digest(path) == expected, 'LOCAL_FILE_SHA_CHANGED')


def private_bytes(path, limit=1048576):
    for parent in path.parents:
        s = parent.lstat()
        require(stat.S_ISDIR(s.st_mode) and s.st_uid == 0 and not s.st_mode & 0o022,
                'PRIVATE_PARENT_UNTRUSTED')
    fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW)
    with os.fdopen(fd, 'rb') as stream:
        s = os.fstat(stream.fileno())
        require(stat.S_ISREG(s.st_mode) and s.st_uid == 0 and
                stat.S_IMODE(s.st_mode) == 0o600 and s.st_nlink == 1 and
                0 < s.st_size <= limit, 'PRIVATE_FILE_UNTRUSTED')
        value = stream.read(limit + 1)
        require(len(value) == s.st_size, 'PRIVATE_FILE_CHANGED')
        return value


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    value = importlib.util.module_from_spec(spec)
    sys.modules[name] = value
    spec.loader.exec_module(value)
    return value


def command(args, timeout=120, binary=False, stdin=None, stdout=None, allowed=(0,)):
    result = subprocess.run(args, stdin=stdin or subprocess.DEVNULL,
                            stdout=stdout or subprocess.PIPE,
                            stderr=subprocess.PIPE, timeout=timeout,
                            text=False if binary or stdout else True)
    require(result.returncode in allowed, 'COMMAND_FAILED')
    if stdout:
        return ''
    return result.stdout if binary else result.stdout.strip()


def inspect(name):
    return json.loads(command(['docker', 'inspect', PROJECT + '-' + name + '-1']))[0]


def snapshot():
    result = {}
    for name in ('api', 'web', 'keycloak', 'postgres', 'minio', 'gateway', 'clamav'):
        current = inspect(name)
        require(current['State']['Status'] == 'running', 'SERVICE_NOT_RUNNING')
        result[name] = [current['Id'], current['Image']]
    return result


def image_identity(image, target):
    require(re.fullmatch(r'sha256:[a-f0-9]{64}', image or '') is not None,
            'IMAGE_ID_INVALID')
    value = json.loads(command(['docker', 'image', 'inspect', image]))[0]
    require(value['Id'] == image and
            value['Config'].get('Labels', {}).get('org.opencontainers.image.revision') == target,
            'IMAGE_IDENTITY_CHANGED')


def environment(rows):
    result = {}
    for row in rows:
        require(isinstance(row, str) and '=' in row, 'ENV_INVALID')
        key, value = row.split('=', 1)
        require(key and key not in result, 'ENV_INVALID')
        result[key] = value
    return result


def current_layers():
    api, web = inspect('api'), inspect('web')
    require(api['Config']['Labels']['com.docker.compose.project.working_dir'] == str(ROOT) and
            web['Config']['Labels']['com.docker.compose.project.working_dir'] == str(ROOT),
            'COMPOSE_WORKDIR_CHANGED')
    result = {}
    for current in (api, web):
        layers = []
        for raw in current['Config']['Labels']['com.docker.compose.project.config_files'].split(','):
            path = Path(raw)
            trusted(path)
            layers.append(path)
        require(layers and layers[0] == ROOT / 'compose.recovery.json', 'COMPOSE_BASE_CHANGED')
        result[current['Config']['Labels']['com.docker.compose.service']] = layers
    return result


def compose_args(layers, overlay=None):
    args = ['docker', 'compose', '--project-name', PROJECT,
            '--project-directory', str(ROOT), '--env-file', str(EMPTY_ENV)]
    for layer in layers:
        args += ['-f', str(layer)]
    if overlay is not None:
        args += ['-f', str(overlay)]
    return args


def compose_config(layers, overlay):
    return json.loads(command(compose_args(layers, overlay) +
                              ['config', '--format', 'json'], timeout=60))['services']


def write_private_json(path, value):
    fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
    with os.fdopen(fd, 'w') as stream:
        json.dump(value, stream, sort_keys=True)
        stream.flush()
        os.fsync(stream.fileno())


def overlay(service, image):
    require(service in ('api', 'web'), 'OVERLAY_SERVICE_INVALID')
    return {'services': {service: {'image': image}}}


def replace_private_json(path, value):
    temporary = path.with_name('.' + path.name + '.tmp-' + str(time.time_ns()))
    write_private_json(temporary, value)
    os.replace(temporary, path)
    parent = os.open(path.parent, os.O_RDONLY | os.O_DIRECTORY)
    try:
        os.fsync(parent)
    finally:
        os.close(parent)


def replace_private_bytes(path, value):
    temporary = path.with_name('.' + path.name + '.tmp-' + str(time.time_ns()))
    fd = os.open(temporary, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
    with os.fdopen(fd, 'wb') as stream:
        stream.write(value)
        stream.flush()
        os.fsync(stream.fileno())
    os.replace(temporary, path)


def backup_database(name, directory):
    require(re.fullmatch(r'[a-z_]+', name) is not None, 'DATABASE_NAME_INVALID')
    path = directory / (name + '.dump')
    fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
    with os.fdopen(fd, 'wb') as output:
        command(['/usr/bin/docker', '--host', 'unix:///var/run/docker.sock', 'exec',
                 PROJECT + '-postgres-1', 'sh', '-c',
                 'exec pg_dump -U "$POSTGRES_USER" -d "$1" -Fc', 'qa525-backup', name],
                timeout=240, stdout=output)
    require(path.stat().st_size > 100, 'BACKUP_EMPTY')
    with path.open('rb') as source:
        command(['/usr/bin/docker', '--host', 'unix:///var/run/docker.sock', 'exec', '-i',
                 PROJECT + '-postgres-1', 'pg_restore', '--list'], timeout=90, stdin=source)
    return {'path': str(path), 'sha256': digest(path), 'size': path.stat().st_size}


def migration_history():
    sql = 'BEGIN READ ONLY; SELECT "MigrationId" FROM public."__EFMigrationsHistory" ORDER BY 1; COMMIT;'
    rows = command(['docker', 'exec', PROJECT + '-postgres-1', 'sh', '-c',
                    'exec psql -X -qAt -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d pmgm -c "$1"',
                    'qa525-history', sql]).splitlines()
    require(len(rows) == len(set(rows)) == 98, 'MIGRATION_HISTORY_CHANGED')
    return rows


def public_health(target):
    version_url = ORIGIN + '/qa-version.json'
    with urlopen(Request(version_url, headers={'User-Agent': 'PMGM-QA525-Apply'}), timeout=20) as r:
        require(r.status == 200 and r.geturl() == version_url, 'PUBLIC_WEB_STATUS')
        version = json.load(r)
    require(version.get('environment') == 'QA_SYNTHETIC' and
            version.get('source_sha') == target, 'PUBLIC_WEB_SHA_MISMATCH')
    for endpoint in ('/health/ready', '/health/web'):
        with urlopen(ORIGIN + endpoint, timeout=20) as r:
            require(r.status == 200, 'PUBLIC_HEALTH_FAILED')


def wait_health(target):
    deadline = time.monotonic() + 120
    while True:
        try:
            public_health(target)
            return
        except Exception:
            if time.monotonic() >= deadline:
                raise Blocked('PUBLIC_HEALTH_TIMEOUT') from None
            time.sleep(4)


def validate_runtime(before_env, target, api_image, web_image):
    api, web = inspect('api'), inspect('web')
    require(api['Image'] == api_image and web['Image'] == web_image,
            'RUNNING_IMAGE_MISMATCH')
    require(api['Config']['Labels'].get('org.opencontainers.image.revision') == target and
            web['Config']['Labels'].get('org.opencontainers.image.revision') == target,
            'RUNNING_REVISION_MISMATCH')
    require(environment(api['Config'].get('Env') or []) == before_env,
            'API_ENVIRONMENT_CHANGED')


def apply():
    require(os.geteuid() == 0 and socket.gethostname() == 'glmdch', 'HOST_NOT_AUTHORIZED')
    os.umask(0o077)
    trusted(Path(__file__))
    require(Path(__file__) == TOOLS / 'qa525-apply-88fb.py', 'INSTALL_OPERATOR_FIRST')
    trusted(TOOLS / 'qa525-update-readiness-88fb.py', PREFLIGHT_SHA)
    trusted(TOOLS / 'qa525-prepare-88fb.py', PREPARE_SHA)
    for name, sha in DEPENDENCIES.items():
        trusted(CODE / name, sha)
    trusted(STATE, directory=True)
    receipt_path = STATE / 'prepared.json'
    receipt = json.loads(private_bytes(receipt_path))
    require(receipt.get('source_sha') == TARGET and
            receipt.get('scope') == 'BUILD_ONLY_NO_DEPLOYMENT_NO_UAT', 'PREPARED_RECEIPT_INVALID')
    api_image, web_image = receipt.get('api_image_id'), receipt.get('web_image_id')
    image_identity(api_image, TARGET)
    image_identity(web_image, TARGET)
    require(not (STATE / 'applied.json').exists(), 'ALREADY_APPLIED_DO_NOT_REPEAT')
    with contextlib.ExitStack() as resources:
        for p in (Path('/opt/centenario/qa-api-refresh/operation.lock'),
                  Path('/opt/centenario/qa-auto-lite/web-update.lock'),
                  Path('/run/lock/pmgm-master-qa-604.lock'),
                  Path('/run/lock/pmgm-qa754-web-hotfix.lock'),
                  Path('/run/lock/pmgm-qa754-api-activation.lock')):
            fd = os.open(p, os.O_RDWR | os.O_CREAT | os.O_NOFOLLOW, 0o600)
            resources.callback(os.close, fd)
            s = os.fstat(fd)
            require(stat.S_ISREG(s.st_mode) and s.st_uid == 0 and not s.st_mode & 0o077,
                    'LOCK_UNTRUSTED')
            fcntl.flock(fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
        command(['systemctl', 'stop', 'pmgm-qa-lite.timer'])
        require(command(['systemctl', 'is-active', 'pmgm-qa-lite.timer'], timeout=20, allowed=(0, 3, 4)) == 'inactive',
                'WEB_TIMER_ACTIVE')
        pre = module('qa525_apply_preflight', TOOLS / 'qa525-update-readiness-88fb.py')
        with contextlib.redirect_stdout(io.StringIO()):
            pre.check()
        before = snapshot()
        require(before == receipt.get('baseline'), 'PREPARED_BASELINE_CHANGED')
        before_env = environment(inspect('api')['Config'].get('Env') or [])
        prepare = module('qa525_apply_prepare', TOOLS / 'qa525-prepare-88fb.py')
        ready = module('qa525_apply_ready', CODE / 'qa-agent-readiness.py')
        require(prepare.fetch('/branches/dev')['commit']['sha'] == TARGET,
                'TARGET_NO_LONGER_DEV_HEAD')
        require(prepare.selection(ready) == receipt.get('selection'), 'SIGNED_SELECTION_CHANGED')
        staging = module('qa525_apply_staging', CODE / 'qa-signed-staging.py')
        archive = STATE / (TARGET + '.zip')
        package = STATE / 'package'
        verifier = prepare.source_verifier(staging, archive,
                                           receipt['selection']['artifact_sha256'], package)
        require(verifier(package, TARGET), 'SIGNED_SOURCE_INVALID')
        layers = current_layers()
        stamp = str(time.time_ns())
        api_active = STATE / ('api-overlay-' + stamp + '.json')
        web_active = STATE / ('web-overlay-' + stamp + '.json')
        write_private_json(api_active, overlay('api', api_image))
        write_private_json(web_active, overlay('web', web_image))
        api_current_cfg = compose_config(layers['api'], None)
        web_current_cfg = compose_config(layers['web'], None)
        api_active_cfg = compose_config(layers['api'], api_active)
        web_active_cfg = compose_config(layers['web'], web_active)
        require(api_active_cfg['api'].get('image') == api_image and
                web_active_cfg['web'].get('image') == web_image and
                api_active_cfg['api'].get('environment', {}) == api_current_cfg['api'].get('environment', {}) and
                web_active_cfg['web'].get('environment', {}) == web_current_cfg['web'].get('environment', {}),
                'PLANNED_CONFIG_CHANGED')
        api_args = compose_args(layers['api'], api_active)
        web_args = compose_args(layers['web'], web_active)
        command(api_args + ['config', '--quiet'], timeout=60)
        command(web_args + ['config', '--quiet'], timeout=60)
        backups_dir = STATE / ('backup-' + str(int(time.time())))
        backups_dir.mkdir(mode=0o700)
        print('QA525_APPLY: RESPALDANDO_PMGM_Y_KEYCLOAK', flush=True)
        backups = {name: backup_database(name, backups_dir) for name in ('pmgm', 'pmgm_keycloak')}
        history = migration_history()
        installed_path = Path('/opt/centenario/qa-api-refresh/installed.json')
        installed_before = private_bytes(installed_path)
        require(prepare.selection(ready) == receipt.get('selection'), 'GATES_CHANGED_BEFORE_APPLY')
        require(snapshot() == before, 'SERVICE_CHANGED_BEFORE_APPLY')
        changed = False
        installed_changed = False
        try:
            print('QA525_APPLY: ACTUALIZANDO_API_Y_WEB_SIN_MIGRACIONES', flush=True)
            changed = True
            command(api_args + ['up', '--detach', '--no-deps', '--no-build', '--pull', 'never',
                                '--force-recreate', 'api'], timeout=180)
            command(web_args + ['up', '--detach', '--no-deps', '--no-build', '--pull', 'never',
                                '--force-recreate', 'web'], timeout=180)
            wait_health(TARGET)
            validate_runtime(before_env, TARGET, api_image, web_image)
            require(migration_history() == history, 'MIGRATIONS_CHANGED_DURING_APPLY')
            after = snapshot()
            require(all(after[name] == before[name] for name in
                        ('keycloak', 'postgres', 'minio', 'gateway', 'clamav')),
                    'DEPENDENCY_CHANGED')
            result = {'source_sha': TARGET, 'api_image_id': api_image,
                      'web_image_id': web_image, 'previous': before,
                      'backups': backups, 'applied_at': int(time.time()),
                      'scope': 'API_AND_WEB_SYNCHRONIZED_NO_AUTHENTICATED_UAT'}
            installed = {'source_sha': TARGET, 'api_image_id': api_image,
                         'web_image_id': web_image, 'database_migrations': history,
                         'backup_files': backups,
                         'scope': 'API_AND_WEB_SYNCHRONIZED_NO_AUTHENTICATED_UAT',
                         'previous_source_sha': pre.BASE_API,
                         'previous_api_image_id': before['api'][1],
                         'previous_web_image_id': before['web'][1],
                         'smtp_encryption_key_file': '/etc/pmgm/qa-smtp-encryption-key',
                         'master_config_file': '/etc/pmgm/master-qa.env'}
            installed_changed = True
            replace_private_json(installed_path, installed)
            write_private_json(STATE / 'applied.json', result)
            print('QA525_APPLY: API_Y_WEB_SINCRONIZADAS_' + TARGET, flush=True)
            print('QA525_APPLY: MASTER_IDENTIDAD_SMTP_Y_DEPENDENCIAS_CONSERVADOS', flush=True)
            print('RESULT: APPLIED_UAT_PENDING_TIMER_REMAINS_PAUSED')
        except Exception:
            if changed:
                print('QA525_APPLY: REVERSION_API_Y_WEB', flush=True)
                command(compose_args(layers['api']) +
                        ['up', '--detach', '--no-deps', '--no-build', '--pull', 'never',
                         '--force-recreate', 'api'], timeout=180)
                command(compose_args(layers['web']) +
                        ['up', '--detach', '--no-deps', '--no-build', '--pull', 'never',
                         '--force-recreate', 'web'], timeout=180)
                wait_health(pre.BASE_WEB)
                require(inspect('api')['Image'] == before['api'][1] and
                        inspect('web')['Image'] == before['web'][1], 'ROLLBACK_IMAGE_MISMATCH')
                require(environment(inspect('api')['Config'].get('Env') or []) == before_env,
                        'ROLLBACK_ENVIRONMENT_CHANGED')
                if installed_changed:
                    replace_private_bytes(installed_path, installed_before)
                (STATE / 'applied.json').unlink(missing_ok=True)
                print('QA525_APPLY: REVERSION_CONFIRMADA', flush=True)
            raise


if __name__ == '__main__':
    try:
        require(sys.argv[1:] == ['--apply'], 'ONLY_EXPLICIT_APPLY_SUPPORTED')
        apply()
    except Blocked as error:
        print('RESULT: BLOCKED_' + str(error), flush=True)
        sys.exit(1)
    except Exception:
        print('RESULT: BLOCKED_APPLY_FAILED_OR_REVERTED', flush=True)
        sys.exit(1)
