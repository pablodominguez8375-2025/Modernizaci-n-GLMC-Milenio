"""QA525 API+WEB closure loaded with the REAL PRE-4 mechanics: `exec(compile(bytes), ModuleType.__dict__)`, NO `__file__`,
one verified instance per file, transitive sibling closure injected by the launcher before any nonce exists."""
import importlib.util
import types
import unittest
from pathlib import Path
from unittest import mock

import test_qa525_vnext_held_flight_bridge as held_t
import test_qa525_vnext_root_apiweb_integration as flight_it

BASE = Path(__file__).resolve().parents[1] / "scripts" / "qa"


def _file(name):
    spec = importlib.util.spec_from_file_location("qa525_t_real_load_" + name[:-3].replace("-", "_"), BASE / name)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


launcher = _file("qa525-vnext-root-launcher.py")


def verified_loader(missing=(), mutate=None):
    calls = []

    def fresh(name):
        calls.append(name)
        if name in missing:
            raise KeyError(name)
        mod = types.ModuleType("qa525_boot_" + name[:-3].replace("-", "_"))
        src = (BASE / name).read_bytes()
        if mutate:
            src = mutate(name, src)
        exec(compile(src, "<verified:%s>" % name, "exec"), mod.__dict__)
        return mod
    return launcher._once(fresh), calls


ROOTS = sorted(set(launcher.APIWEB_MODULES.values()) | set(launcher.APIWEB_HOST_MODULES.values())
               | {launcher.APIWEB_HOST, launcher.APIWEB_EFFECTS})


class ApiwebRealLoadTests(unittest.TestCase):
    def test_every_pinned_module_executes_without_file_and_the_closure_binds(self):
        load, calls = verified_loader()
        launcher._bind_closure(load, ROOTS)
        self.assertEqual(len(calls), len(set(calls)))  # exactly one verified instance per pinned file
        for name in ROOTS:
            self.assertFalse(hasattr(load(name), "__file__"), name)

    def test_shared_instances_by_identity_across_the_transitive_closure(self):
        load, _ = verified_loader()
        launcher._bind_closure(load, ROOTS)
        m = lambda n: load("qa525-vnext-%s.py" % n)  # noqa: E731
        journal = m("journal")
        self.assertIs(m("flight").journal, journal)
        self.assertIs(m("scope-store").journal, journal)
        self.assertIs(m("stage-store").journal, journal)
        self.assertIs(m("terminal").journal, journal)
        self.assertIs(m("flight").store.journal, journal)
        self.assertIs(m("flight").terminal.journal, journal)
        plan = m("baseline-plan")
        for owner in ("flight", "observer", "evidence", "stage-store"):
            self.assertIs(m(owner).plan_mod, plan, owner)
        self.assertIs(m("flight").evidence, m("evidence"))
        self.assertIs(m("flight").store, m("stage-store"))
        self.assertIs(m("flight").terminal, m("terminal"))
        self.assertIs(m("observer").verify, m("verify"))
        self.assertIs(m("b7-held-readonly").root_locks, m("root-locks"))
        self.assertIs(m("admission-bridge").binding, m("g6-anchor").binding)
        self.assertIs(m("switch").containment, m("containment"))
        self.assertIs(m("host-adapters").containment, m("containment"))

    def test_a_missing_pinned_dependency_stops_before_any_effect(self):
        load, _ = verified_loader(missing={"qa525-vnext-journal.py"})
        with self.assertRaises(launcher.Stop) as err:
            launcher._bind_closure(load, ROOTS)
        self.assertEqual(str(err.exception), "LAUNCH_APIWEB_DEPENDENCY_MISSING")

    def test_a_dependency_declaration_that_is_not_closed_is_refused(self):
        def mutate(name, src):
            if name == "qa525-vnext-scope-store.py":
                return src.replace(b'"journal": "qa525-vnext-journal.py"', b'"journal": 7')
            return src
        load, _ = verified_loader(mutate=mutate)
        with self.assertRaises(launcher.Stop) as err:
            launcher._bind_closure(load, ROOTS)
        self.assertEqual(str(err.exception), "LAUNCH_APIWEB_DEPENDENCY_INVALID")

    def test_binding_is_single_shot_exact_and_never_available_from_disk_mode(self):
        load, _ = verified_loader()
        launcher._bind_closure(load, ROOTS)
        scope = load("qa525-vnext-scope-store.py")
        with self.assertRaises(scope.Stop):
            scope.bind_dependencies({"journal": load("qa525-vnext-journal.py")})  # second bind
        disk = _file("qa525-vnext-scope-store.py")
        with self.assertRaises(disk.Stop):
            disk.bind_dependencies({"journal": disk.journal})  # disk mode never accepts injection

    def test_no_pinned_module_resolves_siblings_from_disk_under_the_bootstrap(self):
        # production modules still load from disk only when __file__ exists (library/test mode); with the bootstrap
        # mechanics HERE is None and every dependency stays unbound until the launcher injects the verified one
        for name in ROOTS:
            mod = types.ModuleType("x")
            exec(compile((BASE / name).read_bytes(), name, "exec"), mod.__dict__)
            if hasattr(mod, "SIBLING_MODULES"):
                self.assertIsNone(mod.HERE, name)
                for key in mod.SIBLING_MODULES:
                    self.assertIsNone(getattr(mod, key), (name, key))


class ApiwebRealLoadFlightTests(flight_it.ApiwebIntegrationTests):
    """The SAME end-to-end flights (APPLIED under the five locks, rollback, expiry mid-WEB with dual rollback to
    baseline, clock fault in recovery, lost lock) but with EVERY engine module coming from the injected PRE-4 closure:
    no __file__, one verified instance per file, plane/boundary/journal/plan identities shared by construction."""

    def setUp(self):
        load, self.loaded = verified_loader()
        launcher._bind_closure(load, ROOTS)
        m = lambda n: load("qa525-vnext-%s.py" % n)  # noqa: E731
        orig = held_t.engine
        fresh = dict(bridge=m("admission-bridge"), observer=m("observer"), scopes=m("scope-store"),
                     switch=m("switch"), flight=m("flight"), g6_anchor=m("g6-anchor"))

        class Engine:
            def __getattr__(self, name):
                return fresh[name] if name in fresh else getattr(orig, name)
        for owner, attr, value in ((held_t, "engine", Engine()), (held_t, "b7", m("b7-held-readonly")),
                                   (held_t, "bound", m("b7-root-boundary")), (held_t, "rl", m("root-locks")),
                                   (held_t, "host", load("qa525-vnext-held-flight-bridge.py")),
                                   (flight_it, "rl", m("root-locks"))):
            patcher = mock.patch.object(owner, attr, value)
            patcher.start()
            self.addCleanup(patcher.stop)
        super().setUp()

    def test_the_flight_really_ran_on_the_injected_closure(self):
        self.assertFalse(hasattr(held_t.engine.flight, "__file__"))
        self.assertIs(held_t.engine.flight.journal, held_t.engine.scopes.journal)
        self.assertIs(type(self.outer), flight_it.rl.RootPlane)
        self.assertEqual(self.act()(self.fd()), "APIWEB_APPLIED")


if __name__ == "__main__":
    unittest.main()
