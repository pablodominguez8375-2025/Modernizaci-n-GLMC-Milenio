#!/usr/bin/env python3
"""Explicit prepare/apply for exactly PR813. Never starts the API or runs Down."""
import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import socket
import stat
import sys
import tempfile
import time

HERE = Path(__file__).resolve().parent
APPROVAL = Path('/etc/pmgm/qa525-support00-migration-approved.json')
SPONSOR_REFERENCE = ('https://github.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/'
                     'issues/525#issuecomment-6538679406')
SCOPE = 'QA_GLM DCH_EF99_TO_100_SUPPORT00_ONLY'.replace(' ', '_')
FIELDS = {'source_sha', 'release_asset_sha256', 'reviewed_at', 'expires_at', 'nonce',
          'operator_reviewed', 'review_mode', 'technical_review_reference',
          'sponsor_approval_reference', 'scope', 'g6'}


class Blocked(Exception):
    pass


def require(value, code):
    if not value:
        raise Blocked(code)


def public_error_code(exc):
    try:
        code = str(exc) if isinstance(exc, Blocked) else None
        entry = sys.modules.get('qa525_update_entry')
        if code is None and entry is not None:
            code = entry.public_failure_code(exc)
        if (type(code) is str and code != 'SUPERVISED_APPLY_NOT_COMPLETED'
                and re.fullmatch(r'[A-Z][A-Z0-9_]{2,100}', code)):
            return code
    except Exception:
        pass
    return 'MIGRATION_NOT_CONFIRMED_RECONCILE_REQUIRED'


def approval_contract(record, source, now):
    require(type(record) is dict and set(record) == FIELDS, 'MIGRATION_APPROVAL_FIELDS_INVALID')
    require(record['source_sha'] == source.get('source_sha')
            and record['release_asset_sha256'] == source.get('release_asset_sha256')
            and record['scope'] == SCOPE and record['operator_reviewed'] is True
            and record['review_mode'] == 'single_operator_qa'
            and record['sponsor_approval_reference'] == SPONSOR_REFERENCE,
            'MIGRATION_SCOPE_NOT_REVIEWED')
    require(type(record['technical_review_reference']) is str and re.fullmatch(
        r'https://github\.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/525#issuecomment-[1-9][0-9]{0,19}',
        record['technical_review_reference']) is not None, 'MIGRATION_REVIEW_REFERENCE_INVALID')
    require(type(record['nonce']) is str and re.fullmatch(r'[a-f0-9]{64}', record['nonce']),
            'MIGRATION_NONCE_INVALID')
    issued, expires = record['reviewed_at'], record['expires_at']
    require(type(issued) is int and type(expires) is int and 0 <= now - issued < 1800
            and issued < expires <= issued + 1800 and now < expires,
            'MIGRATION_APPROVAL_EXPIRED')
    require(type(record['g6']) is dict and record['g6'].get('g6_offhost_sha256_verified') is True,
            'MIGRATION_OFFHOST_RECOVERY_UNVERIFIED')


def fingerprint(value):
    return hashlib.sha256(json.dumps(value, sort_keys=True).encode()).hexdigest()


def write_new(path, value):
    fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
    with os.fdopen(fd, 'w') as stream:
        json.dump(value, stream, sort_keys=True)
        stream.flush()
        os.fsync(stream.fileno())
    directory = os.open(path.parent, os.O_DIRECTORY)
    try:
        os.fsync(directory)
    finally:
        os.close(directory)


def run_arguments(image, network, envfile, nonce):
    require(re.fullmatch(r'sha256:[a-f0-9]{64}', image or '')
            and re.fullmatch(r'[a-f0-9]{64}', network or '')
            and re.fullmatch(r'[a-f0-9]{64}', nonce or ''), 'MIGRATION_RUNTIME_BINDING_INVALID')
    require(Path(envfile).is_absolute(), 'MIGRATION_ENV_FILE_NOT_ABSOLUTE')
    return ['run', '--rm', '--name', 'qa525-support00-' + nonce[:16],
            '--network', network, '--read-only', '--tmpfs', '/tmp:rw,noexec,nosuid,size=64m',
            '--cap-drop', 'ALL', '--security-opt', 'no-new-privileges',
            '--pids-limit', '128', '--memory', '512m', '--log-driver', 'none',
            '--env-file', str(envfile), image]


