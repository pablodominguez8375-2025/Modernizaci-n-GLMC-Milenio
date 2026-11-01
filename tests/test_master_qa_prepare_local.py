import importlib.util,json,subprocess,tempfile,unittest
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import patch
spec=importlib.util.spec_from_file_location('prepare_local',Path(__file__).resolve().parents[1]/'scripts/qa/operators/master-qa-prepare-local.py')
m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
class Tests(unittest.TestCase):
 def client(self):
  helper=SimpleNamespace(KC='test-keycloak',get_resource=lambda p:['users','-r','pmgm'])
  module=SimpleNamespace(ROLE='platform_superadmin',LEGACY_QA_SUBJECT='legacy0001',private_file=lambda p:'subject001')
  return m.CLI(helper,module,'/tmp/pmgm604.ABCDEF',{})
 def test_never_create_enabled_or_different_admin(self):
  for body in [{'username':'admin','enabled':True},{'username':'other','enabled':False}]:
   with self.assertRaisesRegex(RuntimeError,'CREATE_MUST_BE_DISABLED_ADMIN'),patch.object(m,'execute') as run:
    self.client().post('/admin/realms/pmgm/users',body)
   run.assert_not_called()
 def test_password_only_stdin_and_no_merge(self):
  body={'type':'password','temporary':True,'value':'secret-synthetic-value'}
  with patch.object(m,'execute',return_value='') as run:self.client().put('/admin/realms/pmgm/users/subject001/reset-password',body)
  args=run.call_args.args[0]
  self.assertNotIn('secret-synthetic-value',' '.join(args));self.assertIn('-n',args)
  self.assertEqual(json.loads(run.call_args.kwargs['input']),body)
 def test_activation_forbidden(self):
  with self.assertRaisesRegex(RuntimeError,'ACTIVATION_NOT_ALLOWED_IN_THIS_PHASE'),patch.object(m,'execute') as run:
   self.client().put('/admin/realms/pmgm/users/subject001',{'enabled':True})
  run.assert_not_called()
 def test_delete_never_removes_user_or_other_identity(self):
  for path in ['/admin/realms/pmgm/users/legacy0001','/admin/realms/pmgm/users/other/role-mappings/realm']:
   with self.assertRaisesRegex(RuntimeError,'DELETE_NOT_ALLOWED'),patch.object(m,'execute') as run:self.client().delete(path,[{'name':'platform_superadmin','id':'r1'}])
   run.assert_not_called()
 def test_retirement_exact_role_and_identity(self):
  with patch.object(m,'execute',return_value='') as run:self.client().delete('/admin/realms/pmgm/users/legacy0001/role-mappings/realm',[{'name':'platform_superadmin','id':'r1'}])
  args=run.call_args.args[0];self.assertIn('remove-roles',args)
  self.assertEqual(args[args.index('--uid')+1],'legacy0001');self.assertEqual(args[args.index('--rolename')+1],'platform_superadmin')
 def test_multiple_role_payload_denied(self):
  with self.assertRaisesRegex(RuntimeError,'ROLE_WRITE_NOT_ALLOWED'),patch.object(m,'execute') as run:self.client().post('/admin/realms/pmgm/users/subject001/role-mappings/realm',[{'name':'platform_superadmin','id':'r1'},{'name':'another','id':'r2'}])
  run.assert_not_called()
 def test_failed_restore_cleans_temporary_db(self):
  with tempfile.NamedTemporaryFile() as f,patch.object(m,'execute',return_value='') as run,patch.object(m.subprocess,'run',return_value=SimpleNamespace(returncode=1)):
   with self.assertRaisesRegex(RuntimeError,'KEYCLOAK_RESTORE_PROBE_FAILED'):m.restore_probe(Path(f.name))
  calls=run.call_args_list;self.assertEqual(len(calls),2)
  created=calls[0].args[0][-1];dropped=calls[1].args[0][-1]
  self.assertEqual(created,dropped);self.assertTrue(created.startswith('qa604_restore_'));self.assertNotEqual(created,'pmgm_keycloak')
 def test_restore_checks_realm_and_cleans_success(self):
  with tempfile.NamedTemporaryFile() as f,patch.object(m,'execute',side_effect=['','1\n','']) as run,patch.object(m.subprocess,'run',return_value=SimpleNamespace(returncode=0)):
   m.restore_probe(Path(f.name))
  self.assertEqual(len(run.call_args_list),3)
if __name__=='__main__':unittest.main()
