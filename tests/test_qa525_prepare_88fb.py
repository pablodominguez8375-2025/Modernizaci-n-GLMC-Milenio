import importlib.util
import tempfile
import unittest
from pathlib import Path
from unittest.mock import Mock, patch

SOURCE = Path(__file__).with_name('qa525-prepare-88fb.py')
spec = importlib.util.spec_from_file_location('prepare525_88fb', SOURCE)
m = importlib.util.module_from_spec(spec)
spec.loader.exec_module(m)


class Prepare88fbTests(unittest.TestCase):
    def test_target_must_still_be_exact_dev_head(self):
        with patch.object(m, 'fetch', return_value={'commit': {'sha': '0' * 40}}), \
                self.assertRaisesRegex(m.Blocked, 'TARGET_NO_LONGER_DEV_HEAD'):
            m.selection(Mock())

    def test_exact_head_is_pinned_during_gate_evaluation(self):
        ready = Mock()
        ready.evaluate.side_effect = lambda fetch: fetch('/branches/dev')
        with patch.object(m, 'fetch', return_value={'commit': {'sha': m.TARGET}}):
            self.assertEqual({'commit': {'sha': m.TARGET}}, m.selection(ready))

    def test_signature_and_manifest_are_rechecked(self):
        with tempfile.TemporaryDirectory() as directory:
            package = Path(directory) / 'package'
            package.mkdir()
            (package / 'MANIFEST.sha256').write_text('same')
            staging = Mock()

            def stage(archive, expected, target, checked, tokenless):
                self.assertTrue(tokenless)
                checked.mkdir()
                (checked / 'MANIFEST.sha256').write_text('same')

            staging.stage.side_effect = stage
            with patch.object(m, 'STATE', Path(directory)):
                verifier = m.source_verifier(staging, Path('archive'), 'digest', package)
                self.assertTrue(verifier(package, m.TARGET))
            staging.verify_manifest.assert_called_once_with(package, m.TARGET)


if __name__ == '__main__':
    unittest.main()
