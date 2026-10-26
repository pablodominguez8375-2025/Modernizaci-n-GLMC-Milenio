import importlib.util, unittest
from pathlib import Path
path = Path(__file__).with_name('scope.py')
if not path.exists(): path = Path(__file__).resolve().parents[1]/'scripts/qa/operators/master-qa-scope-local.py'
spec=importlib.util.spec_from_file_location('scope_repair',path)
m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)

class ScopeTests(unittest.TestCase):
    def user(self): return {'id':'expected','username':'admin','enabled':True,'attributes':{
        'pmgm_master_qa_bootstrap':['issue-604'],'other':['keep']}}
    def test_adds_scope_without_mutating_original(self):
        user=self.user();attrs=m.plan(user,'expected')
        self.assertEqual(attrs['pmgm_scope'],['order']);self.assertEqual(attrs['other'],['keep'])
        self.assertNotIn('pmgm_scope',user['attributes'])
    def test_wrong_subject_or_account_rejected(self):
        for key,value in [('id','foreign'),('username','someone'),('enabled',False)]:
            user=self.user();user[key]=value
            with self.assertRaises(RuntimeError):m.plan(user,'expected')
    def test_unmarked_account_rejected(self):
        user=self.user();user['attributes'].pop('pmgm_master_qa_bootstrap')
        with self.assertRaises(RuntimeError):m.plan(user,'expected')
    def test_org_or_different_scope_rejected(self):
        for key,value in [('pmgm_org',['lodge']),('pmgm_scope',['organization'])]:
            user=self.user();user['attributes'][key]=value
            with self.assertRaises(RuntimeError):m.plan(user,'expected')
    def test_other_user_field_change_detected(self):
        before=self.user();attrs=m.plan(before,'expected');after={**before,'attributes':attrs}
        m.verify_user(before,after,attrs)
        for key,value in [('enabled',False),('username','changed'),('requiredActions',['UPDATE_PASSWORD'])]:
            with self.assertRaises(RuntimeError):m.verify_user(before,{**after,key:value},attrs)
    def test_duplicate_or_missing_scope_mapper_rejected(self):
        for maps in ([],[{'config':{'claim.name':'pmgm_scope'}}]*2):
            with self.assertRaises(RuntimeError):m.validate_mappers(maps)

if __name__=='__main__':unittest.main()
