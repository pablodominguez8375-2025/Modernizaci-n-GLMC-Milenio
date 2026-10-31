#!/usr/bin/env python3
"""One reviewed QA update command; no automatic review, renewal or retry."""
import argparse
import importlib.util
import json
import os
from pathlib import Path
import re
import secrets
import socket
import stat
import sys
import time

HERE = Path(__file__).resolve().parent


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
    return 'UPDATE_NOT_COMPLETED'


def trusted(path):
    for item in (path, *path.parents):
        info = item.lstat()
        require(info.st_uid == 0 and not stat.S_ISLNK(info.st_mode)
                and not info.st_mode & 0o022, 'UPDATE_CODE_UNTRUSTED')


def load_entry():
    path = HERE / 'qa525-supervised-dual-entrypoint.py'
    trusted(Path(__file__))
    trusted(path)
    spec = importlib.util.spec_from_file_location('qa525_update_entry', path)
    entry = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = entry
    spec.loader.exec_module(entry)
    for p in entry.MODULES:
        entry.trusted(p)
    return entry


def packet_contract(packet, now):
    require(type(packet) is dict and set(packet) == {'review', 'post_db', 'schema_mode'},
            'UPDATE_REVIEW_PACKET_FIELDS_INVALID')
    require(packet['schema_mode'] in ('member_history_postdb', 'support00_postdb'),
            'UPDATE_SCHEMA_REQUIRES_SEPARATE_PLAN')
    review, record = packet['review'], packet['post_db']
    require(type(review) is dict and type(record) is dict
            and review.get('source_sha') == record.get('source_sha')
            and review.get('release_asset_sha256') == record.get('release_asset_sha256'),
            'UPDATE_REVIEW_PACKET_CUT_MISMATCH')
    for obj, key in ((review, 'reviewed_at'), (record, 'approved_at')):
        require(type(obj.get(key)) is int and 0 <= now - obj[key] < 1800,
                'UPDATE_REVIEW_PACKET_EXPIRED')
    return min(review['reviewed_at'], record['approved_at']) + 1800


def previous_prepared_is_applied(prepared, journal, live):
    require(type(prepared) is dict and type(journal) is dict
            and journal.get('status') == 'APPLIED'
            and journal.get('source_sha') == prepared.get('source_sha')
            and prepared.get('status') == 'PREPARED_API_WEB_SIGNATURE_VERIFIED_NO_DEPLOYMENT',
            'PREVIOUS_UPDATE_REQUIRES_RECONCILIATION')
    for name in ('api', 'web'):
        image = prepared.get(name + '_image_id')
        require(journal.get('new_images', {}).get(name) == image
                and live.get(name, [None, None, None])[1:3] == [image, 'running'],
                'PREVIOUS_UPDATE_NOT_LIVE')


def verify_technical_reference(fetch, reference, source, sections):
    match = re.fullmatch(r'https://github\.com/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/issues/525#issuecomment-([1-9][0-9]{0,19})', reference or '')
    require(match is not None, 'UPDATE_TECHNICAL_REFERENCE_INVALID')
    comment = fetch('/issues/comments/' + match[1])
    require(type(comment) is dict and comment.get('user', {}).get('login') ==
            'pablodominguez8375-2025' and type(comment.get('body')) is str
            and len(comment['body']) < 16384, 'UPDATE_TECHNICAL_REVIEW_AUTHOR_INVALID')
    try:
        review = json.loads(comment['body'])
    except (ValueError, TypeError):
        raise Blocked('UPDATE_TECHNICAL_REVIEW_NOT_STRUCTURED') from None
    require(type(review) is dict and review.get('schema') == 'qa525-reviewed-cut-v1'
            and review.get('source_sha') == source.get('source_sha')
            and review.get('release_asset_sha256') == source.get('release_asset_sha256')
            and review.get('review_kind') == 'automated_ai'
            and review.get('review_mode') == 'single_operator_qa'
            and review.get('reviewed_sections') == sorted(sections)
            and review.get('status') == 'REVIEWED_FOR_QA_UPDATE',
            'UPDATE_TECHNICAL_REVIEW_NOT_THIS_CUT')


