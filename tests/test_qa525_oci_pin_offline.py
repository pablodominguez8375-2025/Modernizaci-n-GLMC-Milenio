import importlib.util
import json
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location("oci", Path(__file__).resolve().parents[1] / "scripts/qa/qa525-oci-pin-offline.py")
oci = importlib.util.module_from_spec(spec)
spec.loader.exec_module(oci)

class PinTests(unittest.TestCase):
    def fixture(self):
        c = b'{"config":{"Labels":{"org.opencontainers.image.revision":"abc"}}}'
        m = {"schemaVersion":2,"mediaType":"application/vnd.oci.image.manifest.v1+json",
             "config":{"mediaType":"application/vnd.oci.image.config.v1+json","size":len(c),"digest":oci.digest(c)}}
        return json.dumps(m).encode(), c
    def test_distinct_digests(self):
        m,c = self.fixture()
        r = oci.verify(m,c,oci.digest(m),"abc")
        self.assertEqual(r["config_digest"],oci.digest(c))
        self.assertNotEqual(r["manifest_digest"],r["config_digest"])
        self.assertFalse(r["may_apply"])
    def test_reject_wrong_manifest(self):
        m,c = self.fixture()
        with self.assertRaises(ValueError): oci.verify(m,c,oci.digest(c))
    def test_reject_changed_config(self):
        m,c = self.fixture()
        with self.assertRaises(ValueError): oci.verify(m,c+b" ",oci.digest(m))
    def test_reject_wrong_revision(self):
        m,c = self.fixture()
        with self.assertRaises(ValueError): oci.verify(m,c,oci.digest(m),"def")
    def test_reject_index(self):
        m,c = self.fixture()
        m = m.replace(b"image.manifest",b"image.index")
        with self.assertRaises(ValueError): oci.verify(m,c,oci.digest(m))
    def test_reject_duplicate(self):
        with self.assertRaises(ValueError): oci.parse(b'{"a":1,"a":2}')
    def test_reject_nonfinite_json_tokens(self):
        for value in (b"NaN", b"Infinity", b"-Infinity"):
            with self.subTest(value=value), self.assertRaises(ValueError):
                oci.parse(b'{"ignored":' + value + b'}')

    def test_reject_nonfinite_config_with_matching_descriptor(self):
        m, c = self.fixture()
        c = c[:-1] + b',"ignored":NaN}'
        doc = json.loads(m)
        doc["config"]["size"] = len(c)
        doc["config"]["digest"] = oci.digest(c)
        m = json.dumps(doc).encode()
        with self.assertRaises(ValueError):
            oci.verify(m, c, oci.digest(m), "abc")

    def test_reject_fractional_schema_version(self):
        m, c = self.fixture()
        doc = json.loads(m)
        doc["schemaVersion"] = 2.0
        m = json.dumps(doc).encode()
        with self.assertRaises(ValueError):
            oci.verify(m, c, oci.digest(m), "abc")

    def test_reject_nonobject_top_level(self):
        for raw in (b'[]', b'null', b'"string"'):
            with self.subTest(raw=raw), self.assertRaises(ValueError):
                oci.verify(raw, raw, oci.digest(raw))

    def test_reject_size(self):
        m,c = self.fixture()
        d = json.loads(m); d["config"]["size"] += 1
        m = json.dumps(d).encode()
        with self.assertRaises(ValueError): oci.verify(m,c,oci.digest(m))

if __name__ == "__main__":
    unittest.main()
