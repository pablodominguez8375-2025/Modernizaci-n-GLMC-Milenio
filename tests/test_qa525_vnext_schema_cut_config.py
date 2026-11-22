import hashlib
import importlib.util
import json
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location("schema_config", Path(__file__).resolve().parents[1] / "scripts/qa/qa525-vnext-schema-cut-config.py")
config = importlib.util.module_from_spec(spec)
spec.loader.exec_module(config)

def digest(raw):
    return "sha256:" + hashlib.sha256(raw).hexdigest()

def encode(obj):
    return (json.dumps(obj, sort_keys=True, separators=(",", ":")) + "\n").encode("ascii")

def fixture():
    base = {k: "sha256:" + "a"*64 for k in config.BASE}
    base.update(ops_source_sha="b"*40, app_source_sha="c"*40, go_fingerprint="SHA256:"+"A"*43)
    return dict(schema="qa525-schema-cut-config-v1", host="glmdch", environment="QA_SYNTHETIC",
                purpose="SCHEMA_API_WEB_QA_ONLY", pins=base,
                schema_pins={k:"sha256:"+"d"*64 for k in config.SCHEMA})

class ConfigTests(unittest.TestCase):
    def check(self, raw):
        return config.check_config(raw, expected_sha256=digest(raw))

    def test_exact_pinned_bytes(self):
        obj = fixture()
        result = self.check(encode(obj))
        self.assertEqual(result["pins"], obj["pins"])
        self.assertEqual(result["schema_pins"], obj["schema_pins"])
        self.assertIs(result["may_apply"], False)

    def test_tamper(self):
        raw = encode(fixture())
        with self.assertRaises(config.Stop):
            config.check_config(raw+b" ", expected_sha256=digest(raw))

    def test_scope_and_host(self):
        for key, value in [("purpose","API_WEB_QA_ONLY"), ("host","srv01"), ("environment","PRODUCTION")]:
            with self.subTest(key=key):
                obj = fixture()
                obj[key] = value
                with self.assertRaises(config.Stop):
                    self.check(encode(obj))

    def test_noncanonical_and_duplicate(self):
        raw = encode(fixture())
        for bad in [raw+b" ", b'{"host":"glmdch",'+raw[1:], b'{"x":NaN}', b'\xff']:
            with self.subTest(raw=bad):
                with self.assertRaises(config.Stop):
                    self.check(bad)

    def test_pin_shape_and_type(self):
        for mutation in ("missing","extra","wrong_type","uppercase"):
            obj = fixture()
            pins = obj["schema_pins"]
            key = "migrator_image_digest"
            if mutation == "missing":
                del pins[key]
            elif mutation == "extra":
                pins["extra"] = pins[key]
            elif mutation == "wrong_type":
                pins[key] = True
            else:
                pins[key] = "sha256:"+"A"*64
            with self.subTest(mutation=mutation):
                with self.assertRaises(config.Stop):
                    self.check(encode(obj))

if __name__ == "__main__":
    unittest.main()
