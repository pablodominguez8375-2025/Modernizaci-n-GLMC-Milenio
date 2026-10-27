import importlib.util
from pathlib import Path
import tempfile, types, unittest
from unittest.mock import patch, Mock, call

path = Path(__file__).with_name('activate.py')
if not path.exists():
    path = Path(__file__).resolve().parents[1]/'scripts/qa/operators/master-qa-activate-local.py'
spec = importlib.util.spec_from_file_location('activation', path)
m = importlib.util.module_from_spec(spec); spec.loader.exec_module(m)

class Transport:
    def subject(self): return 'protected-subject'
    def call(self, args, payload): self.sent = (args, payload)

class ActivationTests(unittest.TestCase):
    def env(self):
        return {'Authentication__Authority':m.AUTHORITY, m.SMTP:'existing-secret',
                'Database__ApplyMigrationsOnStartup':'false',
                'Database__RunMigrationsAndExit':'false','Other':'preserve'}
    def cli(self): return m.activate_transport(types.SimpleNamespace(CLI=Transport))()
    def test_master_config_preserves_every_existing_key(self):
        before = self.env(); after = m.expected_environment(before,'subject-123','issuer')
        self.assertEqual({k:after[k] for k in before}, before)
        self.assertNotIn('InitialLoad__MasterSubject',before)
    def test_existing_master_cannot_be_overwritten(self):
        env = self.env(); env['InitialLoad__MasterSubject']='someone-else'
        with self.assertRaisesRegex(RuntimeError,'ALREADY_PRESENT'):
            m.expected_environment(env,'subject-123','issuer')
    def test_migrations_cannot_be_enabled(self):
        for key in ('Database__ApplyMigrationsOnStartup','Database__RunMigrationsAndExit'):
            env=self.env();env[key]='true'
            with self.assertRaisesRegex(RuntimeError,'MIGRATIONS'):
                m.expected_environment(env,'subject-123','issuer')
    def test_missing_smtp_blocks(self):
        env=self.env();del env[m.SMTP]
        with self.assertRaisesRegex(RuntimeError,'SMTP_KEY_MISSING'):
            m.expected_environment(env,'subject-123','issuer')
    def test_activation_cannot_precede_runtime_verification(self):
        cli=self.cli()
        with self.assertRaisesRegex(RuntimeError,'WRITE_FORBIDDEN'):
            cli.put('/admin/realms/pmgm/users/protected-subject',{'enabled':True})
    def test_activation_only_exact_subject_and_boolean_payload(self):
        cli=self.cli();cli.armed=True
        for path,body in [('/admin/realms/pmgm/users/foreign',{'enabled':True}),
            ('/admin/realms/pmgm/users/protected-subject',{'enabled':1}),
            ('/admin/realms/pmgm/users/protected-subject',{'enabled':False}),
            ('/admin/realms/pmgm/users/protected-subject',{'enabled':True,'roles':['admin']})]:
            with self.assertRaisesRegex(RuntimeError,'WRITE_FORBIDDEN'): cli.put(path,body)
    def test_exact_activation_json_goes_to_stdin_adapter(self):
        cli=self.cli();cli.armed=True
        cli.put('/admin/realms/pmgm/users/protected-subject',{'enabled':True})
        self.assertEqual(cli.sent,(['update','users/protected-subject','-r','pmgm','-n','-f','-'],{'enabled':True}))
    def test_create_delete_and_role_mapping_forbidden(self):
        cli=self.cli();cli.armed=True
        for method in (cli.post,cli.delete):
            with self.assertRaises(RuntimeError): method('/any',{})
    def test_runtime_rejects_smtp_or_other_env_drift(self):
        before=self.env()
        api={'Image':m.IMAGE,'State':{'Running':True},'Config':{
             'Labels':{'org.opencontainers.image.revision':m.TARGET},
             'Env':[k+'='+v for k,v in before.items()]}}
        with patch.object(m,'inspect_api',return_value=api),patch.object(m,'dependencies',return_value={}):
            m.verify_runtime(before,{})
            for key in (m.SMTP,'Other'):
                changed={**before,key:'changed'}
                with self.assertRaisesRegex(RuntimeError,'ENV_MISMATCH'):m.verify_runtime(changed,{})
    def test_runtime_rejects_dependency_recreation(self):
        api={'Image':m.IMAGE,'State':{'Running':True},'Config':{
             'Labels':{'org.opencontainers.image.revision':m.TARGET},'Env':[]}}
        with patch.object(m,'inspect_api',return_value=api),patch.object(m,'dependencies',return_value={'db':'new'}):
            with self.assertRaisesRegex(RuntimeError,'OTHER_CONTAINER'):m.verify_runtime({}, {'db':'old'})
    def test_duplicate_runtime_env_rejected(self):
        with self.assertRaisesRegex(RuntimeError,'DUPLICATE'):
            m.environment({'Config':{'Env':['KEY=one','KEY=two']}})
    def test_rollback_restores_bytes_before_up_then_verifies(self):
        timeline=Mock()
        with patch.object(m,'atomic',timeline.atomic),patch.object(m,'up',timeline.up),\
             patch.object(m,'wait_health',timeline.health),patch.object(m,'verify_runtime',timeline.verify):
            m.recover_api(b'original',{'env':'old'},{'db':'same'})
        self.assertEqual(timeline.mock_calls,[call.atomic(m.OVERLAY,b'original'),call.up(),
            call.health(),call.verify({'env':'old'},{'db':'same'})])
    def test_rollback_failure_is_not_hidden(self):
        with patch.object(m,'atomic',side_effect=RuntimeError('disk')):
            with self.assertRaises(RuntimeError):m.recover_api(b'original',{}, {})

if __name__ == '__main__': unittest.main()
