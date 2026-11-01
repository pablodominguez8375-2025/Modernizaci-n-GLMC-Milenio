import contextlib,importlib.util,io,unittest
from pathlib import Path
from types import SimpleNamespace
spec=importlib.util.spec_from_file_location('local_inventory',Path(__file__).resolve().parents[1]/'scripts/qa/operators/master-qa-inventory-local.py')
m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
class Tests(unittest.TestCase):
 def test_resource_mapping(self):
  self.assertEqual(m.get_resource('/admin/realms/pmgm/users?first=0&max=100'),['users','-r','pmgm','-q','first=0','-q','max=100'])
 def test_reject_other_realm_and_origin(self):
  for p in ['/admin/realms/master/users','https://attacker.invalid/users','//attacker.invalid']:
   with self.assertRaises(RuntimeError):m.get_resource(p)
 def test_no_password_argument(self):
  self.assertNotIn('--password',m.AUTH)
  self.assertIn('KC_CLI_PASSWORD',m.AUTH)
  self.assertIn('umask 077',m.AUTH)
 def test_summary_never_discloses_identity(self):
  f=io.StringIO()
  with contextlib.redirect_stdout(f):m.describe({'username':'private-name','id':'private-id'}, {'id':'private-id','username':'private-name'},SimpleNamespace(LEGACY_QA_SUBJECT='legacy'))
  self.assertNotIn('private-name',f.getvalue());self.assertNotIn('private-id',f.getvalue())
 def test_exact_legacy_flag_only(self):
  f=io.StringIO()
  with contextlib.redirect_stdout(f):m.describe(None,{'id':'legacy','username':'qa.admin'},SimpleNamespace(LEGACY_QA_SUBJECT='legacy'))
  self.assertIn('ROL_MASTER_EN_FIXTURE_QA_HEREDADO',f.getvalue())
if __name__=='__main__':unittest.main()
