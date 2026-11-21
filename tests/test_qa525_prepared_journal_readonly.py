"""Focused tests for the QA525 read-only evidence reader."""
import importlib.util
import io
import json
import os
from pathlib import Path
import tempfile
import unittest
from contextlib import redirect_stdout
from unittest.mock import patch


SOURCE = Path(__file__).resolve().parents[1] / 'scripts/qa/operators/qa525-prepared-journal-readonly.py'
SPEC = importlib.util.spec_from_file_location('qa525_prepared_reader', SOURCE)
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


class EvidenceReaderTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.state = Path(self.tmp.name)
        self.original_state = MODULE.STATE
        MODULE.STATE = self.state
        self.addCleanup(setattr, MODULE, 'STATE', self.original_state)
        original_fstat = os.fstat

        def root_owned(fd):
            stat = list(original_fstat(fd))
            stat[4] = 0
            return os.stat_result(stat)

        self.patcher = patch.object(MODULE.os, 'fstat', side_effect=root_owned)
        self.patcher.start()
        self.addCleanup(self.patcher.stop)

    def write(self, name, data):
        p = self.state / name
        p.write_bytes(data)
        p.chmod(0o600)
        return p

    def test_reads_bounded_json_and_hash(self):
        data = b'{"source_sha":"' + b'a' * 40 + b'","status":"APPLIED"}'
        self.write('prepared.json', data)
        obj, digest = MODULE.read_private('prepared.json')
        self.assertEqual(obj['status'], 'APPLIED')
        self.assertEqual(len(digest), 64)

    def test_rejects_duplicate_keys(self):
        self.write('prepared.json', b'{"status":"APPLIED","status":"PENDING"}')
        with self.assertRaises(MODULE.Stop):
            MODULE.read_private('prepared.json')

    def test_rejects_symlink(self):
        self.write('source', json.dumps({'status': 'APPLIED'}).encode())
        (self.state / 'prepared.json').symlink_to(self.state / 'source')
        with self.assertRaises(OSError):
            MODULE.read_private('prepared.json')

    def test_rejects_world_readable_evidence(self):
        p = self.write('prepared.json', b'{"status":"APPLIED"}')
        p.chmod(0o644)
        with self.assertRaises(MODULE.Stop):
            MODULE.read_private('prepared.json')

    def run_main(self, prepared=None, journal=None):
        sha = 'a' * 40
        prepared = prepared or {'source_sha': sha, 'source_asset_sha256':
                                'sha256:' + 'b' * 64, 'status': 'APPLIED'}
        journal = journal or {'source_sha': sha, 'status': 'APPLIED'}
        self.write('prepared.json', json.dumps(prepared).encode())
        self.write('dual-' + sha + '.json', json.dumps(journal).encode())
        original_lstat = Path.lstat

        def trusted_directory(path):
            st = list(original_lstat(path))
            if path in (self.state, self.state.parent, self.state.parent.parent):
                st[0] = (st[0] & ~0o777) | 0o700
                st[4] = 0
            return os.stat_result(st)

        with patch.object(MODULE.os, 'geteuid', return_value=0), \
             patch.object(MODULE.socket, 'gethostname', return_value='glmdch'), \
             patch.object(Path, 'lstat', trusted_directory), \
             redirect_stdout(io.StringIO()) as output:
            MODULE.main()
        return json.loads(output.getvalue())

    def test_main_reports_prepared_journal_and_pending_markers(self):
        image = 'sha256:' + 'c' * 64
        self.write('prepared-successor-archive-pending.json', b'{}')
        self.write('dual-' + 'a' * 40 + '.json.next', b'{}')
        self.write('reconcile-previous-cut', b'{}')
        report = self.run_main(
            prepared={'source_sha': 'a' * 40, 'source_asset_sha256': 'sha256:' + 'b' * 64,
                      'status': 'APPLIED', 'api_image_id': image,
                      'baseline': {'api': {'image_id': image}}},
            journal={'source_sha': 'a' * 40, 'status': 'APPLIED',
                     'new_images': {'api': image}})
        self.assertEqual(report['pending_artifact_count'], 2)
        self.assertEqual(report['reconcile_artifact_count_unclassified'], 1)
        self.assertEqual(report['prepared_new_images']['api'], image)
        self.assertEqual(report['journal_new_images']['api'], image)
        self.assertIsNone(report['journal_new_images']['web'])
        self.assertIn('api_image_id', report['prepared']['key_names'])
        self.assertIn('new_images', report['journal']['key_names'])
        self.assertNotIn('nonce', json.dumps(report))

    def test_main_identifies_bad_image_field(self):
        with self.assertRaisesRegex(MODULE.Stop, 'IMAGE_ID_INVALID_PREPARED_API'):
            self.run_main(prepared={'source_sha': 'a' * 40,
                                    'source_asset_sha256': 'sha256:' + 'b' * 64,
                                    'status': 'APPLIED', 'api_image_id': 'short'})

    def test_main_rejects_mismatched_journal(self):
        with self.assertRaises(MODULE.Stop):
            self.run_main(journal={'source_sha': 'b' * 40, 'status': 'APPLIED'})

    def test_main_rejects_unbounded_status(self):
        with self.assertRaises(MODULE.Stop):
            self.run_main(journal={'source_sha': 'a' * 40, 'status': 'X' * 100})

    def test_main_rejects_untrusted_host(self):
        with patch.object(MODULE.os, 'geteuid', return_value=1000):
            with self.assertRaises(MODULE.Stop):
                MODULE.main()


if __name__ == '__main__':
    unittest.main()