def make_review_packet(entry, args, modules):
    core, build, approval, policy, signed, adapter, gate, probe = modules
    require(args.source and re.fullmatch(r'[a-f0-9]{40}', args.source)
            and args.digest and re.fullmatch(r'sha256:[a-f0-9]{64}', args.digest)
            and args.g6_review and args.g6_review.is_absolute() and args.operator_name,
            'UPDATE_REVIEW_ARGUMENTS_MISSING')
    source = signed.gate.evaluate(signed.gate.readiness.fetch_public, expected_sha=args.source)
    require(source['release_asset_sha256'] == args.digest, 'UPDATE_REVIEW_DIGEST_MISMATCH')
    verify_technical_reference(signed.gate.readiness.fetch_public,
                               args.technical_review_reference, source, entry.REVIEWED)
    support00 = args.schema_mode == 'support00_postdb'
    if support00:
        entry.trusted(HERE / 'qa525-support00-schema-gate.py')
        gate = entry.load('qa525_update_packet_gate', HERE / 'qa525-support00-schema-gate.py')
    decision, state = entry.current_change_review(policy, signed, source, gate)
    require(state == getattr(gate, 'SCHEMA_REVIEW_STATE', 'PR801_REVIEWED_ADDITIVE_NEEDS_DBA'),
            'UPDATE_REVIEW_UNKNOWN_SCHEMA')
    g6 = entry.private_json(args.g6_review)
    entry.validate_g6(g6, int(time.time()))
    require(g6.get('g6_offhost_sha256_verified') is True, 'UPDATE_REVIEW_OFFHOST_NOT_VERIFIED')
    entry.single_operator_runtime(adapter.docker)
    if support00:
        completion = entry.private_json(entry.STATE / 'support00-completed.json')
        require(completion.get('status') == 'SUPPORT00_EF100_VERIFIED'
                and completion.get('migration_id') == gate.MIGRATION_ID
                and completion.get('migration_blob_sha') == gate.MIGRATION_BLOB,
                'UPDATE_REVIEW_MIGRATION_NOT_COMPLETED')
        completed = completion['completed_at']
    else:
        prior = entry.private_json(entry.SINGLE_OPERATOR_REVIEW)
        entry.validate_single_operator_review(prior)
        require(prior.get('migration_id') == gate.MIGRATION_ID
                and prior.get('migration_blob_sha') == gate.MIGRATION_BLOB,
                'UPDATE_REVIEW_PR801_RECORD_INVALID')
        completed = prior['migration_completed_at']
    now = int(time.time())
    review = dict(source_sha=source['source_sha'], release_asset_sha256=args.digest,
        baseline_sha=entry.BASELINE, reviewed_sections=sorted(entry.REVIEWED),
        sensitive_reasons=decision['reasons'], no_new_ef_migrations=False,
        runtime_migration_flags_disabled=True, legacy_master_and_smtp_preserved=True,
        reviewed_at=now, **{k: g6[k] for k in ('g6_archive_name', 'g6_archive_sha256',
            'g6_drive_file_id', 'g6_offhost_sha256_verified', 'g6_restore_report_name')})
    post = dict(source_sha=source['source_sha'], release_asset_sha256=args.digest,
        baseline_sha=entry.BASELINE, migration_id=gate.MIGRATION_ID,
        migration_blob_sha=gate.MIGRATION_BLOB, dbcontext_blob_sha=gate.CONTEXT_BLOB,
        migration_completed_at=completed, approved_at=now, additive_up_only=True,
        previous_api_forward_compatible=True, post_db_g6_archive_name=review['g6_archive_name'],
        post_db_g6_archive_sha256=review['g6_archive_sha256'],
        post_db_g6_restore_report_name=review['g6_restore_report_name'],
        review_mode='single_operator_qa', environment='QA_SYNTHETIC', host='glmdch',
        scope=getattr(gate, 'SINGLE_OPERATOR_SCOPE', 'POST_PR801_API_WEB_ONLY_NO_MIGRATION'),
        sponsor_approval_reference=entry.SINGLE_OPERATOR_DECISION,
        sponsor_accepts_single_operator_risk=True, independent_human_review=False,
        technical_review_kind='automated_ai', operator_name=args.operator_name,
        technical_review_reference=args.technical_review_reference)
    return {'schema_mode': args.schema_mode, 'review': review, 'post_db': post}, (source, decision, state)


