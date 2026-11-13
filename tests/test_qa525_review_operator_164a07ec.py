"""Sandbox checks for separate review operators; no Docker, root, host, or network.

The evidence reader's UID/GID expectation is substituted ONLY in test copies so
root custody remains an explicitly unproven physical property in this sandbox.
"""
import base64
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]
OPS = ROOT / 'ops/qa525/164a07ec/review'
SHA = '164a07ecd36eb45428b6aeac4854a17e339ca525'
ARCHIVE = 'centenario-qa-g6-v2-CIFRADO-20261113T165400Z.tar.gz'
DIGEST = 'b0c77c1c26ba48e1c6942abab3ea5d5b1a9aaa3228cbd65753bc274a775276fe'
REPO = 'b4df75c52a71050a4bbf493d25a7cd5dd5921d1fdb4ab8226eaf7b6b7ffb65ce'
EPOCH = '75870a7a926569c94e853a2a44f0a580'
DRIVE = '1THaJaMm0sKRi1_VGp1eqOBuyzgQP54aT'


def canonical(o):
    return (json.dumps(o, sort_keys=True, separators=(',', ':'), ensure_ascii=True) + '\n').encode()


class ReviewOperators(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.base = Path(self.tmp.name)
        self.receipt = self.base / 'receipt.json'
        self.restore = self.base / 'isolated-restore-v2-20261113T165400Z.report.json'
        self.review = self.base / 'review.json'
        self.r = {'schema': 'qa525-g6-v2-receipt', 'archive_name': ARCHIVE,
                  'sha256': DIGEST, 'status': 'ENCRYPTED_LOCAL_ARCHIVE_READY_OFFHOST_PENDING',
                  'restic_repo_id': REPO, 'key_epoch': EPOCH}
        self.s = {'schema': 'qa525-g6-v2-restore',
                  'result': 'G6_SAME_HOST_ISOLATED_RESTORE_PASS_NOT_DISASTER_RECOVERY',
                  'archive_filename': ARCHIVE, 'archive_sha256': DIGEST,
                  'restic_repo_id': REPO, 'key_epoch': EPOCH,
                  'postgres_tables_by_database': {'pmgm': 120, 'pmgm_keycloak': 100},
                  'silo_objects_verified': 34, 'production_touched': False}
        self.v = {'schema': 'qa525-g6-v2-review', 'g6_archive_name': ARCHIVE,
                  'g6_archive_sha256': DIGEST, 'g6_restore_report_name': self.restore.name,
                  'g6_offhost_sha256_verified': True, 'g6_drive_file_id': DRIVE,
                  'restic_repo_id': REPO, 'key_epoch': EPOCH}
        self.put(self.receipt, self.r)
        self.put(self.restore, self.s)
        host = (OPS / 'qa525-g6-review-go-host-164a07ec.sh').read_text()
        self.reader = host.split('validate_evidence() {', 1)[1].split("<<'PY'\n", 1)[1].split('\nPY\n', 1)[0]
        self.assertEqual(self.reader.count('st.st_uid==st.st_gid==0'), 1)
        self.reader = self.reader.replace('st.st_uid==st.st_gid==0',
                                         'st.st_uid==os.getuid() and st.st_gid==os.getgid()')

    def put(self, path, obj):
        path.write_bytes(canonical(obj))
        path.chmod(0o600)

    def check_evidence(self, phase='pre'):
        return subprocess.run([sys.executable, '-I', '-S', '-c', self.reader,
                               str(self.receipt), str(self.restore), str(self.review),
                               ARCHIVE, DIGEST, DRIVE, phase], capture_output=True).returncode

    def test_pre_and_post_match_real_contract(self):
        self.assertEqual(self.check_evidence(), 0)
        self.put(self.review, self.v)
        self.assertNotEqual(self.check_evidence(), 0)
        self.assertEqual(self.check_evidence('post'), 0)

    def test_actual_producer_finalizes_once_without_changing_inputs(self):
        import runpy
        producer = runpy.run_path(str(ROOT / 'scripts/qa/qa525-vnext-g6-v2-producer.py'))
        state = self.base / 'state-v2'; state.mkdir(mode=0o700)
        downloads = self.base / 'downloads-v2'; downloads.mkdir(mode=0o700)
        archive = downloads / ARCHIVE
        archive.write_bytes(b'encrypted-fixture-only-not-a-real-backup')
        archive.chmod(0o600)
        digest = hashlib.sha256(archive.read_bytes()).hexdigest()
        receipt = state / 'receipt-20261113T165400Z.json'
        restore = state / self.restore.name
        r = dict(self.r, sha256=digest); s = dict(self.s, archive_sha256=digest)
        self.put(receipt, r); self.put(restore, s)
        before = [p.read_bytes() for p in (archive, receipt, restore)]
        result = producer['finalize_review'](str(self.base), archive_name=ARCHIVE,
                   drive_file_id=DRIVE, expected_repo_id=REPO,
                   expected_key_epoch=EPOCH, uid=os.getuid())
        self.assertEqual(result, dict(self.v, g6_archive_sha256=digest))
        self.assertEqual(before, [p.read_bytes() for p in (archive, receipt, restore)])
        review = state / 'review-20261113T165400Z.json'
        self.assertEqual(review.read_bytes(), canonical(result))
        self.assertEqual(review.stat().st_mode & 0o777, 0o600)
        with self.assertRaises(producer['Stop']):
            producer['finalize_review'](str(self.base), archive_name=ARCHIVE,
                drive_file_id=DRIVE, expected_repo_id=REPO,
                expected_key_epoch=EPOCH, uid=os.getuid())
        self.assertEqual(review.read_bytes(), canonical(result))

    def test_receipt_and_report_negative_matrix(self):
        cases = [('receipt', 'sha256', '0' * 64), ('receipt', 'restic_repo_id', '0' * 64),
                 ('receipt', 'key_epoch', '0' * 32), ('receipt', 'archive_name', 'other'),
                 ('restore', 'production_touched', True), ('restore', 'result', 'PASS'),
                 ('restore', 'postgres_tables_by_database', {'pmgm': 0, 'pmgm_keycloak': 100}),
                 ('restore', 'postgres_tables_by_database', {'pmgm': True, 'pmgm_keycloak': 100}),
                 ('restore', 'silo_objects_verified', 0), ('restore', 'archive_sha256', '0' * 64)]
        for kind, key, value in cases:
            with self.subTest(kind=kind, key=key, value=value):
                self.put(self.receipt, self.r); self.put(self.restore, self.s)
                obj = dict(self.r if kind == 'receipt' else self.s)
                obj[key] = value
                self.put(self.receipt if kind == 'receipt' else self.restore, obj)
                self.assertNotEqual(self.check_evidence(), 0)

    def test_rejects_noncanonical_duplicate_keys_and_wrong_mode(self):
        self.receipt.write_text(json.dumps(self.r))
        self.assertNotEqual(self.check_evidence(), 0)
        self.put(self.receipt, self.r)
        raw = canonical(self.r)
        self.receipt.write_bytes(raw.replace(b'{', b'{"schema":"qa525-g6-v2-receipt",', 1))
        self.assertNotEqual(self.check_evidence(), 0)
        self.put(self.receipt, self.r)
        self.restore.chmod(0o644)
        self.assertNotEqual(self.check_evidence(), 0)

    def test_rejects_symlinks_hardlinks_and_existing_review_link(self):
        original = self.base / 'saved'
        self.receipt.rename(original)
        self.receipt.symlink_to(original)
        self.assertNotEqual(self.check_evidence(), 0)
        self.receipt.unlink()
        os.link(original, self.receipt)
        self.assertNotEqual(self.check_evidence(), 0)
        self.receipt.unlink(); original.unlink(); self.put(self.receipt, self.r)
        self.review.symlink_to(self.base / 'missing')
        self.assertNotEqual(self.check_evidence(), 0)

    def test_review_must_have_exact_drive_hash_and_boolean(self):
        self.assertNotEqual(self.check_evidence('post'), 0)
        for key, value in [('g6_drive_file_id', 'different-drive-id'),
                           ('g6_archive_sha256', '0' * 64),
                           ('g6_offhost_sha256_verified', False),
                           ('g6_offhost_sha256_verified', 1)]:
            obj = dict(self.v); obj[key] = value
            self.put(self.review, obj)
            self.assertNotEqual(self.check_evidence('post'), 0)

    def signing_script(self):
        key = self.base / 'key'
        if not key.exists():
            subprocess.run(['ssh-keygen', '-q', '-t', 'ed25519', '-N', '', '-f', str(key)], check=True)
        fp = subprocess.check_output(['ssh-keygen', '-lf', str(key) + '.pub'], text=True).split()[1]
        src = (OPS / 'qa525-g6-review-go-termux-164a07ec.sh').read_text()
        src = re.sub(r'FP_EXPECTED="[^"]+"', 'FP_EXPECTED="' + fp + '"', src)
        path = self.base / 'termux.sh'; path.write_text(src)
        return path, key

    def sign(self, command='review', sha=SHA):
        path, key = self.signing_script()
        fields = [sha, *['sha256:' + 'a' * 64] * 4, command, 'sha256:' + 'b' * 64]
        line = 'QA525KITG6:' + base64.b64encode('|'.join(fields).encode()).decode()
        env = dict(os.environ, QA525_KEY=str(key))
        return subprocess.run(['bash', str(path)], input=line + '\n', text=True,
                              capture_output=True, env=env), key

    def test_real_ssh_signature_and_fresh_review_go(self):
        result, key = self.sign()
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertIn('COMANDO=review', result.stdout)
        token = next(x for x in result.stdout.splitlines() if x.startswith('QA525GO1:'))
        _, data, sig = token.split(':')
        raw = base64.b64decode(data); o = json.loads(raw)
        self.assertEqual(o['g6_command'], 'review')
        self.assertEqual(o['action'], 'g6-v2-review-supervised')
        self.assertEqual(raw, canonical(o))
        self.assertRegex(o['nonce'], r'^[0-9a-f]{64}$')
        import datetime as dt
        issued = dt.datetime.fromisoformat(o['issued_at'].replace('Z', '+00:00'))
        expires = dt.datetime.fromisoformat(o['expires_at'].replace('Z', '+00:00'))
        self.assertEqual((expires - issued).total_seconds(), 860)
        self.assertGreater((expires - dt.datetime.now(dt.timezone.utc)).total_seconds(), 800)
        signature = self.base / 'go.sig'; signature.write_bytes(base64.b64decode(sig))
        signers = self.base / 'signers'
        signers.write_text('qa525-ops namespaces="qa525-go-cut-v1@pmgm" ' + Path(str(key) + '.pub').read_text())
        verify = subprocess.run(['ssh-keygen', '-Y', 'verify', '-f', str(signers), '-I', 'qa525-ops',
                                 '-n', 'qa525-go-cut-v1@pmgm', '-s', str(signature)],
                                input=raw, capture_output=True)
        self.assertEqual(verify.returncode, 0, verify.stderr)

    def test_signer_rejects_backup_restore_and_wrong_cut(self):
        for command in ('backup', 'restore'):
            result, _ = self.sign(command)
            self.assertNotEqual(result.returncode, 0)
            self.assertNotIn('QA525GO1:', result.stdout)
            self.assertIn('STOP_KIT_LINE_IS_NOT_REVIEW', result.stdout)
        result, _ = self.sign(sha='0' * 40)
        self.assertNotEqual(result.returncode, 0)
        self.assertNotIn('QA525GO1:', result.stdout)

    def test_shell_syntax_and_embedded_python_compile(self):
        for path in OPS.glob('*.sh'):
            subprocess.run(['bash', '-n', str(path)], check=True)
            src = path.read_text()
            for code in re.findall(r"<<'PY'\n(.*?)\nPY", src, re.S):
                compile(code, str(path), 'exec')
        kit = (OPS / 'qa525-g6-review-kit-A-164a07ec.sh').read_text()
        code = kit.split('"$QA_PY" -I -S -c \'\n', 1)[1].split("\n' || {", 1)[0]
        compile(code, 'kit-A embedded python', 'exec')

    def test_host_go_content_rejects_old_command_and_expiry_before_install(self):
        import datetime as dt
        host = (OPS / 'qa525-g6-review-go-host-164a07ec.sh').read_text()
        marker = '"$PY" -I -S -c \'\n'
        code = host.split(marker, 1)[1].split("\n')\" || stop GO_CONTENT_OR_EXPIRED", 1)[0]
        compile(code, 'GO-H content gate', 'exec')
        self.assertLess(host.index('|| stop GO_CONTENT_OR_EXPIRED'), host.index('GO_INSTALLED=1'))
        self.assertLess(host.index('validate_evidence pre'), host.index('IFS= read -r CONSENT'))
        now = dt.datetime.now(dt.timezone.utc)
        fmt = lambda t: t.strftime('%Y-%m-%dT%H:%M:%SZ')
        env = dict(os.environ, QA_ADMISSION='sha256:' + 'a' * 64, QA_APP='b' * 64,
                   QA_SHA=SHA, QA_OPERATION='sha256:' + 'c' * 64,
                   QA_PLAN='sha256:' + 'd' * 64, QA_POLICY='sha256:' + 'e' * 64)
        go = {'action': 'g6-v2-review-supervised', 'admission_sha256': env['QA_ADMISSION'],
              'app_archive_sha256': 'sha256:' + env['QA_APP'], 'app_source_sha': SHA,
              'environment': 'QA_GLMDCH_ONLY', 'g6_command': 'review',
              'g6_operation_sha256': env['QA_OPERATION'], 'host': 'glmdch',
              'operator': 'soporte@glmdch', 'ops_source_sha': SHA,
              'plan_sha256': env['QA_PLAN'], 'policy_hash': env['QA_POLICY'],
              'schema': 'qa525-go-g6-v1', 'scope': 'QA525_G6_V2_ONLY_NO_LAUNCHER_NO_API_WEB',
              'nonce': 'f' * 64, 'issued_at': fmt(now - dt.timedelta(seconds=20)),
              'expires_at': fmt(now + dt.timedelta(seconds=840))}
        file = self.base / 'go.json'; env['QA_GO'] = str(file)
        def run(obj):
            file.write_bytes(canonical(obj))
            return subprocess.run([sys.executable, '-I', '-S', '-c', code],
                                  capture_output=True, env=env).returncode
        self.assertEqual(run(go), 0)
        for field, value in [('g6_command', 'backup'), ('g6_command', 'restore'),
                             ('expires_at', fmt(now - dt.timedelta(seconds=1))),
                             ('expires_at', fmt(now + dt.timedelta(seconds=100))),
                             ('issued_at', fmt(now + dt.timedelta(seconds=60))),
                             ('policy_hash', 'sha256:' + '0' * 64)]:
            obj = dict(go); obj[field] = value
            self.assertNotEqual(run(obj), 0, (field, value))


if __name__ == '__main__':
    unittest.main()
