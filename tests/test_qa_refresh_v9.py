import importlib.util, json, tempfile, unittest
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import Mock, patch

path = Path(__file__).with_name('base.py')
if not path.exists(): path = Path(__file__).resolve().parents[1]/'scripts/qa/operators/refresh-14438bb-v9.py'
spec = importlib.util.spec_from_file_location('refresh_v9',path)
m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)

class PreservationTests(unittest.TestCase):
    def master(self):return {'InitialLoad__MasterSubject':'protected-subject',
        'InitialLoad__MasterIssuer':'https://sistema.granlogiamixta.cl/identity/realms/pmgm',
        'Authentication__Authority':'http://keycloak:8080/identity/realms/pmgm'}
    def text(self,values=None):return '\n'.join(k+'='+v for k,v in (values or self.master()).items())
    def inst(self,env):
        return SimpleNamespace(adapter=SimpleNamespace(docker=Mock(return_value=json.dumps([k+'='+v for k,v in env.items()]))))
    def test_missing_master_file_key_blocks(self):
        for key in self.master():
            values=self.master();del values[key]
            with self.assertRaisesRegex(RuntimeError,'MASTER_FILE'):m.master_values(self.text(values))
    def test_duplicate_key_and_foreign_issuer_block(self):
        with self.assertRaisesRegex(RuntimeError,'AMBIGUOUS'):
            m.master_values(self.text()+'\nInitialLoad__MasterSubject=foreign')
        values=self.master();values['InitialLoad__MasterIssuer']='https://foreign/realms/pmgm'
        with self.assertRaisesRegex(RuntimeError,'INVALID'):m.master_values(self.text(values))
    def test_candidate_and_rollback_preserve_same_master_and_smtp(self):
        master=self.master()
        candidate=m.preserved_overlay('candidate','existing-smtp',master)
        rollback=m.preserved_overlay('previous','existing-smtp',master)
        self.assertEqual(candidate['services']['api']['environment'],rollback['services']['api']['environment'])
        self.assertEqual(candidate['services']['api']['environment'],{**master,m.SMTP_VARIABLE:'existing-smtp'})
        self.assertEqual(rollback['services']['api']['image'],'previous')
        self.assertEqual(set(candidate['services']),{'api','migrator'})
    def test_runtime_rejects_missing_or_different_master_after_recreate(self):
        env={**self.master(),'Database__ApplyMigrationsOnStartup':'false','Database__RunMigrationsAndExit':'false'}
        m.verify_master(self.inst(env),self.master())
        for key in self.master():
            changed={**env,key:'different'}
            with self.assertRaisesRegex(RuntimeError,'MASTER_RUNTIME'):m.verify_master(self.inst(changed),self.master())
    def test_runtime_rejects_migration_startup(self):
        env={**self.master(),'Database__ApplyMigrationsOnStartup':'true','Database__RunMigrationsAndExit':'false'}
        with self.assertRaisesRegex(RuntimeError,'MIGRATIONS_MUST'):m.verify_master(self.inst(env),self.master())
    def test_duplicate_runtime_variables_block(self):
        inst=SimpleNamespace(adapter=SimpleNamespace(docker=lambda args:'["key=a","key=b"]'))
        with self.assertRaisesRegex(RuntimeError,'DUPLICATE'):m.runtime_environment(inst)
    def test_missing_master_cannot_generate_overlay(self):
        with self.assertRaisesRegex(RuntimeError,'MASTER_CONFIG_REQUIRED'):m.preserved_overlay('image','smtp',None)

class MigrationTests(unittest.TestCase):
    def setUp(self):
        self.tmp=tempfile.TemporaryDirectory();self.root=Path(self.tmp.name)
        self.addCleanup(self.tmp.cleanup)
        folder=self.root/'backend/src/PMGM.Api/Migrations';folder.mkdir(parents=True)
        self.files={}
        for name,digest in m.MIGRATIONS.items():
            raw=name.encode();(folder/(name+'.cs')).write_bytes(raw);self.files[raw]=digest
        self.expected={'applied-'+str(i) for i in range(94)}|set(m.MIGRATIONS)
        self.plan=patch.object(m,'migration_plan',return_value=(self.expected,{'inactive'}))
        self.plan.start();self.addCleanup(self.plan.stop)
        self.hash=patch.object(m.hashlib,'sha256',side_effect=lambda raw:SimpleNamespace(hexdigest=lambda:self.files.get(raw,'changed')))
        self.hash.start();self.addCleanup(self.hash.stop)
    def test_exact_two_reviewed_pending_allowed(self):
        expected,_=m.reviewed_plan(self.root,self.expected-set(m.MIGRATIONS));self.assertEqual(len(expected),96)
    def test_unknown_applied_migration_blocks(self):
        with self.assertRaisesRegex(RuntimeError,'UNEXPECTED_DATABASE'):m.reviewed_plan(self.root,self.expected|{'unknown'})
    def test_missing_old_migration_blocks(self):
        with self.assertRaisesRegex(RuntimeError,'UNEXPECTED_DATABASE'):m.reviewed_plan(self.root,self.expected-{'applied-0'})
    def test_changed_reviewed_source_blocks(self):
        name=next(iter(m.MIGRATIONS));(self.root/'backend/src/PMGM.Api/Migrations'/(name+'.cs')).write_text('modified')
        with self.assertRaisesRegex(RuntimeError,'UNREVIEWED_MIGRATION'):m.reviewed_plan(self.root,self.expected)

if __name__=='__main__':unittest.main()
