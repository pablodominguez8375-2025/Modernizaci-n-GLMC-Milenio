#!/usr/bin/env python3
"""G8 evidence on the existing private LAB2. Synthetic users exclusively.

Inspects current Keycloak and institutional DB fixtures, then performs OIDC
login and API read-only authorization probes over HTTPS bound to 127.0.0.1.
Successful session GET requests can create normal synthetic audit events.
Never edits accounts, roles, DB fixtures or Docker; never prints secrets/tokens.
Not a frontend/UAT acceptance.
"""
import base64
import http.client
import json
import os
from pathlib import Path
import socket
import ssl
import stat
import subprocess
import sys
from urllib.parse import urlencode

PROJECT = 'centenario-qa-lab2'
SHA = '4ef456fee3a578a9e6418f3f7072af5dd79eb045'
BASE = Path('/opt/centenario/qa-lab-firstboot/recovery-v2')
ISSUER = 'https://sistema.granlogiamixta.cl/identity/realms/pmgm'
HOST = 'sistema.granlogiamixta.cl'
LODGE = '23232323-2323-2323-2323-232323232323'
OTHER = '11111111-1111-1111-1111-111111111111'
PG = PROJECT + '-postgres-1'

class Blocked(Exception):
    pass

def guard(ok, code):
    if not ok:
        raise Blocked(code)

def read_private(name):
    p = BASE / name
    guard(p.is_file() and not p.is_symlink(), 'EVIDENCE_MISSING')
    st = p.stat()
    guard(st.st_uid == 0 and stat.S_IMODE(st.st_mode) == 0o600,
          'EVIDENCE_PERMISSIONS')
    return json.loads(p.read_text())

def docker(*argv):
    try:
        r = subprocess.run(['docker', *argv], stdout=subprocess.PIPE,
                           stderr=subprocess.PIPE, stdin=subprocess.DEVNULL,
                           timeout=25, check=False)
    except subprocess.TimeoutExpired:
        raise Blocked('DOCKER_READ_TIMEOUT') from None
    guard(r.returncode == 0 and len(r.stdout) <= 65536,
          'DOCKER_READ_FAILED')
    return r.stdout.decode('utf-8', 'strict').strip()

def scalar(database, query):
    guard(database in ('pmgm','pmgm_keycloak'), 'UNKNOWN_DATABASE')
    value = docker('exec', PG, 'psql', '-X', '-Atq', '-v', 'ON_ERROR_STOP=1',
                   '-U', 'pmgm_app', '-d', database, '-c', query)
    guard('\n' not in value and len(value) <= 24, 'QUERY_UNEXPECTED_OUTPUT')
    return value

def preflight():
    guard(os.geteuid() == 0, 'ROOT_REQUIRED')
    os.umask(0o077)
    state, model = read_private('status.json'), read_private('compose.recovery.json')
    guard(state.get('status') == 'PRIVATE_LAB_SMOKE_PASS' and
          state.get('source_sha') == SHA and state.get('project') == PROJECT and
          state.get('scope') == 'PRIVATE_SYNTHETIC_LAB_NOT_G3_G6_UAT',
          'QA_LAB_SIGNED_SMOKE_NOT_VERIFIED')
    services = model.get('services', {})
    guard({'api','keycloak','postgres','gateway'}.issubset(services),
          'LAB_SERVICE_MISSING')
    guard(all(not s.get('privileged',False) and s.get('network_mode') != 'host'
              for s in services.values()), 'DANGEROUS_LAB_MODEL')
    ports = services['gateway'].get('ports', [])
    guard(len(ports) == 2 and
          {str(p.get('published')) for p in ports} == {'18443','18080'} and
          all(p.get('host_ip') == '127.0.0.1' for p in ports),
          'GATEWAY_NOT_PRIVATE')
    env = services['api'].get('environment', {})
    guard(env.get('ASPNETCORE_ENVIRONMENT') == 'Staging' and
          all(str(env.get(k,'')).lower() == 'true' for k in
              ('DemoData__Enabled','PMGM_QA_G8_ENABLED','PMGM_QA_HTTPS',
               'PMGM_QA_DB2')), 'G8_FIXTURE_FLAGS_NOT_ENABLED')
    secret = services['keycloak'].get('environment', {}).get('PMGM_QA_USER_PASSWORD')
    guard(isinstance(secret, str) and len(secret) >= 20 and
          not secret.startswith('$'), 'G8_QA_PASSWORD_NOT_CONFIGURED')
    up = docker('ps','--filter','label=com.docker.compose.project='+PROJECT,
                '--format','{{.Label "com.docker.compose.service"}}').splitlines()
    guard({'api','keycloak','postgres','gateway'}.issubset(up),
          'G8_RUNTIME_INCOMPLETE')
    guard(docker('inspect','-f','{{.State.Health.Status}}',PG) == 'healthy',
          'POSTGRES_NOT_HEALTHY')
    print('G8_SIGNED_PRIVATE_QA_PREFLIGHT: PASS',flush=True)
    return secret

