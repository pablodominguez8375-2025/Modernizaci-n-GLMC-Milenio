"""One supervised QA operation; uses the installed signed-source verifier."""
import base64, secrets, stat, contextlib, fcntl, hashlib, importlib.util, json, os, re, socket, subprocess, sys, time
from pathlib import Path

TARGET = '4d60a37be6d69ba4335a494ab7682fa06972da71'
BASELINE_SOURCE = 'e3880db57973459b3572896d7d6c8ee541095cf0'
PROJECT = 'centenario-qa-lab2'
API = PROJECT+'-api-1'
PG = PROJECT+'-postgres-1'
OLD_API = None
BACKUP_LOADER = Path('/opt/centenario/qa-auto-lite/run-with-backup-20261022.py')
BACKUP_LOADER_SHA = 'b31b67a2a0d35cecd8e12e8b5fbcc072814a7fbb308aa9bf41c6213af6a38ce7'
STATE = Path('/opt/centenario/qa-api-refresh')
CODE = Path('/opt/centenario/qa-auto-lite/code-4c0101fa439acd52e2f1e78a9f4bfb3b1eb49c4b')
MIGRATIONS = {'20261026000000_AddSupportCorrections': 'e73cbe40b0806a0f6fc2b250af73d679191e7578d3a1b194e129a413b1c3c5f8', '20261026120000_AddSupportCorrectionDetail': 'f4851cdbd95fb05061c0b32f343d2d030ca23039b2e92eee84fecd362dbc3b32'}
PHASE = 'PREFLIGHT'
ACTIVE_CONTEXTS = {
 'PmgmDbContext', 'BootstrapDbContext', 'DocumentManagementDbContext',
 'GrandArchiveDbContext', 'NotificationDbContext', 'CalendarDbContext',
 'RegimenInteriorDbContext', 'CandidateIntakeDbContext', 'AdmissionsDbContext',
}
INACTIVE_MIGRATIONS = {
 '20260912140000_AddAnonymousLodgeBallots': 'af4d893fdc5a34dcabd117daf980c7aa2bb04f36e43ae8b477857f7fbaa61abd',
 '20260912141000_AddBallotProcedureNumber': 'c5ee43d9f7be21c79f1a49949c4f47c88bc4f433e46672835af7655043e52697',
}

SMTP_KEY = Path('/etc/pmgm/qa-smtp-encryption-key')
SMTP_ENV = Path('/etc/pmgm/srv01.env')
SMTP_VARIABLE = 'SystemMail__EncryptionKey'

def checked_smtp_key(value):
    require(isinstance(value, str), 'SMTP_ENCRYPTION_KEY_INVALID')
    try:
        raw = base64.b64decode(value, validate=True)
    except Exception:
        raise RuntimeError('SMTP_ENCRYPTION_KEY_INVALID') from None
    require(len(raw) == 32, 'SMTP_ENCRYPTION_KEY_INVALID')
    return base64.b64encode(raw).decode()


def private_text(path):
    fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW)
    with os.fdopen(fd, 'r') as stream:
        info = os.fstat(stream.fileno())
        require(stat.S_ISREG(info.st_mode) and info.st_uid == 0 and
                stat.S_IMODE(info.st_mode) == 0o600 and info.st_nlink == 1,
                'SMTP_KEY_FILE_NOT_PRIVATE')
        return stream.read()


def runtime_smtp_key(inst):
    data = json.loads(inst.adapter.docker(['inspect','--format','{{json .Config.Env}}',API]))
    require(isinstance(data, list), 'API_ENVIRONMENT_INVALID')
    values = [v.split('=',1)[1] for v in data if isinstance(v,str) and v.startswith(SMTP_VARIABLE+'=')]
    require(len(values) <= 1, 'DUPLICATE_SMTP_ENCRYPTION_KEY')
    return values[0] if values else ''


