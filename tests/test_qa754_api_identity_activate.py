import importlib.util
import io
import pathlib
import unittest
from unittest.mock import patch
from contextlib import redirect_stdout

MODULE = pathlib.Path(__file__).resolve().parents[1] / 'scripts/qa/operators/pmgm-qa754-api-identity-activate.py'
spec = importlib.util.spec_from_file_location('qa754_activation', MODULE)
m = importlib.util.module_from_spec(spec)
spec.loader.exec_module(m)

class Tests(unittest.TestCase):
    def setUp(self):
        self.ref='synthetic-not-a-secret-for-testing'
        self.stage={**m.IDENTITY, m.SECRET_KEY:self.ref}
        self.oldenv={**{k: '' for k in m.CHANGING}, 'UserAccounts__Identity__ClientId':m.CLIENT,
                     'SystemMail__EncryptionKey':'fake-base64-for-synthetic-test',
                     'Authentication__Authority':m.AUTHORITY,'Database__ApplyMigrationsOnStartup':'false', 'Other':'keep-original'}
        self.newenv={**self.oldenv,**self.stage}
        self.old={'name':m.PROJECT,'services':{'api':{'image':'pmgm/testapi:old','environment':self.oldenv},
                                            'web':{'image':'do-not-touch'},'keycloak':{'image':'kc-do-not-touch'}}}
        self.new={'name':m.PROJECT,'services':{'api':{'image':'pmgm/testapi:old','environment':self.newenv},
                                            'web':{'image':'do-not-touch'},'keycloak':{'image':'kc-do-not-touch'}}}
    def blocked(self, fn, code):
        with self.assertRaises(m.Blocked) as ctx: fn()
        self.assertIn(code,str(ctx.exception))
    def test_scope_only_identity_pass(self):
        a,b=m.diff_only_identity(self.old,self.new,self.stage)
        self.assertEqual(a,self.oldenv)
        self.assertEqual(b,self.newenv)
    def test_reject_other_service_change(self):
        self.new['services']['web']['image']='bad'
        self.blocked(lambda:m.diff_only_identity(self.old,self.new,self.stage),'OTHER_SERVICE_CHANGED')
    def test_reject_smtp_change(self):
        self.newenv['SystemMail__EncryptionKey']='wrong'
        self.blocked(lambda:m.diff_only_identity(self.old,self.new,self.stage),'API_UNRELATED_ENV_CHANGED')
    def test_reject_other_env_change(self):
        self.newenv['Other']='evil'
        self.blocked(lambda:m.diff_only_identity(self.old,self.new,self.stage),'API_UNRELATED_ENV_CHANGED')
    def test_reject_network_change(self):
        self.new['networks']={'bad': {}}
        self.blocked(lambda:m.diff_only_identity(self.old,self.new,self.stage),'TOPLEVEL_CHANGED')
    def test_reject_already_active(self):
        self.oldenv['UserAccounts__Identity__ServerUrl']=m.SERVER
        self.blocked(lambda:m.diff_only_identity(self.old,self.new,self.stage),'API_ALREADY_CONFIGURED')
    def test_reject_extra_stage_variable(self):
        doc={'services':{'api':{'environment':{**self.stage,'SMTP_PASSWORD':'bad'}}}}
        self.blocked(lambda:m.stage_env(__import__('json').dumps(doc).encode(),self.ref),'STAGE_ENV_SCOPE')
    def test_reject_wrong_stage_secret(self):
        doc={'services':{'api':{'environment':{**self.stage,m.SECRET_KEY:'changed'}}}}
        self.blocked(lambda:m.stage_env(__import__('json').dumps(doc).encode(),self.ref),'STAGE_SERVICE_SECRET_MISMATCH')
    def test_reject_wrong_issuer(self):
        doc={'services':{'api':{'environment':{**self.stage,'UserAccounts__Identity__Issuer':'https://wrong.invalid'}}}}
        self.blocked(lambda:m.stage_env(__import__('json').dumps(doc).encode(),self.ref),'STAGE_URL_MISMATCH')
    def test_stage_valid(self):
        doc={'services':{'api':{'environment':self.stage}}}
        self.assertEqual(m.stage_env(__import__('json').dumps(doc).encode(),self.ref),self.stage)
    def test_baseline_runtime_image_match(self):
        image={'Id':'sha256:mock','Config':{'Env':['DEFAULT=ok']}}
        actual=['DEFAULT=ok']+[f'{k}={v}' for k,v in self.oldenv.items()]
        self.assertEqual(m.baseline_match(self.old,{'Config':{'Image':'pmgm/testapi:old','Env':actual},'Image':'sha256:mock'},image),dict([a.split('=',1) for a in actual]))
    def test_baseline_runtime_missing_smtp_key(self):
        self.oldenv['SystemMail__EncryptionKey']=''
        image={'Id':'sha256:mock','Config':{'Env':[]}}
        actual=[f'{k}={v}' for k,v in self.oldenv.items()]
        self.blocked(lambda:m.baseline_match(self.old,{'Config':{'Image':'pmgm/testapi:old','Env':actual},'Image':'sha256:mock'},image),'SMTP_KEY_MISSING')
    def test_migrations_enabled_blocks(self):
        self.oldenv['Database__ApplyMigrationsOnStartup']='true'
        image={'Id':'sha256:mock','Config':{'Env':[]}}
        actual=[f'{k}={v}' for k,v in self.oldenv.items()]
        self.blocked(lambda:m.baseline_match(self.old,{'Config':{'Image':'pmgm/testapi:old','Env':actual},'Image':'sha256:mock'},image),'API_MIGRATIONS_MUST_BE_DISABLED')
    def test_baseline_changed_image_id(self):
        image={'Id':'sha256:other','Config':{'Env':[]}}
        actual=[f'{k}={v}' for k,v in self.oldenv.items()]
        self.blocked(lambda:m.baseline_match(self.old,{'Config':{'Image':'pmgm/testapi:old','Env':actual},'Image':'sha256:mock'},image),'API_IMAGE_ID_DRIFT')
    def test_apply_success_needs_real_uat(self):
        s={'paths':[], 'old_api_id':'old'}
        output=io.StringIO()
        with patch.object(m,'change_compose') as change,patch.object(m,'post_verify') as verify,redirect_stdout(output):
            status=m.apply(s)
        self.assertEqual(status,0)
        change.assert_called_once_with([],True)
        verify.assert_called_once_with(s,True)
        self.assertIn('UAT_PENDING',output.getvalue())
        self.assertNotIn(self.ref,output.getvalue())
    def test_apply_failure_auto_rollback(self):
        s={'paths':[], 'old_api_id':'old'}
        with patch.object(m,'change_compose') as change,patch.object(m,'post_verify') as verify,redirect_stdout(io.StringIO()) as _: # noqa
            verify.side_effect=[m.Blocked('test synthetic fail'),None]
            status=m.apply(s)
        self.assertEqual(status,3)
        self.assertEqual(change.call_args_list[0].args,([],True))
        self.assertEqual(change.call_args_list[1].args,([],False))
    def test_rollback_failure_reported(self):
        s={'paths':[], 'old_api_id':'old'}
        output=io.StringIO()
        with patch.object(m,'change_compose',side_effect=[None,m.Blocked('rollback fail')]),\
             patch.object(m,'post_verify',side_effect=m.Blocked('health fail')),redirect_stdout(output):
            status=m.apply(s)
        self.assertEqual(status,4)
        self.assertIn('CRITICAL_ROLLBACK_FAILED',output.getvalue())
    def test_encrypted_backup_sha_check_synthetic(self):
        import hashlib, tempfile
        from unittest.mock import patch
        with tempfile.TemporaryDirectory() as d:
            p=pathlib.Path(d)/'backup-cifrado-ficticio.tar.gz'
            p.write_bytes(b'synthetic_encrypted_blob_only')
            with patch.object(m,'G6_BYTES',p.stat().st_size),patch.object(m,'G6_SHA256',hashlib.sha256(p.read_bytes()).hexdigest()):
                m.sha256_file(p)
                p.write_bytes(b'tampered_blob_different_len')
                self.blocked(lambda:m.sha256_file(p),'BACKUP_SIZE_CHANGED')
    def test_timer_active_blocks(self):
        with patch.object(m,'cmd',return_value=('active',0)):
            self.blocked(m.timer_paused,'WEB_TIMER_NOT_INACTIVE')
    def test_command_never_prints_secret_errors(self):
        with patch.object(m.subprocess,'run') as cmd:
            cmd.return_value.returncode=1
            cmd.return_value.stdout=b'SECRET=synthetic-test-secret'
            err=io.StringIO()
            with redirect_stdout(err):
                self.blocked(lambda:m.cmd(['docker','compose','config']),'COMMAND_FAILED')
            self.assertNotIn('synthetic-test-secret',err.getvalue())

if __name__=='__main__': unittest.main()