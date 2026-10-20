"""Restricted Docker adapter tests: commands, drift, partial apply and rollback."""
import copy
from dataclasses import replace
import hashlib
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]


def load(name, filename):
    spec = importlib.util.spec_from_file_location(name, ROOT / "scripts/qa" / filename)
    value = importlib.util.module_from_spec(spec)
    sys.modules[name] = value
    spec.loader.exec_module(value)
    return value


mod = load("compose_adapter", "qa-lite-compose-adapter.py")
tx = load("adapter_transaction", "qa-lite-web-transaction.py")
OLD = "sha256:" + "a" * 64
NEW = "sha256:" + "b" * 64
OLD_SHA = "c" * 40
NEW_SHA = "d" * 40
PLAN = tx.WebReleasePlan(mod.PROJECT, OLD_SHA, NEW_SHA, OLD, NEW, "e" * 64)


class Engine:
    def __init__(self, workdir, config):
        self.calls = []
        self.fail_apply = False
        self.changed_dependency = False
        self.workdir = workdir
        self.config = config
        self.state = {
            "web": ["1" * 64, OLD, "running", "start-web", 0, "web", workdir,
                    config, [], {"qa": {"NetworkID": "network-1"}}, {}, 0],
            "postgres": ["2" * 64, OLD, "running", "start-pg", 0, "postgres",
                         workdir, config, [{"Name": "qa-pg", "Destination": "/data"}],
                         {"qa": {"NetworkID": "network-1"}}, {}, 0],
        }

    def __call__(self, args, **kwargs):
        self.calls.append((args, kwargs))
        if args[0] == "ps":
            return "\n".join(x[0] for x in self.state.values())
        if args[0] == "inspect":
            value = next(x for x in self.state.values() if x[0] == args[-1])
            return json.dumps(value)
        if args[:2] == ["image", "inspect"]:
            return NEW_SHA if "revision" in args[-2] else args[-1]
        if args[0] == "compose":
            override = Path(args[args.index("up") - 1])
            image = json.loads(override.read_text())["services"]["web"]["image"]
            self.state["web"][1] = image
            self.state["web"][7] = self.config + "," + str(override)
            if self.fail_apply and image == NEW:
                raise mod.Blocked("DOCKER_OPERATION_FAILED")
            return ""
        raise AssertionError(args)