def smtp_key(inst):
    previous = runtime_smtp_key(inst)
    candidates = [checked_smtp_key(previous)] if previous else []
    if SMTP_KEY.exists() or SMTP_KEY.is_symlink():
        candidates.append(checked_smtp_key(private_text(SMTP_KEY).strip()))
    if SMTP_ENV.exists() or SMTP_ENV.is_symlink():
        rows = [line.split('=',1)[1].strip() for line in private_text(SMTP_ENV).splitlines()
                if line.strip().startswith('PMGM_SMTP_ENCRYPTION_KEY=')]
        require(len(rows) <= 1, 'DUPLICATE_SMTP_ENCRYPTION_KEY')
        if rows and rows[0]:
            value = rows[0]
            if len(value) > 1 and value[0] in ('"', "'") and value[-1] == value[0]:
                value = value[1:-1]
            if value:
                candidates.append(checked_smtp_key(value))
    require(len(set(candidates)) <= 1, 'SMTP_ENCRYPTION_KEYS_CONFLICT')
    if candidates:
        key = candidates[0]
    else:
        # Never create a replacement key over an existing protected credential.
        count = sql(inst, '''SELECT count(*) FROM core.institutional_rule_settings
WHERE "Code" = 'system.mail.smtp_secret.protected' ''')
        require(count == '0', 'EXISTING_SMTP_CREDENTIAL_REQUIRES_ORIGINAL_KEY')
        key = base64.b64encode(secrets.token_bytes(32)).decode()
    parent = SMTP_KEY.parent
    info = parent.lstat()
    require(not parent.is_symlink() and parent.is_dir() and info.st_uid == 0 and
            not info.st_mode & 0o022, 'SMTP_KEY_DIRECTORY_NOT_TRUSTED')
    if not SMTP_KEY.exists():
        fd = os.open(SMTP_KEY, os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW | os.O_WRONLY, 0o600)
        with os.fdopen(fd,'w') as stream:
            stream.write(key+'\n')
            stream.flush()
            os.fsync(stream.fileno())
    require(checked_smtp_key(private_text(SMTP_KEY).strip()) == key,
            'SMTP_KEY_PERSISTENCE_FAILED')
    return key, previous


def verify_smtp(inst, key):
    require(runtime_smtp_key(inst) == checked_smtp_key(key), 'SMTP_ENCRYPTION_NOT_ACTIVE')
    log('SMTP_ENCRYPTION_READY_LOCAL_ONLY')

def migration_plan(package):
    program = (package/'backend/src/PMGM.Api/Program.cs').read_text()
    pairs = re.findall(r'var\s+(\w+)\s*=\s*scope\.ServiceProvider\.GetRequiredService<(\w+)>\(\);\s*await\s+\1\.Database\.MigrateAsync\(\);', program)
    require(len(pairs) == 9 and {context for _,context in pairs} == ACTIVE_CONTEXTS,
            'MIGRATOR_CONTEXTS_CHANGED')
    expected, inactive = set(), set()
    for source in (package/'backend/src/PMGM.Api/Migrations').glob('*.cs'):
        text = source.read_text()
        ids = re.findall(r'\[Migration\("([^"]+)"\)\]', text)
        if not ids:
            continue
        contexts = re.findall(r'\[DbContext\(typeof\((\w+)\)\)\]', text)
        require(len(ids) == len(contexts) == 1, 'MIGRATION_METADATA_INVALID')
        name, context = ids[0], contexts[0]
        require(name not in expected|inactive, 'DUPLICATE_MIGRATION')
        if context in ACTIVE_CONTEXTS:
            expected.add(name)
        else:
            require(context == 'LodgeManagementDbContext' and
                INACTIVE_MIGRATIONS.get(name) == hashlib.sha256(source.read_bytes()).hexdigest(),
                'UNREVIEWED_INACTIVE_MIGRATION')
            inactive.add(name)
    require(inactive == set(INACTIVE_MIGRATIONS) and len(expected) == 94,
            'MIGRATION_SET_CHANGED')
    return expected, inactive


