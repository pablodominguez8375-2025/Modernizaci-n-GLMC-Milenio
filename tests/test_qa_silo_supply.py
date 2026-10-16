"""Offline negative tests for registry descriptor verification, no remote service."""
import hashlib
import importlib.util
import json
from pathlib import Path
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('qa_supply', ROOT / 'scripts/qa/verify-silo-supply.py')
supply = importlib.util.module_from_spec(spec)
spec.loader.exec_module(supply)


def encoded(value):
    return json.dumps(value, sort_keys=True).encode()


class QaSiloSupplyTests(unittest.TestCase):
    def fixture(self, architecture='amd64', duplicate=False):
        config = encoded({'os': 'linux', 'architecture': architecture})
        child = encoded({'config': {'digest': supply.digest(config)}, 'layers': []})
        descriptor = {'digest': supply.digest(child), 'platform': {'os': 'linux', 'architecture': 'amd64'}}
        index = encoded({'manifests': [descriptor, descriptor] if duplicate else [descriptor]})
        mapping = {supply.digest(v): v for v in (config, child, index)}
        def fetch(url, headers=None, maximum=None):
            if 'auth.docker.io' in url:
                return encoded({'token': 'synthetic-no-privilege-token'})
            return mapping[url.rsplit('/', 1)[-1]]
        return mapping, fetch, 'docker.io/pgsty/silo@' + supply.digest(index)

    def test_verifies_bytes_of_index_child_and_config(self):
        _, fetch, image = self.fixture()
        with patch.object(supply, 'IMAGE', image), patch.object(supply, 'fetch', side_effect=fetch):
            evidence = supply.registry_evidence()
        self.assertEqual('amd64', evidence['architecture'])
        self.assertEqual(image.split('@')[1], evidence['index_sha256'])
        self.assertNotIn('synthetic-no-privilege-token', json.dumps(evidence))

    def test_rejects_tampered_descriptor_bytes(self):
        mapping, fetch, image = self.fixture()
        mapping[image.split('@')[1]] += b' '
        with patch.object(supply, 'IMAGE', image), patch.object(supply, 'fetch', side_effect=fetch):
            with self.assertRaises(ValueError):
                supply.registry_evidence()

    def test_rejects_wrong_config_platform(self):
        _, fetch, image = self.fixture(architecture='arm64')
        with patch.object(supply, 'IMAGE', image), patch.object(supply, 'fetch', side_effect=fetch):
            with self.assertRaises(ValueError):
                supply.registry_evidence()

    def test_rejects_ambiguous_amd64_descriptor(self):
        _, fetch, image = self.fixture(duplicate=True)
        with patch.object(supply, 'IMAGE', image), patch.object(supply, 'fetch', side_effect=fetch):
            with self.assertRaises(ValueError):
                supply.registry_evidence()


if __name__ == '__main__':
    unittest.main()
