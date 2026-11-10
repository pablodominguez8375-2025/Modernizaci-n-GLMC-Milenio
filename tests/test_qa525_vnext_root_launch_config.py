"""QA525 root config: purely offline, NO root, NO Sponsor keys or physical GO."""
import copy
import importlib.util
import math
import pathlib
import unittest

SCRIPTS = pathlib.Path(__file__).resolve().parents[1] / "scripts" / "qa"


def load(filename, name):
    spec = importlib.util.spec_from_file_location(name, SCRIPTS / filename)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


factory = load("qa525-vnext-root-launch-config.py", "qa525_launch_config")
launcher = load("qa525-vnext-root-launcher.py", "qa525_launcher_for_config_test")


def values():
    h = lambda c: "sha256:" + c * 64
    return {
        "pins": {
            "ops_source_sha": "a" * 40,
            "app_source_sha": "b" * 40,
            "app_archive_sha256": h("a"),
            "plan_sha256": h("b"),
            "admission_sha256": h("c"),
            "policy_hash": h("d"),
        },
        "launcher_pins": {
            "release_asset_sha256": h("e"),
            "old_launcher_sha256": "f" * 64,
            "candidate_operator_sha256": "1" * 64,
        },
        "allowed_signers": b"go-test-signers-lines",
        "go_fingerprint": "SHA256:" + "A" * 43,
        "ops_v3_sha256": h("2"),
        "app_subject_sha256": h("3"),
        "inbox": "/opt/centenario/qa525/inbox",
        "temp_root": "/opt/centenario/qa525/tmp",
        "socket_path": "/opt/centenario/qa525/socket",
        "journal_root": "/opt/centenario/qa525/journal",
        "lease_dir": "/opt/centenario/qa525/lease",
        "peer_uid": 1001,
        "timeout": 5,
        "children": dict(factory.CHILD_MODULES),
        "g6_pins": None,
        "authenticity": {
            "ops_allowed_signers": b"ops-test-signers-lines",
            "ops_signature_sha256": h("4"),
            "app_bundle_sha256": h("5"),
            "trusted_root_sha256": h("6"),
            "app_archive_path": "/opt/centenario/qa525/release.zip",
            "drop_uid": 30033,
            "drop_gid": 30033,
            "source_repository_id": "424242",
        },
    }