def reviewed_plan(package, applied):
    migration_dir = package/'backend/src/PMGM.Api/Migrations'
    for name, digest in MIGRATIONS.items():
        require(hashlib.sha256((migration_dir/(name+'.cs')).read_bytes()).hexdigest() == digest,
                'UNREVIEWED_MIGRATION')
    expected, inactive = migration_plan(package)
    require(applied <= expected|inactive and (expected-applied) <= set(MIGRATIONS),
            'UNEXPECTED_DATABASE_MIGRATIONS')
    return expected, inactive


def log(phase):
    global PHASE
    PHASE = phase
    print('QA_'+phase, flush=True)

def require(ok, code):
    if not ok:
        raise RuntimeError(code)

def run(args, timeout=90, stdin=None, stdout=None):
    result = subprocess.run(args, stdin=stdin or subprocess.DEVNULL,
        stdout=stdout or subprocess.PIPE, stderr=subprocess.PIPE, timeout=timeout)
    require(result.returncode == 0, 'LOCAL_COMMAND_FAILED')
    return result.stdout.decode().strip() if stdout is None else ''

def system(*args):
    return run(['/usr/bin/systemctl', *args])

def load_installer():
    for p in [BACKUP_LOADER, *BACKUP_LOADER.parents]:
        st = p.lstat()
        require(not p.is_symlink() and st.st_uid == 0 and not st.st_mode & 0o022, 'CODE_NOT_TRUSTED')
    require(hashlib.sha256(BACKUP_LOADER.read_bytes()).hexdigest() == BACKUP_LOADER_SHA,
            'BACKUP_REFERENCE_CODE_CHANGED')
    spec = importlib.util.spec_from_file_location('api_refresh_backup_loader', BACKUP_LOADER)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module.load_installer()


def api_baseline(inst):
    receipt = inst.read_json(STATE/'installed.json')
    source, image = receipt.get('source_sha'), receipt.get('api_image_id')
    require(source in {BASELINE_SOURCE, TARGET} and isinstance(image, str) and
            re.fullmatch(r'sha256:[a-f0-9]{64}', image) is not None, 'API_RECEIPT_INVALID')
    require(inst.adapter.docker(['inspect','--format','{{.Image}}',API]) == image,
            'API_BASELINE_CHANGED')
    require(inst.adapter.docker(['inspect','--format',
        '{{index .Config.Labels "org.opencontainers.image.revision"}}',API]) == source,
        'API_SOURCE_CHANGED')
    if source == TARGET:
        verify_smtp(inst, smtp_key(inst)[0])
        require(set(receipt.get('database_migrations',[])) <= history(inst) and
                len(receipt.get('database_migrations',[])) == 94, 'TARGET_MIGRATION_RECEIPT_INVALID')
        if not inst.public_health(TARGET):
            require(receipt.get('scope') == 'API_HEALTHY_WEB_PENDING' and inst.public_health(),
                    'EXISTING_TARGET_NOT_HEALTHY')
            resume_web(inst, receipt)
        elif receipt.get('scope') == 'API_HEALTHY_WEB_PENDING':
            receipt['scope'] = 'API_AND_WEB_SYNCHRONIZED_NO_AUTHENTICATED_UAT'
            inst.write_json(STATE/'installed.json', receipt)
        log('API_Y_WEB_YA_SINCRONIZADAS_'+TARGET)
        return None
    return image



