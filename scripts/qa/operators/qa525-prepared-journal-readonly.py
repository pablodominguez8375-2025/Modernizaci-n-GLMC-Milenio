#!/usr/bin/env python3
"""QA525: bounded, read-only inventory of the existing prepared and its journal.

Run only on glmdch after verifying this file's exact SHA-256 against a reviewed
commit. This is a diagnostic, not a release check, approval, or apply gate.
"""
import hashlib
import json
import os
from pathlib import Path
import re
import socket
import stat
import sys

STATE = Path('/opt/centenario/qa525-signed-prepare')
SHA = re.compile(r'[a-f0-9]{40}\Z')
DIGEST = re.compile(r'sha256:[a-f0-9]{64}\Z')
LIMIT = 2_000_000


class Stop(Exception):
    pass


def require(ok, reason):
    if not ok:
        raise Stop(reason)


def pairs_unique(pairs):
    obj = {}
    for key, value in pairs:
        require(key not in obj, 'DUPLICATE_JSON_KEY')
        obj[key] = value
    return obj


def read_private(name):
    require('/' not in name and name not in ('', '.', '..'), 'INVALID_NAME')
    fd = os.open(STATE / name, os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC)
    try:
        meta = os.fstat(fd)
        require(stat.S_ISREG(meta.st_mode) and meta.st_uid == 0
                and stat.S_IMODE(meta.st_mode) == 0o600 and meta.st_nlink == 1
                and 0 < meta.st_size <= LIMIT, 'UNTRUSTED_EVIDENCE_FILE')
        data = os.read(fd, LIMIT + 1)
        require(len(data) == meta.st_size, 'EVIDENCE_CHANGED_DURING_READ')
        obj = json.loads(data, object_pairs_hook=pairs_unique)
        require(type(obj) is dict, 'INVALID_EVIDENCE_OBJECT')
        return obj, hashlib.sha256(data).hexdigest()
    finally:
        os.close(fd)


def main():
    require(os.geteuid() == 0 and socket.gethostname() == 'glmdch', 'WRONG_HOST_OR_UID')
    for folder in (STATE, STATE.parent, STATE.parent.parent):
        meta = folder.lstat()
        require(stat.S_ISDIR(meta.st_mode) and meta.st_uid == 0
                and not (meta.st_mode & 0o022), 'UNTRUSTED_STATE_PATH')
    require(stat.S_IMODE(STATE.stat().st_mode) == 0o700, 'UNTRUSTED_STATE_MODE')
    prepared, prepared_hash = read_private('prepared.json')
    source = prepared.get('source_sha')
    require(type(source) is str and SHA.fullmatch(source), 'PREPARED_SOURCE_INVALID')
    asset = prepared.get('source_asset_sha256')
    require(type(asset) is str and DIGEST.fullmatch(asset), 'PREPARED_ASSET_INVALID')
    journal_name = 'dual-' + source + '.json'
    journal, journal_hash = read_private(journal_name)
    require(journal.get('source_sha') == source, 'JOURNAL_SOURCE_MISMATCH')
    require(type(prepared.get('status')) is str and type(journal.get('status')) is str,
            'STATUS_INVALID')
    names = [p.name for p in STATE.iterdir()]
    other_journals = sorted(n for n in names if re.fullmatch(r'dual-[a-f0-9]{40}\.json', n)
                            and n != journal_name)
    # A status field alone does not prove that an update completed successfully.
    print(json.dumps({
        'diagnostic': 'QA525_PREPARED_JOURNAL_READ_ONLY',
        'prepared': {'source_sha': source, 'status': prepared['status'],
                     'asset_sha256': asset, 'file_sha256': prepared_hash},
        'journal': {'status': journal['status'], 'file_sha256': journal_hash},
        'other_journal_count': len(other_journals),
        'journal_next_present': any(n.startswith('dual-') and n.endswith('.json.next')
                                    for n in names),
        'archive_marker_present': 'expired-prepared-archive-pending.json' in names,
        'reconciliation_marker_present': 'pending-dual-reconciliation.json' in names,
        'may_apply': False,
    }, sort_keys=True))


if __name__ == '__main__':
    try:
        main()
    except (Stop, OSError, ValueError, UnicodeError) as error:
        code = str(error) if isinstance(error, Stop) else type(error).__name__
        print('QA525_PREPARED_JOURNAL=STOP_' + code, file=sys.stderr)
        raise SystemExit(2)
