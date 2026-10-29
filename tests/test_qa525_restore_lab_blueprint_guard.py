"""Hermetic QA525 offhost Compose blueprint validation; never starts Docker."""
import copy
import importlib.util
import io
import json
from pathlib import Path
import unittest
from unittest.mock import Mock, patch

SRC = Path(__file__).resolve().parents[1] / "scripts/qa/operators/qa525-restore-lab-blueprint-guard.py"
spec = importlib.util.spec_from_file_location("qa525_lab_blueprint_test", SRC)
mod = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mod)
PIN = "postgres:17@sha256:" + "c" * 64


def template():
    return json.loads(mod.SOURCE.read_text())


def rendered():
    d = template()
    d["services"]["postgres"]["image"] = PIN
    d["services"]["postgres"]["volumes"] = [
        {"type": "volume", "source": "pgdata",
         "target": "/var/lib/postgresql/data", "read_only": False}
    ]
    return d


class LabBlueprintTests(unittest.TestCase):
    def test_template_matches_expected_inert_contract(self):
        r = mod.validate(template())
        self.assertEqual("OFFHOST_LAB_STRUCTURE_MATCHES_NO_ATTESTATION", r["status"])
        self.assertEqual("template", r["configuration_kind"])
        self.assertFalse(r["image_digest_syntax_checked"])
        self.assertFalse(r["may_apply"])
        self.assertFalse(r["lab_provisioned"])

    def test_rendered_exact_postgres17_digest_matches_without_attestation(self):
        r = mod.validate(rendered(), rendered=True)
        self.assertTrue(r["image_digest_syntax_checked"])
        self.assertFalse(r["image_signature_verified"])
        self.assertFalse(r["host_isolation_verified"])
        self.assertFalse(r["may_apply"])

    def test_unpinned_image_is_always_blocked(self):
        for value in ("postgres:17", "postgres:latest",
                      "postgres:17@sha256:1234",
                      "evil/postgres:17@sha256:" + "a" * 64):
            data = rendered()
            data["services"]["postgres"]["image"] = value
            with self.assertRaisesRegex(mod.Blocked, "POSTGRES_IMAGE_NOT_EXACT_DIGEST"):
                mod.validate(data, rendered=True)

    def test_no_unexpected_service_or_project(self):
        for key, value in (("name", "centenario-qa-lab2"),
                           ("services", {"postgres": {}, "api": {}})):
            data = template()
            data[key] = value
            with self.assertRaisesRegex(mod.Blocked, "LAB_PROJECT_OR_TOPOLOGY_INVALID"):
                mod.validate(data)

    def test_no_networks_even_in_top_level(self):
        data = template()
        data["networks"] = {"production": {"external": True}}
        with self.assertRaisesRegex(mod.Blocked, "LAB_PROJECT_OR_TOPOLOGY_INVALID"):
            mod.validate(data)

    def test_no_host_network_pid_or_ipc(self):
        for key in ("network_mode", "pid", "ipc"):
            data = template()
            data["services"]["postgres"][key] = "host"
            with self.assertRaisesRegex(mod.Blocked, "LAB_ISOLATION_CHANGED"):
                mod.validate(data)

    def test_no_privileges_published_ports_or_missing_security_opt(self):
        for key, value in (("privileged", True),
                           ("ports", ["5432:5432"]),
                           ("security_opt", []),
                           ("cap_drop", [])):
            data = template()
            data["services"]["postgres"][key] = value
            with self.assertRaisesRegex(mod.Blocked, "LAB_ISOLATION_CHANGED"):
                mod.validate(data)

    def test_no_exec_entrypoint_override_build_devices_or_host_volumes(self):
        for key, value in (("build", "."), ("entrypoint", ["sh"]),
                           ("devices", ["/dev/sda:/dev/sda"]),
                           ("read_only", False), ("secrets", ["prod"])):
            data = template()
            data["services"]["postgres"][key] = value
            with self.assertRaisesRegex(mod.Blocked, "LAB_SERVICE_HAS_UNREVIEWED_FIELDS"):
                mod.validate(data)

    def test_no_secrets_or_password_environment_in_blueprint(self):
        for env in ({"POSTGRES_PASSWORD": "fake"},
                    {"POSTGRES_DB": "pmgm"},
                    {"POSTGRES_HOST_AUTH_METHOD": "md5", "POSTGRES_DB": "postgres"}):
            data = template()
            data["services"]["postgres"]["environment"] = env
            with self.assertRaisesRegex(mod.Blocked, "LAB_ENVIRONMENT_UNEXPECTED"):
                mod.validate(data)

    def test_no_other_pgdata_mount(self):
        for volume in (["/prod:/var/lib/postgresql/data"],
                       ["pgdata:/var/lib/postgresql/data", "extra:/backup"],
                       ["qa525:/var/lib/postgresql/data"]):
            data = template()
            data["services"]["postgres"]["volumes"] = volume
            with self.assertRaisesRegex(mod.Blocked, "LAB_VOLUME_INVALID"):
                mod.validate(data)

    def test_rendered_mount_must_not_be_readonly_or_bind(self):
        for key, value in (("type", "bind"),
                           ("target", "/data"), ("source", "prod"),
                           ("read_only", True)):
            data = rendered()
            data["services"]["postgres"]["volumes"][0][key] = value
            with self.assertRaisesRegex(mod.Blocked, "LAB_VOLUME_INVALID"):
                mod.validate(data, rendered=True)

    def test_volume_must_be_owned_by_this_project_not_external(self):
        for key, value in (("name", "centenario-qa-lab2_pgdata"),
                           ("external", True),
                           ("driver", "nfs"), ("labels", {"prod": "yes"})):
            data = template()
            data["volumes"]["pgdata"][key] = value
            with self.assertRaisesRegex(mod.Blocked, "LAB_VOLUME_NOT_EXCLUSIVE"):
                mod.validate(data)

    def test_healthcheck_must_not_be_suppressed_or_modified(self):
        data = template()
        data["services"]["postgres"]["healthcheck"]["disable"] = True
        with self.assertRaisesRegex(mod.Blocked, "LAB_HEALTHCHECK_INVALID"):
            mod.validate(data)

    def test_cli_template_is_no_deployment(self):
        out = io.StringIO()
        with patch("sys.stdout", out):
            self.assertEqual(0, mod.main(["--check-template"]))
        self.assertIn('"may_apply": false', out.getvalue())
        self.assertNotIn("AGE_PRIVATE", out.getvalue())

    def test_cli_rendered_accepts_only_sanitized_json(self):
        out = io.StringIO()
        stdin = Mock()
        stdin.buffer.read.return_value = json.dumps(rendered()).encode()
        with patch("sys.stdout", out), patch.object(mod.sys, "stdin", stdin):
            self.assertEqual(0, mod.main(["--check-rendered"]))
        self.assertIn('"image_digest_syntax_checked": true', out.getvalue())

    def test_cli_apply_is_never_supported(self):
        out = io.StringIO()
        with patch("sys.stdout", out):
            self.assertEqual(2, mod.main(["--apply"]))
        self.assertIn('"may_apply": false', out.getvalue())

    def test_code_never_launches_docker_or_executes_scripts(self):
        text = SRC.read_text()
        for banned in ("subprocess", "os.system", "docker compose up",
                       "pg_restore", "pg_dump", "Popen(", "exec("):
            self.assertNotIn(banned, text)
        self.assertIn('"may_apply": False', text)


if __name__ == "__main__":
    unittest.main()