def resume_web(inst, receipt):
    selection = select(inst)
    before = untouched(inst)
    timer = subprocess.run(['/usr/bin/systemctl','is-active','--quiet','pmgm-qa-lite.timer']).returncode == 0
    try:
        system('stop','pmgm-qa-lite.timer')
        for unit in ['pmgm-qa-lite.service','pmgm-qa-lite-firstboot.service']:
            require(subprocess.run(['/usr/bin/systemctl','is-active','--quiet',unit]).returncode != 0,
                    'ANOTHER_WEB_UPDATE_ACTIVE')
        inst.ready.evaluate_latest_validated = lambda fetch: selection
        log('RETOMANDO_WEB_CON_API_NUEVA_YA_VERIFICADA')
        result = inst.install()
        require(result.get('source_sha') == TARGET and inst.public_health(TARGET), 'WEB_SHA_MISMATCH')
        require(inst.adapter.docker(['inspect','--format','{{.Image}}',API]) == receipt['api_image_id'],
                'API_IMAGE_MISMATCH')
        require(untouched(inst) == before, 'DEPENDENCY_CHANGED')
        receipt['scope'] = 'API_AND_WEB_SYNCHRONIZED_NO_AUTHENTICATED_UAT'
        inst.write_json(STATE/'installed.json', receipt)
    finally:
        if timer:
            system('start','pmgm-qa-lite.timer')


def sql(inst, statement):
    return inst.adapter.docker(['exec', PG, 'sh', '-c',
        'exec psql -X -v ON_ERROR_STOP=1 -At -U "$POSTGRES_USER" -d pmgm -c "$1"',
        'qa-refresh', statement])

def history(inst):
    return set(sql(inst, 'SELECT "MigrationId" FROM public."__EFMigrationsHistory" ORDER BY 1').splitlines())

def untouched(inst):
    return {k:v for k,v in inst.adapter.metadata().items() if k not in {'api','web'}}

def compose(inst, overlay):
    return ['compose', '--project-name', PROJECT, '--project-directory', str(inst.builder.COMPOSE.parent),
            '--env-file', '/opt/centenario/qa-auto-lite/empty.env',
            '-f', str(inst.builder.COMPOSE), '-f', str(overlay)]

def change_api(inst, overlay):
    return inst.adapter.docker([*compose(inst, overlay), 'up', '--detach', '--no-deps',
        '--no-build', '--pull', 'never', '--force-recreate', 'api'], timeout=120)

def healthy(inst):
    deadline = time.monotonic()+90
    while time.monotonic() < deadline:
        if inst.public_health():
            return
        time.sleep(3)
    raise RuntimeError('PUBLIC_HEALTH_FAILED')

def build_api(inst, package, verifier):
    require(verifier(package, TARGET) is True, 'SOURCE_INVALID')
    iid = STATE/'candidate-image-id'
    iid.unlink(missing_ok=True)
    inst.adapter.docker(['build', '--pull=false', '--iidfile', str(iid),
        '--label', 'org.opencontainers.image.revision='+TARGET,
        '--file', str(package/'backend/Dockerfile'), str(package/'backend')], timeout=1200)
    image = iid.read_text().strip()
    require(re.fullmatch(r'sha256:[a-f0-9]{64}', image) is not None, 'IMAGE_ID_INVALID')
    require(inst.adapter.docker(['image','inspect','--format',
        '{{index .Config.Labels "org.opencontainers.image.revision"}}', image]) == TARGET, 'IMAGE_LABEL_INVALID')
    require(verifier(package, TARGET) is True, 'SOURCE_CHANGED')
    return image

def backup(inst):
    log('RESPALDANDO_PMGM_ANTES_DE_MIGRAR')
    dump = STATE/('pmgm-before-'+str(time.time_ns())+'.dump')
    with dump.open('xb') as output:
        os.chmod(dump, 0o600)
        run(['/usr/bin/docker','--host','unix:///var/run/docker.sock','exec',PG,
            'sh','-c','exec pg_dump -U "$POSTGRES_USER" -d pmgm -Fc'], timeout=180, stdout=output)
    require(dump.stat().st_size > 100, 'BACKUP_EMPTY')
    with dump.open('rb') as source:
        run(['/usr/bin/docker','--host','unix:///var/run/docker.sock','exec','-i',PG,
            'pg_restore','--list'], timeout=60, stdin=source)
    return dump

