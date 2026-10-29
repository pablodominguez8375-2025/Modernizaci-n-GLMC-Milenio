"""Offline source-trust and seven-service inventory tests (NO Docker)."""
import copy
import importlib.util
import io
import json
from pathlib import Path
import unittest
from unittest.mock import Mock, patch

SRC = (Path(__file__).resolve().parents[1] / "scripts/qa/operators" /
       "qa525-host-inventory-readonly.py")
spec = importlib.util.spec_from_file_location("qa525_host_inventory_test", SRC)
mod = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mod)
COMPOSE = "/opt/centenario/qa-lab-firstboot/recovery-v2/compose.recovery.json"
IMAGE = "sha256:" + "a" * 64
CID = "b" * 64


def fixture():
    return {
        svc: [
            CID, IMAGE, "running", "synthetic-start", 0, svc,
            "/opt/centenario/qa-lab-firstboot/recovery-v2",
            COMPOSE, [{"Secret": "CONFIDENTIAL-MOUNT"}],
            {"internal": {"Secret": "PRIVATE-NETWORK"}},
            {"Port": "PRIVATE-PORT"}, 0
        ] for svc in mod.SERVICES
    }


class HostInventoryReadOnlyTests(unittest.TestCase):
    def test_bounded_seven_service_inventory_without_secret_values(self):
        data = fixture()
        original = copy.deepcopy(data)
        captured = []
        def digest(path):
            captured.append(path)
            return "c" * 64
        summary = mod.summarize(data, digest)
        self.assertEqual(
            "PHYSICAL_METADATA_CAPTURED_NO_CHANGE_AUTHORITY",
            summary["status"])
        self.assertEqual({"api", "web", "keycloak", "postgres",
                          "minio", "gateway", "clamav"}, set(summary["services"]))
        self.assertEqual([COMPOSE], captured)
        self.assertEqual([{"path": COMPOSE, "sha256": "c" * 64}],
                         summary["compose_file_digests"])
        self.assertFalse(summary["identity_verified"])
        self.assertFalse(summary["may_prepare"])
        self.assertFalse(summary["may_apply"])
        self.assertEqual(data, original)
        for secret in ("CONFIDENTIAL-MOUNT", "PRIVATE-NETWORK", "PRIVATE-PORT"):
            self.assertNotIn(secret, json.dumps(summary))

    def test_missing_service_fails_closed_before_hashing(self):
        data = fixture()
        del data["postgres"]
        digest = Mock()
        with self.assertRaisesRegex(mod.Blocked, "SEVEN_SERVICE_INVENTORY_INCOMPLETE"):
            mod.summarize(data, digest)
        digest.assert_not_called()

    def test_nonrunning_service_blocks(self):
        for state in ("exited", "created", "restarting"):
            with self.subTest(state=state):
                data = fixture()
                data["api"][2] = state
                with self.assertRaisesRegex(mod.Blocked, "CONTAINER_RUNTIME_INVALID"):
                    mod.summarize(data, Mock())

    def test_image_or_container_id_must_be_exact_docker_digest(self):
        for index, value in ((0, "not-id"), (1, "image:latest")):
            data = fixture()
            data["web"][index] = value
            with self.assertRaisesRegex(mod.Blocked, "CONTAINER_RUNTIME_INVALID"):
                mod.summarize(data, Mock())

    def test_foreign_compose_service_rejected(self):
        data = fixture()
        data["api"][5] = "fake"
        with self.assertRaisesRegex(mod.Blocked, "CONTAINER_RUNTIME_INVALID"):
            mod.summarize(data, Mock())

    def test_divergent_compose_workdir_blocks(self):
        data = fixture()
        data["web"][6] = "/opt/centenario/other"
        with self.assertRaisesRegex(mod.Blocked, "COMPOSE_PROJECT_WORKDIR_DIVERGED"):
            mod.summarize(data, Mock())

    def test_relative_or_duplicate_compose_paths_block(self):
        for path in ("compose.json", COMPOSE + ",../escape.json",
                     COMPOSE + "," + COMPOSE):
            with self.subTest(path=path):
                data = fixture()
                data["api"][7] = path
                with self.assertRaisesRegex(mod.Blocked, "COMPOSE_PATHS_INVALID"):
                    mod.summarize(data, Mock())

    def test_bad_or_unverified_compose_digest_blocks(self):
        with self.assertRaisesRegex(mod.Blocked, "COMPOSE_DIGEST_NOT_VERIFIED"):
            mod.summarize(fixture(), lambda _: "bad-hash")

    def test_two_different_compose_layers_are_both_hashed(self):
        data = fixture()
        extra = "/etc/pmgm/qa754-identity-overlay.stage.json"
        data["web"][7] += "," + extra
        called = []
        def digest(path):
            called.append(path)
            return "d" * 64
        report = mod.summarize(data, digest)
        self.assertEqual(sorted([COMPOSE, extra]), called)
        self.assertEqual(2, len(report["compose_file_digests"]))

    def test_before_import_all_source_files_must_be_root_trusted(self):
        checked = []
        def safe(path):
            checked.append(Path(path))
            if Path(path) == mod.ADAPTER:
                raise mod.Blocked("ROOT_PATH_UNTRUSTED")
        with patch.object(mod, "root_trusted", side_effect=safe), \
             patch.object(importlib.util, "spec_from_file_location") as imported:
            with self.assertRaisesRegex(mod.Blocked, "ROOT_PATH_UNTRUSTED"):
                mod.load_adapter()
            imported.assert_not_called()
        self.assertEqual([SRC, mod.ADAPTER], checked)

    def test_non_glmdch_host_blocks_before_any_import_or_docker(self):
        with patch.object(mod.os, "geteuid", return_value=1000), \
             patch.object(mod.socket, "gethostname", return_value="other"), \
             patch.object(mod, "load_adapter") as adapter:
            with self.assertRaisesRegex(mod.Blocked, "HOST_NOT_AUTHORIZED"):
                mod.inventory()
            adapter.assert_not_called()

    def test_cli_rejects_any_apply_or_prepare(self):
        for args in (["--apply"], ["--prepare"], [], ["--inventory", "--apply"]):
            with patch.object(mod, "inventory") as inv, \
                 patch("sys.stdout", io.StringIO()):
                self.assertEqual(2, mod.main(args))
                inv.assert_not_called()

    def test_cli_error_does_not_leak_secret_exception(self):
        out = io.StringIO()
        with patch.object(mod, "inventory",
                          side_effect=RuntimeError("PASSWORD=REAL_PRIVATE")), \
             patch("sys.stdout", out):
            self.assertEqual(2, mod.main(["--inventory"]))
        self.assertNotIn("REAL_PRIVATE", out.getvalue())
        self.assertIn("READONLY_INVENTORY_UNAVAILABLE", out.getvalue())

    def test_no_mutating_docker_or_shell_operations(self):
        source = SRC.read_text()
        for banned in ("subprocess", "os.system(", "systemctl", "docker compose up",
                       "pg_dump", '"--force-recreate"', 'os.replace(', 'write_text('):
            self.assertNotIn(banned, source)
        self.assertIn('"may_apply": False', source)


if __name__ == "__main__":
    unittest.main()