def preflight(entry, record, source, modules, gate, probe):
    core, build, approval, policy, signed, adapter, member, oldprobe = modules
    now = int(time.time())
    approval_contract(record, source, now)
    entry.validate_g6(record['g6'], now)
    # A new DB intervention needs a current recovery checkpoint, not merely
    # an older archive that happens to remain within the binary48h window.
    import calendar
    name = record['g6']['g6_archive_name']
    stamp = calendar.timegm(time.strptime(name[25:40], '%Y%m%dT%H%M%S'))
    require(0 <= now - stamp < 1800, 'MIGRATION_FRESH_RECOVERY_CHECKPOINT_REQUIRED')
    entry.single_operator_runtime(adapter.docker)
    before = adapter.metadata()
    require(set(before) == set(core.TARGETS + core.DEPENDENCIES + core.ONESHOTS),
            'MIGRATION_UNEXPECTED_SERVICE_SET')
    entry.reviewed_layers(core, {n: before[n][7].split(',') for n in core.TARGETS}, entry.WORKDIR)
    entry.checked_compose_env_file(core.ENV_FILE)
    receipt = entry.private_json(Path('/opt/centenario/qa-api-refresh/installed.json'))
    oldprobe.inspect(receipt, adapter.docker, member)
    # This operator is intentionally limited to the observed empty support
    # case; preexisting/ambiguous organizations need a separate review.
    sql = ('BEGIN READ ONLY; SELECT count(*) FROM core.organizations '
           'WHERE lower("Type")=\'support\' OR "Number"=\'00\'; COMMIT;')
    counts = adapter.docker(['exec', 'centenario-qa-lab2-postgres-1', 'sh', '-c',
        'exec psql -X -qAt -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d pmgm -c "$1"',
        'qa525-support00-before', sql])
    require(counts == '0', 'MIGRATION_SUPPORT00_NOT_EMPTY')
    decision, state = entry.current_change_review(policy, signed, source, gate)
    require(state == gate.SCHEMA_REVIEW_STATE, 'MIGRATION_SOURCE_NOT_EXACT_REVIEWED_DIFF')
    return before, receipt['database_migrations'] + [member.MIGRATION_ID]


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    choices = parser.add_mutually_exclusive_group(required=True)
    choices.add_argument('--prepare-reviewed', action='store_true')
    choices.add_argument('--migrate-reviewed', action='store_true')
    choices.add_argument('--status', action='store_true')
    args = parser.parse_args(argv)
    require(os.geteuid() == 0 and socket.gethostname() == 'glmdch', 'MIGRATION_ONLY_GLM DCH_ROOT'.replace(' ', '_'))
    sys.dont_write_bytecode = True
    # Trust before importing any project helper, including all parents.
    for p in (Path(__file__), HERE / 'qa525-update.py'):
        for q in (p, *p.parents):
            info = q.lstat()
            require(info.st_uid == 0 and not stat.S_ISLNK(info.st_mode)
                    and not info.st_mode & 0o022, 'MIGRATION_CODE_UNTRUSTED')
    spec = importlib.util.spec_from_file_location('qa525_migration_update', HERE / 'qa525-update.py')
    update = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(update)
    entry = update.load_entry()
    entry.trusted(entry.STATE, directory=True)
    require(stat.S_IMODE(entry.STATE.stat().st_mode) == 0o700, 'MIGRATION_STATE_NOT_PRIVATE')
    for filename in ('qa525-support00-schema-gate.py', 'qa525-support00-post-db-readonly.py'):
        entry.trusted(HERE / filename)
    modules = [entry.load('qa525_migration_' + str(i), p) for i, p in enumerate(entry.MODULES)]
    core, build, approval, policy, signed, adapter, member, oldprobe = modules
    gate = entry.load('qa525_migration_support_gate', HERE / 'qa525-support00-schema-gate.py')
    probe = entry.load('qa525_migration_support_probe', HERE / 'qa525-support00-post-db-readonly.py')
    with build.exclusive():
        if args.status:
            receipt = entry.private_json(Path('/opt/centenario/qa-api-refresh/installed.json'))
            history = oldprobe.checked_query(adapter.docker, oldprobe.HISTORY_SQL)
            require(len(history) in (99, 100), 'MIGRATION_HISTORY_REQUIRES_RECONCILIATION')
            state = (oldprobe.inspect(receipt, adapter.docker, member) if len(history) == 99
                     else probe.inspect(receipt, adapter.docker, gate))
            return {'status': 'SUPPORT00_SCHEMA_STATUS_READ_ONLY', 'history_count': state['history_count'],
                    'may_apply': False, 'may_migrate': False}
        record = entry.private_json(APPROVAL, maximum=16384)
        source = signed.gate.evaluate(signed.gate.readiness.fetch_public,
                                     expected_sha=record.get('source_sha'))
        before, history = preflight(entry, record, source, modules, gate, probe)
        update.verify_technical_reference(signed.gate.readiness.fetch_public,
            record['technical_review_reference'], source, entry.REVIEWED)
        completion_path = entry.STATE / 'support00-completed.json'
        require(not completion_path.exists() and not completion_path.is_symlink(),
                'MIGRATION_COMPLETION_ALREADY_EXISTS_RECONCILE_REQUIRED')
        target = source['source_sha']
        prepared_path = entry.STATE / ('support00-prepared-' + target + '.json')
        journal_path = entry.STATE / ('support00-migration-' + target + '.json')
        used_path = entry.STATE / ('support00-nonce-' + record['nonce'])
        require(not journal_path.exists() and not journal_path.is_symlink()
                and not used_path.exists() and not used_path.is_symlink(),
                'MIGRATION_PREVIOUS_ATTEMPT_REQUIRES_RECONCILIATION')
        # Cryptographic verification on BOTH prepare and migrate; a prepared
        # receipt never stands in for source, signature, schema or backup proof.
        with tempfile.TemporaryDirectory(prefix='.support00-source-', dir=entry.STATE) as tmp:
            package = Path(tmp) / 'signed'
            result = signed.verify_current(package, selection=source)
            require(result['source_sha'] == target, 'MIGRATION_SIGNATURE_TARGET_CHANGED')
            gate.reviewed_migration_source((package / gate.MIGRATION_PATH).read_text())
            recipe = package / 'scripts/qa/operators/support00-migrator/Dockerfile'
            require(recipe.is_file(), 'MIGRATION_RUNNER_NOT_IN_SIGNED_PACKAGE')
            if args.prepare_reviewed:
                require(not prepared_path.exists() and not prepared_path.is_symlink(),
                        'MIGRATION_PREPARED_ALREADY_EXISTS')
                iid = Path(tmp) / 'iid'
                adapter.docker(['build', '--pull=false', '--iidfile', str(iid),
                    '--label', 'org.opencontainers.image.revision=' + target,
                    '--file', str(recipe), str(package)], timeout=1200)
                image = iid.read_text().strip()
                require(core.IMAGE.fullmatch(image), 'MIGRATION_IMAGE_INVALID')
                require(signed.gate.evaluate(signed.gate.readiness.fetch_public,
                    expected_sha=target) == source, 'MIGRATION_SOURCE_CHANGED_DURING_BUILD')
                after, current_history = preflight(entry, record, source, modules, gate, probe)
                require(after == before and current_history == history, 'MIGRATION_BASELINE_CHANGED')
                write_new(prepared_path, dict(source_sha=target,
                    release_asset_sha256=source['release_asset_sha256'], image_id=image,
                    before_fingerprint=fingerprint(before), history=history,
                    review_fingerprint=fingerprint(record), status='PREPARED_NOT_MIGRATED'))
                return {'status': 'SUPPORT00_MIGRATOR_PREPARED_NO_DB_CHANGE', 'source_sha': target}
        prepared = entry.private_json(prepared_path)
        require(prepared.get('status') == 'PREPARED_NOT_MIGRATED'
                and prepared.get('source_sha') == target
                and prepared.get('release_asset_sha256') == source['release_asset_sha256']
                and prepared.get('before_fingerprint') == fingerprint(before)
                and prepared.get('history') == history
                and prepared.get('review_fingerprint') == fingerprint(record),
                'MIGRATION_PREPARED_EVIDENCE_CHANGED')
        image = prepared.get('image_id')
        require(type(image) is str and core.IMAGE.fullmatch(image)
                and adapter.docker(['image', 'inspect', '--format', '{{.Id}}', image]) == image
                and adapter.docker(['image', 'inspect', '--format',
                    '{{index .Config.Labels "org.opencontainers.image.revision"}}', image]) == target,
                'MIGRATION_IMAGE_IDENTITY_CHANGED')
        migration = json.loads(adapter.docker(['inspect', 'centenario-qa-lab2-migrator-1']))[0]
        labels = migration['Config'].get('Labels', {})
        require(labels.get('com.docker.compose.project') == core.PROJECT
                and labels.get('com.docker.compose.service') == 'migrator'
                and migration['State']['Status'] == 'exited' and migration['State']['ExitCode'] == 0,
                'MIGRATION_DDL_CONTAINER_IDENTITY_INVALID')
        settings = dict(x.split('=', 1) for x in migration['Config'].get('Env', []) if '=' in x)
        connection = settings.get('ConnectionStrings__MainDatabase')
        require(type(connection) is str and connection and '\n' not in connection and '\r' not in connection,
                'MIGRATION_DDL_CONNECTION_UNAVAILABLE')
        api_networks = before['api'][9]
        ddl_networks = migration['NetworkSettings']['Networks']
        require(len(api_networks) == len(ddl_networks) == 1
                and set(api_networks) == set(ddl_networks), 'MIGRATION_NETWORK_CHANGED')
        network = next(iter(api_networks.values()))['NetworkID']
        require(next(iter(ddl_networks.values()))['NetworkID'] == network, 'MIGRATION_NETWORK_CHANGED')
        approval_contract(record, source, int(time.time()))
        require(signed.gate.evaluate(signed.gate.readiness.fetch_public, expected_sha=target) == source
                and adapter.metadata() == before, 'MIGRATION_ACTIVATION_EVIDENCE_CHANGED')
        with tempfile.TemporaryDirectory(prefix='.support00-env-', dir=entry.STATE) as tmp:
            envfile = Path(tmp) / 'migration.env'
            fd = os.open(envfile, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
            with os.fdopen(fd, 'w') as stream:
                stream.write('QA525_DDL_CONNECTION=' + connection + '\n')
                stream.write('QA525_EXPECTED_EF_HISTORY=' + json.dumps(history) + '\n')
                stream.flush()
                os.fsync(stream.fileno())
            write_new(used_path, {'source_sha': target})
            # Durable PENDING before the first DB mutation. Never remove it on
            # failure or retry implicitly; reconcile actual PostgreSQL first.
            write_new(journal_path, dict(source_sha=target, status='PENDING',
                nonce=record['nonce'], image_id=image, started_at=int(time.time()),
                backup_reference=record['g6']['g6_archive_sha256']))
            output = adapter.docker(run_arguments(image, network, envfile, record['nonce']), timeout=300)
            require(json.loads(output) == {'status': 'SUPPORT00_EF100_MIGRATED'},
                    'MIGRATION_RUNNER_NOT_CONFIRMED_RECONCILE_REQUIRED')
        state = probe.inspect(entry.private_json(Path('/opt/centenario/qa-api-refresh/installed.json')),
                              adapter.docker, gate)
        require(state['history_count'] == 100 and adapter.metadata() == before,
                'MIGRATION_POSTCONDITION_FAILED_RECONCILE_REQUIRED')
        completed_at = int(time.time())
        # Completion receipt is separate and O_EXCL; PENDING is preserved as
        # start evidence. A completion receipt alone cannot open API deployment.
        write_new(entry.STATE / 'support00-completed.json',
            dict(source_sha=target, status='SUPPORT00_EF100_VERIFIED', completed_at=completed_at,
                 image_id=image, migration_id=gate.MIGRATION_ID, migration_blob_sha=gate.MIGRATION_BLOB))
        return {'status': 'SUPPORT00_EF100_VERIFIED_POST_DB_RECOVERY_REQUIRED',
                'source_sha': target, 'api_web_unchanged': True, 'may_apply': False}


if __name__ == '__main__':
    try:
        print(json.dumps(main(), sort_keys=True))
    except Exception as exc:
        print(json.dumps({'status': 'BLOCKED_QA525_SUPPORT00_MIGRATION',
            'code': public_error_code(exc),
            'may_apply': False}, sort_keys=True))
        raise SystemExit(2)