def select(inst):
    def pinned(path):
        return {'commit': {'sha': TARGET}} if path == '/branches/dev' else inst.ready.fetch_public(path)
    deadline = time.monotonic()+1800
    while True:
        try:
            selection = inst.ready.evaluate(pinned)
            inst.selection_still_valid(selection)
            return selection
        except inst.ready.GateFailure:
            require(time.monotonic() < deadline, 'EXACT_SHA_GATES_NOT_READY')
            log('ESPERANDO_PRUEBAS_DEL_SHA_'+TARGET[:7])
            time.sleep(120)

def operation_main(resources):
    global OLD_API
    require(os.geteuid() == 0 and socket.gethostname() == 'glmdch', 'HOST_NOT_AUTHORIZED')
    os.umask(0o077)
    STATE.mkdir(mode=0o700, exist_ok=True)
    require(not STATE.is_symlink() and STATE.stat().st_uid == 0, 'STATE_NOT_TRUSTED')
    STATE.chmod(0o700)
    operation = resources.enter_context(open(STATE/'operation.lock','a'))
    fcntl.flock(operation, fcntl.LOCK_EX | fcntl.LOCK_NB)
    inst = load_installer()
    inst.builder.build_inputs()
    inst.backup_verified()
    require(inst.public_health(), 'INITIAL_HEALTH_FAILED')
    require(not (inst.STATE/'paused').exists(), 'QA_PAUSED_BY_OPERATOR')
    require(not (inst.STATE/'pending.json').exists(), 'PENDING_WEB_RECOVERY')
    OLD_API = api_baseline(inst)
    if OLD_API is None:
        return
    selection = select(inst)
    timer = subprocess.run(['/usr/bin/systemctl','is-active','--quiet','pmgm-qa-lite.timer']).returncode == 0
    lock = None
    changed = False
    api_verified = False
    db_started = False
    original_web = None
    rollback = STATE/'api-previous.json'
    try:
        system('stop','pmgm-qa-lite.timer')
        for unit in ['pmgm-qa-lite.service','pmgm-qa-lite-firstboot.service']:
            require(subprocess.run(['/usr/bin/systemctl','is-active','--quiet',unit]).returncode != 0,
                    'ANOTHER_WEB_UPDATE_ACTIVE')
        lock = open(inst.STATE/'web-update.lock','a')
        fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        inst.binding_for(False)
        require(inst.adapter.docker(['inspect','--format','{{.Image}}',API]) == OLD_API, 'API_BASELINE_CHANGED')
        original_web = inst.read_json(inst.STATE/'installed.json')
        if original_web['source_sha'] != TARGET:
            require(inst.check_ancestry(original_web['source_sha'],TARGET), 'WEB_NEWER_THAN_TARGET')
        before = untouched(inst)
        log('VERIFICANDO_PAQUETE_FIRMADO_'+TARGET[:7])
        package, verifier = inst.verified_candidate(selection)
        applied = history(inst)
        expected, inactive = reviewed_plan(package, applied)
        print('QA_MIGRACIONES_ACTIVAS='+str(len(expected))+' PENDIENTES='+str(len(expected-applied)), flush=True)
        log('CONSTRUYENDO_API_VERIFICADA')
        image = build_api(inst, package, verifier)
        smtp_secret, previous_smtp = smtp_key(inst)
        active = STATE/('api-'+TARGET+'.json')
        inst.write_json(active, {'services': {'api': {'image': image, 'environment':{SMTP_VARIABLE:smtp_secret}}, 'migrator': {'image': image}}})
        inst.write_json(rollback, {'services': {'api': {'image': OLD_API, 'environment':{SMTP_VARIABLE:previous_smtp}}}})
        inst.adapter.docker([*compose(inst,active),'config','--quiet'])
        inst.selection_still_valid(selection)
        inst.backup_verified()
        require(verifier(package,TARGET) is True, 'SOURCE_REVERIFY_FAILED')
        require(untouched(inst) == before, 'DEPENDENCY_CHANGED')
        dump = backup(inst)
        log('EJECUTANDO_MIGRADOR_CAMBIOS_COMPATIBLES_REVISADOS')
        db_started = True
        inst.adapter.docker([*compose(inst,active),'run','--rm','--no-deps','--pull','never','migrator'],timeout=300)
        require(expected <= history(inst), 'MIGRATIONS_INCOMPLETE')
        log('APLICANDO_PERMISOS_BD2_EXISTENTES')
        inst.adapter.docker([*compose(inst,active),'run','--rm','--no-deps','--pull','never','db-grants'],timeout=90)
        log('ACTUALIZANDO_SOLO_API')
        changed = True
        change_api(inst,active)
        healthy(inst)
        require(inst.adapter.docker(['inspect','--format','{{.Image}}',API]) == image, 'API_IMAGE_MISMATCH')
        verify_smtp(inst, smtp_secret)
        api_verified = True
        require(untouched(inst) == before, 'DEPENDENCY_CHANGED')
        api_receipt = {'source_sha':TARGET,'api_image_id':image,
            'database_migrations':sorted(expected),'backup_file':str(dump),
            'scope':'API_HEALTHY_WEB_PENDING','backup_sha256':inst.BACKUP_SHA,
            'offhost_file':inst.OFFHOST_FILE,'previous_source_sha':BASELINE_SOURCE,
            'previous_api_image_id':OLD_API,'smtp_encryption_key_file':str(SMTP_KEY)}
        inst.write_json(STATE/'installed.json', api_receipt)
        # The trusted web coordinator retains its journal, rollback, marker and baseline.
        lock.close()
        lock = None
        inst.ready.evaluate_latest_validated = lambda fetch: selection
        log('SINCRONIZANDO_WEB_AL_MISMO_SHA')
        result = inst.install()
        require(result.get('source_sha') == TARGET and inst.public_health(TARGET), 'WEB_SHA_MISMATCH')
        require(untouched(inst) == before, 'DEPENDENCY_CHANGED')
        inst.write_json(STATE/'installed.json', {'source_sha':TARGET,'api_image_id':image,
            'database_migrations':sorted(expected),'backup_file':str(dump),
            'scope':'API_AND_WEB_SYNCHRONIZED_NO_AUTHENTICATED_UAT',
            'backup_sha256':inst.BACKUP_SHA, 'offhost_file':inst.OFFHOST_FILE,
            'previous_source_sha':BASELINE_SOURCE, 'previous_api_image_id':OLD_API,'smtp_encryption_key_file':str(SMTP_KEY)})
        log('API_Y_WEB_SINCRONIZADAS_'+TARGET)
    except Exception:
        if changed and not api_verified:
            log('RECUPERANDO_IMAGEN_API_ANTERIOR')
            change_api(inst,rollback)
            healthy(inst)
            log('API_ANTERIOR_RECUPERADA')
        elif api_verified:
            # Web coordinator owns its rollback/journal; preserve a healthy API.
            log('API_NUEVA_SANA_SINCRONIZACION_WEB_NO_CONFIRMADA')
        if db_started:
            log('SIN_DOWN_DE_BASE_DATOS_TABLAS_ADICIONALES_CONSERVADAS')
        raise
    finally:
        if lock is not None:
            lock.close()
        if timer:
            system('start','pmgm-qa-lite.timer')

def main():
    with contextlib.ExitStack() as resources:
        operation_main(resources)

if __name__ == '__main__':
    try:
        main()
    except Exception as error:
        print('QA_ACTUALIZACION_NO_CONFIRMADA_ETAPA='+PHASE, flush=True)
        if type(error) is RuntimeError:
            print('QA_CODIGO='+str(error), flush=True)
        sys.exit(1)



