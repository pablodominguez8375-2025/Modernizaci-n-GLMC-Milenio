import copy
import importlib.util
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('ingress', ROOT / 'scripts/qa/public-ingress.py')
ingress = importlib.util.module_from_spec(spec)
spec.loader.exec_module(ingress)
ENV = {'PMGM_QA_HTTPS': 'true', 'PMGM_QA_WEB_PUBLIC_URL': ingress.ORIGIN,
       'PMGM_QA_OIDC_PUBLIC_URL': ingress.ORIGIN + '/identity'}


def fixture():
    return {'services': {
        'keycloak': {'ports': ['127.0.0.1:8180:8080'], 'environment': {}, 'volumes': [
            {'type': 'bind', 'source': 'old', 'target': '/opt/keycloak/data/import/pmgm-realm.json'}]},
        'web': {'ports': ['127.0.0.1:8081:80'], 'build': {'args': {}}},
        'api': {'environment': {}},
        'minio': {'image': 'sha256:' + 'a' * 64, 'pull_policy': 'never'}},
        'volumes': {}, 'networks': {'pmgm_srv01_internal': {}}}


class PublicIngressTests(unittest.TestCase):
    def test_opt_in_and_invalid_mode(self):
        model = fixture()
        self.assertIs(model, ingress.configure(model, {}))
        for value in ('TRUE', '1', '', 'yes'):
            with self.assertRaises(ValueError):
                ingress.configure(fixture(), {'PMGM_QA_HTTPS': value})

    def test_reject_noncanonical_urls(self):
        for name in ('PMGM_QA_WEB_PUBLIC_URL', 'PMGM_QA_OIDC_PUBLIC_URL'):
            for value in ('http://localhost:8081', ingress.ORIGIN + '/', 'https://evil.invalid', ''):
                with self.subTest(name=name, value=value), self.assertRaises(ValueError):
                    ingress.configure(fixture(), ENV | {name: value})

    def test_only_gateway_published_and_storage_untouched(self):
        source = fixture()
        storage = copy.deepcopy(source['services']['minio'])
        model = ingress.configure(source, ENV)
        self.assertEqual(['gateway'], [n for n, s in model['services'].items() if s.get('ports')])
        self.assertEqual(storage, model['services']['minio'])
        self.assertEqual(['start', '--import-realm'], model['services']['keycloak']['command'])
        self.assertEqual('/identity', model['services']['keycloak']['environment']['KC_HTTP_RELATIVE_PATH'])
        self.assertEqual('false', model['services']['web']['build']['args']['VITE_OIDC_ALLOW_HTTP_QA'])
        self.assertIn('/identity/realms/pmgm', model['services']['api']['environment']['Authentication__Authority'])

    def test_reject_extra_service_port_and_missing_import(self):
        model = fixture()
        model['services']['minio']['ports'] = ['9000:9000']
        with self.assertRaises(ValueError):
            ingress.configure(model, ENV)
        model = fixture()
        model['services']['keycloak']['volumes'] = []
        with self.assertRaises(ValueError):
            ingress.configure(model, ENV)

    def test_public_realm_is_derived_and_callbacks_exact(self):
        source = json.loads((ROOT / 'infrastructure/keycloak/pmgm-realm.json').read_text())
        realm = json.loads((ROOT / 'infrastructure/keycloak/pmgm-qa-public-realm.json').read_text())
        self.assertEqual(ingress.public_realm(source), realm)
        self.assertEqual(source['users'], realm['users'])
        client = next(c for c in realm['clients'] if c['clientId'] == 'pmgm-web')
        self.assertEqual([ingress.ORIGIN + '/auth/callback'], client['redirectUris'])
        self.assertEqual([ingress.ORIGIN], client['webOrigins'])
        self.assertEqual(ingress.ORIGIN + '/auth/logout', client['attributes']['post.logout.redirect.uris'])
        self.assertNotIn('localhost', json.dumps(client))