def check_institutional_data():
    kc = scalar('pmgm_keycloak',
      "SELECT count(*) FROM public.user_entity WHERE id LIKE 'aa820001-0000-4000-8000-%' AND enabled=true")
    members = scalar('pmgm', """
WITH expected AS (
 SELECT n, 'aa820001-0000-4000-8000-'||lpad(n::text,12,'0') AS s,
 'bb820002-0000-4000-8000-'||lpad(n::text,12,'0') AS m,
 CASE WHEN n=11 THEN 'apprentice' WHEN n=12 THEN 'fellowcraft'
 ELSE 'master' END AS g FROM generate_series(2,19) n
)
SELECT count(DISTINCT e.n) FROM expected e
JOIN core.member_identity_links l ON l."Subject"=e.s
 AND l."Issuer"='https://sistema.granlogiamixta.cl/identity/realms/pmgm'
 AND l."RevokedAtUtc" IS NULL AND l."MemberId"::text=e.m
JOIN core.memberships ms ON ms."MemberId"=l."MemberId"
 AND ms."Status"='active'
 AND ms."OrganizationId"='23232323-2323-2323-2323-232323232323'
 AND (ms."StartDate" IS NULL OR ms."StartDate" <= CURRENT_DATE)
 AND (ms."EndDate" IS NULL OR ms."EndDate" >= CURRENT_DATE)
JOIN core.degree_events d ON d."MemberId"=l."MemberId"
 AND d."Degree"=e.g AND d."EffectiveDate"<=CURRENT_DATE
""")
    offices = scalar('pmgm', """
SELECT (
 (SELECT count(*) FROM core.office_assignments
  WHERE "MemberId"='bb820002-0000-4000-8000-000000000003'
  AND "OfficeType"='venerable_master'
  AND "StartDate"<=CURRENT_DATE
  AND ("EndDate" IS NULL OR "EndDate">=CURRENT_DATE))=1
 AND
 (SELECT count(*) FROM core.office_assignments
  WHERE "MemberId"='bb820002-0000-4000-8000-000000000014'
  AND "EndDate"<CURRENT_DATE)=1
)::text
""")
    print('G8_KEYCLOAK_USERS: '+('PASS' if kc=='19' else 'PENDING'),flush=True)
    print('G8_MEMBER_LINKS_AND_GRADES: '+('PASS' if members=='18' else 'PENDING'),flush=True)
    print('G8_ACTIVE_AND_EXPIRED_OFFICES: '+('PASS' if offices=='t' else 'PENDING'),flush=True)
    guard(kc=='19' and members=='18' and offices=='t','G8_FIXTURES_NOT_READY')

class LoopbackTLS(http.client.HTTPSConnection):
    def __init__(self):
        ctx = ssl.SSLContext(ssl.PROTOCOL_TLS_CLIENT)
        ctx.check_hostname = False
        ctx.verify_mode = ssl.CERT_NONE  # QA's self-signed private gateway only.
        super().__init__(HOST,18443,timeout=15,context=ctx)
    def connect(self):
        sock = socket.create_connection(('127.0.0.1',18443),self.timeout)
        self.sock = self._context.wrap_socket(sock,server_hostname=HOST)

def request(method,path,body=None,token=None):
    guard((method=='POST' and path ==
            '/identity/realms/pmgm/protocol/openid-connect/token') or
          (method=='GET' and path in ('/api/session/me',
                                     '/api/calendar?organizationId='+OTHER)),
          'REQUEST_NOT_READ_ONLY')
    headers={'Cache-Control':'no-store'}
    if body is not None:
        headers['Content-Type']='application/x-www-form-urlencoded'
    if token:
        headers['Authorization']='Bearer '+token
    conn = LoopbackTLS()
    try:
        conn.request(method,path,body=body,headers=headers)
        r=conn.getresponse()
        data=r.read(131073)
        guard(len(data)<=131072,'HTTP_RESPONSE_TOO_LARGE')
        return r.status,data
    finally:
        conn.close()

