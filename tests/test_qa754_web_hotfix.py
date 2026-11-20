"""QA #754 synthetic/fail-closed contract tests. Never touch Docker or accounts."""
import importlib.util
from pathlib import Path
from collections import namedtuple
from unittest import TestCase, main
from unittest.mock import patch
import tempfile
import json
import os
import stat
from types import SimpleNamespace

SRC = Path(__file__).resolve().parents[1]/'scripts/qa/operators/pmgm-qa754-web-hotfix.py'
if not SRC.is_file(): SRC = Path(__file__).with_name('pmgm-qa754-web-hotfix.py')
spec = importlib.util.spec_from_file_location('qa754web', SRC)
mod = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mod)

class HotfixContract(TestCase):
    def test_fixed_commits(self):
        self.assertEqual(mod.BASE_COMMIT, '6ff4ae0d092d4214df4ea31af484efbc969fde54')
        self.assertEqual(mod.HOTFIX_COMMIT, '424153c3a06c2b0bc0f8c2e8599eed4bb9ac6786')

    def test_only_expected_source_files(self):
        self.assertEqual(mod.ALLOWED_DIFF, {
            'frontend/src/api/pmgmApi.ts',
            'frontend/src/api/qa754-hotfix415.test.ts',
            'docs/handoffs/2026-10-27-chatgpt-qa754-web-hotfix-6ff4.md'})

    def test_patch_contains_single_explicit_json_helper(self):
        self.assertIn('postJson<UserAccountCreated>', mod.NEW_CALL)
        self.assertIn('JSON.stringify(request)', mod.OLD_CALL)
        self.assertNotEqual(mod.OLD_CALL, mod.NEW_CALL)

    def test_no_mutation_without_explicit_mode(self):
        with self.assertRaises(SystemExit):
            mod.main([])

    def test_require_raises_sanitized_code(self):
        with self.assertRaisesRegex(mod.Blocked, 'FIXED_CODE'):
            mod.require(False, 'FIXED_CODE')

    def test_api_identity_requires_all_expected_values(self):
        d = {k: v for k, v in mod.EXPECTED_IDENTITY.items()}
        d['UserAccounts__Identity__ClientSecret'] = 'placeholder-secret'
        api = {'Config': {'Env': [k+'='+v for k,v in d.items()]}}
        mod.identity_active(api)
        d['UserAccounts__Identity__ClientId'] = 'bad-client'
        with self.assertRaisesRegex(mod.Blocked, 'API_IDENTITY_NOT_ACTIVE'):
            mod.identity_active({'Config': {'Env': [k+'='+v for k,v in d.items()]}})

    def test_no_duplicate_env_keys(self):
        with self.assertRaisesRegex(mod.Blocked, 'API_ENV_DUPLICATE'):
            mod.env_dict({'Config': {'Env': ['A=1','A=2']}})

    def test_public_build_only_safe_public_keys(self):
        config={'services': {'web': {'build': {'args': {
            'VITE_USE_MOCKS':'false','VITE_API_BASE_URL':'',
            'VITE_OIDC_AUTHORITY':mod.WEB_ORIGINAL+'/identity/realms/pmgm',
            'VITE_OIDC_CLIENT_ID':'pmgm-web'}}}}}
        with tempfile.TemporaryDirectory() as td:
            path=Path(td)/'c.json';path.write_text(json.dumps(config))
            result=mod.public_build_args(path)
            self.assertEqual(result['VITE_OIDC_CLIENT_ID'],'pmgm-web')
            config['services']['web']['build']['args']['API_PRIVATE_TOKEN']='secret'
            path.write_text(json.dumps(config))
            with self.assertRaisesRegex(mod.Blocked,'UNSAFE_WEB_BUILD_ARGS'):
                mod.public_build_args(path)

    def test_public_build_denies_mock_mode(self):
        with tempfile.TemporaryDirectory() as td:
            path=Path(td)/'c.json';path.write_text(json.dumps({'services': {'web': {'build': {'args': {
                'VITE_USE_MOCKS':'true','VITE_OIDC_CLIENT_ID':'pmgm-web'}}}}}))
            with self.assertRaisesRegex(mod.Blocked,'WEB_BUILD_IDENTITY_CHANGED'):
                mod.public_build_args(path)

    def test_public_health_version_mismatch_fails(self):
        def get(path, require_json=False):
            if path=='/health/web':return b'ok\n'
            if path=='/health/ready':return {'status':'ready','database':'postgresql'}
            return {'environment':'QA_SYNTHETIC','source_sha':mod.BASE_COMMIT}
        with patch.object(mod,'public_request',side_effect=get):
            mod.public_check(expected_sha=mod.BASE_COMMIT)
            with self.assertRaisesRegex(mod.Blocked,'PUBLIC_WEB_VERSION_UNEXPECTED'):
                mod.public_check(expected_sha=mod.HOTFIX_COMMIT)

    def test_root_overlay_json_limited_to_web(self):
        with tempfile.TemporaryDirectory() as td:
            path=Path(td)/('web-'+'a'*64+'.json')
            path.write_text(json.dumps({'services':{'web':{'image':'sha256:'+'a'*64}}}))
            pretend_root=SimpleNamespace(st_mode=stat.S_IFDIR|0o700,st_uid=0)
            with patch.object(mod,'PROJECT_DIR',Path(td)),\
                 patch.object(mod,'root_regular',return_value=None),\
                 patch.object(Path,'lstat',return_value=pretend_root):
                mod.expected_web_override(path,'sha256:'+'a'*64)
                path.write_text(json.dumps({'services':{'web':{'image':'sha256:'+'a'*64},'api':{}}}))
                with self.assertRaisesRegex(mod.Blocked,'WEB_OVERRIDE_CONTENT_CHANGED'):
                    mod.expected_web_override(path,'sha256:'+'a'*64)

    def test_legacy_web_overlay_in_private_qa_auto_lite_is_accepted(self):
        with tempfile.TemporaryDirectory() as td:
            directory=Path(td)
            image='sha256:'+'d'*64
            layer=directory/('web-'+'d'*64+'.json')
            layer.write_text(json.dumps({'services':{'web':{'image':image}}}))
            rootdir=SimpleNamespace(st_mode=stat.S_IFDIR|0o700,st_uid=0)
            with patch.object(mod,'LEGACY_WEB_STATE',directory),\
                 patch.object(mod,'root_regular',return_value=None),\
                 patch.object(Path,'lstat',return_value=rootdir):
                mod.expected_web_override(layer,image)

    def test_web_override_parent_must_be_root_controlled(self):
        with tempfile.TemporaryDirectory() as td:
            directory=Path(td)
            image='sha256:'+'d'*64
            layer=directory/('web-'+'d'*64+'.json')
            layer.write_text(json.dumps({'services':{'web':{'image':image}}}))
            unsafe=SimpleNamespace(st_mode=stat.S_IFDIR|0o777,st_uid=0)
            with patch.object(mod,'LEGACY_WEB_STATE',directory),\
                 patch.object(mod,'root_regular',return_value=None),\
                 patch.object(Path,'lstat',return_value=unsafe):
                with self.assertRaisesRegex(mod.Blocked,'WEB_OVERRIDE_PARENT_NOT_ROOT_CONTROLLED'):
                    mod.expected_web_override(layer,image)

    def test_web_override_parent_symlink_is_rejected(self):
        with tempfile.TemporaryDirectory() as td:
            directory=Path(td)
            image='sha256:'+'e'*64
            layer=directory/('web-'+'e'*64+'.json')
            with patch.object(mod,'LEGACY_WEB_STATE',directory),\
                 patch.object(Path,'is_symlink',return_value=True):
                with self.assertRaisesRegex(mod.Blocked,'WEB_OVERRIDE_PARENT_SYMLINK'):
                    mod.expected_web_override(layer,image)

    def test_web_snapshot_accepts_api_three_layers_web_legacy_two(self):
        root=Path('/opt/centenario/qa-lab-firstboot/recovery-v2')
        legacy=Path('/opt/centenario/qa-auto-lite')
        image='sha256:'+'f'*64
        base=root/'compose.recovery.json'
        api_image=root/('api-'+'f'*40+'.json')
        web_image=legacy/('web-'+'f'*64+'.json')
        api={'Id':'api-stable','Image':image,'Config':{'Labels':{
             'com.docker.compose.project.working_dir':str(root)}}}
        web={'Id':'web-running','Image':image,'Config':{'Labels':{
             'com.docker.compose.project.working_dir':str(root)}}}
        others={'Id':'unchanged'}
        def lookup(name):
            return {'api':api,'web':web}.get(name,others)
        def paths(obj):
            return [base,api_image,mod.IDENTITY_STAGE] if obj is api else [base,web_image]
        with patch.object(mod.os,'geteuid',return_value=0),\
             patch.object(mod.socket,'gethostname',return_value='glmdch'),\
             patch.object(mod,'inspect',side_effect=lookup),\
             patch.object(mod,'compose_paths',side_effect=paths),\
             patch.object(mod,'root_regular'),\
             patch.object(mod,'identity_active'),\
             patch.object(mod,'expected_web_override') as validator:
            state=mod.get_sanitized_snapshot()
            self.assertEqual(state['web_paths'],[base,web_image])
            self.assertEqual(state['boundary']['api'],'api-stable')
            validator.assert_called_once_with(web_image,image)

    def test_root_overlay_wrong_path_denied(self):
        path=Path('/tmp/web-'+'b'*64+'.json')
        with self.assertRaisesRegex(mod.Blocked,'WEB_OVERRIDE_UNEXPECTED'):
            mod.expected_web_override(path,'sha256:'+'b'*64)

    def test_check_does_not_trigger_change(self):
        fake={'boundary':{},'web':{'Image':'sha256:'+'a'*64}}
        with patch.object(mod,'get_sanitized_snapshot',return_value=fake),\
             patch.object(mod,'backup_check'),patch.object(mod,'timer_check'),\
             patch.object(mod,'public_check') as pc,patch.object(mod,'compose_up') as up:
            mod.check()
            pc.assert_called_once_with(expected_sha=mod.BASE_COMMIT)
            up.assert_not_called()

    def test_timer_active_fails_closed(self):
        with patch.object(mod,'exec_fixed',return_value=('active',0)):
            with self.assertRaisesRegex(mod.Blocked,'WEB_TIMER_MUST_STAY_INACTIVE'):
                mod.timer_check()

    def test_change_docker_compose_targets_web_only(self):
        base=Path('/opt/recovery/base.json');layer=Path('/opt/recovery/web.json')
        with patch.object(mod,'exec_fixed',return_value=('',0)) as runner:
            mod.compose_up(base,layer)
            command=runner.call_args.args[0]
            self.assertEqual(command[-1],'web')
            self.assertIn('--no-deps',command)
            self.assertIn('--no-build',command)
            self.assertIn('--pull',command)
            self.assertNotIn('down',command)
            self.assertNotIn('api',command)

    def test_dependency_changed_rejected(self):
        def service(s):return {'Id':'unchanged' if s!='api' else 'changed'}
        with patch.object(mod,'inspect',side_effect=service),patch.object(mod,'timer_check'),\
             patch.object(mod,'identity_active'):
            with self.assertRaisesRegex(mod.Blocked,'NON_WEB_CONTAINER_WAS_CHANGED'):
                mod.verify_dependencies({'api':'unchanged','postgres':'unchanged'})

    def test_prepare_fails_before_mutation_for_low_disk(self):
        fake={'web':{'Image':'sha256:'+'b'*64},'web_paths':[Path('/tmp/base'),Path('/tmp/override')]}
        stat=os.statvfs('/')
        with patch.object(mod.shutil,'disk_usage',return_value=namedtuple('usage','total used free')(100,99,1)),\
             patch.object(mod,'git') as git:
            with self.assertRaisesRegex(mod.Blocked,'INSUFFICIENT_BUILD_DISK_FREE'):
                mod.prepare(fake)
            git.assert_not_called()

    def test_manual_rollback_requires_existing_journal(self):
        with patch.object(mod,'read_root_json',side_effect=mod.Blocked('TRUSTED_FILE_MISSING')):
            with self.assertRaisesRegex(mod.Blocked,'TRUSTED_FILE_MISSING'):
                mod.rollback()

    def test_rollback_requires_exact_journal_keys(self):
        with patch.object(mod,'read_root_json',side_effect=[{'status':'started','not_expected':1},{}]):
            with self.assertRaisesRegex(mod.Blocked,'ROLLBACK_JOURNAL_INVALID'):
                mod.rollback()

    def test_host_guard_denies_wrong_host(self):
        with patch.object(mod.os,'geteuid',return_value=0),patch.object(mod.socket,'gethostname',return_value='not-glmdch'):
            with self.assertRaisesRegex(mod.Blocked,'HOST_NOT_AUTHORIZED'):
                mod.get_sanitized_snapshot()

if __name__=='__main__':main()
