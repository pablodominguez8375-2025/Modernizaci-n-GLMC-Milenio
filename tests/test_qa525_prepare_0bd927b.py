import importlib.util
import hashlib
import tempfile
import unittest
from pathlib import Path
from unittest.mock import Mock, patch

ROOT = Path(__file__).resolve().parents[1]
def load(name):
    spec = importlib.util.spec_from_file_location(name, ROOT / 'scripts/qa/operators' / (name + '.py'))
    value = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(value)
    return value

m = load('qa525-prepare-0bd927b')
p = load('qa525-update-readiness-0bd927b')

class CandidateTests(unittest.TestCase):
    def test_preflight_bytes_match_pinned_hash(self):
        self.assertEqual(m.PREFLIGHT_SHA, hashlib.sha256(Path(p.__file__).read_bytes()).hexdigest())

    def test_new_head_blocks_preparation(self):
        with patch.object(m, 'fetch', return_value={'commit': {'sha': '0'*40}}):
            with self.assertRaisesRegex(m.Blocked, 'TARGET_NO_LONGER_DEV_HEAD'):
                m.selection(Mock())

    def test_unreviewed_program_blocks_before_receipt_or_network(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            program = root / 'backend/src/PMGM.Api/Program.cs'
            program.parent.mkdir(parents=True)
            program.write_text('unreviewed')
            with self.assertRaisesRegex(m.Blocked, 'TARGET_PROGRAM_REVIEW_CHANGED'):
                m.unchanged_schema(root, Mock())

    def test_mutation_or_introspection_enabled_blocks_preflight(self):
        for key in ('UserAccounts__Identity__RequireLiveTokenIntrospection',
                    'UserAccounts__Identity__EnableLifecycleMutations'):
            with self.subTest(key=key), self.assertRaisesRegex(p.Blocked, 'ACCOUNT_LIFECYCLE_FLAGS_REQUIRE_REVIEW'):
                p.validate_identity({key: 'true'}, {}, {}, '', '')

if __name__ == '__main__':
    unittest.main()
