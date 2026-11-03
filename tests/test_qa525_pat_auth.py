"""Offline QA525 PAT authentication tests. Never contact GitHub or read host PAT."""
import ast
from io import BytesIO
import importlib.util
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch
from urllib.error import HTTPError

ROOT = Path(__file__).resolve().parents[1]
MODULE = ROOT / 'scripts/qa/qa-agent-readiness.py'
ROUTE = ROOT / 'scripts/qa/operators/qa525-route-a-pat-6eed4-20261103.sh'
spec = importlib.util.spec_from_file_location('qa525_pat_readiness_test', MODULE)
agent = importlib.util.module_from_spec(spec)
spec.loader.exec_module(agent)

class Response(BytesIO):
    def __init__(self, url, value=b'{"ok":true}'):
        super().__init__(value)
        self.url = url
    def geturl(self):
        return self.url

class PatClientTests(unittest.TestCase):
    def test_nonqa_keeps_original_public_client(self):
        url = agent.API + '/branches/dev'
        with patch.object(agent.socket, 'gethostname', return_value='ci-runner'), \
             patch.object(agent, 'urlopen', return_value=Response(url)) as request, \
             patch.object(agent, '_authenticated_api_get') as authenticated:
            self.assertEqual(agent.fetch_public('/branches/dev'), {'ok': True})
        self.assertFalse(request.call_args.args[0].has_header('Authorization'))
        authenticated.assert_not_called()

    def test_host_nonroot_fail_closed(self):
        with patch.object(agent.socket, 'gethostname', return_value='glmdch'), \
             patch.object(agent.os, 'geteuid', return_value=1000), \
             patch.object(agent, 'urlopen') as anonymous:
            with self.assertRaisesRegex(agent.GateFailure, 'QA_PAT_REQUIRES_ROOT'):
                agent.fetch_public('/branches/dev')
        anonymous.assert_not_called()

    def test_host_absent_pat_no_anonymous_fallback(self):
        with tempfile.TemporaryDirectory() as d:
            with patch.object(agent.socket, 'gethostname', return_value='glmdch'), \
                 patch.object(agent.os, 'geteuid', return_value=0), \
                 patch.object(agent, 'QA_PAT', Path(d) / 'missing'), \
                 patch.object(agent, 'urlopen') as anonymous:
                with self.assertRaises(agent.GateFailure):
                    agent.fetch_public('/branches/dev')
        anonymous.assert_not_called()

    def test_host_passes_pat_to_authenticated_helper_not_url(self):
        token = 'github_pat_' + 'A'*75
        with patch.object(agent, '_read_qa_pat', return_value=token), \
             patch.object(agent, '_authenticated_api_get', return_value={'ok':1}) as auth:
            self.assertEqual(agent.fetch_public('/branches/dev'), {'ok':1})
        self.assertEqual(auth.call_args.args, (agent.API+'/branches/dev', token))
        self.assertNotIn(token, auth.call_args.args[0])

    def test_helper_blocks_external_origin_and_repo_before_credentials(self):
        bad = ('https://evil.example/steal', 'https://api.github.com/rate_limit',
               'http://api.github.com/repos/' + agent.REPO + '/branches/dev',
               agent.API + '/x#fragment', agent.API+'/x@evil', agent.API+'/bad\nheader')
        with patch.object(agent, 'build_opener') as opener:
            for url in bad:
                with self.subTest(url=url):
                    with self.assertRaisesRegex(agent.GateFailure, 'INVALID_AUTHENTICATED_API_URL'):
                        agent._authenticated_api_get(url, 'github_pat_'+'B'*75)
        opener.assert_not_called()

    def test_helper_sends_auth_only_to_expected_api_and_disables_redirects(self):
        class FakeOpener:
            def open(self, req, timeout):
                self.request = req
                return Response(agent.API + '/branches/dev')
        fake = FakeOpener()
        with patch.object(agent, 'build_opener', return_value=fake):
            self.assertEqual(agent._authenticated_api_get(agent.API+'/branches/dev',
                             'github_pat_'+'B'*75), {'ok':True})
        self.assertEqual(fake.request.full_url, agent.API+'/branches/dev')
        self.assertEqual(fake.request.get_header('Authorization'),
                         'Bearer github_pat_'+'B'*75)

    def test_redirect_rejected_even_in_authenticated_mode(self):
        class FakeOpener:
            def open(self, req, timeout):
                return Response('https://example.org/steal')
        with patch.object(agent, 'build_opener', return_value=FakeOpener()):
            with self.assertRaisesRegex(agent.GateFailure, 'API_REDIRECT_FORBIDDEN'):
                agent._authenticated_api_get(agent.API+'/branches/dev', 'github_pat_'+'C'*75)

    def test_401_returns_fixed_code_without_secret(self):
        error = HTTPError(agent.API+'/branches/dev', 401, 'token-sensitive', {}, None)
        with patch.object(agent, '_read_qa_pat', return_value='github_pat_'+'D'*75), \
             patch.object(agent, '_authenticated_api_get', side_effect=error):
            with self.assertRaises(agent.GateFailure) as result:
                agent.fetch_public('/branches/dev')
        self.assertEqual(str(result.exception), 'GITHUB_API_TOKEN_REJECTED')

    def test_invalid_paths_rejected_before_pat_read(self):
        with patch.object(agent, '_read_qa_pat') as loader:
            for path in ('//evil.com', '/@evil', '/x#fragment', '/bad\\path', '/bad\nheader'):
                with self.assertRaisesRegex(agent.GateFailure, 'INVALID_API_PATH'):
                    agent.fetch_public(path)
        loader.assert_not_called()

    def test_private_file_source_rejects_symlink(self):
        with tempfile.TemporaryDirectory() as td:
            parent = Path(td)
            target = parent/'real'; target.write_text('github_pat_'+'Z'*75+'\n')
            link = parent/'link'; link.symlink_to(target)
            with patch.object(agent.socket, 'gethostname', return_value='glmdch'), \
                 patch.object(agent.os, 'geteuid', return_value=0), \
                 patch.object(agent, 'QA_PAT', link):
                with self.assertRaises(agent.GateFailure):
                    agent._read_qa_pat()

    def test_rate_script_shell_and_inner_python_syntax(self):
        source = ROUTE.read_text()
        check = subprocess.run(['bash','-n',str(ROUTE)],capture_output=True,text=True)
        self.assertEqual(check.returncode,0,check.stderr)
        inner = source.split("<<'PY_AUTH_RATE'\n",1)[1].split('\nPY_AUTH_RATE',1)[0]
        ast.parse(inner)
        signed = source.split("<<'PY_SIGNED_LAUNCHER'\n", 1)[1].split(
            '\nPY_SIGNED_LAUNCHER', 1)[0]
        ast.parse(signed)
        for invariant in (
            'LAUNCHER_ATOMIC_SWAP_COMMITTED', 'candidate_operator_sha256',
            'PAT_READINESS_HASH', 'os.O_NOFOLLOW', 'os.O_CLOEXEC',
            'latest.st_ino == before.st_ino', 'current == expected',
            'source_sha', 'matches == 1',
        ):
            self.assertIn(invariant, signed)
        self.assertNotIn('if False else True', signed)
        self.assertNotIn('61e3f1f7e7dd561567e5fac415a6fa9695c2f74fa86adde21dd8ed0330d03cd3', source)

    def test_route_preserves_original_security_controls(self):
        s=ROUTE.read_text()
        for expected in ('MIN_RL=58','rem >= MIN_RL','AUTH_QUOTA_LOW_NO_RETRY',
                         'hold_locks','state_ok','custody','review-and-run',
                         'G6_REVIEW_HASH','EF_HISTORY_DRIFT','POST_IMAGE_REVISION'):
            self.assertIn(expected,s)
        self.assertNotIn('QA525_GITHUB_ANON_QUOTA',s)
        self.assertIn('ProxyHandler({})',s)
        self.assertIn('os.O_NOFOLLOW',s)

if __name__=='__main__':
    unittest.main()
