"""Prepare signed API and WEB images only. Does not expose any deploy operation."""
import contextlib
import fcntl
import hashlib
import importlib.util
import io
import json
import os
import re
import stat
import subprocess
import sys
import tempfile
import time
from pathlib import Path
from urllib.error import HTTPError
from urllib.request import Request, urlopen

TARGET = 'e3e90cbbc21d131a477445b3127b4ce96a7e3605'
TOOLS = Path('/opt/centenario/qa-tools')
STATE = Path('/opt/centenario/qa-api-refresh/prepare-e3e90')
CODE = Path('/opt/centenario/qa-auto-lite/code-4c0101fa439acd52e2f1e78a9f4bfb3b1eb49c4b')
PREFLIGHT_SHA = 'a141fe827ca49d1391577d45880bb9ec6ca4b32f6e76664e26836d7752b4974c'
DEPENDENCIES = {
    'qa-agent-readiness.py': 'afc1d54a90696a4a33bdfe689c47406b5dc0aaf8838426f907761adab20ddd55',
    'qa-lite-compose-adapter.py': '5fa03db393159a49c3606276635248467f6cada8ffdbd5ccaded5afb8b68ed7d',
    'qa-lite-web-build.py': '299aa40b12df1ae615a8aaa1cbc6437effc2fd0bf1d83211b5a97550084c1aef',
    'qa-public-release-download.py': '3fbce9e62961dd23619f320fd5b592b48ea2f04f6aeb35fc48cf4c3e5f5574b5',
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
                'LOCAL_CODE_OR_PARENT_UNTRUSTED')
    require(path.is_dir() if directory else path.is_file(), 'LOCAL_PATH_TYPE')
    if expected:
        require(digest(path) == expected, 'LOCAL_VERIFIER_SHA_CHANGED')


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    m = importlib.util.module_from_spec(spec)
    sys.modules[name] = m
    spec.loader.exec_module(m)
    return m


def fetch(path):
    require(path.startswith('/') and '//' not in path and '@' not in path, 'API_PATH_INVALID')
    url = 'https://api.github.com/repos/pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio' + path
    try:
        with urlopen(Request(url, headers={'User-Agent': 'PMGM-QA525-Prepare',
                     'Accept': 'application/vnd.github+json'}), timeout=15) as response:
            require(response.geturl() == url, 'API_REDIRECT')
            return json.load(response)
    except HTTPError as e:
        reset = e.headers.get('X-RateLimit-Reset', '')
        if e.code in (403, 429) and e.headers.get('X-RateLimit-Remaining') == '0' and reset.isdigit():
            raise Blocked('GITHUB_RATE_LIMIT_RESET_' + reset) from None
        raise Blocked('GITHUB_HTTP_' + str(e.code)) from None


def selection(ready):
    tip = fetch('/branches/dev')['commit']['sha']
    if tip != TARGET:
        comparison = fetch('/compare/' + TARGET + '...' + tip)
        require(comparison.get('status') == 'ahead' and
                comparison.get('merge_base_commit', {}).get('sha') == TARGET,
                'TARGET_NOT_ON_DEV')
    return ready.evaluate(lambda p: {'commit': {'sha': TARGET}} if p == '/branches/dev' else fetch(p))


def source_verifier(staging, archive, expected, package):
    def verify(current, sha):
        require(current == package and sha == TARGET, 'SOURCE_BINDING_CHANGED')
        # Recheck Sigstore identity/digest AND the complete file manifest each time.
        with tempfile.TemporaryDirectory(prefix='.verify-', dir=STATE) as tmp:
            checked = Path(tmp) / 'package'
            staging.stage(archive, expected, TARGET, checked, tokenless=True)
            require((checked / 'MANIFEST.sha256').read_bytes() ==
                    (package / 'MANIFEST.sha256').read_bytes(), 'MANIFEST_BINDING_CHANGED')
        staging.verify_manifest(package, TARGET)
        return True
    return verify


def unchanged_schema(package, pre):
    receipt = json.loads(pre.private(Path('/opt/centenario/qa-api-refresh/installed.json')))
    ids = []
    for p in (package / 'backend/src/PMGM.Api/Migrations').glob('*.cs'):
        ids += re.findall(r'\[Migration\("([^"]+)"\)\]', p.read_text())
    require(len(ids) == len(set(ids)) == 98 and set(ids) == set(receipt['database_migrations']),
            'TARGET_MIGRATION_SET_CHANGED')
    # Also reject edited migration bodies/program even when IDs stay unchanged.
    comparison = fetch('/compare/' + pre.BASE_API + '...' + TARGET)
    require(comparison.get('merge_base_commit', {}).get('sha') == pre.BASE_API and
            len(comparison.get('files', [])) < 300 and
            all(not f['filename'].startswith('backend/src/PMGM.Api/Migrations/') and
                f['filename'] != 'backend/src/PMGM.Api/Program.cs'
                for f in comparison.get('files', [])), 'TARGET_SCHEMA_CODE_CHANGED')


def snapshot(pre):
    out = {}
    for name in ('api', 'web', 'keycloak', 'postgres', 'minio', 'gateway', 'clamav'):
        c = json.loads(pre.command(['docker', 'inspect', pre.PROJECT + '-' + name + '-1']))[0]
        require(c['State']['Status'] == 'running', 'SERVICE_NOT_RUNNING')
        out[name] = (c['Id'], c['Image'])
    return out