class RootConfigFactoryTests(unittest.TestCase):
    def denied(self, mutate):
        candidate = values()
        mutate(candidate)
        with self.assertRaises(factory.Stop) as raised:
            factory.materialize(preverified_config=candidate)
        self.assertEqual(str(raised.exception), "LAUNCH_CONFIG_INVALID")

    def test_import_is_never_authority(self):
        self.assertIsNone(factory.CONFIG)
        with self.assertRaises(launcher.Stop) as err:
            launcher._config(factory)
        self.assertEqual(str(err.exception), "LAUNCH_CONFIG_INVALID")

    def test_exact_runtime_schema_matches_root_launcher(self):
        original = values()
        module = factory.materialize(preverified_config=original)
        self.assertEqual(launcher._config(module), original)
        self.assertIsNot(module.CONFIG, original)
        self.assertIsNot(module.CONFIG["authenticity"], original["authenticity"])
        self.assertIsNone(factory.CONFIG)

    def test_caller_cannot_modify_detached_config(self):
        original = values()
        module = factory.materialize(preverified_config=original)
        original["pins"]["policy_hash"] = "sha256:" + "7" * 64
        original["authenticity"]["ops_allowed_signers"] = b"replaced"
        original["children"]["LAUNCHER_ONLY"] = "other.py"
        self.assertEqual(module.CONFIG["pins"]["policy_hash"], "sha256:" + "d" * 64)
        self.assertEqual(module.CONFIG["authenticity"]["ops_allowed_signers"], b"ops-test-signers-lines")
        self.assertEqual(module.CONFIG["children"], factory.CHILD_MODULES)

    def test_missing_extra_and_wrong_type_fail_closed(self):
        self.denied(lambda x: x.pop("pins"))
        self.denied(lambda x: x.update(evil=True))
        self.denied(lambda x: x.__setitem__("pins", []))
        self.denied(lambda x: x["pins"].pop("policy_hash"))
        self.denied(lambda x: x["launcher_pins"].update(evil="true"))
        self.denied(lambda x: x["authenticity"].pop("trusted_root_sha256"))

    def test_scope_cannot_be_redirected_to_g6_or_arbitrary_child(self):
        self.denied(lambda x: x.__setitem__("g6_pins", {"g6_command": "backup"}))
        self.denied(lambda x: x["children"].__setitem__("LAUNCHER_ONLY", "qa525-vnext-root-g6-backup.py"))
        self.denied(lambda x: x["children"].pop("G6:review"))
        self.denied(lambda x: x["children"].update(extra="another.py"))

    def test_all_pins_are_mandatory_and_strict(self):
        self.denied(lambda x: x["pins"].__setitem__("ops_source_sha", "sha256:" + "a" * 64))
        self.denied(lambda x: x["pins"].__setitem__("policy_hash", "d" * 64))
        self.denied(lambda x: x["launcher_pins"].__setitem__("candidate_operator_sha256", "sha256:" + "1" * 64))
        self.denied(lambda x: x["launcher_pins"].__setitem__("release_asset_sha256", "e" * 64))
        self.denied(lambda x: x["authenticity"].__setitem__("app_bundle_sha256", "0" * 64))
        self.denied(lambda x: x.__setitem__("go_fingerprint", "SHA256:fake"))

    def test_sponsor_identity_is_not_guessed(self):
        self.denied(lambda x: x["authenticity"].__setitem__("source_repository_id", "0"))
        self.denied(lambda x: x["authenticity"].__setitem__("source_repository_id", "0424242"))
        self.denied(lambda x: x["authenticity"].__setitem__("source_repository_id", "owner/name"))
        self.denied(lambda x: x["authenticity"].__setitem__("source_repository_id", 424242))
        self.denied(lambda x: x["authenticity"].__setitem__("source_repository_id", "1" * 21))

    def test_paths_must_be_absolute_canonical_with_no_null_byte(self):
        self.denied(lambda x: x.__setitem__("inbox", "../../tmp"))
        self.denied(lambda x: x.__setitem__("temp_root", "/tmp/../tmp"))
        self.denied(lambda x: x.__setitem__("journal_root", "/tmp//journal"))
        self.denied(lambda x: x["authenticity"].__setitem__("app_archive_path", "/tmp/bad\0file"))
        self.denied(lambda x: x.__setitem__("socket_path", "relative.sock"))

    def test_identity_and_clock_fail_closed(self):
        self.denied(lambda x: x.__setitem__("peer_uid", True))
        self.denied(lambda x: x.__setitem__("peer_uid", 0))
        self.denied(lambda x: x["authenticity"].__setitem__("drop_uid", x["peer_uid"]))
        self.denied(lambda x: x["authenticity"].__setitem__("drop_gid", -1))
        self.denied(lambda x: x.__setitem__("timeout", math.nan))
        self.denied(lambda x: x.__setitem__("timeout", math.inf))
        self.denied(lambda x: x.__setitem__("timeout", 61))
        self.denied(lambda x: x.__setitem__("timeout", False))

    def test_signer_lines_must_be_bounded_bytes(self):
        self.denied(lambda x: x.__setitem__("allowed_signers", "public-key-text"))
        self.denied(lambda x: x.__setitem__("allowed_signers", b""))
        self.denied(lambda x: x["authenticity"].__setitem__("ops_allowed_signers", b"X" * 4097))

    def test_not_a_root_entrypoint(self):
        self.assertIsNone(factory.CONFIG)
        self.assertFalse(hasattr(factory, "launch"))
        self.assertFalse(hasattr(factory, "act"))
        self.assertFalse(hasattr(factory, "main"))


if __name__ == "__main__":
    unittest.main()
