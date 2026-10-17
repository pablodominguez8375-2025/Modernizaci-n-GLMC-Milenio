"""Signed Silo binding across Docker classic and containerd image stores.

No real container, registry, credential or SSH access is needed for these tests.
"""
import hashlib
import importlib.util
import io
import json
import os
from pathlib import Path
import subprocess
import tarfile
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location(
    'qa_silo_archive_containerd', ROOT / 'scripts/qa/prepare-silo-archive.py')
archive = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(archive)
MANIFEST_ID = 'sha256:' + '2' * 64
DIFF = 'sha256:' + '3' * 64


def make_tar(path, config):
    raw = json.dumps(config, sort_keys=True).encode()
    with tarfile.open(path, 'w') as tar:
        for name, data in [('config.json', raw),
                           ('manifest.json', b'[{"Config":"config.json","Layers":[]}]')]:
            info = tarfile.TarInfo(name)
            info.size = len(data)
            tar.addfile(info, io.BytesIO(data))
    return 'sha256:' + hashlib.sha256(raw).hexdigest()


class ContainerdIdentityTests(unittest.TestCase):
    def setUp(self):
        self.stage = tempfile.TemporaryDirectory(prefix='qa-silo-unit-')
        self.addCleanup(self.stage.cleanup)
        self.path = Path(self.stage.name)
        self.config = {'os': 'linux', 'architecture': 'amd64',
                       'rootfs': {'type': 'layers', 'diff_ids': [DIFF]}}
        self.expected = make_tar(self.path / 'silo-qa-review-only.tar', self.config)
        self.report = {'local_image_id': self.expected}

    def fake(self, ids, exported_config=None, inspect_layers=None):
        exported_config = exported_config if exported_config is not None else self.config
        inspect_layers = inspect_layers if inspect_layers is not None else [DIFF]
        def run(command, **kwargs):
            out = ''
            if command[:3] == ['docker', 'image', 'ls']:
                out = '\n'.join(ids) + '\n'
            elif command[:3] == ['docker', 'image', 'inspect']:
                out = json.dumps({'Id': command[-1], 'Os': 'linux',
                                  'Architecture': 'amd64',
                                  'RootFS': {'Layers': inspect_layers}})
            elif command[:3] == ['docker', 'image', 'save']:
                make_tar(Path(command[command.index('--output') + 1]), exported_config)
            else:
                raise AssertionError('unexpected command: ' + repr(command))
            return subprocess.CompletedProcess(command, 0, stdout=out)
        return run

    def test_classic_store_accepts_exact_config_id(self):
        with patch.object(archive, 'run', side_effect=self.fake([self.expected])):
            self.assertEqual(self.expected, archive.resolve_loaded_image(self.path, self.report))

    def test_containerd_store_accepts_distinct_manifest_id_after_full_checks(self):
        self.assertNotEqual(self.expected, MANIFEST_ID)
        with patch.object(archive, 'run', side_effect=self.fake([MANIFEST_ID])):
            self.assertEqual(MANIFEST_ID, archive.resolve_loaded_image(self.path, self.report))

    def test_changed_exported_configuration_is_rejected(self):
        altered = dict(self.config, architecture='arm64')
        with patch.object(archive, 'run',
                          side_effect=self.fake([MANIFEST_ID], exported_config=altered)):
            with self.assertRaises(LookupError):
                archive.resolve_loaded_image(self.path, self.report)

    def test_changed_rootfs_layers_are_rejected_before_save(self):
        with patch.object(archive, 'run',
                          side_effect=self.fake([MANIFEST_ID], inspect_layers=['sha256:' + '4' * 64])) as run:
            with self.assertRaises(LookupError):
                archive.resolve_loaded_image(self.path, self.report)
            self.assertFalse(any(c.args[0][:3] == ['docker','image','save']
                                 for c in run.call_args_list))

    def test_ambiguous_matching_ids_fail_closed(self):
        with patch.object(archive, 'run',
                          side_effect=self.fake([self.expected, MANIFEST_ID])):
            with self.assertRaisesRegex(ValueError, 'ambiguous'):
                archive.resolve_loaded_image(self.path, self.report)

    def test_no_matching_id_never_pulls_or_picks_unknown_image(self):
        with patch.object(archive, 'run', side_effect=self.fake(['bad', 'sha256:' + '9'*64],
                                                               inspect_layers=[])) as run:
            with self.assertRaises(LookupError):
                archive.resolve_loaded_image(self.path, self.report)
            self.assertFalse(any('pull' in c.args[0] for c in run.call_args_list))

    def test_prepare_uses_verified_local_id_for_binary_and_all_storage_services(self):
        report = dict(self.report, server_cve_triage={'server_binary_sha256': 'f' * 64})
        model = {'services': {n: {'image': 'wrong'} for n in archive.SERVICES}}
        calls = []
        def fake_run(command, **kwargs):
            calls.append(command)
            if command[:2] == ['docker', 'run']:
                return subprocess.CompletedProcess(command, 0, stdout='f'*64 + '  /usr/bin/silo\n')
            if command[:2] == ['docker', 'compose']:
                return subprocess.CompletedProcess(command, 0, stdout=json.dumps(model))
            raise AssertionError('unexpected command')
        with patch.object(archive, 'verify_bundle', return_value=report), \
             patch.object(archive, 'resolve_loaded_image', return_value=MANIFEST_ID), \
             patch.object(archive, 'run', side_effect=fake_run), \
             patch.dict(os.environ, {'PMGM_QA_HTTPS':'false', 'PMGM_QA_DB2':'false'}):
            output = self.path / 'compose.json'
            self.assertEqual(MANIFEST_ID, archive.prepare(self.path, 'a'*40, output))
        configured = json.loads(output.read_text())
        for name in archive.SERVICES:
            self.assertEqual(MANIFEST_ID, configured['services'][name]['image'])
            self.assertEqual('never', configured['services'][name]['pull_policy'])
        self.assertIn(MANIFEST_ID, next(c for c in calls if c[:2] == ['docker', 'run']))
        self.assertFalse(any(c[:2] == ['docker', 'load'] for c in calls))


if __name__ == '__main__':
    unittest.main()
