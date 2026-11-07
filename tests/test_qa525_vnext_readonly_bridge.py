"""Adversarial synthetic tests for the vNext physical-shape READ-ONLY bridge."""
import copy
import importlib.util
from pathlib import Path
import unittest

HERE = Path(__file__).resolve().parents[1] / "scripts" / "qa"


def load(name, file):
    spec = importlib.util.spec_from_file_location(name, HERE / file)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


bridge = load("qa525_readonly_bridge_contract", "qa525-vnext-readonly-bridge.py")
BASE = "/opt/centenario/qa-lab-firstboot/recovery-v2/compose.recovery.json"
API = [BASE, "/opt/centenario/qa-api-refresh/api-legacy.json",
       "/etc/pmgm/qa754-identity-overlay.stage.json"] + [
           "/opt/centenario/qa525-signed-prepare/api-" + sha + ".json"
           for sha in ("725bc319", "41d9c490", "6eed4f00")]
WEB = [BASE, "/opt/centenario/qa-lab-firstboot/recovery-v2/web-legacy.json"] + [
    "/opt/centenario/qa525-signed-prepare/web-" + sha + ".json"
    for sha in ("725bc319", "41d9c490", "6eed4f00")]
RUNNING = ("api", "web", "keycloak", "postgres", "minio", "gateway", "clamav")
ONESHOTS = ("db-grants", "migrator", "minio-init")
WEB_SHA = "d" * 40


def rows():
    result = {}
    for index, service in enumerate(RUNNING + ONESHOTS, 1):
        ch = format(index, "x")
        layer = API if service == "api" else WEB if service == "web" else [BASE]
        result[service] = [
            ch * 64, "sha256:" + ch * 64,
            "running" if service in RUNNING else "exited",
            "2026-11-07T00:00:00Z", 0, service,
            "/opt/centenario/qa-lab-firstboot/recovery-v2",
            ",".join(layer), [], {}, {}, 0,
        ]
    return result


class Compose:
    def __init__(self, records, mode="ok"):
        self.records = records
        self.mode = mode
        self.calls = 0

    def metadata(self, run):
        self.calls += 1
        if self.mode == "malicious":
            run(["inspect", "--format", "{{json .Config.Env}}", "api"])
        elif self.mode == "write":
            run(["compose", "up", "-d"])
        else:
            run(list(bridge.adapters.LIST_ARGV))
        return copy.deepcopy(self.records)


