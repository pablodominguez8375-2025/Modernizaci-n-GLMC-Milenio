"""Synthetic tests only: never connect to QA or invoke host inventory."""
import copy
import importlib.machinery
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]


def load(name, path):
    loader = importlib.machinery.SourceFileLoader(name, str(ROOT / path))
    spec = importlib.util.spec_from_loader(name, loader)
    module = importlib.util.module_from_spec(spec)
    loader.exec_module(module)
    return module


helper = load('qa_helper', 'scripts/qa/centenario-qa-readonly')
validator = load('qa_validator', 'scripts/qa/validate-report.py')
HASH = 'a' * 64


def fixture():
    return dict(schema=1, helper_version=helper.VERSION, helper_sha256=HASH,
                timestamp='2026-10-16T02:00:00Z', hostname='glmdch', cpu_count=8,
                mem_total_kib=7600780, opt_total_bytes=100000, opt_available_bytes=85000,
                services={k: 'inactive' for k in helper.SERVICES},
                packages={k: False for k in helper.PACKAGES},
                listeners=[dict(protocol='tcp', port=22, scope='wildcard')],
                firewall='NO_VERIFICADO_REQUIERE_OPERADOR', qa='NO_VERIFICADO')


class QaReadonlyTests(unittest.TestCase):
    def test_denies_command_injection_shell_sftp_scp_and_arguments(self):
        for command in ('', 'id', 'centenario-qa-readonly-v1 extra', 'centenario-qa-readonly-v1; id',
                        'internal-sftp', 'scp -t /tmp/x', '$(id)', 'bash'):
            with self.subTest(command=command), patch.dict(os.environ, {'SSH_ORIGINAL_COMMAND': command}, clear=True), patch.object(helper, 'collect') as collect, patch('sys.argv', ['helper']):
                self.assertEqual(helper.main(), 64)
                collect.assert_not_called()

    def test_denies_pty_and_process_arguments(self):
        for env, argv in (({'SSH_ORIGINAL_COMMAND': helper.VERSION, 'SSH_TTY': '/dev/pts/1'}, ['helper']),
                          ({'SSH_ORIGINAL_COMMAND': helper.VERSION}, ['helper', 'extra'])):
            with patch.dict(os.environ, env, clear=True), patch('sys.argv', argv), patch.object(helper, 'collect') as collect:
                self.assertEqual(helper.main(), 64)
                collect.assert_not_called()

    def test_accepts_only_contract(self):
        self.assertEqual(validator.validate(json.dumps(fixture()), HASH)['qa'], 'NO_VERIFICADO')

    def test_rejects_identity_leaks_and_false_readiness(self):
        mutations = [('hostname', 'srv01'), ('helper_sha256', 'b' * 64), ('qa', 'PASS'),
                     ('firewall', 'PASS'), ('cpu_count', True), ('timestamp', 'bad'),
                     ('unexpected_secret', 'never publish'), ('listeners', [{'protocol': 'tcp', 'port': 22, 'scope': '1.2.3.4'}])]
        for key, value in mutations:
            data = copy.deepcopy(fixture())
            data[key] = value
            with self.subTest(key=key), self.assertRaises((ValueError, TypeError)):
                validator.validate(json.dumps(data), HASH)

    def test_rejects_invalid_and_oversized_json(self):
        for raw in ('not json', 'x' * 65537):
            with self.assertRaises(ValueError):
                validator.validate(raw, HASH)

    def test_collect_sanitizes_addresses_and_queries_only_allowlist(self):
        def fake_command(args):
            if args[0] == '/usr/bin/ss':
                return 0, 'tcp LISTEN 0 128 192.0.2.1:22 0.0.0.0:*\nudp UNCONN 0 0 127.0.0.1:53 0.0.0.0:*\n'
            if args[0] == '/usr/bin/systemctl':
                self.assertIn(args[-1], [s + '.service' for s in helper.SERVICES])
                return 3, 'inactive\n'
            self.assertEqual(args[0], '/usr/bin/dpkg-query')
            self.assertIn(args[-1], helper.PACKAGES)
            return 1, ''
        original_read = Path.read_text
        def read(path, *args, **kwargs):
            if str(path) == '/proc/meminfo':
                return 'MemTotal: 7600780 kB\n'
            return original_read(path, *args, **kwargs)
        with patch.object(helper.socket, 'gethostname', return_value='glmdch'), patch.object(helper, 'command', side_effect=fake_command), patch.object(Path, 'read_text', read):
            data = helper.collect()
        self.assertNotIn('192.0.2.1', json.dumps(data))
        validator.validate(json.dumps(data), data['helper_sha256'])

    def test_client_cleans_key_and_suppresses_failed_ssh_output(self):
        with tempfile.TemporaryDirectory() as temp:
            binary = Path(temp) / 'bin'
            binary.mkdir()
            fake = binary / 'ssh'
            fake.write_text('#!/bin/sh\necho synthetic-private-content\necho synthetic-error >&2\nexit 255\n')
            fake.chmod(0o755)
            env = dict(os.environ, PATH=str(binary) + ':' + os.environ['PATH'],
                       RUNNER_TEMP=temp, QA_READONLY_SSH_KEY='synthetic-private-key',
                       QA_SSH_HOST='qa.example.invalid', QA_SSH_PORT='22107',
                       QA_SSH_USER='qa_check', QA_SSH_KNOWN_HOSTS='synthetic')
            result = subprocess.run(['bash', 'scripts/qa/check-qa.sh'], cwd=ROOT, env=env, capture_output=True)
            self.assertNotEqual(result.returncode, 0)
            self.assertNotIn(b'synthetic', result.stdout + result.stderr)
            self.assertEqual(list(Path(temp).glob('qa-readonly.*')), [])
            self.assertFalse((Path(temp) / 'qa-evidence/report.json').exists())

    def test_bad_configuration_cannot_start_ssh(self):
        with tempfile.TemporaryDirectory() as temp:
            env = dict(os.environ, RUNNER_TEMP=temp, QA_READONLY_SSH_KEY='synthetic',
                       QA_SSH_HOST='-oProxyCommand=id', QA_SSH_PORT='22107',
                       QA_SSH_USER='qa_check', QA_SSH_KNOWN_HOSTS='synthetic')
            result = subprocess.run(['bash', 'scripts/qa/check-qa.sh'], cwd=ROOT, env=env, capture_output=True)
            self.assertNotEqual(result.returncode, 0)
            self.assertEqual(list(Path(temp).iterdir()), [])


if __name__ == '__main__':
    unittest.main()