def claims(token):
    parts=token.split('.')
    guard(len(parts)==3,'TOKEN_NOT_JWT')
    p=parts[1]+'='*((-len(parts[1]))%4)
    data=base64.urlsafe_b64decode(p)
    guard(len(data)<16000,'TOKEN_TOO_LARGE')
    return json.loads(data)

# Each tuple: synthetic principal, realm user index, organization/order,
# capabilities that must be TRUE, capabilities that must be FALSE.
PROFILES=(
 ('qa.hermano1',11,'organization',(),('canManageLodgeTreasury','canManageLodgeSecretariat','canConfigureSystem')),
 ('qa.hermano2',12,'organization',(),('canManageLodgeTreasury','canManageGrandSecretariat')),
 ('qa.hermano3',13,'organization',(),('canManageLodgeTreasury','canManageGrandSecretariat')),
 ('qa.cargovencido23',14,'organization',(),('canApproveLodgeExpenses','canManageLodgeSecretariat')),
 ('qa.tesoreria23',9,'organization',('canManageLodgeTreasury',),('canManageLodgeSecretariat','canConfigureSystem')),
 ('qa.secretaria23',8,'organization',('canManageLodgeSecretariat',),('canManageLodgeTreasury','canConfigureSystem')),
 ('qa.venerable23',3,'organization',('canApproveLodgeExpenses',),('canManageLodgeTreasury','canConfigureSystem')),
 ('qa.gransecretaria',15,'order',('canManageGrandSecretariat',),('canConfigureSystem',)),
 ('qa.regimeninterior',16,'order',('canRunRegimenInteriorReports',),('canConfigureSystem',)),
 ('qa.granarchivero',17,'order',('canManageGrandArchive',),('canConfigureSystem',))
)

def check_sessions(secret):
    status,_=request('GET','/api/session/me')
    guard(status==401,'ANONYMOUS_SESSION_NOT_DENIED')
    print('G8_ANONYMOUS_SESSION_DENIED: PASS',flush=True)
    for user,index,scope,allowed,denied in PROFILES:
        form=urlencode({'client_id':'pmgm-web','grant_type':'password',
                        'username':user,'password':secret}).encode()
        status,raw=request('POST',
                '/identity/realms/pmgm/protocol/openid-connect/token',form)
        code=user.upper().replace('.','_')
        guard(status==200,'OIDC_LOGIN_FAILED_'+code)
        token=json.loads(raw).get('access_token')
        guard(isinstance(token,str) and len(token)>50,'TOKEN_MISSING_'+code)
        c=claims(token)
        guard(c.get('iss')==ISSUER and
              c.get('sub')==f'aa820001-0000-4000-8000-{index:012d}' and
              c.get('pmgm_scope')==scope,
              'OIDC_SUBJECT_OR_ISSUER_MISMATCH_'+code)
        if scope=='organization':
            guard(c.get('pmgm_org')==LODGE,'WRONG_LODGE_CLAIM_'+code)
        status,raw=request('GET','/api/session/me',token=token)
        guard(status==200,'API_SESSION_LOGIN_FAILED_'+code)
        profile=json.loads(raw)
        caps=profile.get('capabilities',{})
        guard(profile.get('accessScope')==scope and
              all(caps.get(k) is True for k in allowed) and
              all(caps.get(k) is False for k in denied),
              'API_RBAC_CAPABILITIES_MISMATCH_'+code)
        if user=='qa.hermano1':
            status,_=request('GET','/api/calendar?organizationId='+OTHER,token=token)
            guard(status==403,'CROSS_LODGE_CALENDAR_NOT_DENIED')
            print('G8_OTHER_LODGE_ACCESS_DENIED: PASS',flush=True)
        print('G8_'+code+': PASS',flush=True)
    print('G8_REAL_OIDC_SESSIONS_AND_RBAC: PASS',flush=True)

def main():
    secret=preflight()
    check_institutional_data()
    check_sessions(secret)
    print('=== G8 QA SYNTHETIC IDENTITY/RBAC PHASE1 PASS - UI/UAT PENDING ===',flush=True)

if __name__=='__main__':
    try:
        main()
    except Blocked as error:
        print('BLOQUEADO: '+str(error),flush=True)
        sys.exit(1)
    except Exception as error:
        print('BLOQUEADO: '+type(error).__name__,flush=True)
        sys.exit(1)
