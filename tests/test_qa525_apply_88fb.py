import importlib.util
import json
import os
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

SOURCE = Path(__file__).with_name('qa525-apply-88fb.py')
spec = importlib.util.spec_from_file_location('apply525_88fb', SOURCE)
m = importlib.util.module_from_spec(spec)
spec.loader.exec_module(m)


class Apply88fbTests(unittest.TestCase):
    def test_overlay_changes_only_two_images(self):
        value = m.overlay('api', 'sha256:' + 'a' * 64)
        self.assertEqual({'api'}, set(value['services']))
        self.assertEqual({'image'}, set(value['services']['api']))

    def test_overlay_rejects_dependency_services(self):
        with self.assertRaisesRegex(m.Blocked, 'OVERLAY_SERVICE_INVALID'):
            m.overlay('postgres', 'sha256:' + 'a' * 64)

    def test_private_receipt_is_exclusive_and_mode_600(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'receipt.json'
            m.write_private_json(path, {'ok': True})
            self.assertEqual({'ok': True}, json.loads(path.read_text()))
            self.assertEqual(0o600, path.stat().st_mode & 0o777)
            with self.assertRaises(FileExistsError):
                m.write_private_json(path, {'ok': False})

    def test_duplicate_runtime_environment_is_blocked(self):
        with self.assertRaisesRegex(m.Blocked, 'ENV_INVALID'):
            m.environment(['A=1', 'A=2'])

    def test_both_databases_are_named_by_apply(self):
        source = SOURCE.read_text()
        self.assertIn("for name in ('pmgm', 'pmgm_keycloak')", source)
        self.assertNotIn("'run', '--rm'", source)

    def test_image_label_must_match_target(self):
        image = 'sha256:' + 'a' * 64
        response = json.dumps([{'Id': image, 'Config': {'Labels': {
            'org.opencontainers.image.revision': 'wrong'}}}])
        with patch.object(m, 'command', return_value=response), \
                self.assertRaisesRegex(m.Blocked, 'IMAGE_IDENTITY_CHANGED'):
            m.image_identity(image, m.TARGET)


if __name__ == '__main__':
    unittest.main()
