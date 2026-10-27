import importlib.util
import io
from contextlib import redirect_stdout
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

script=Path(__file__).resolve().parents[1]/'scripts/qa/operators/pmgm-qa754-web-topology-v2.py'
if not script.is_file():
    script=Path(__file__).with_name('pmgm-qa754-web-topology-v2.py')
spec=importlib.util.spec_from_file_location('pmgm754webv2',script)
m=importlib.util.module_from_spec(spec)
spec.loader.exec_module(m)

class WebTopologyTests(unittest.TestCase):
    def setUp(self):
        self.tmp=tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.dir=Path(self.tmp.name)
        self.p0=self.dir/'compose.recovery.json'
        self.p1=self.dir/('api-'+'a'*40+'.json')
        self.p2=self.dir/'private-stage.json'
        self.webimg=self.dir/('web-'+'b'*64+'.json')
        self.altapi=self.dir/('api-'+'c'*40+'.json')
        self.other=self.dir/'unrecognized.json'
        for p in (self.p0,self.p1,self.p2,self.webimg,self.altapi,self.other):p.write_text('{}')
        self.p2.chmod(0o600)
        self.stagepatch=patch.object(m,'IDENTITY_STAGE',self.p2)
        self.stagepatch.start()
        self.addCleanup(self.stagepatch.stop)
        self.api=self.obj('api',[self.p0,self.p1,self.p2])

    def obj(self,service,paths):
        return {'Config':{'Labels':{
            'com.docker.compose.project':m.PROJECT,
            'com.docker.compose.service':service,
            'com.docker.compose.project.config_files':','.join(str(p) for p in paths)
        },'Env':[]},'State':{'Running':True}}

    def classify(self,paths):return m.classify_layers(self.api,self.obj('web',paths))

    def test_valid_base_two_layers(self):
        self.assertEqual(self.classify([self.p0,self.p1]),(['RECOVERY_BASE_SHARED','API_IMAGE_LAYER_SHARED'],True))

    def test_valid_base_three_layers(self):
        self.assertEqual(self.classify([self.p0,self.p1,self.webimg]),(['RECOVERY_BASE_SHARED','API_IMAGE_LAYER_SHARED','WEB_IMAGE_OVERRIDE'],True))

    def test_distinct_web_image_override_not_blocked(self):
        self.assertEqual(self.classify([self.p0,self.webimg]),(['RECOVERY_BASE_SHARED','WEB_IMAGE_OVERRIDE'],False))

    def test_distinct_api_image_not_blocked(self):
        self.assertEqual(self.classify([self.p0,self.altapi]),(['RECOVERY_BASE_SHARED','DIFFERENT_API_IMAGE_LAYER'],False))

    def test_single_recovery_layer_not_blocked(self):
        self.assertEqual(self.classify([self.p0]),(['RECOVERY_BASE_SHARED'],False))

    def test_four_web_layers_classified_not_approved(self):
        layers,canonical=self.classify([self.p0,self.p1,self.webimg,self.other])
        self.assertEqual(layers[-1],'UNRECOGNIZED_LOCAL_COMPOSE_LAYER')
        self.assertFalse(canonical)

    def test_same_name_different_recovery_path_not_approved(self):
        another=self.dir/'another';another.mkdir()
        p=another/'compose.recovery.json';p.write_text('{}')
        self.assertEqual(self.classify([p,self.p1])[0][0],'DIFFERENT_RECOVERY_BASE')
        self.assertFalse(self.classify([p,self.p1])[1])

    def test_bad_api_rejected(self):
        with self.assertRaises(m.Blocked):m.classify_layers(self.obj('api',[self.p0,self.p1]),self.obj('web',[self.p0]))

    def test_missing_files_rejected(self):
        with self.assertRaises(m.Blocked):self.classify([self.p0,self.dir/'missing.json'])

    def test_symlink_rejected(self):
        p=self.dir/'link.json';p.symlink_to(self.webimg)
        with self.assertRaises(m.Blocked):self.classify([self.p0,p])

    def test_no_web_layers_rejected(self):
        with self.assertRaises(m.Blocked):self.classify([])

    def test_more_than_five_rejected(self):
        with self.assertRaises(m.Blocked):self.classify([self.p0]*6)

    def test_private_stage_permissions_enforced(self):
        self.p2.chmod(0o644)
        with self.assertRaises(m.Blocked):self.classify([self.p0,self.p1])

    def test_identity_active_no_secret_output(self):
        values={
          'UserAccounts__Identity__ServerUrl':'http://keycloak:8080/identity',
          'UserAccounts__Identity__Issuer':'https://sistema.granlogiamixta.cl/identity/realms/pmgm',
          'UserAccounts__Identity__ClientId':'pmgm-account-admin',
          'UserAccounts__Identity__ClientSecret':'SENSITIVE_DO_NOT_OUTPUT',
          'UserAccounts__Mail__LoginUrl':'https://sistema.granlogiamixta.cl/'
        }
        api={'Config':{'Env':[f'{k}={v}' for k,v in values.items()]}}
        output=io.StringIO()
        with redirect_stdout(output):self.assertTrue(m.identity_active(api))
        self.assertNotIn('SENSITIVE',output.getvalue())

    def test_main_uses_no_compose_and_reports_classification(self):
        env={
          'UserAccounts__Identity__ServerUrl':'http://keycloak:8080/identity',
          'UserAccounts__Identity__Issuer':'https://sistema.granlogiamixta.cl/identity/realms/pmgm',
          'UserAccounts__Identity__ClientId':'pmgm-account-admin',
          'UserAccounts__Identity__ClientSecret':'SENSITIVE_DO_NOT_OUTPUT',
          'UserAccounts__Mail__LoginUrl':'https://sistema.granlogiamixta.cl/'
        }
        api=self.api
        api['Config']['Env']=[f'{k}={v}' for k,v in env.items()]
        web=self.obj('web',[self.p0,self.webimg])
        def fake_container(name):return api if name=='api' else web if name=='web' else self.obj(name,[self.p0])
        with patch.object(m,'container',side_effect=fake_container),patch.object(m,'verify_g6'),patch.object(m,'timer'),patch.object(m,'version',return_value='SOURCE_SHA_TEST'),patch.object(m,'historical_installer',return_value='HISTORICAL_OCT19_PIN_UNSUITABLE'),patch.object(m,'installed_state',return_value='IMAGE_MATCH'),patch.object(m.os,'geteuid',return_value=0),patch.object(m.socket,'gethostname',return_value='glmdch'):
            b=io.StringIO()
            with redirect_stdout(b):m.main()
        result=b.getvalue()
        self.assertIn('WEB_LAYER_2_CLASS: WEB_IMAGE_OVERRIDE',result)
        self.assertIn('WEB_API_COMPOSE_RELATION: DIFFERENT_REQUIRES_SUPERVISED_PLAN',result)
        self.assertIn('RESULT: READ_ONLY_TOPOLOGY_CLASSIFIED_NO_DEPLOYMENT_NO_CHANGES',result)
        self.assertNotIn('SENSITIVE',result)

if __name__=='__main__':unittest.main()