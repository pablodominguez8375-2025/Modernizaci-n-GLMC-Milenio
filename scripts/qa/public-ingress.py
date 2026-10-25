#!/usr/bin/env python3
"""Opt-in canonical HTTPS QA model; no host changes and no storage substitution."""
import copy
import json
import os
from pathlib import Path

ORIGIN = 'https://sistema.granlogiamixta.cl'
ROOT = Path(__file__).resolve().parents[2]


def public_realm(source):
    realm = copy.deepcopy(source)
    realm['sslRequired'] = 'external'
    # #604: keep the isolated CI fixture Master intact, but never import an
    # implicit second Master from the derived canonical operational QA realm.
    # A real QA admin is provided by the host-only controlled bootstrap.
    for user in realm['users']:
        if user['username'] == 'qa.admin':
            if user['id'] != 'aa820001-0000-4000-8000-000000000001':
                raise ValueError('unexpected QA synthetic admin subject')
            roles = user['realmRoles']
            if roles.count('platform_superadmin') != 1:
                raise ValueError('unexpected QA synthetic Master role fixture')
            user['realmRoles'] = [role for role in roles if role != 'platform_superadmin']
        elif 'platform_superadmin' in user.get('realmRoles', []):
            raise ValueError('unexpected second synthetic Master role')
    client = next(c for c in realm['clients'] if c['clientId'] == 'pmgm-web')
    client['redirectUris'] = [ORIGIN + '/auth/callback']
    client['webOrigins'] = [ORIGIN]
    client['attributes']['post.logout.redirect.uris'] = ORIGIN + '/auth/logout'
    return realm


def configure(model, env=None):
    env = os.environ if env is None else env
    mode = env.get('PMGM_QA_HTTPS', 'false')
    if mode not in ('true', 'false'):
        raise ValueError('PMGM_QA_HTTPS must be true or false')
    if mode == 'false':
        return model
    if (env.get('PMGM_QA_WEB_PUBLIC_URL') != ORIGIN or
            env.get('PMGM_QA_OIDC_PUBLIC_URL') != ORIGIN + '/identity'):
        raise ValueError('QA HTTPS requires the approved canonical origin and /identity issuer')
    realm_path = ROOT / 'infrastructure/keycloak/pmgm-qa-public-realm.json'
    expected = public_realm(json.loads((ROOT / 'infrastructure/keycloak/pmgm-realm.json').read_text()))
    if json.loads(realm_path.read_text()) != expected:
        raise ValueError('public QA realm drift; regenerate before packaging')
    services = model['services']
    kc = services['keycloak']
    kc['command'] = ['start', '--import-realm']
    kc.pop('ports', None)
    kc['environment'].update({
        'KC_HTTP_RELATIVE_PATH': '/identity', 'KC_PROXY_HEADERS': 'xforwarded',
        'KC_HOSTNAME': ORIGIN + '/identity', 'KC_HOSTNAME_STRICT': 'true',
        'KC_HOSTNAME_BACKCHANNEL_DYNAMIC': 'true', 'KC_HTTP_ENABLED': 'true'})
    # Replace only the import, preserving all other mounts.
    imports = [v for v in kc['volumes'] if v.get('target') == '/opt/keycloak/data/import/pmgm-realm.json']
    if len(imports) != 1:
        raise ValueError('expected exactly one QA realm import')
    imports[0]['source'] = str(realm_path)
    web = services['web']
    web.pop('ports', None)
    web['build']['args'].update({'VITE_OIDC_ALLOW_HTTP_QA': 'false',
                                'VITE_OIDC_AUTHORITY': ORIGIN + '/identity/realms/pmgm'})
    services['api']['environment']['Authentication__Authority'] = 'http://keycloak:8080/identity/realms/pmgm'
    # Default-deny all direct public service ports, not just known web/KC ports.
    if any(s.get('ports') for s in services.values()):
        raise ValueError('direct service port forbidden in public QA')
    network = 'pmgm_srv01_internal'
    services['gateway'] = {
        'image': 'caddy:2-alpine', 'restart': 'unless-stopped',
        'depends_on': ['web', 'keycloak'],
        'ports': [{'target': 80, 'published': '80', 'protocol': 'tcp'},
                  {'target': 443, 'published': '443', 'protocol': 'tcp'}],
        'volumes': [
            {'type': 'bind', 'source': str(ROOT / 'infrastructure/caddy/Caddyfile.qa'),
             'target': '/etc/caddy/Caddyfile', 'read_only': True},
            {'type': 'volume', 'source': 'pmgm_qa_caddy_data', 'target': '/data'},
            {'type': 'volume', 'source': 'pmgm_qa_caddy_config', 'target': '/config'}],
        'networks': {network: None}}
    model.setdefault('volumes', {}).update({'pmgm_qa_caddy_data': {}, 'pmgm_qa_caddy_config': {}})
    return model


if __name__ == '__main__':
    # Deterministic developer generation; no secrets or environment expansion.
    source = json.loads((ROOT / 'infrastructure/keycloak/pmgm-realm.json').read_text())
    (ROOT / 'infrastructure/keycloak/pmgm-qa-public-realm.json').write_text(
        json.dumps(public_realm(source), indent=2) + '\n')
