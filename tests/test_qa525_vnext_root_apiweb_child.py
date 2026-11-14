"""QA525 API_WEB_QA_ONLY child: composition only. Every effect is an injected double; nothing here is authority."""
import hashlib
import importlib.util
import os
import pathlib
import types
import unittest

SRC = pathlib.Path(__file__).resolve().parents[1] / "scripts/qa/qa525-vnext-root-apiweb-child.py"
spec = importlib.util.spec_from_file_location("qa525_t_apiweb_child", SRC)
child = importlib.util.module_from_spec(spec)
spec.loader.exec_module(child)

DATA = b"signed-release-zip" * 50
ARCHIVE = "sha256:" + hashlib.sha256(DATA).hexdigest()
NONCE = "e" * 64
CLAIMS = {"purpose": "API_WEB_QA_ONLY", "nonce": NONCE, "app_source_sha": "b" * 40, "app_archive_sha256": ARCHIVE}


class ApiwebChildFakePlane:
    def __init__(self, port=None, journal=None, lease=None, entries=()):
        self.port, self.journal, self.lease, self._entries = port, journal, lease, entries
        self.held = True

    def assert_held(self):
        if not self.held:
            raise RuntimeError("ROOTLOCK_LOST")


class ApiwebChildLease:
    def __init__(self):
        self.closed = 0

    def close(self):
        self.closed += 1


class ApiwebChildJournal:
    def __init__(self):
        self.lease = ApiwebChildLease()
        self.begun = []

    def begin(self, root, admission, sha, now):
        self.begun.append((root, admission, sha, now))
        return self.lease


def fd_with(data):
    fd = os.memfd_create("t")
    os.write(fd, data)
    return fd


class ApiwebChildEnv(unittest.TestCase):
    def setUp(self):
        self.rl = types.SimpleNamespace(RootPlane=ApiwebChildFakePlane, Stop=Exception, STATUS="s", LEGACY_LOCKS=())
        self.outer = ApiwebChildFakePlane(entries=(("lock-a", 11, None), ("lock-b", 12, None)))
        self.calls, self.fds = [], []
        self.result = {"terminal_outcome": "APPLIED"}
        self.built = []
        self.inputs = {"clock": 1, "target": {}}

        def run_held(**kw):
            self.calls.append(kw)
            if isinstance(self.result, BaseException):
                raise self.result
            return self.result
        mods = {k: types.SimpleNamespace(name=k) for k in child.MODULES}
        mods["root_locks"] = self.rl
        mods["run_held"] = types.SimpleNamespace(run_held=run_held)
        self.modules = mods
        self.host = types.SimpleNamespace(build=self.build)
        self.host_result = lambda: {"g6_evidence": {"e": 1}, "inputs": dict(self.inputs)}

    def build(self, data, claims, *rest):
        self.rest = rest
        self.built.append((data, claims))
        r = self.host_result()
        if isinstance(r, BaseException):
            raise r
        return r

    def fd(self, data=DATA):
        fd = fd_with(data)
        self.fds.append(fd)
        return fd

    def tearDown(self):
        for fd in self.fds:
            try:
                os.close(fd)
            except OSError:
                pass

    def act(self, claims=CLAIMS, **over):
        kw = dict(claims=claims if type(claims) is not dict else dict(claims), modules=self.modules, host=self.host,
                  outer_plane=lambda: self.outer, port="PORT", authority={"a": 1}, host_modules={"m": 1}, effects="FX")
        kw.update(over)
        return child.act_for(**kw)

    def stop(self, code, fn):
        with self.assertRaises(child.Stop) as err:
            fn()
        self.assertEqual(str(err.exception), code)


class ApiwebChildClaimsAndBinding(ApiwebChildEnv):
    def test_only_the_api_web_scope_with_the_exact_claim_shape(self):
        for bad in (dict(CLAIMS, purpose="LAUNCHER_ONLY"), dict(CLAIMS, purpose="G6_V2_ONLY"),
                    dict(CLAIMS, g6_command="backup"), {k: v for k, v in CLAIMS.items() if k != "nonce"},
                    dict(CLAIMS, nonce="x"), dict(CLAIMS, app_source_sha="b" * 39),
                    dict(CLAIMS, app_archive_sha256="b" * 64), None, []):
            with self.subTest(bad=bad):
                self.stop("APIWEB_GO_CLAIMS_INVALID", lambda: self.act(claims=bad))
        self.assertEqual(self.calls, [])

    def test_missing_modules_host_or_plane_provider_stop_before_any_effect(self):
        incomplete = dict(self.modules)
        incomplete.pop("switch")
        self.stop("APIWEB_BINDING_INVALID", lambda: self.act(modules=incomplete))
        self.stop("APIWEB_BINDING_INVALID", lambda: self.act(modules=dict(self.modules, extra=1)))
        self.stop("APIWEB_BINDING_INVALID", lambda: self.act(host=types.SimpleNamespace()))
        self.stop("APIWEB_BINDING_INVALID", lambda: self.act(outer_plane=None))
        self.stop("APIWEB_BINDING_INVALID",
                  lambda: self.act(modules=dict(self.modules, run_held=types.SimpleNamespace())))


