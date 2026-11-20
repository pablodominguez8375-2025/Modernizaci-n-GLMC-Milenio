import importlib.util, json, unittest
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import patch
from urllib.error import HTTPError

path=Path(__file__).with_name('base.py')
if not path.exists():path=Path(__file__).resolve().parents[1]/'scripts/qa/operators/refresh-6ff4ae0-v10.py'
spec=importlib.util.spec_from_file_location('v10',path)
m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
FIRST='20260912140000_AddAnonymousLodgeBallots'
SECOND='20260912141000_AddBallotProcedureNumber'

class SchemaTests(unittest.TestCase):
    def test_both_pending_and_absent_allowed(self):
        m.ballot_schema_plan(set(),{'meetings':True,'ballots':False,'procedure':False})
    def test_second_pending_allowed_only_without_column(self):
        m.ballot_schema_plan({FIRST},{'meetings':True,'ballots':True,'procedure':False})
    def test_both_applied_require_table_and_column(self):
        m.ballot_schema_plan({FIRST,SECOND},{'meetings':True,'ballots':True,'procedure':True})
    def test_existing_table_without_history_blocks(self):
        with self.assertRaisesRegex(RuntimeError,'TABLE_HISTORY_CONFLICT'):
            m.ballot_schema_plan(set(),{'meetings':True,'ballots':True,'procedure':False})
    def test_existing_column_without_history_blocks(self):
        with self.assertRaisesRegex(RuntimeError,'COLUMN_HISTORY_CONFLICT'):
            m.ballot_schema_plan({FIRST},{'meetings':True,'ballots':True,'procedure':True})
    def test_applied_history_without_table_blocks(self):
        with self.assertRaisesRegex(RuntimeError,'TABLE_HISTORY_CONFLICT'):
            m.ballot_schema_plan({FIRST},{'meetings':True,'ballots':False,'procedure':False})
    def test_missing_meetings_blocks(self):
        with self.assertRaisesRegex(RuntimeError,'MEETINGS_TABLE_MISSING'):
            m.ballot_schema_plan(set(),{'meetings':False,'ballots':False,'procedure':False})
    def test_incomplete_metadata_blocks(self):
        with self.assertRaisesRegex(RuntimeError,'METADATA_INVALID'):
            m.ballot_schema_plan(set(),{'meetings':True})

class PreservationAndTransportTests(unittest.TestCase):
    def master(self):return {'InitialLoad__MasterSubject':'protected-subject',
        'InitialLoad__MasterIssuer':'https://sistema.granlogiamixta.cl/identity/realms/pmgm',
        'Authentication__Authority':'http://keycloak:8080/identity/realms/pmgm'}
    def test_candidate_and_rollback_conserve_master_and_smtp(self):
        for image in ('old','new'):
            overlay=m.preserved_overlay(image,'same-key',self.master())
            self.assertEqual(overlay['services']['api']['environment'],{**self.master(),m.SMTP_VARIABLE:'same-key'})
            self.assertEqual(set(overlay['services']),{'api','migrator'})
    def test_runtime_identity_drift_rejected(self):
        env={**self.master(),'InitialLoad__MasterSubject':'foreign','Database__ApplyMigrationsOnStartup':'false',
             'Database__RunMigrationsAndExit':'false'}
        inst=SimpleNamespace(adapter=SimpleNamespace(docker=lambda args:json.dumps([k+'='+v for k,v in env.items()])))
        with self.assertRaisesRegex(RuntimeError,'MASTER_RUNTIME_CONFIG_CHANGED'):m.verify_master(inst,self.master())
    def test_rate_limit_is_reported_immediately_with_reset(self):
        error=HTTPError('https://api.github.com',403,'Forbidden',{'X-RateLimit-Remaining':'0',
                          'X-RateLimit-Reset':'1793030356'},None)
        with patch.object(m.urllib.request,'urlopen',side_effect=error):
            with self.assertRaisesRegex(RuntimeError,'GITHUB_RATE_LIMIT_RESET_1793030356'):
                m.public_fetch('/branches/dev')
    def test_403_without_quota_evidence_is_not_mislabeled(self):
        error=HTTPError('https://api.github.com',403,'Forbidden',{},None)
        with patch.object(m.urllib.request,'urlopen',side_effect=error):
            with self.assertRaisesRegex(RuntimeError,'GITHUB_HTTP_403_RETRY_LATER'):
                m.public_fetch('/branches/dev')
    def test_invalid_endpoint_rejected_before_network(self):
        with patch.object(m.urllib.request,'urlopen') as request:
            with self.assertRaises(RuntimeError):m.public_fetch('//foreign')
            request.assert_not_called()
    def test_timer_is_not_restarted_in_any_path(self):
        import ast
        tree=ast.parse(path.read_text())
        starts=[node for node in ast.walk(tree) if isinstance(node,ast.Call)
                and isinstance(node.func,ast.Name) and node.func.id=='system'
                and [getattr(arg,'value',None) for arg in node.args]==['start','pmgm-qa-lite.timer']]
        self.assertEqual(starts,[])

if __name__=='__main__':unittest.main()