def build_api(adapter, package, verifier):
    require(verifier(package, TARGET), 'SOURCE_NOT_VERIFIED')
    with tempfile.TemporaryDirectory(prefix='.api-build-', dir=STATE) as tmp:
        iid = Path(tmp) / 'image-id'
        adapter.docker(['build', '--pull=false', '--iidfile', str(iid),
                        '--label', 'org.opencontainers.image.revision=' + TARGET,
                        '--file', str(package / 'backend/Dockerfile'), str(package / 'backend')],
                       timeout=1200)
        image = iid.read_text().strip()
    require(re.fullmatch(r'sha256:[a-f0-9]{64}', image), 'API_IMAGE_ID_INVALID')
    require(adapter.docker(['image', 'inspect', '--format', '{{.Id}}', image]) == image and
            adapter.docker(['image', 'inspect', '--format',
                            '{{index .Config.Labels "org.opencontainers.image.revision"}}', image]) == TARGET,
            'API_IMAGE_IDENTITY_CHANGED')
    require(verifier(package, TARGET), 'SOURCE_CHANGED')
    return image


def prepare():
    require(os.geteuid() == 0, 'ROOT_REQUIRED')
    trusted(Path(__file__))
    require(Path(__file__) == TOOLS / 'qa525-prepare-e3e90.py', 'INSTALL_OPERATOR_FIRST')
    for name, sha in DEPENDENCIES.items():
        trusted(CODE / name, sha)
    trusted(TOOLS / 'qa525-update-readiness.py', PREFLIGHT_SHA)
    pre = module('qa525_preflight', TOOLS / 'qa525-update-readiness.py')
    # Acquire the locks used by existing updater/identity/web operators.
    with contextlib.ExitStack() as resources:
        for p in (Path('/opt/centenario/qa-api-refresh/operation.lock'),
                  Path('/opt/centenario/qa-auto-lite/web-update.lock'),
                  Path('/run/lock/pmgm-master-qa-604.lock'),
                  Path('/run/lock/pmgm-qa754-web-hotfix.lock'),
                  Path('/run/lock/pmgm-qa754-api-activation.lock')):
            fd = os.open(p, os.O_RDWR | os.O_CREAT | os.O_NOFOLLOW, 0o600)
            resources.callback(os.close, fd)
            st = os.fstat(fd)
            require(stat.S_ISREG(st.st_mode) and st.st_uid == 0 and not st.st_mode & 0o077,
                    'LOCK_UNTRUSTED')
            fcntl.flock(fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
        with contextlib.redirect_stdout(io.StringIO()):
            pre.check()
        before = snapshot(pre)
        os.umask(0o077)
        if not STATE.exists():
            STATE.mkdir(mode=0o700)
        trusted(STATE, directory=True)
        require(stat.S_IMODE(STATE.stat().st_mode) == 0o700, 'STATE_NOT_PRIVATE')
        require(not (STATE / 'prepared.json').exists(), 'ALREADY_PREPARED_DO_NOT_REPEAT')
        ready = module('qa525_ready', CODE / 'qa-agent-readiness.py')
        download = module('qa525_download', CODE / 'qa-public-release-download.py')
        staging = module('qa525_staging', CODE / 'qa-signed-staging.py')
        builder = module('qa525_builder', CODE / 'qa-lite-web-build.py')
        selected = selection(ready)
        print('QA525_PREPARE: EXACT_TARGET_GATES_PASS', flush=True)
        archive = STATE / (TARGET + '.zip')
        if not archive.exists():
            download.download(selected, archive)
        trusted(archive)
        expected = selected['artifact_sha256']
        require('sha256:' + digest(archive) == expected, 'ENVELOPE_DIGEST_CHANGED')
        package = STATE / 'package'
        if not package.exists():
            staging.stage(archive, expected, TARGET, package, tokenless=True)
        trusted(package, directory=True)
        verifier = source_verifier(staging, archive, expected, package)
        require(verifier(package, TARGET), 'SIGNED_SOURCE_INVALID')
        unchanged_schema(package, pre)
        print('QA525_PREPARE: SIGNED_SOURCE_AND_UNCHANGED_SCHEMA_VERIFIED', flush=True)
        print('QA525_PREPARE: BUILDING_API_NO_DEPLOYMENT', flush=True)
        api_image = build_api(builder.transport, package, verifier)
        print('QA525_PREPARE: BUILDING_WEB_NO_DEPLOYMENT', flush=True)
        web = builder.prepare_web(package, TARGET, verifier)
        require(selection(ready) == selected, 'GATES_CHANGED_DURING_BUILD')
        with contextlib.redirect_stdout(io.StringIO()):
            pre.check()
        require(snapshot(pre) == before, 'RUNNING_SERVICE_CHANGED_DURING_BUILD')
        for name in ('api', 'web'):
            builder.transport.docker(['image', 'inspect', before[name][1]])
        result = {'source_sha': TARGET, 'api_image_id': api_image, 'web_image_id': web['web_image_id'],
                  'baseline': before, 'selection': selected, 'web_build': web,
                  'prepared_at': int(time.time()), 'scope': 'BUILD_ONLY_NO_DEPLOYMENT_NO_UAT'}
        fd = os.open(STATE / 'prepared.json', os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
        with os.fdopen(fd, 'w') as stream:
            json.dump(result, stream, sort_keys=True)
            stream.flush()
            os.fsync(stream.fileno())
        print('QA525_PREPARE: BOTH_IMAGES_PINNED_ORIGINAL_IMAGES_RETAINED')
        print('RESULT: PREPARED_API_AND_WEB_SERVICES_UNCHANGED_APPLY_NOT_IMPLEMENTED')


if __name__ == '__main__':
    try:
        require(sys.argv[1:] == ['--prepare'], 'ONLY_EXPLICIT_PREPARE_SUPPORTED')
        prepare()
    except Blocked as error:
        print('RESULT: BLOCKED_' + str(error), flush=True)
        sys.exit(1)
    except Exception:
        print('RESULT: BLOCKED_PREPARE_FAILED_NO_SERVICE_CHANGE_REQUESTED', flush=True)
        sys.exit(1)