class AdapterTests(unittest.TestCase):
    def setUp(self):
        # Bypass root filesystem ownership only in offline test fixtures.
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.config = self.root / "compose.yml"
        self.env = self.root / "qa.env"
        self.config.write_text("services: {}\n")
        self.env.write_text("SYNTHETIC=value\n")
        self.binding = mod.Binding(str(self.root), (str(self.config),), str(self.env),
                                   tuple((str(p), hashlib.sha256(p.read_bytes()).hexdigest())
                                         for p in (self.config, self.env)), str(self.root))
        self.engine = Engine(str(self.root), str(self.config))
        self.evidence = True
        self.health = True
        self.adapter = mod.ComposeWebAdapter(self.binding, lambda plan: self.evidence,
                                             lambda: self.health, run=self.engine)
        self.adapter.locked = True
        patcher = patch.object(mod, "trusted_path", side_effect=lambda p, **kw: Path(p))
        patcher.start()
        self.addCleanup(patcher.stop)

    def test_success_only_recreates_web_by_local_image_id(self):
        result = tx.coordinate(PLAN, self.adapter)
        self.assertEqual("WEB_HEALTHY_PENDING_AUTHENTICATED_QA", result["status"])
        calls = [args for args, kw in self.engine.calls if args[0] == "compose"]
        self.assertEqual(1, len(calls))
        cmd = calls[0]
        self.assertEqual(["up", "--detach", "--no-deps", "--no-build", "--pull", "never",
                          "--force-recreate", "--wait", "--wait-timeout", "60", "web"],
                         cmd[cmd.index("up"):])
        self.assertNotIn("down", cmd)
        self.assertNotIn("--volumes", cmd)
        self.assertEqual(NEW, self.engine.state["web"][1])

    def test_partial_apply_failure_restores_old_image(self):
        before = copy.deepcopy(self.engine.state["postgres"])
        self.engine.fail_apply = True
        with self.assertRaisesRegex(tx.Blocked, "APPLY_FAILED_ROLLBACK_VERIFIED"):
            tx.coordinate(PLAN, self.adapter)
        self.assertEqual(OLD, self.engine.state["web"][1])
        self.assertEqual(before, self.engine.state["postgres"])
        self.assertEqual(2, sum(args[0] == "compose" for args, kw in self.engine.calls))

    def completed_jobs(self):
        for digit, name in enumerate(sorted(mod.COMPLETED_JOBS), start=3):
            value = copy.deepcopy(self.engine.state["postgres"])
            value[0], value[2], value[5], value[11] = str(digit) * 64, "exited", name, 0
            self.engine.state[name] = value
        return {k: copy.deepcopy(v) for k, v in self.engine.state.items() if k != "web"}

    def test_completed_initializers_allow_apply_and_remain_untouched(self):
        before = self.completed_jobs()
        result = tx.coordinate(PLAN, self.adapter)
        self.assertEqual("WEB_HEALTHY_PENDING_AUTHENTICATED_QA", result["status"])
        self.assertEqual(before, {k: v for k, v in self.engine.state.items() if k != "web"})
        self.assertTrue(self.adapter.verify_untouched_dependencies())

    def test_completed_initializers_are_preserved_during_rollback(self):
        before = self.completed_jobs()
        health = iter([False, True])
        self.adapter.public_health = lambda: next(health)
        with self.assertRaisesRegex(tx.Blocked, "APPLY_FAILED_ROLLBACK_VERIFIED"):
            tx.coordinate(PLAN, self.adapter)
        self.assertEqual(OLD, self.engine.state["web"][1])
        self.assertEqual(before, {k: v for k, v in self.engine.state.items() if k != "web"})

    def test_failed_unfinished_or_unknown_jobs_and_stopped_services_block(self):
        self.completed_jobs()
        cases = [(name, status, code) for name in mod.COMPLETED_JOBS
                 for status, code in [("exited", 1), ("running", 0), ("created", 0),
                                      ("restarting", 0), ("dead", 0), ("paused", 0)]]
        cases += [("postgres", "exited", 0), ("postgres", "exited", 1)]
        extra = copy.deepcopy(self.engine.state["migrator"])
        extra[0], extra[5] = "6" * 64, "unknown-init"
        cases.append(("unknown-init", "exited", 0))
        for name, status, code in cases:
            with self.subTest(name=name, status=status, code=code):
                original = copy.deepcopy(self.engine.state)
                if name == "unknown-init":
                    self.engine.state[name] = extra
                self.engine.state[name][2], self.engine.state[name][11] = status, code
                with self.assertRaisesRegex(mod.Blocked, "DEPENDENCY_NOT_RUNNING"):
                    self.adapter.reverify(PLAN)
                self.engine.state = original
        self.assertFalse(any(args[0] == "compose" for args, kw in self.engine.calls))

    def test_completed_job_exit_code_drift_blocks_apply(self):
        self.completed_jobs()
        self.adapter.reverify(PLAN)
        self.engine.state["migrator"][11] = 1
        self.assertFalse(self.adapter.verify_untouched_dependencies())
        with self.assertRaisesRegex(mod.Blocked, "RUNTIME_CHANGED_BEFORE_APPLY"):
            self.adapter.activate_web(PLAN)
        self.assertFalse(any(args[0] == "compose" for args, kw in self.engine.calls))

    def test_health_failure_restores_old_image_and_reports_failure(self):
        checks = iter([False, True])
        self.adapter.public_health = lambda: next(checks)
        with self.assertRaisesRegex(tx.Blocked, "APPLY_FAILED_ROLLBACK_VERIFIED"):
            tx.coordinate(PLAN, self.adapter)
        self.assertEqual(OLD, self.engine.state["web"][1])

    def test_requires_exclusive_session_and_independent_evidence(self):
        for locked, evidence in ((False, True), (True, False)):
            self.adapter.locked, self.evidence = locked, evidence
            with self.assertRaises(mod.Blocked):
                self.adapter.reverify(PLAN)
        self.assertFalse(any(a[0] == "compose" for a, kw in self.engine.calls))

    def test_unknown_git_sha_only_allowed_for_explicit_first_image_bootstrap(self):
        initial = replace(PLAN, previous_sha=None)
        with self.assertRaisesRegex(mod.Blocked, "PLAN_SCOPE_INVALID"):
            self.adapter.reverify(initial)
        self.adapter.binding = replace(self.binding, initial_image_id=OLD)
        self.assertTrue(self.adapter.reverify(initial))
        (self.root / "installed.json").write_text("{}")
        with self.assertRaisesRegex(mod.Blocked, "PLAN_SCOPE_INVALID"):
            self.adapter.reverify(initial)

    def test_changed_env_or_compose_blocks_before_mutation(self):
        for p in (self.config, self.env):
            original = p.read_text()
            p.write_text("changed")
            with self.assertRaisesRegex(mod.Blocked, "CONFIG_CHANGED"):
                self.adapter.reverify(PLAN)
            p.write_text(original)
        self.assertEqual([], self.engine.calls)

    def test_metadata_drift_between_reverify_and_apply_blocks(self):
        self.adapter.reverify(PLAN)
        self.config.write_text("changed")
        with self.assertRaisesRegex(mod.Blocked, "CONFIG_CHANGED"):
            self.adapter.activate_web(PLAN)
        self.assertFalse(any(a[0] == "compose" for a, kw in self.engine.calls))

    def test_wrong_project_file_labels_or_image_blocks(self):
        for index, bad in ((1, NEW), (6, "/opt/other"), (7, "/opt/other.yml")):
            original = self.engine.state["web"][index]
            self.engine.state["web"][index] = bad
            with self.assertRaises(mod.Blocked):
                self.adapter.reverify(PLAN)
            self.engine.state["web"][index] = original

    def test_duplicate_services_rejected(self):
        self.engine.state["postgres"][5] = "web"
        with self.assertRaisesRegex(mod.Blocked, "DUPLICATE_OR_UNNAMED_SERVICE"):
            self.adapter.reverify(PLAN)

    def test_mount_network_and_port_drift_fail_verification(self):
        self.adapter.reverify(PLAN)
        for index, bad in ((8, [{"Destination": "/var/run/docker.sock"}]),
                           (9, {"new": {"NetworkID": "different"}}),
                           (10, {"80/tcp": [{"HostIp": "0.0.0.0", "HostPort": "80"}]})):
            original = self.engine.state["web"][index]
            self.engine.state["web"][index] = bad
            self.assertFalse(self.adapter.verify_running_web(OLD))
            self.engine.state["web"][index] = original

    def test_dependency_restart_is_not_silently_accepted(self):
        self.adapter.reverify(PLAN)
        self.engine.state["postgres"][3] = "restarted"
        self.assertFalse(self.adapter.verify_untouched_dependencies())

    def test_concurrent_manual_change_blocks_apply(self):
        self.adapter.reverify(PLAN)
        self.engine.state["web"][1] = NEW
        with self.assertRaisesRegex(mod.Blocked, "RUNTIME_CHANGED_BEFORE_APPLY"):
            self.adapter.activate_web(PLAN)
        self.assertFalse(any(a[0] == "compose" for a, kw in self.engine.calls))

    def test_exclusive_lock_blocks_second_session(self):
        other = mod.ComposeWebAdapter(self.binding, lambda p: True, lambda: True, run=self.engine)
        with patch.object(mod.os, "geteuid", return_value=0), \
                patch.object(mod.socket, "gethostname", return_value="glmdch"):
            with self.adapter.exclusive():
                with self.assertRaisesRegex(mod.Blocked, "ANOTHER_UPDATE_RUNNING"):
                    with other.exclusive():
                        self.fail("second session must not enter")
            self.assertFalse(self.adapter.locked)

    def test_override_cannot_be_replaced_or_injected(self):
        self.adapter.reverify(PLAN)
        self.adapter.override_path(NEW).write_text("tampered")
        with self.assertRaisesRegex(mod.Blocked, "OVERRIDE_CHANGED"):
            self.adapter.activate_web(PLAN)
        with self.assertRaisesRegex(mod.Blocked, "IMAGE_NOT_PINNED"):
            self.adapter.override_path("nginx:latest; echo secret")

    def test_prior_immutable_override_is_recognized(self):
        self.engine.state["web"][7] += "," + str(self.adapter.override_path(OLD))
        self.assertTrue(self.adapter.reverify(PLAN))

    def test_probe_inspection_never_reads_env_or_full_inspect(self):
        state = mod.metadata(self.engine)
        self.assertIn("web", state)
        for args, kw in self.engine.calls:
            self.assertNotIn(".Config.Env", " ".join(args))
            if args[0] == "inspect":
                self.assertEqual("--format", args[1])

    def test_docker_transport_has_fixed_socket_no_shell_and_no_inherited_secrets(self):
        with patch.object(mod.subprocess, "run") as run:
            run.return_value.stdout = "ok\n"
            self.assertEqual("ok", mod.docker(["ps"]))
            args, kwargs = run.call_args
            self.assertEqual(["/usr/bin/docker", "--host", "unix:///var/run/docker.sock", "ps"], args[0])
            self.assertNotIn("shell", kwargs)
            self.assertEqual({"PATH": "/usr/bin:/bin", "HOME": "/root"}, kwargs["env"])
            run.side_effect = subprocess.CalledProcessError(1, ["docker"], stderr="SECRET")
            with self.assertRaisesRegex(mod.Blocked, "DOCKER_OPERATION_FAILED") as error:
                mod.docker(["ps"])
            self.assertNotIn("SECRET", str(error.exception))


if __name__ == "__main__":
    unittest.main()
