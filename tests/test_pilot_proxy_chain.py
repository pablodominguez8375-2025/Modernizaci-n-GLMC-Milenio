"""Pruebas 100% offline para evidencia de proxy IP exacta #470."""
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]
SCRIPT = ROOT / "scripts/check-pilot-proxy-chain.py"
spec = importlib.util.spec_from_file_location("pilot_proxy_chain", SCRIPT)
gate = importlib.util.module_from_spec(spec)
spec.loader.exec_module(gate)
NETWORK = "pmgm_pilot_pmgm_pilot_internal"


def fixture(known="172.19.0.12,172.19.0.13",
            actual="172.19.0.12,172.19.0.13"):
    compose = {"services": {"api": {"environment": {
        "ForwardedHeaders__KnownProxies": known,
        "ConnectionStrings__MainDatabase": "DO_NOT_LOG_SYNTHETIC_SECRET",
    }}}}
    ips = {"api": "172.19.0.11", "web": "172.19.0.12", "gateway": "172.19.0.13"}
    containers = {}
    for service in ("api", "web", "gateway"):
        containers[service] = {
            "State": {"Running": True},
            "NetworkSettings": {"Networks": {NETWORK: {"IPAddress": ips[service]}}},
            "Config": {"Env": [
                "FORBIDDEN_PASSWORD=DO_NOT_LOG_SYNTHETIC_SECRET",
                "ForwardedHeaders__KnownProxies=" + (actual if service == "api" else ""),
            ]},
        }
    return compose, containers


class PilotProxyChainEvidenceTests(unittest.TestCase):
    def assert_blocked(self, config, inspected):
        result = gate.evaluate(config, inspected)
        self.assertEqual("BLOCKED", result["status"])
        self.assertEqual("NO_QA_CERTIFICATION", result["scope"])
        self.assertNotIn("DO_NOT_LOG_SYNTHETIC_SECRET", json.dumps(result))
        return result

    def test_exact_two_proxy_hops_are_only_prepared_for_qa_acceptance(self):
        result = gate.evaluate(*fixture())
        self.assertEqual("READY_FOR_AUDIT_ACCEPTANCE", result["status"])
        self.assertTrue(result["audit_evidence_pending"])
        self.assertEqual("NO_QA_CERTIFICATION", result["scope"])
        self.assertEqual(2, result["trusted_proxy_count"])
        self.assertNotIn("DO_NOT_LOG_SYNTHETIC_SECRET", json.dumps(result))

    def test_empty_trusted_proxy_configuration_fails_closed(self):
        self.assert_blocked(*fixture(known="", actual=""))

    def test_untrusted_extra_proxy_is_rejected(self):
        self.assert_blocked(*fixture(
            known="172.19.0.12,172.19.0.13,172.19.0.14",
            actual="172.19.0.12,172.19.0.13,172.19.0.14"))

    def test_different_configured_proxy_rejected(self):
        self.assert_blocked(*fixture(
            known="172.19.0.12,172.19.0.22",
            actual="172.19.0.12,172.19.0.22"))

    def test_stale_running_api_environment_rejected(self):
        self.assert_blocked(*fixture(actual="172.19.0.12,172.19.0.99"))

    def test_invalid_or_broad_networks_rejected(self):
        for ip in ("0.0.0.0", "127.0.0.1", "::ffff:172.19.0.12",
                   "172.19.0.0/16", "224.0.0.3", "bad"):
            with self.subTest(ip=ip):
                config, inspected = fixture()
                inspected["web"]["NetworkSettings"]["Networks"][NETWORK]["IPAddress"] = ip
                self.assert_blocked(config, inspected)

    def test_duplicate_proxy_ip_rejected(self):
        config, inspected = fixture()
        inspected["gateway"]["NetworkSettings"]["Networks"][NETWORK]["IPAddress"] = "172.19.0.12"
        self.assert_blocked(config, inspected)

    def test_disjoint_docker_network_rejected(self):
        config, inspected = fixture()
        inspected["gateway"]["NetworkSettings"]["Networks"] = {
            "untrusted-network": {"IPAddress": "172.19.0.13"}
        }
        self.assert_blocked(config, inspected)

    def test_stopped_service_rejected(self):
        config, inspected = fixture()
        inspected["gateway"]["State"]["Running"] = False
        self.assert_blocked(config, inspected)

    def test_missing_container_or_compose_structure_rejected(self):
        config, inspected = fixture()
        inspected.pop("gateway")
        self.assert_blocked(config, inspected)
        self.assert_blocked({}, fixture()[1])

    def test_local_env_missing_does_not_call_docker(self):
        with tempfile.TemporaryDirectory() as tmp:
            result = gate.inspect_running_pilot(str(Path(tmp) / "nonexistent.env"))
        self.assertEqual("BLOCKED", result["status"])

    def test_cli_failure_never_leaks_env_location_or_secret(self):
        with tempfile.TemporaryDirectory() as tmp:
            secret_path = str(Path(tmp) / "secret-pilot.env")
            process = subprocess.run(
                [sys.executable, str(SCRIPT), "--env-file", secret_path],
                capture_output=True, text=True, timeout=10)
        self.assertNotEqual(0, process.returncode)
        result = json.loads(process.stdout)
        self.assertEqual("BLOCKED", result["status"])
        self.assertNotIn(secret_path, process.stdout)
        self.assertNotIn("Traceback", process.stderr)

    def test_script_does_not_edit_env_or_use_shell(self):
        source = SCRIPT.read_text(encoding="utf-8")
        self.assertNotIn("shell=True", source)
        self.assertNotIn(".write_text(", source)
        self.assertNotIn(".write_bytes(", source)
        self.assertNotIn("docker compose up", source)
        self.assertNotIn("docker compose restart", source)


if __name__ == "__main__":
    unittest.main()
