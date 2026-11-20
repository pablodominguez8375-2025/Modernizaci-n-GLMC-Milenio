"""Offline negative tests for registry descriptor verification, no remote service."""
import hashlib
import importlib.util
import json
from pathlib import Path
import unittest
import urllib.error
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('qa_supply', ROOT / 'scripts/qa/verify-silo-supply.py')
supply = importlib.util.module_from_spec(spec)
spec.loader.exec_module(supply)
rebuild_spec = importlib.util.spec_from_file_location('qa_rebuild_diag', ROOT / 'scripts/qa/rebuild-silo-candidate.py')
rebuild = importlib.util.module_from_spec(rebuild_spec)
rebuild_spec.loader.exec_module(rebuild)


def encoded(value):
    return json.dumps(value, sort_keys=True).encode()


class QaSiloSupplyTests(unittest.TestCase):
    def fixture(self, architecture='amd64', duplicate=False):
        config = encoded({'os': 'linux', 'architecture': architecture})
        child = encoded({'config': {'digest': supply.digest(config)}, 'layers': []})
        descriptor = {'digest': supply.digest(child), 'platform': {'os': 'linux', 'architecture': 'amd64'}}
        index = encoded({'manifests': [descriptor, descriptor] if duplicate else [descriptor]})
        mapping = {supply.digest(v): v for v in (config, child, index)}
        def fetch(url, headers=None, maximum=None, stage=None):
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


class QaSiloFetchDiagnosticsTests(unittest.TestCase):
    """Failure labels must separate registry stages and HTTP classes, without leaking URLs or headers."""
    URL = 'https://registry.invalid/v2/secret-path?token=SECRET'

    def raise_with(self, error, stage):
        with patch.object(supply.urllib.request, 'urlopen', side_effect=error):
            with self.assertRaises(type(error)) as caught:
                supply.fetch(self.URL, {'Authorization': 'Bearer SECRET'}, stage=stage)
        return caught.exception

    def http(self, code, final=None):
        return urllib.error.HTTPError(final or self.URL, code, 'msg', {}, None)

    def test_each_stage_and_http_class_is_labelled_without_url_or_header(self):
        for stage in supply.FETCH_STAGES:
            for code, expected in ((400, 'client_error'), (403, 'client_error'), (429, 'rate_limited'),
                                   (500, 'server_error'), (503, 'server_error')):
                with self.subTest(stage=stage, code=code):
                    details = rebuild.failure_details(self.raise_with(self.http(code), stage))
                    self.assertEqual({'type': 'HTTPError', 'http_status': code, 'fetch_stage': stage,
                                      'failure_class': expected, 'redirected': False}, details)
                    self.assertNotIn('SECRET', repr(details))
                    self.assertNotIn('registry.invalid', repr(details))

    def test_redirect_flag_is_boolean_only(self):
        error = self.raise_with(self.http(400, 'https://cdn.invalid/blob?sig=SECRET'), 'config')
        details = rebuild.failure_details(error)
        self.assertIs(details['redirected'], True)
        self.assertNotIn('cdn.invalid', repr(details))

    def test_timeout_and_network_errors_have_their_own_class(self):
        timeout = rebuild.failure_details(self.raise_with(TimeoutError('slow'), 'token'))
        self.assertEqual({'type': 'TimeoutError', 'fetch_stage': 'token', 'failure_class': 'timeout'}, timeout)
        network = rebuild.failure_details(self.raise_with(urllib.error.URLError('dns SECRET'), 'index'))
        self.assertEqual({'type': 'URLError', 'fetch_stage': 'index', 'failure_class': 'network'}, network)

    def test_unlabelled_or_unknown_stage_keeps_the_previous_minimal_shape(self):
        for stage in (None, 'https://evil.invalid/', 'LICENSE'):
            with self.subTest(stage=stage):
                self.assertEqual({'type': 'HTTPError', 'http_status': 400},
                                 rebuild.failure_details(self.raise_with(self.http(400), stage)))

    def test_registry_evidence_labels_the_failing_step(self):
        config = encoded({'os': 'linux', 'architecture': 'amd64'})
        child = encoded({'config': {'digest': supply.digest(config)}, 'layers': []})
        descriptor = {'digest': supply.digest(child), 'platform': {'os': 'linux', 'architecture': 'amd64'}}
        index = encoded({'manifests': [descriptor]})
        mapping = {supply.digest(v): v for v in (config, child, index)}
        stages = []
        def fetch(url, headers=None, maximum=None, stage=None):
            stages.append(stage)
            if stage == 'config':
                raise self.http(400)
            if 'auth.docker.io' in url:
                return encoded({'token': 'synthetic-no-privilege-token'})
            return mapping[url.rsplit('/', 1)[-1]]
        with patch.object(supply, 'IMAGE', 'docker.io/pgsty/silo@' + supply.digest(index)), \
                patch.object(supply, 'fetch', side_effect=fetch):
            with self.assertRaises(urllib.error.HTTPError):
                supply.registry_evidence()
        self.assertEqual(['token', 'index', 'manifest', 'config'], stages)

    def test_fail_closed_behaviour_unchanged_on_success_path(self):
        class Response:
            def __enter__(self): return self
            def __exit__(self, *a): return False
            def read(self, n): return b'ok'
            def geturl(self): return 'https://registry.invalid/'
        with patch.object(supply.urllib.request, 'urlopen', return_value=Response()):
            self.assertEqual(b'ok', supply.fetch('https://registry.invalid/', stage='license'))
        class Big(Response):
            def read(self, n): return b'x' * n
        with patch.object(supply.urllib.request, 'urlopen', return_value=Big()):
            with self.assertRaisesRegex(ValueError, 'oversized'):
                supply.fetch('https://registry.invalid/', maximum=4, stage='license')


def load_tests(loader, tests, pattern):
    """Run G6 plaintext-cleanup regression under the existing QA Silo CI gate.

    The reusable QA Silo workflow runs only this filename, not every
    test_qa525_vnext_g6_v2_producer.py test. Keep strict cleanup negative
    tests mandatory without editing a workflow claimed by another PR.
    """
    from test_qa525_vnext_g6_v2_producer import G6V2ProducerStrictCleanupTests
    tests.addTests(loader.loadTestsFromTestCase(G6V2ProducerStrictCleanupTests))
    return tests


if __name__ == '__main__':
    unittest.main()
