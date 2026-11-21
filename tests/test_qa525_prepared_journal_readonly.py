"""Focused tests for the QA525 read-only evidence reader."""
import importlib.util
import json
import os
from pathlib import Path
import tempfile
import unittest
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


if __name__ == '__main__':
    unittest.main()