def install_review_packet(entry, packet, modules, *, verified=None):
    core, build, approval, policy, signed, adapter, gate, probe = modules
    support00 = packet['schema_mode'] == 'support00_postdb'
    if support00:
        for filename in ('qa525-support00-schema-gate.py', 'qa525-support00-post-db-readonly.py'):
            entry.trusted(HERE / filename)
        gate = entry.load('qa525_update_support_gate', HERE / 'qa525-support00-schema-gate.py')
        probe = entry.load('qa525_update_support_probe', HERE / 'qa525-support00-post-db-readonly.py')
    expires = packet_contract(packet, int(time.time()))
    review, post = packet['review'], packet['post_db']
    if verified is None:
        source = signed.gate.evaluate(signed.gate.readiness.fetch_public,
                                     expected_sha=review.get('source_sha'))
        decision, schema = entry.current_change_review(policy, signed, source, gate)
    else:
        # Ephemeral full gate/comment/diff from make_review_packet in THIS
        # locked command only. Not a CLI option, file cache or persisted claim.
        source, decision, schema = verified
    require(schema == getattr(gate, 'SCHEMA_REVIEW_STATE', 'PR801_REVIEWED_ADDITIVE_NEEDS_DBA'),
            'UPDATE_UNKNOWN_SCHEMA')
    entry.approved_review(review, source, decision, int(time.time()), schema_approved=True)
    entry.validate_single_operator_review(post, scope=getattr(gate, 'SINGLE_OPERATOR_SCOPE',
        'POST_PR801_API_WEB_ONLY_NO_MIGRATION'))
    entry.validate_g6(review, int(time.time()))
    entry.single_operator_runtime(adapter.docker)
    live = adapter.metadata()
    require(not (entry.STATE / ('dual-' + source['source_sha'] + '.json')).exists(),
            'UPDATE_TARGET_ALREADY_ATTEMPTED')
    entry.reviewed_layers(core, {n: live[n][7].split(',') for n in core.TARGETS}, entry.WORKDIR)
    receipt = entry.STATE / 'prepared.json'
    # No interrupted/rolled-back operation is automatically retried, archived,
    # replaced or called successful, even for a different target SHA.
    for p in entry.STATE.glob('dual-*.json'):
        journal = entry.private_json(p)
        require(journal.get('status') == 'APPLIED', 'UPDATE_JOURNAL_REQUIRES_RECONCILIATION')
    if receipt.exists() or receipt.is_symlink():
        previous = entry.private_json(receipt)
        journal = entry.private_json(entry.STATE / ('dual-' + previous['source_sha'] + '.json'))
        previous_prepared_is_applied(previous, journal, live)
        require(previous['source_sha'] != source['source_sha'], 'UPDATE_ALREADY_APPLIED')
        require(entry.health(previous['source_sha']), 'PREVIOUS_UPDATE_HEALTH_UNVERIFIED')
    paths = [entry.REVIEW, entry.SINGLE_OPERATOR_REVIEW, approval.APPROVAL, receipt]
    archive = entry.STATE / ('review-archive-' + secrets.token_hex(12))
    archive.mkdir(mode=0o700)
    moved, created = [], []
    try:
        for p in paths:
            if p.exists() or p.is_symlink():
                entry.private_json(p)
                dest = archive / p.name
                p.rename(dest)
                moved.append((p, dest))
        now = int(time.time())
        require(now < expires, 'UPDATE_REVIEW_PACKET_EXPIRED')
        cut = dict(target_sha=source['source_sha'], release_asset_sha256=source['release_asset_sha256'],
            scope=approval.SCOPE, environment=approval.ENVIRONMENT, action=approval.ACTION,
            nonce=secrets.token_hex(32), issued_at=now, expires_at=expires, operator_reviewed=True)
        for p, obj in ((entry.REVIEW, review), (entry.SINGLE_OPERATOR_REVIEW, post),
                       (approval.APPROVAL, cut)):
            fd = os.open(p, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
            created.append((p, os.fstat(fd).st_ino))
            with os.fdopen(fd, 'w') as stream:
                json.dump(obj, stream, sort_keys=True)
                stream.flush()
                os.fsync(stream.fileno())
        entry.validate_post_db_dba(review, source, int(time.time()), adapter.docker,
                                  gate, probe, single_operator=True)
        approval.read_root_approval()
        require(adapter.metadata() == live, 'UPDATE_RUNTIME_CHANGED')
    except BaseException:
        for p, inode in reversed(created):
            if p.exists() and p.lstat().st_ino == inode:
                p.unlink()
        for p, saved in reversed(moved):
            require(not p.exists() and not p.is_symlink(), 'UPDATE_REVIEW_RESTORE_COLLISION')
            saved.rename(p)
        raise
    return support00



def expired_prepared_archive_contract(prepared, review, post, cut, *, sha, digest,
                                      baseline, now, journals, nonce_consumed):
    """Pure, fail-closed archive decision. This never grants deploy authority."""
    require(type(prepared) is dict and type(review) is dict
            and type(post) is dict and type(cut) is dict,
            'RECOVERY_RECORDS_INVALID')
    require(type(sha) is str and re.fullmatch(r'[a-f0-9]{40}', sha)
            and type(digest) is str and re.fullmatch(r'sha256:[a-f0-9]{64}', digest),
            'RECOVERY_CUT_INVALID')
    require(prepared.get('status') == 'PREPARED_API_WEB_SIGNATURE_VERIFIED_NO_DEPLOYMENT'
            and prepared.get('source_sha') == sha
            and prepared.get('source_asset_sha256') == digest
            and prepared.get('schema_mode') == 'support00_postdb'
            and prepared.get('review_mode') == 'single_operator_qa'
            and prepared.get('images_built') is True
            and prepared.get('services_unchanged') is True
            and prepared.get('may_apply') is False
            and prepared.get('baseline') == baseline,
            'RECOVERY_PREPARED_OR_BASELINE_INVALID')
    require(review.get('source_sha') == sha
            and review.get('release_asset_sha256') == digest
            and post.get('source_sha') == sha
            and post.get('release_asset_sha256') == digest
            and post.get('review_mode') == 'single_operator_qa'
            and cut.get('target_sha') == sha
            and cut.get('release_asset_sha256') == digest
            and cut.get('operator_reviewed') is True
            and cut.get('scope') == 'LOCAL_SUPERVISION_MATCH_ONLY_NO_APPLY'
            and cut.get('action') == 'first-qa-api-web-supervised'
            and cut.get('environment') == 'QA_SYNTHETIC',
            'RECOVERY_REVIEWS_NOT_THIS_CUT')
    nonce = cut.get('nonce')
    require(type(nonce) is str and re.fullmatch(r'[0-9a-f]{64}', nonce)
            and nonce_consumed is False, 'RECOVERY_NONCE_CONSUMED_OR_INVALID')
    times = (review.get('reviewed_at'), post.get('approved_at'),
             cut.get('issued_at'), cut.get('expires_at'))
    require(all(type(t) is int and t >= 0 for t in times)
            and times[3] > times[2]
            and times[3] <= times[2] + 1800
            and now >= max(times[0] + 1800, times[1] + 1800, times[3]),
            'RECOVERY_REVIEW_NOT_FULLY_EXPIRED')
    require(type(journals) in (tuple, list)
            and all(type(j) is dict and j.get('status') == 'APPLIED'
                    and j.get('source_sha') != sha for j in journals),
            'RECOVERY_JOURNAL_REQUIRES_RECONCILIATION')
    return True


def archive_expired_prepared(entry, args, modules):
    """Archive an unconsumed, expired QA preparation; no image/apply changes.

    Only a NEW exact-HEAD reviewed release may be prepared afterwards.
    This action never renews timestamps, claims backup freshness or consumes
    the old nonce. The original artifacts remain in root-owned history.
    """
    import hashlib
    core, build, approval, _, _, adapter, _, _ = modules
    require(args.source and re.fullmatch(r'[a-f0-9]{40}', args.source)
            and args.digest and re.fullmatch(r'sha256:[a-f0-9]{64}', args.digest)
            and args.operator_name == 'soporte@glmdch',
            'RECOVERY_EXPLICIT_TARGET_AND_OPERATOR_REQUIRED')
    entry.single_operator_runtime(adapter.docker)
    live = adapter.metadata()
    require(all(live[n][2] == 'running' for n in core.TARGETS),
            'RECOVERY_SERVICES_NOT_RUNNING')
    entry.reviewed_layers(core, {n: live[n][7].split(',') for n in core.TARGETS},
                          entry.WORKDIR)
    receipt = entry.STATE / 'prepared.json'
    prepared = entry.private_json(receipt)
    review = entry.private_json(entry.REVIEW)
    post = entry.private_json(entry.SINGLE_OPERATOR_REVIEW)
    cut = entry.private_json(approval.APPROVAL)
    target_journal = entry.STATE / ('dual-' + args.source + '.json')
    require(not target_journal.exists() and not target_journal.is_symlink(),
            'RECOVERY_TARGET_JOURNAL_PRESENT')
    journals = [entry.private_json(p) for p in entry.STATE.glob('dual-*.json')]
    nonce = cut.get('nonce', '')
    consumed = entry.STATE / ('approval-consumed-' + nonce)
    before = build.snapshot(adapter.docker)
    expired_prepared_archive_contract(
        prepared, review, post, cut, sha=args.source, digest=args.digest,
        baseline=before, now=int(time.time()), journals=journals,
        nonce_consumed=(consumed.exists() or consumed.is_symlink()))
    for name in core.TARGETS:
        image = prepared.get(name + '_image_id')
        require(type(image) is str and re.fullmatch(r'sha256:[a-f0-9]{64}', image)
                and adapter.docker(['image', 'inspect', '--format', '{{.Id}}', image]) == image
                and adapter.docker(['image', 'inspect', '--format',
                    '{{index .Config.Labels "org.opencontainers.image.revision"}}',
                    image]) == args.source,
                'RECOVERY_PREBUILT_IMAGE_IDENTITY_CHANGED')
    old_sha = adapter.docker(['image', 'inspect', '--format',
        '{{index .Config.Labels "org.opencontainers.image.revision"}}',
        live['web'][1]])
    require(type(old_sha) is str and re.fullmatch(r'[a-f0-9]{40}', old_sha)
            and old_sha != args.source and entry.health(old_sha),
            'RECOVERY_OLD_WEB_NOT_HEALTHY')
    require(adapter.metadata() == live and build.snapshot(adapter.docker) == before,
            'RECOVERY_RUNTIME_DRIFT')
    paths = [receipt, entry.REVIEW, entry.SINGLE_OPERATOR_REVIEW, approval.APPROVAL]
    hashes = {}
    for p in paths:
        entry.private_json(p)
        with p.open('rb') as stream:
            hashes[p.name] = hashlib.file_digest(stream, 'sha256').hexdigest()
    archive = entry.STATE / ('review-archive-' + secrets.token_hex(12))
    archive.mkdir(mode=0o700)
    moved = []
    try:
        for p in paths:
            destination = archive / p.name
            p.rename(destination)
            moved.append((p, destination))
        with os.fdopen(os.open(archive / 'expired-prepared-archive.json',
                             os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW,
                             0o600), 'w') as stream:
            json.dump({'status': 'EXPIRED_PREPARED_ARCHIVED_NO_DEPLOYMENT',
                       'source_sha': args.source,
                       'source_asset_sha256': args.digest,
                       'last_running_web_sha': old_sha,
                       'archived_at': int(time.time()),
                       'files_sha256': hashes,
                       'operator': args.operator_name}, stream, sort_keys=True)
            stream.flush()
            os.fsync(stream.fileno())
        for directory in (archive, entry.STATE):
            fd = os.open(directory, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW)
            try:
                os.fsync(fd)
            finally:
                os.close(fd)
    except BaseException:
        for p, archived in reversed(moved):
            if archived.exists() and not p.exists() and not p.is_symlink():
                archived.rename(p)
        raise
    return {'status': 'EXPIRED_PREPARED_ARCHIVED_NO_DEPLOYMENT',
            'source_sha': args.source, 'may_apply': False}


def invoke(entry, action, support00):
    previous = sys.argv
    sys.argv = [str(HERE / 'qa525-supervised-dual-entrypoint.py'), action, '--single-operator-qa']
    if support00:
        sys.argv.append('--support00-postdb')
    try:
        return entry.main()
    finally:
        sys.argv = previous


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    choice = parser.add_mutually_exclusive_group(required=True)
    choice.add_argument('--status', action='store_true')
    choice.add_argument('--run-reviewed', type=Path, metavar='ROOT_REVIEW_PACKET')
    choice.add_argument('--apply-prepared', action='store_true')
    choice.add_argument('--archive-expired-prepared', action='store_true')
    choice.add_argument('--install-launcher', action='store_true')
    choice.add_argument('--review-and-run', action='store_true')
    parser.add_argument('--source')
    parser.add_argument('--digest')
    parser.add_argument('--technical-review-reference')
    parser.add_argument('--g6-review', type=Path)
    parser.add_argument('--operator-name')
    parser.add_argument('--schema-mode', choices=['member_history_postdb', 'support00_postdb'],
                        default='support00_postdb')
    args = parser.parse_args(argv)
    require(os.geteuid() == 0 and socket.gethostname() == 'glmdch', 'UPDATE_ONLY_GLM DCH_ROOT'.replace(' ', '_'))
    entry = load_entry()
    if args.install_launcher:
        launcher = Path('/usr/local/sbin/pmgm-qa-update')
        trusted(launcher.parent)
        fd = os.open(launcher, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o700)
        with os.fdopen(fd, 'w') as stream:
            stream.write('#!/usr/bin/python3\nimport os,sys\n')
            stream.write('os.execv("/usr/bin/python3", ["/usr/bin/python3", "-B", '
                         + repr(str(Path(__file__).resolve())) + ', *sys.argv[1:]])\n')
            stream.flush()
            os.fsync(stream.fileno())
        return {'status': 'QA525_UPDATE_LAUNCHER_INSTALLED_NO_UPDATE', 'may_apply': False}
    entry.trusted(entry.STATE, directory=True)
    require(stat.S_IMODE(entry.STATE.stat().st_mode) == 0o700, 'UPDATE_STATE_NOT_PRIVATE')
    modules = [entry.load('qa525_update_module_' + str(i), p) for i, p in enumerate(entry.MODULES)]
    build, adapter = modules[1], modules[5]
    if args.status:
        with build.exclusive():
            live = adapter.metadata()
            return {'status': 'QA525_UPDATE_STATUS_READ_ONLY',
                'services_running': all(live[n][2] == 'running' for n in modules[0].TARGETS),
                'prepared_exists': (entry.STATE / 'prepared.json').exists(),
                'may_apply': False}
    if args.archive_expired_prepared:
        with build.exclusive():
            return archive_expired_prepared(entry, args, modules)
    if args.apply_prepared:
        prepared = entry.private_json(entry.STATE / 'prepared.json')
        mode = prepared.get('schema_mode', 'member_history_postdb')
        require(mode in ('member_history_postdb', 'support00_postdb'), 'UPDATE_PREPARED_SCHEMA_UNKNOWN')
        return invoke(entry, '--apply-supervised', mode == 'support00_postdb')
    with build.exclusive():
        if args.review_and_run:
            packet, verified = make_review_packet(entry, args, modules)
            support00 = install_review_packet(entry, packet, modules, verified=verified)
        else:
            require(args.run_reviewed.is_absolute(), 'UPDATE_PACKET_PATH_NOT_ABSOLUTE')
            packet = entry.private_json(args.run_reviewed, maximum=16384)
            support00 = install_review_packet(entry, packet, modules)
    prepared = invoke(entry, '--prepare-reviewed', support00)
    require(prepared.get('status') == 'PREPARED_REVIEWED_API_WEB_NO_DEPLOYMENT',
            'UPDATE_PREPARE_NOT_CONFIRMED')
    return invoke(entry, '--apply-supervised', support00)


if __name__ == '__main__':
    try:
        print(json.dumps(main(), sort_keys=True))
    except Exception as exc:
        print(json.dumps({'status': 'BLOCKED_QA525_UPDATE',
                          'code': public_error_code(exc),
                          'may_apply': False}, sort_keys=True))
        raise SystemExit(2)
