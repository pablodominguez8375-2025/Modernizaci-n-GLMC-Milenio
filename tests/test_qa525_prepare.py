import importlib.util
import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import Mock, patch

SOURCE = Path(__file__).with_name('qa525-prepare-e3e90.py')
if not SOURCE.exists():
    SOURCE = Path(__file__).resolve().parents[1] / 'scripts/qa/operators/qa525-prepare-e3e90.py'
spec = importlib.util.spec_from_file_location('prepare525', SOURCE)
m = importlib.util.module_from_spec(spec)
spec.loader.exec_module(m)


class PrepareTests(unittest.TestCase):
    def test_target_cannot_leave_dev_ancestry(self):
        responses = [{'commit': {'sha': 'a' * 40}}, {'status': 'diverged'}]
        with patch.object(m, 'fetch', side_effect=responses), self.assertRaisesRegex(m.Blocked, 'TARGET_NOT_ON_DEV'):
            m.selection(Mock())

    def test_exact_target_evaluation_does_not_follow_new_tip(self):
        ready = Mock()
        ready.evaluate.side_effect = lambda fetch: fetch('/branches/dev')
        with patch.object(m, 'fetch', return_value={'commit': {'sha': m.TARGET}}):
            self.assertEqual(m.selection(ready), {'commit': {'sha': m.TARGET}})

    def test_archive_reverified_not_receipt_boolean(self):
        with tempfile.TemporaryDirectory() as directory:
            package = Path(directory) / 'package'
            package.mkdir()
            (package / 'MANIFEST.sha256').write_text('verified manifest')
            staging = Mock()
            def stage(archive, digest, sha, checked, tokenless):
                self.assertTrue(tokenless)
                self.assertEqual(sha, m.TARGET)
                checked.mkdir()
                (checked / 'MANIFEST.sha256').write_text('verified manifest')
            staging.stage.side_effect = stage
            with patch.object(m, 'STATE', Path(directory)):
                verify = m.source_verifier(staging, Path('archive'), 'sha256:test', package)
                self.assertTrue(verify(package, m.TARGET))
                staging.verify_manifest.assert_called_once_with(package, m.TARGET)

    def test_manifest_substitution_blocks(self):
        with tempfile.TemporaryDirectory() as directory:
            package = Path(directory) / 'package'
            package.mkdir()
            (package / 'MANIFEST.sha256').write_text('tampered')
            staging = Mock()
            def stage(a, b, c, checked, **kwargs):
                checked.mkdir()
                (checked / 'MANIFEST.sha256').write_text('original')
            staging.stage.side_effect = stage
            with patch.object(m, 'STATE', Path(directory)), self.assertRaisesRegex(m.Blocked, 'MANIFEST_BINDING_CHANGED'):
                m.source_verifier(staging, Path('archive'), 'digest', package)(package, m.TARGET)

    def test_signature_failure_cannot_reach_manifest_validation(self):
        staging = Mock()
        staging.stage.side_effect = RuntimeError('signature-invalid')
        with tempfile.TemporaryDirectory() as directory, patch.object(m, 'STATE', Path(directory)):
            with self.assertRaises(RuntimeError):
                m.source_verifier(staging, Path('archive'), 'digest', Path(directory))(Path(directory), m.TARGET)
            staging.verify_manifest.assert_not_called()

    def test_wrong_source_binding_blocks_before_signature_tool(self):
        staging = Mock()
        with self.assertRaisesRegex(m.Blocked, 'SOURCE_BINDING_CHANGED'):
            m.source_verifier(staging, Path('archive'), 'digest', Path('package'))(Path('other'), m.TARGET)
        staging.stage.assert_not_called()

    def test_api_build_is_reverified_before_and_after(self):
        image = 'sha256:' + 'a' * 64
        calls = []
        def docker(args, **kwargs):
            calls.append(args)
            if args[0] == 'build':
                Path(args[args.index('--iidfile') + 1]).write_text(image)
                return ''
            return image if args[args.index('--format') + 1] == '{{.Id}}' else m.TARGET
        adapter = Mock(docker=docker)
        verify = Mock(return_value=True)
        with tempfile.TemporaryDirectory() as directory, patch.object(m, 'STATE', Path(directory)):
            self.assertEqual(m.build_api(adapter, Path('package'), verify), image)
        self.assertEqual(verify.call_count, 2)
        self.assertTrue(all(c[0] in ('build', 'image') for c in calls))

    def test_api_build_cannot_skip_verification(self):
        adapter = Mock()
        with self.assertRaisesRegex(m.Blocked, 'SOURCE_NOT_VERIFIED'):
            m.build_api(adapter, Path('package'), lambda *a: False)
        adapter.docker.assert_not_called()

    def test_dependency_symlink_is_not_executable_trust(self):
        with tempfile.TemporaryDirectory() as directory:
            p = Path(directory) / 'link'
            p.symlink_to('/etc/passwd')
            with self.assertRaises(m.Blocked):
                m.trusted(p)

    def test_dependency_hash_mismatch_blocks(self):
        with tempfile.TemporaryDirectory() as directory:
            p = Path(directory) / 'file'
            p.write_text('different source')
            with self.assertRaises(m.Blocked):
                m.trusted(p, '0' * 64)

    def test_api_image_label_mismatch_prevents_receipt(self):
        image = 'sha256:' + 'a' * 64
        def docker(args, **kwargs):
            if args[0] == 'build':
                Path(args[args.index('--iidfile') + 1]).write_text(image)
                return ''
            return image if args[args.index('--format') + 1] == '{{.Id}}' else 'wrong-source'
        with tempfile.TemporaryDirectory() as directory, patch.object(m, 'STATE', Path(directory)):
            with self.assertRaisesRegex(m.Blocked, 'API_IMAGE_IDENTITY_CHANGED'):
                m.build_api(Mock(docker=docker), Path('package'), lambda *a: True)


if __name__ == '__main__':
    unittest.main()