class ApiwebChildArchiveAndHost(ApiwebChildEnv):
    def test_archive_that_differs_from_the_signed_digest_never_reaches_the_host(self):
        self.stop("APIWEB_ARCHIVE_MISMATCH", lambda: self.act()(self.fd(DATA + b"x")))
        self.assertEqual((self.built, self.calls), ([], []))

    def test_non_regular_fd_is_refused(self):
        r, w = os.pipe()
        self.fds += [r, w]
        self.stop("APIWEB_FD_INVALID", lambda: self.act()(r))
        self.assertEqual(self.built, [])

    def test_host_failure_or_malformed_answer_stops_without_running_the_flight(self):
        self.host_result = lambda: RuntimeError("/secret/path")
        self.stop("APIWEB_HOST_DENIED", lambda: self.act()(self.fd()))
        for bad in ({}, {"g6_evidence": {}}, {"g6_evidence": {}, "inputs": [], }, {"g6_evidence": {}, "inputs": {}, "x": 1}):
            self.host_result = lambda bad=bad: bad
            self.stop("APIWEB_HOST_INVALID", lambda: self.act()(self.fd()))
        self.assertEqual(self.calls, [])

    def test_inputs_naming_a_migrator_database_identity_or_volume_are_refused(self):
        for key in ("migrator", "alembic_cmd", "psql", "pg_dump_path", "volume_map", "identity_store"):
            self.inputs = {"clock": 1, key: object()}
            self.stop("APIWEB_HOST_INVALID", lambda: self.act()(self.fd()))
        self.assertEqual(self.calls, [])


class ApiwebChildFlight(ApiwebChildEnv):
    def test_applied_runs_the_held_flight_once_with_reentrant_locks_and_the_injected_port(self):
        self.assertEqual(self.act()(self.fd()), "APIWEB_APPLIED")
        self.assertEqual(len(self.calls), 1)
        kw = self.calls[0]
        self.assertIsInstance(kw["root_locks"], child.Reentrant)
        self.assertEqual((kw["port"], kw["g6_evidence"], kw["clock"]), ("PORT", {"e": 1}, 1))
        self.assertEqual(self.built[0][0], DATA)
        self.assertEqual(self.built[0][1], CLAIMS)
        self.assertEqual(self.rest, ({"a": 1}, {"m": 1}, "FX"))

    def test_rolled_back_or_unknown_outcome_is_never_success(self):
        self.result = {"terminal_outcome": "ROLLED_BACK"}
        self.stop("APIWEB_ROLLED_BACK", lambda: self.act()(self.fd()))
        for bad in ({"terminal_outcome": "FLIGHT_STOP"}, {}, None, "APPLIED"):
            self.result = bad
            self.stop("APIWEB_TERMINAL_UNKNOWN", lambda: self.act()(self.fd()))

    def test_flight_stop_code_passes_through_and_anything_else_is_opaque(self):
        self.result = RuntimeError("HELDFLIGHT_G6_DENIED")
        self.stop("HELDFLIGHT_G6_DENIED", lambda: self.act()(self.fd()))
        self.result = RuntimeError("/etc/secret leaked")
        self.stop("APIWEB_RUN_FAILED", lambda: self.act()(self.fd()))
        self.result = KeyboardInterrupt()
        self.stop("APIWEB_RUN_FAILED", lambda: self.act()(self.fd()))

    def test_authenticated_claims_are_copied_not_shared(self):
        claims = dict(CLAIMS)
        act = self.act(claims=claims)
        claims["purpose"] = "G6_V2_ONLY"
        self.assertEqual(act(self.fd()), "APIWEB_APPLIED")
        self.assertEqual(self.built[0][1]["purpose"], "API_WEB_QA_ONLY")


class ApiwebChildReentrantPlane(ApiwebChildEnv):
    def enter(self, outer=None, journal=None):
        rl = child.Reentrant(self.rl, lambda: outer or self.outer)
        return rl, journal or ApiwebChildJournal()

    def test_it_reuses_the_held_locks_and_takes_only_the_flight_lease(self):
        rl, journal = self.enter()
        with rl.root_flight("P", journal, "root", b"adm", "sha256:" + "1" * 64, 5) as plane:
            self.assertIsInstance(plane, ApiwebChildFakePlane)
            self.assertEqual(plane._entries, self.outer._entries)
            self.assertIs(plane.lease, journal.lease)
            self.assertEqual(journal.lease.closed, 0)
        self.assertEqual((journal.lease.closed, journal.begun[0][0]), (1, "root"))

    def test_lease_is_closed_when_the_flight_raises_and_outer_stays_held(self):
        rl, journal = self.enter()
        with self.assertRaises(ValueError):
            with rl.root_flight("P", journal, "root", b"adm", "sha256:" + "1" * 64, 5):
                raise ValueError("boom")
        self.assertEqual(journal.lease.closed, 1)
        self.outer.assert_held()

    def test_a_lost_outer_plane_or_a_foreign_plane_class_never_begins_a_lease(self):
        self.outer.held = False
        rl, journal = self.enter()
        with self.assertRaises(RuntimeError):
            with rl.root_flight("P", journal, "root", b"adm", "s", 5):
                pass
        rl2 = child.Reentrant(self.rl, lambda: object())
        with self.assertRaises(child.Stop):
            with rl2.root_flight("P", journal, "root", b"adm", "s", 5):
                pass
        self.assertEqual(journal.begun, [])

    def test_a_lease_that_cannot_begin_is_a_stop_not_a_partial_plane(self):
        rl, journal = self.enter()
        journal.begin = lambda *a: (_ for _ in ()).throw(RuntimeError("busy"))
        with self.assertRaises(RuntimeError):
            with rl.root_flight("P", journal, "root", b"adm", "s", 5):
                self.fail("must not yield")


if __name__ == "__main__":
    unittest.main()
