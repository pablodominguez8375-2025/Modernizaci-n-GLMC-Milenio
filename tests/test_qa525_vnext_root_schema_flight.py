"""Schema flight adapter, with injected API/WEB host and held flight; no Docker/root."""
import importlib.util
from pathlib import Path
import types
import unittest

PATH = Path(__file__).resolve().parents[1] / "scripts/qa/qa525-vnext-root-schema-flight.py"
spec = importlib.util.spec_from_file_location("qa525_schema_flight_test", PATH)
flight = importlib.util.module_from_spec(spec)
spec.loader.exec_module(flight)


class RootPlane:
    def __init__(self):
        self.calls = 0

    def assert_held(self):
        self.calls += 1


class SchemaFlightReadbackTests(unittest.TestCase):
    def test_stopped_api_transition_blocks_before_nonce(self):
        with self.assertRaises(flight.Stop) as cm:
            flight.precheck_schema_transition()
        self.assertEqual(str(cm.exception), "SCHEMA_FLIGHT_STOPPED_API_BASELINE_UNSUPPORTED")

    def bind(self, outcome="APPLIED", health=True, revision="a" * 40):
        plane = RootPlane()
        seen = []

        def host_build(*args):
            seen.append("host")
            return {"g6_evidence": {}, "inputs": {}}

        def run_held(**kw):
            seen.append("held")
            self.assertIs(kw["root_locks"].outer, plane)
            return {"terminal_outcome": outcome}

        class Reentrant:
            def __init__(self, rl, outer):
                self.outer = outer()

        deps = {key: types.SimpleNamespace() for key in flight.SIBLING_MODULES}
        deps["root_locks"] = types.SimpleNamespace(RootPlane=RootPlane)
        deps["apiweb_child"] = types.SimpleNamespace(Reentrant=Reentrant)
        deps["apiweb_host"] = types.SimpleNamespace(build=host_build)
        deps["apiweb_effects"] = types.SimpleNamespace(https_fetch=lambda: object())
        deps["posix"] = types.SimpleNamespace(PosixPort=lambda: object())
        deps["run_held"] = types.SimpleNamespace(run_held=run_held)
        deps["health"] = types.SimpleNamespace(build=lambda fetch: (lambda: health, lambda: revision))
        flight.bind_dependencies(deps)
        return plane, seen

    def make(self, plane):
        return flight.make(archive_bytes=b"signed zip", claims={"purpose": "SCHEMA_API_WEB_QA_ONLY"},
                           authority={"target": {"web_sha": "a" * 40}}, plane=plane)

    def test_applied_requires_health_and_served_sha_readback(self):
        plane, seen = self.bind()
        result = self.make(plane)()
        self.assertEqual({"journal": "APPLIED", "health": True, "served_sha": "a" * 40}, result)
        self.assertEqual(["host", "held"], seen)
        self.assertGreaterEqual(plane.calls, 3)

    def test_rollback_never_reports_applied(self):
        plane, _ = self.bind(outcome="ROLLED_BACK")
        self.assertEqual({"journal": "FAILED", "health": False, "served_sha": ""}, self.make(plane)())

    def test_wrong_health_or_revision_is_visible_to_port(self):
        for health, revision in ((False, "a" * 40), (True, "b" * 40)):
            plane, _ = self.bind(health=health, revision=revision)
            result = self.make(plane)()
            self.assertNotEqual((True, "a" * 40), (result["health"], result["served_sha"]))

    def test_scope_and_plane_are_exact(self):
        plane, _ = self.bind()
        with self.assertRaises(flight.Stop):
            flight.make(archive_bytes=b"zip", claims={"purpose": "API_WEB_QA_ONLY"},
                        authority={"target": {"web_sha": "a" * 40}}, plane=plane)
        with self.assertRaises(flight.Stop):
            self.make(object())


if __name__ == "__main__":
    unittest.main()