class Qa525ReadonlyShapeBridgeContractTests(unittest.TestCase):
    def setUp(self):
        self.calls = []
        self.data = rows()
        self.compose = Compose(self.data)

    def docker(self, argv):
        self.calls.append(argv)
        if argv == bridge.adapters.LIST_ARGV:
            return ""
        if argv[:4] == ["image", "inspect", "--format", bridge.adapters.REVISION_FORMAT]:
            return WEB_SHA
        raise AssertionError("unexpected command")

    def inspect(self, **kwargs):
        return bridge.inspect(
            docker_read=self.docker, compose_adapter=self.compose,
            health_check=lambda: True, served_web_revision=lambda: WEB_SHA,
            expected_layers={"api": list(API), "web": list(WEB)},
            **kwargs)

    def test_stable_ten_containers_and_six_five_layers_reports_no_authority(self):
        result = self.inspect()
        self.assertEqual(result["status"], bridge.RESULT_STATUS)
        self.assertEqual(result["exited_zero_oneshots_verified"], 3)
        self.assertEqual(result["running_services_expected"], 7)
        self.assertEqual((result["api_compose_layers"], result["web_compose_layers"]), (6, 5))
        self.assertEqual(self.compose.calls, 4)  # baseline + three observations
        self.assertFalse(result["may_apply"])
        self.assertFalse(result["may_install"])
        self.assertFalse(result["sourced_from_authenticated_host"])
        self.assertNotIn("api", repr(result["baseline_sha256"]))
        self.assertTrue(all(call == bridge.adapters.LIST_ARGV or call[0] == "image"
                            for call in self.calls))

    def test_missing_and_extra_oneshots_fail_closed(self):
        for name in ONESHOTS:
            with self.subTest(missing=name):
                data = rows()
                del data[name]
                self.compose = Compose(data)
                with self.assertRaisesRegex(bridge.Stop, "BRIDGE_BASELINE_STOP"):
                    self.inspect()
        data = rows()
        data["intruder"] = copy.deepcopy(data["api"])
        self.compose = Compose(data)
        with self.assertRaisesRegex(bridge.Stop, "BRIDGE_BASELINE_STOP"):
            self.inspect()

    def test_oneshot_nonzero_or_running_fails_closed(self):
        for name in ONESHOTS:
            for status, code in (("running", 0), ("exited", 1), ("restarting", 0)):
                with self.subTest(name=name, status=status):
                    data = rows()
                    data[name][2], data[name][11] = status, code
                    self.compose = Compose(data)
                    with self.assertRaisesRegex(bridge.Stop, "BRIDGE_BASELINE_STOP"):
                        self.inspect()

    def test_compose_missing_reordered_or_different_base_refused(self):
        for name in ("api", "web"):
            for variation in ("missing", "extra", "reorder"):
                with self.subTest(name=name, variation=variation):
                    data = rows()
                    layers = data[name][7].split(",")
                    if variation == "missing":
                        layers.pop()
                    elif variation == "extra":
                        layers.append("/opt/centenario/qa525-signed-prepare/orphan.json")
                    else:
                        layers[-1], layers[-2] = layers[-2], layers[-1]
                    data[name][7] = ",".join(layers)
                    self.compose = Compose(data)
                    with self.assertRaises(bridge.Stop):
                        self.inspect()
        data = rows()
        data["web"][7] = "/opt/wrong.json," + ",".join(WEB[1:])
        self.compose = Compose(data)
        with self.assertRaisesRegex(bridge.Stop, "BRIDGE_BASELINE_STOP"):
            self.inspect()

    def test_metadata_mutation_after_baseline_or_between_samples_denied(self):
        class Changing(Compose):
            def metadata(self, run):
                result = super().metadata(run)
                if self.calls == 2:
                    result["api"][1] = "sha256:" + "a" * 64
                return result
        self.compose = Changing(rows())
        with self.assertRaisesRegex(bridge.Stop, "BRIDGE_OBSERVATION_STOP"):
            self.inspect()
        class Restarting(Compose):
            def metadata(self, run):
                result = super().metadata(run)
                if self.calls == 3:
                    result["migrator"][2] = "running"
                return result
        self.compose = Restarting(rows())
        with self.assertRaisesRegex(bridge.Stop, "BRIDGE_OBSERVATION_STOP"):
            self.inspect()

    def test_bad_health_or_served_web_revision_denied(self):
        for kwargs in (
            {"health_check": lambda: False, "served_web_revision": lambda: WEB_SHA},
            {"health_check": lambda: True, "served_web_revision": lambda: "b" * 40},
            {"health_check": lambda: True, "served_web_revision": lambda: None},
        ):
            with self.subTest(values=str(kwargs.keys())):
                with self.assertRaisesRegex(bridge.Stop, "BRIDGE_OBSERVATION_STOP"):
                    bridge.inspect(docker_read=self.docker, compose_adapter=Compose(rows()),
                                   expected_layers={"api": list(API), "web": list(WEB)}, **kwargs)

    def test_forged_docker_metadata_or_write_command_denied_before_effect(self):
        for mode in ("malicious", "write"):
            with self.subTest(mode=mode):
                self.compose = Compose(rows(), mode)
                with self.assertRaisesRegex(bridge.Stop, "BRIDGE_BASELINE_STOP"):
                    self.inspect()
                self.assertEqual(self.calls, [])

    def test_future_reviewed_append_only_seven_six_layers_are_accepted(self):
        # A future legitimate update extends API6/WEB5 to API7/WEB6.
        # Every ordered path is still independently pinned; not just a count.
        data = rows()
        api = API + ["/opt/centenario/qa525-signed-prepare/api-next.json"]
        web = WEB + ["/opt/centenario/qa525-signed-prepare/web-next.json"]
        data["api"][7], data["web"][7] = ",".join(api), ",".join(web)
        result = bridge.inspect(
            docker_read=self.docker, compose_adapter=Compose(data),
            health_check=lambda: True, served_web_revision=lambda: WEB_SHA,
            expected_layers={"api": api, "web": web})
        self.assertEqual((result["api_compose_layers"], result["web_compose_layers"]), (7, 6))
        self.assertIs(result["may_apply"], False)

    def test_unpinned_or_invalid_compose_pin_refused(self):
        for pin in (None, {}, {"api": list(API)},
                    {"api": list(API), "web": list(WEB)[:-1]}):
            with self.subTest(pin=pin):
                with self.assertRaisesRegex(bridge.Stop, "BRIDGE_PIN_INVALID"):
                    bridge.inspect(docker_read=self.docker, compose_adapter=self.compose,
                                   health_check=lambda: True, served_web_revision=lambda: WEB_SHA,
                                   expected_layers=pin)

    def test_invalid_provider_and_sample_count_refused(self):
        for n in (1, 6, True, None):
            with self.subTest(n=n):
                with self.assertRaisesRegex(bridge.Stop, "BRIDGE_SAMPLES_INVALID"):
                    self.inspect(samples=n)
        with self.assertRaisesRegex(bridge.Stop, "BRIDGE_PORT_INVALID"):
            bridge.inspect(docker_read=None, compose_adapter=self.compose,
                           health_check=lambda: True, served_web_revision=lambda: WEB_SHA,
                           expected_layers={"api": list(API), "web": list(WEB)})


if __name__ == "__main__":
    unittest.main()
