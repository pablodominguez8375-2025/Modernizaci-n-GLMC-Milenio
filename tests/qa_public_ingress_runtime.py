#!/usr/bin/env python3
"""Real isolated Caddy + PostgreSQL + Keycloak. No glmdch/DNS/real credentials."""
import importlib.util
import json
import os
from pathlib import Path
import secrets
import subprocess
import tempfile
import time
import urllib.parse
import uuid

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('qa_ingress', ROOT / 'scripts/qa/public-ingress.py')
ingress = importlib.util.module_from_spec(spec)
spec.loader.exec_module(ingress)
ORIGIN = ingress.ORIGIN
HOST = urllib.parse.urlsplit(ORIGIN).hostname


def run(args, **kwargs):
    return subprocess.run(args, check=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                          text=True, timeout=kwargs.pop('timeout', 180), **kwargs)


def main():
    project = 'qa-ingress-' + uuid.uuid4().hex[:12]
    env = dict(os.environ)
    env.update({'PMGM_QA_HTTPS': 'true', 'PMGM_QA_WEB_PUBLIC_URL': ORIGIN,
                'PMGM_QA_OIDC_PUBLIC_URL': ORIGIN + '/identity',
                'PMGM_QA_DB_PASSWORD': secrets.token_urlsafe(32),
                'PMGM_QA_USER_PASSWORD': 'Aa1!' + secrets.token_urlsafe(28),
                'PMGM_QA_KEYCLOAK_ADMIN_PASSWORD': secrets.token_urlsafe(32)})
    # Required Compose placeholders unrelated to this OIDC-only laboratory.
    for name in ('STORAGE_ADMIN_USER', 'STORAGE_ADMIN_PASSWORD', 'DOCUMENT_ACCESS_KEY',
                 'DOCUMENT_SECRET_KEY', 'BACKUP_ACCESS_KEY', 'BACKUP_SECRET_KEY'):
        env['PMGM_QA_' + name] = secrets.token_urlsafe(32)
    evidence = {'source_sha': run(['git', '-C', str(ROOT), 'rev-parse', 'HEAD']).stdout.strip(),
                'scope': 'SYNTHETIC_EPHEMERAL_RUNNER_ONLY', 'checks': []}
    with tempfile.TemporaryDirectory(prefix='qa-ingress-') as tmp:
        tmp = Path(tmp)
        model = json.loads(run(['docker', 'compose', '-f', str(ROOT / 'infrastructure/docker-compose.srv01.yml'),
                               '--profile', '*', 'config', '--no-interpolate', '--format', 'json'], env=env).stdout)
        model = ingress.configure(model, env)
        model.pop('name', None)
        # Exercise the exact transformed KC and gateway; exclude application/storage.
        model['services'] = {k: v for k, v in model['services'].items() if k in ('postgres', 'keycloak', 'gateway')}
        model['services']['web'] = {'image': 'nginx:alpine', 'networks': ['pmgm_srv01_internal']}
        gateway = model['services']['gateway']
        gateway['ports'] = [{'target': 443, 'published': '0', 'host_ip': '127.0.0.1', 'protocol': 'tcp'}]
        # Only lab trust changes: same host/path/routes/headers as packaged config.
        # Never ship tls internal in the production profile.
        caddyfile = tmp / 'Caddyfile'
        caddyfile.write_text((ROOT / 'infrastructure/caddy/Caddyfile.qa').read_text().replace(
            HOST + ' {', HOST + ' {\n    tls internal'))
        gateway['volumes'][0]['source'] = str(caddyfile)
        for section in ('volumes', 'networks'):
            for resource in model.get(section, {}).values():
                resource.pop('name', None)
        model['volumes'] = {k: v for k, v in model['volumes'].items()
                            if k in ('pmgm_srv01_postgres_data', 'pmgm_qa_caddy_data', 'pmgm_qa_caddy_config')}
        path = tmp / 'compose.json'
        path.write_text(json.dumps(model))
        compose = ['docker', 'compose', '-p', project, '-f', str(path)]
        try:
            run(compose + ['up', '-d'], env=env, timeout=240)
            port = run(compose + ['port', 'gateway', '443'], env=env).stdout.strip().rsplit(':', 1)[1]
            # Export ephemeral CA and verify all HTTPS requests using it (no curl -k).
            ca = tmp / 'root.crt'
            for _ in range(40):
                copied = subprocess.run(compose + ['cp', 'gateway:/data/caddy/pki/authorities/local/root.crt', str(ca)],
                                        env=env, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=10)
                if copied.returncode == 0:
                    break
                time.sleep(2)
            assert ca.is_file(), 'ephemeral CA unavailable'

            def request(route, params=None, method='GET'):
                args = ['curl', '--silent', '--show-error', '--noproxy', '*', '--max-time', '12',
                        '--cacert', str(ca), '--connect-to', HOST + ':443:127.0.0.1:' + port,
                        '--dump-header', str(tmp / 'headers'), '--output', str(tmp / 'body'),
                        '--write-out', '%{http_code}', '--request', method]
                if params is not None:
                    for k, v in params.items():
                        args += ['--data-urlencode', k + '=' + v]
                    if method == 'GET':
                        args += ['--get']
                status = int(run(args + [ORIGIN + route], timeout=20).stdout)
                return status, (tmp / 'headers').read_text().lower(), (tmp / 'body').read_text()

            discovery_path = '/identity/realms/pmgm/.well-known/openid-configuration'
            status = 0
            for _ in range(100):
                try:
                    status, headers, body = request(discovery_path)
                    if status == 200:
                        break
                except subprocess.CalledProcessError:
                    pass
                time.sleep(2)
            assert status == 200, 'Keycloak discovery unavailable'
            discovery = json.loads(body)
            issuer = ORIGIN + '/identity/realms/pmgm'
            assert discovery['issuer'] == issuer
            for key in ('authorization_endpoint', 'token_endpoint', 'jwks_uri', 'end_session_endpoint'):
                assert discovery[key].startswith(issuer + '/'), key
            evidence['checks'].append('TRUSTED_TLS_AND_CANONICAL_DISCOVERY')
            status, headers, _ = request('/')
            assert status == 200 and 'strict-transport-security: max-age=31536000' in headers
            assert "content-security-policy: default-src 'self'" in headers
            _, identity_headers, _ = request(discovery_path)
            assert "default-src 'self'; base-uri 'self'; object-src 'none'" not in identity_headers
            evidence['checks'].append('HSTS_AND_SCOPED_CSP')
            for route in ('/identity/admin', '/identity/admin/', '/identity/admin/master/console/',
                          '/identity/realms/master', '/identity/realms/master/.well-known/openid-configuration'):
                assert request(route)[0] == 404, 'admin/master route exposed'
            evidence['checks'].append('ADMIN_AND_MASTER_DENIED')
            auth = '/identity/realms/pmgm/protocol/openid-connect/auth'
            params = {'client_id': 'pmgm-web', 'response_type': 'code', 'scope': 'openid',
                      'code_challenge_method': 'S256', 'code_challenge': 'a' * 43,
                      'redirect_uri': ORIGIN + '/auth/callback'}
            status, _, body = request(auth, params)
            assert status == 200 and 'username' in body, 'valid PKCE callback rejected'
            for bad in ('http://localhost:8081/auth/callback', 'http://127.0.0.1:8081/auth/callback',
                        'http://' + HOST + '/auth/callback', 'https://evil.invalid/auth/callback',
                        ORIGIN + '/unexpected'):
                status, headers, _ = request(auth, params | {'redirect_uri': bad})
                assert status == 400 and '\nlocation:' not in headers, 'unsafe callback accepted'
            without_pkce = {k: v for k, v in params.items() if not k.startswith('code_challenge')}
            status, headers, _ = request(auth, without_pkce)
            # OIDC may reject at the validated callback with error=invalid_request.
            # A bare 302 is not evidence of rejection: bind origin/path and error, no code.
            locations = [h.split(':', 1)[1].strip() for h in headers.splitlines() if h.startswith('location:')]
            denied_redirect = False
            if status == 302 and len(locations) == 1:
                location = urllib.parse.urlsplit(locations[0])
                query = urllib.parse.parse_qs(location.query)
                denied_redirect = (location.scheme + '://' + location.netloc == ORIGIN and
                                   location.path == '/auth/callback' and
                                   query.get('error') == ['invalid_request'] and 'code' not in query)
            assert status == 400 or denied_redirect, 'PKCE omission not rejected with an OIDC error'
            evidence['pkce_omission_http_status'] = status
            evidence['checks'].append('PKCE_AUTHORIZATION_PAGE_AND_INVALID_CALLBACKS')
            status, _, _ = request('/identity/realms/pmgm/protocol/openid-connect/logout',
                {'client_id': 'pmgm-web', 'post_logout_redirect_uri': ORIGIN + '/auth/logout'})
            assert status in (200, 302), 'valid logout redirect rejected'
            for bad in ('http://localhost:8081/auth/logout', 'https://evil.invalid/auth/logout'):
                status, headers, _ = request('/identity/realms/pmgm/protocol/openid-connect/logout',
                    {'client_id': 'pmgm-web', 'post_logout_redirect_uri': bad})
                assert status == 400 and '\nlocation:' not in headers, 'unsafe logout accepted'
            evidence['checks'].append('INVALID_LOGOUT_REDIRECTS')
            # Existing authenticated smoke still needs direct grant; expose no tokens in evidence/logs.
            status, _, body = request('/identity/realms/pmgm/protocol/openid-connect/token',
                {'client_id': 'pmgm-web', 'grant_type': 'password', 'username': 'qa.admin',
                 'password': env['PMGM_QA_USER_PASSWORD']}, method='POST')
            assert status == 200, 'synthetic login failed'
            token = json.loads(body)['access_token']
            import base64
            claims = json.loads(base64.urlsafe_b64decode(token.split('.')[1] + '=='))
            assert claims['iss'] == issuer and 'pmgm-api' in claims['aud']
            evidence['checks'].append('SYNTHETIC_TOKEN_ISSUER_AND_AUDIENCE')
            evidence['status'] = 'PASS_NOT_DEPLOYED'
            output = ROOT / 'artifacts/qa-public-ingress.json'
            output.parent.mkdir(exist_ok=True)
            output.write_text(json.dumps(evidence, indent=2) + '\n')
            print(json.dumps(evidence))
        finally:
            subprocess.run(compose + ['down', '-v', '--remove-orphans'], env=env,
                           stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=90)


if __name__ == '__main__':
    try:
        main()
    except Exception as error:
        # subprocess exceptions include full argv, which can contain synthetic credentials.
        print('QA ingress FAIL: ' + (str(error) if isinstance(error, AssertionError) else type(error).__name__))
        raise SystemExit(1)
