"""QA525 full-stack orchestrator, E2E off-host with a STATEFUL simulated stack (plan #1033, cases E1-E13).

The unit tests (test_qa525_vnext_auto.py) check calls and outcomes with a scripted fake. This file checks the
WORLD: a simulated DB (EF history + schema), containers, a persistent journal and a persistent nonce store that
survive a crash, driven only through the same host ports the real adapters must implement. The invariants are
asserted on the world state, never on the receipt alone:

  I1  APPLIED is journaled only if the world really is on the target images with the migration applied.
  I2  The migration is executed at most once across ALL runs (also after a crash and a replay).
  I3  A spent nonce is never reusable, whatever happened after it was spent.
  I4  Locks are always released, also when the process crashes.
  I5  A STOP_BEFORE_DB leaves the world byte-identical (no migration, no nonce, no recreate).
  I6  A blocked outcome never touches API/WEB again after the uncertainty.

The simulated stack is NOT physical evidence (G6 or host): it is the executable contract that the real adapters
(policy/adapters written by ChatGPT, host-side) must satisfy. Nothing here touches glmdch, main or production.
"""
import hashlib
import importlib.util
import threading
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
_spec = importlib.util.spec_from_file_location("qa525_vnext_auto_e2e_module", ROOT / "scripts/qa/qa525-vnext-auto.py")
auto = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(auto)

_pspec = importlib.util.spec_from_file_location("qa525_vnext_policy_e2e_module", ROOT / "scripts/qa/qa525-vnext-policy.py")
policy = importlib.util.module_from_spec(_pspec)
_pspec.loader.exec_module(policy)

SHA = "a" * 40
MIG = "20261118000001_add_nullable_col"
SET = "sha256:" + hashlib.sha256((MIG + "\n").encode()).hexdigest()
BASE = {"api": "sha256:" + "b" * 64, "web": "sha256:" + "c" * 64}
TARGET = {"api": "sha256:" + "3" * 64, "web": "sha256:" + "4" * 64}
RELEASE = {"sha": SHA, "nonce": "n" * 32, "expected_images": TARGET, "baseline_images": BASE}


class E2eClock:
    def __init__(self):
        self.t = 1000.0

    def __call__(self):
        return self.t

    def sleep(self, seconds):
        self.t += seconds


class E2eCrash(BaseException):
    """Process death (kill -9 / power): not an Exception, nothing in the orchestrator may swallow it."""


def additive_plan(migration=MIG, **flags):
    item = {"id": migration, "additive": True, "backward_compatible_with_api_n": True, "arbitrary_sql": False,
            "unbounded_backfill": False, "incompatible_ddl_lock": False, "needs_api_n_plus_1": False}
    item.update(flags)
    return {"ordered_set_sha256": SET, "migrations": [item]}


class SimStack:
    """The persistent world. Everything a crash must NOT erase lives here."""

    def __init__(self, clock):
        self.clock = clock
        self.counter = 0
        self.history = []                      # EF __EFMigrationsHistory
        self.columns = set()                   # schema
        self.containers = {"api": self._box("api", BASE["api"]), "web": self._box("web", BASE["web"])}
        self.journal = []                      # persistent journal states
        self.nonces = set()                    # persistent one-shot store
        self.locks_held = False
        self.lease = True
        self.migrate_runs = 0
        self.recreates = []                    # (step, which) in order, the physical-action log
        # --- fault knobs (the world, not the orchestrator) ---
        self.api_target_boots = True           # API N+1 starts only if the column exists AND this is True
        self.api_target_delay = 0.0            # seconds until ready
        self.web_target_boots = True
        self.migrate_fails_before_commit = False
        self.migrate_commits_then_raises = False
        self.g6_ok = True
        self.g6_release = SHA
        self.destructive = False
        self.preflight_ok = True
        self.supply_ok = True
        # The signed GO, INDEPENDENT of the release dict the orchestrator receives: the host authenticates it, binds the
        # pins and the nonce to it. A real harness must expose the same fields (sig_ok=False => everything fails closed).
        self.go = {"sig_ok": True, "nonce": RELEASE["nonce"], "release_sha": SHA,
                   "plan_sha256": policy.plan_digest(additive_plan())}
        self._nonce_lock = threading.Lock()     # the one-shot store is atomic across processes

    def _box(self, step, image):
        self.counter += 1
        return {"image": image, "cid": "c-%s-%d" % (step, self.counter), "started": self.clock()}

    def snapshot(self):
        return (tuple(self.history), frozenset(self.columns), tuple(sorted((k, v["image"], v["cid"]) for k, v in self.containers.items())),
                tuple(self.journal), frozenset(self.nonces), self.migrate_runs, tuple(self.recreates))

    def world_is_applied(self):
        return (self.history == [MIG] and "new_col" in self.columns
                and {k: v["image"] for k, v in self.containers.items()} == TARGET)


class SimHost:
    """Adapter over SimStack. `crash_at` = N-th port call dies the process (E2eCrash)."""

    def __init__(self, stack, crash_at=None):
        self.s = stack
        self.calls = 0
        self.crash_at = crash_at
        self.crash_label = None
        self.trace = []

    def _tick(self, label):
        self.calls += 1
        self.trace.append(label)
        if self.crash_at is not None and self.calls == self.crash_at:
            self.crash_label = label
            raise E2eCrash(label)

    # --- gates ---
    def preflight(self):
        self._tick("preflight")
        # adapter contract: a journal left PENDING (no terminal state) blocks any new attempt until reconciled
        pending = bool(self.s.journal) and self.s.journal[-1] == "PENDING"
        return self.s.preflight_ok and not pending

    def supply(self):
        self._tick("supply")
        return self.s.supply_ok

    def migration_plan(self):
        self._tick("migration_plan")
        return additive_plan(**destructive_flag(self.s))

    def plan_attested(self, plan, release):
        self._tick("plan_attested")
        # REAL policy (#1036). Pins are built independently of the host world: from what was SIGNED
        # (the additive plan, release sha and image identities), never from what the world reports now.
        go = self.s.go
        if go.get("sig_ok") is not True:        # unauthenticated pins are never trusted
            return False
        pins = {"release_sha": go["release_sha"], "plan_sha256": go["plan_sha256"],
                "baseline_images": dict(BASE), "expected_images": dict(TARGET)}
        return policy.plan_attested(plan, release, pins)

    def evidence(self):
        self._tick("evidence")
        return {"images": {k: v["image"] for k, v in self.s.containers.items()},
                "containers": {k: v["cid"] for k, v in self.s.containers.items()}}

    def g6_evidence(self):
        self._tick("g6_evidence")
        return {"isolated_restore_ok": self.s.g6_ok, "release_sha": self.s.g6_release, "expires_at": 10 ** 9}

    # --- authority ---
    def acquire_locks(self):
        self._tick("acquire_locks")
        if self.s.locks_held:
            return False
        self.s.locks_held = True
        return True

    def release_locks(self):
        self.s.locks_held = False          # no crash point: the OS drops the lock when the process dies

    def spend_nonce(self, nonce):
        self._tick("spend_nonce")
        go = self.s.go
        if go.get("sig_ok") is not True or nonce != go["nonce"]:   # only the exact authorized nonce can be spent
            return False
        with self.s._nonce_lock:
            if nonce in self.s.nonces:
                return False
            self.s.nonces.add(nonce)
            return True

    def lease_alive(self):
        self._tick("lease_alive")
        return self.s.lease

    def journal(self, state):
        self._tick("journal:" + state)
        self.s.journal.append(state)

    # --- database ---
    def ef_history_sha(self):
        self._tick("ef_history_sha")
        return "sha256:" + hashlib.sha256(("|".join(self.s.history)).encode()).hexdigest()

    def migrations_already_applied(self, set_sha):
        self._tick("migrations_already_applied")
        return self.s.history == [MIG]

    def migrate_once(self):
        self._tick("migrate_once")
        self.s.migrate_runs += 1
        if self.s.migrate_fails_before_commit:
            raise RuntimeError("migration aborted, transaction rolled back")
        self.s.columns.add("new_col")
        self.s.history.append(MIG)
        if self.s.migrate_commits_then_raises:
            raise RuntimeError("connection lost after COMMIT")

    def schema_ok(self):
        self._tick("schema_ok")
        return "new_col" in self.s.columns

    def ef_history_matches(self, set_sha):
        self._tick("ef_history_matches")
        return self.s.history == [MIG]

    # --- containers ---
    def recreate(self, step, which):
        self._tick("recreate:%s:%s" % (step, which))
        self.s.recreates.append((step, which))
        image = (TARGET if which == "target" else BASE)[step]
        self.s.containers[step] = self.s._box(step, image)

    def ready(self, step):
        self._tick("ready:" + step)
        box = self.s.containers[step]
        if box["image"] == BASE[step]:
            return True                                    # baseline always comes back healthy
        if step == "api":
            if not (self.s.api_target_boots and "new_col" in self.s.columns):
                return False                               # API N+1 needs the column
            return self.s.clock() - box["started"] >= self.s.api_target_delay
        return self.s.web_target_boots


def destructive_flag(stack):
    return {"additive": False} if stack.destructive else {}


# What a REAL adapter harness must provide to run this battery unchanged (ChatGPT #1037, 18-nov): subclass each test
# class below, override `new_world` / `new_host`, and expose the same world facade. Everything runs OFF-HOST
# (isolated PostgreSQL + Docker Compose + temp filesystem); nothing here talks to glmdch.
#
#   world.snapshot() -> comparable value of the whole persistent state (history, schema, containers, journal, nonces,
#                       migrate count, physical-action log)           [I5 world byte-identical]
#   world.history / columns / containers / journal / nonces / locks_held / lease / migrate_runs / recreates
#   world.world_is_applied()                                          [I1]
#   fault knobs: api_target_boots, api_target_delay, web_target_boots, migrate_fails_before_commit,
#                migrate_commits_then_raises, g6_ok, g6_release, destructive, preflight_ok, supply_ok
#   host(world, crash_at=N): the N-th port call raises E2eCrash (a BaseException: process death)
#   _box(step, image): a fresh container of `image` (used to reset the host to the baseline after an APPLIED run)
WORLD_API = (
    "snapshot", "world_is_applied", "history", "columns", "containers", "journal", "nonces", "locks_held", "lease",
    "migrate_runs", "recreates", "api_target_boots", "api_target_delay", "web_target_boots",
    "migrate_fails_before_commit", "migrate_commits_then_raises", "g6_ok", "g6_release", "destructive",
    "preflight_ok", "supply_ok", "go", "_box",
)
HOST_PORTS = (
    "preflight", "supply", "migration_plan", "plan_attested", "evidence", "g6_evidence", "acquire_locks",
    "release_locks", "spend_nonce", "lease_alive", "journal", "ef_history_sha", "migrations_already_applied",
    "migrate_once", "schema_ok", "ef_history_matches", "recreate", "ready",
)


class E2eBase(unittest.TestCase):
    def new_world(self, clock):
        """Override in a real-adapter harness: return an isolated world exposing WORLD_API."""
        return SimStack(clock)

    def new_host(self, world, crash_at=None):
        """Override in a real-adapter harness: return the real host adapter over `world`, exposing HOST_PORTS."""
        return SimHost(world, crash_at=crash_at)

    def setUp(self):
        self.clock = E2eClock()
        self.stack = self.new_world(self.clock)

    def go(self, host=None, release=None):
        host = host or self.new_host(self.stack)
        return auto.run(host, release or RELEASE, clock=self.clock, sleep=self.clock.sleep)

    def assert_invariants(self):
        s = self.stack
        self.assertLessEqual(s.migrate_runs, 1, "I2 migration executed more than once")
        self.assertFalse(s.locks_held, "I4 locks leaked")
        if "APPLIED" in s.journal:
            self.assertTrue(s.world_is_applied(), "I1 APPLIED without the applied world")
            self.assertEqual(s.journal[-1], "APPLIED")
        self.assertLessEqual(len(s.nonces), 1)


class Qa525AutoE2eContractSurface(E2eBase):
    """The battery's required surface is explicit: a harness that misses a member fails here first, not mid-scenario."""

    def test_world_and_host_expose_the_documented_contract_surface(self):
        world = self.new_world(E2eClock())
        host = self.new_host(world)
        self.assertEqual([m for m in WORLD_API if not hasattr(world, m)], [])
        self.assertEqual([m for m in HOST_PORTS if not callable(getattr(host, m, None))], [])

    def test_orchestrator_only_uses_the_documented_host_ports(self):
        used = set()

        class Spy(SimHost):
            def __getattribute__(self, name):
                if not name.startswith("_") and name not in ("s", "calls", "crash_at", "crash_label", "trace"):
                    used.add(name)
                return super().__getattribute__(name)

        auto.run(Spy(self.new_world(self.clock)), RELEASE, clock=self.clock, sleep=self.clock.sleep)
        self.assertEqual(sorted(used - set(HOST_PORTS)), [])


class Qa525AutoE2eHappyAndLatency(E2eBase):
    def test_e1_happy_path_world_state(self):
        r = self.go()
        self.assertEqual(r["outcome"], auto.OUT_APPLIED)
        self.assertTrue(self.stack.world_is_applied())
        self.assertEqual(self.stack.journal, ["PENDING", "APPLIED"])
        self.assertEqual(self.stack.recreates, [("api", "target"), ("web", "target")])
        self.assertEqual(r["images"], TARGET)
        self.assert_invariants()

    def test_e3_api_takes_six_seconds_no_rollback(self):
        self.stack.api_target_delay = 6.0
        r = self.go()
        self.assertEqual(r["outcome"], auto.OUT_APPLIED)
        self.assertNotIn(("api", "baseline"), self.stack.recreates)
        self.assertGreaterEqual(r["duration_s"], 6.0)
        self.assert_invariants()

    def test_api_slower_than_budget_rolls_back_not_applied(self):
        self.stack.api_target_delay = 10 ** 6
        r = self.go()
        self.assertEqual(r["outcome"], auto.OUT_ROLLED_BACK)
        self.assertNotIn("APPLIED", self.stack.journal)
        self.assert_invariants()


class Qa525AutoE2eRollback(E2eBase):
    def test_e2_api_target_does_not_boot_rolls_back_schema_stays(self):
        self.stack.api_target_boots = False
        r = self.go()
        self.assertEqual(r["outcome"], auto.OUT_ROLLED_BACK)
        self.assertEqual(r["failure_class"], "API_NOT_READY")
        s = self.stack
        self.assertEqual({k: v["image"] for k, v in s.containers.items()}, BASE)      # N back
        self.assertIn("new_col", s.columns)                                           # additive schema stays
        self.assertEqual(s.history, [MIG])
        self.assertEqual(s.journal, ["PENDING", "ROLLED_BACK"])
        self.assertEqual(s.recreates, [("api", "target"), ("api", "baseline")])       # web never touched
        self.assertEqual(s.migrate_runs, 1)
        self.assert_invariants()

    def test_web_target_does_not_boot_rolls_back_web_then_api(self):
        self.stack.web_target_boots = False
        r = self.go()
        self.assertEqual(r["outcome"], auto.OUT_ROLLED_BACK)
        self.assertEqual(self.stack.recreates[-2:], [("web", "baseline"), ("api", "baseline")])
        self.assertEqual({k: v["image"] for k, v in self.stack.containers.items()}, BASE)
        self.assert_invariants()

    def test_rolled_back_run_can_never_be_replayed_with_same_nonce(self):
        self.stack.api_target_boots = False
        self.go()
        before = self.stack.snapshot()
        r = self.go()
        self.assertEqual(r["failure_class"], "NONCE_NOT_SPENDABLE")
        self.assertEqual(self.stack.snapshot(), before)           # I3 + I5: replay changes nothing
        self.assert_invariants()


class Qa525AutoE2eStopBeforeDb(E2eBase):
    def assert_untouched(self, expect_code):
        before = self.stack.snapshot()
        r = self.go()
        self.assertEqual(r["outcome"], auto.OUT_STOP)
        self.assertEqual(r["failure_class"], expect_code)
        self.assertEqual(self.stack.snapshot(), before, "I5 STOP_BEFORE_DB must not change the world")
        self.assertEqual(self.stack.nonces, set())
        self.assert_invariants()

    def test_e4_destructive_migration(self):
        self.stack.destructive = True
        self.assert_untouched("MIGRATION_NOT_PROVEN_ADDITIVE")

    def test_e5_g6_restore_unavailable(self):
        self.stack.g6_ok = False
        self.assert_untouched("G6_UNAVAILABLE_OR_EXPIRED")

    def test_e13_g6_of_another_release(self):
        self.stack.g6_release = "b" * 40
        self.assert_untouched("G6_UNAVAILABLE_OR_EXPIRED")

    def test_preflight_and_supply_failures(self):
        self.stack.preflight_ok = False
        self.assert_untouched("PREFLIGHT_FAILED")
        self.setUp()
        self.stack.supply_ok = False
        self.assert_untouched("SUPPLY_NOT_VERIFIED")

    def test_locks_held_by_another_process_does_not_burn_nonce(self):
        self.stack.locks_held = True
        before = self.stack.snapshot()
        r = self.go()
        self.assertEqual(r["failure_class"], "LOCKS_NOT_ACQUIRED")
        self.assertEqual(self.stack.snapshot(), before)
        self.stack.locks_held = True     # owned by the other process, never by us

    def test_e8_incompatible_with_api_n_is_manual(self):
        host = self.new_host(self.stack)
        host.migration_plan = lambda: additive_plan(backward_compatible_with_api_n=False)
        before = self.stack.snapshot()
        r = self.go(host)
        self.assertEqual(r["failure_class"], "MIGRATION_NOT_PROVEN_ADDITIVE")
        self.assertEqual(self.stack.snapshot(), before)


class Qa525AutoE2eDatabaseUncertainty(E2eBase):
    def test_e7_replay_after_applied_changes_nothing(self):
        self.go()
        # the host is put back on the baseline images but the DB keeps the migration (operator rollback of containers)
        self.stack.containers = {"api": self.stack._box("api", BASE["api"]), "web": self.stack._box("web", BASE["web"])}
        before = self.stack.snapshot()
        r = self.go()
        self.assertEqual(r["failure_class"], "NONCE_NOT_SPENDABLE")
        self.assertEqual(self.stack.snapshot(), before)
        self.assertEqual(self.stack.migrate_runs, 1)

    def test_e9_migration_already_applied_not_rerun_and_stack_updates(self):
        self.stack.columns.add("new_col")
        self.stack.history.append(MIG)
        r = self.go()
        self.assertEqual(r["outcome"], auto.OUT_APPLIED)
        self.assertEqual(self.stack.migrate_runs, 0)
        self.assertTrue(self.stack.world_is_applied())
        self.assert_invariants()

    def test_e10_migration_aborts_before_commit_blocks_without_rerun_or_recreate(self):
        self.stack.migrate_fails_before_commit = True
        r = self.go()
        s = self.stack
        self.assertEqual(r["outcome"], auto.OUT_BLOCKED)
        self.assertEqual(r["failure_class"], "MIGRATION_FAILED_NO_RERUN")
        self.assertEqual(s.recreates, [])                       # I6: API/WEB untouched
        self.assertEqual(s.migrate_runs, 1)
        self.assertEqual(s.journal[0], "PENDING")
        self.assertNotIn("APPLIED", s.journal)
        # a replay can neither re-run the migration nor reuse the nonce
        r2 = self.go()
        self.assertEqual(r2["outcome"], auto.OUT_STOP)
        self.assertEqual(s.migrate_runs, 1)
        self.assert_invariants()

    def test_e10b_connection_lost_after_commit_blocks_and_never_reruns(self):
        self.stack.migrate_commits_then_raises = True
        r = self.go()
        self.assertEqual(r["outcome"], auto.OUT_BLOCKED)
        self.assertEqual(self.stack.history, [MIG])             # the commit really happened
        self.assertEqual(self.stack.recreates, [])
        self.assertEqual(self.stack.migrate_runs, 1)
        self.assert_invariants()

    def test_e11_lease_lost_after_migration_blocks_before_api_web(self):
        host = self.new_host(self.stack)
        real = host.migrate_once

        def migrate_then_lose_lease():
            real()
            self.stack.lease = False

        host.migrate_once = migrate_then_lose_lease
        r = self.go(host)
        self.assertEqual(r["outcome"], auto.OUT_BLOCKED)
        self.assertEqual(r["failure_class"], "LEASE_LOST_AFTER_MIGRATION")
        self.assertEqual(self.stack.recreates, [])
        self.assertEqual({k: v["image"] for k, v in self.stack.containers.items()}, BASE)
        self.assert_invariants()

    def test_lease_lost_during_api_stops_all_physical_action(self):
        host = self.new_host(self.stack)
        real = host.recreate

        def recreate_then_lose_lease(step, which):
            real(step, which)
            self.stack.lease = False

        host.recreate = recreate_then_lose_lease
        r = self.go(host)
        self.assertEqual(r["outcome"], auto.OUT_BLOCKED)
        self.assertEqual(self.stack.recreates, [("api", "target")])    # no web, no rollback without authority
        self.assert_invariants()


class Qa525AutoE2eCrashSweep(E2eBase):
    """E6 + E12 generalized: kill the process at EVERY port call of the happy path and replay."""

    def happy_call_count(self):
        probe = self.new_world(E2eClock())
        host = self.new_host(probe)
        auto.run(host, RELEASE, clock=probe.clock, sleep=probe.clock.sleep)
        return host.calls

    def test_crash_at_every_port_call_keeps_all_invariants(self):
        total = self.happy_call_count()
        self.assertGreater(total, 20)
        crashed = 0
        for point in range(1, total + 1):
            with self.subTest(crash_at=point):
                self.setUp()
                host = self.new_host(self.stack, crash_at=point)
                try:
                    auto.run(host, RELEASE, clock=self.clock, sleep=self.clock.sleep)
                    self.fail("crash point %d was never reached" % point)
                except E2eCrash:
                    crashed += 1
                s = self.stack
                self.assert_invariants()
                self.assertNotEqual(s.journal[-1:] , ["APPLIED"], "a crash can never leave APPLIED unless it was written")
                nonce_spent = bool(s.nonces)
                migrated_before = s.migrate_runs
                # replay with the SAME release after the process restarts (locks were dropped by the OS)
                s.locks_held = False
                r = auto.run(self.new_host(s), RELEASE, clock=self.clock, sleep=self.clock.sleep)
                self.assertLessEqual(s.migrate_runs, 1, "I2 across crash + replay")
                if nonce_spent:
                    self.assertNotEqual(r["outcome"], auto.OUT_APPLIED, "I3 spent nonce can never finish an APPLIED")
                    self.assertEqual(s.migrate_runs, migrated_before, "replay must not touch the DB after the nonce was spent")
                self.assert_invariants()
        self.assertEqual(crashed, total)

    def test_e12_crash_right_after_journal_pending_blocks_a_fresh_nonce_too(self):
        host = self.new_host(self.stack)
        def crash_on_first_history_read():
            raise E2eCrash("after journal PENDING")

        host.ef_history_sha = crash_on_first_history_read
        with self.assertRaises(E2eCrash):
            self.go(host)
        s = self.stack
        self.assertEqual(s.journal, ["PENDING"])
        self.assertEqual(s.migrate_runs, 0)
        self.assertEqual(s.recreates, [])
        fresh = dict(RELEASE, nonce="m" * 32)
        r = self.go(release=fresh)
        # adapter contract (preflight refuses a journal stuck in PENDING): needs manual EF-history reconciliation
        self.assertEqual(r["outcome"], auto.OUT_STOP)
        self.assertEqual(r["failure_class"], "PREFLIGHT_FAILED")
        self.assertEqual(s.migrate_runs, 0)
        self.assertNotIn("APPLIED", s.journal)

    def test_e6_crash_after_api_recreate_never_reports_active(self):
        host = self.new_host(self.stack)
        def crash_while_waiting_for_api(step):
            raise E2eCrash("kill during API wait")

        host.ready = crash_while_waiting_for_api
        with self.assertRaises(E2eCrash):
            self.go(host)
        s = self.stack
        self.assertEqual(s.journal, ["PENDING"])
        self.assertFalse(s.locks_held)
        self.assertEqual(s.history, [MIG])                 # DB committed: replay must never migrate again
        # replay: the journal stuck in PENDING stops it at preflight (adapter contract); even if that gate
        # were bypassed, the spent nonce still rejects it
        r = self.go()
        self.assertEqual(r["failure_class"], "PREFLIGHT_FAILED")
        s.journal.append("RECONCILED_BY_OPERATOR")
        before = s.snapshot()
        r = self.go()
        # the API container is already on the target image, so the world no longer matches the signed baseline:
        # the replay stops before locks/nonce/DB (and the nonce could not be reused anyway)
        self.assertEqual(r["outcome"], auto.OUT_STOP)
        self.assertEqual(r["failure_class"], "BASELINE_EVIDENCE_INVALID")
        self.assertEqual(s.snapshot(), before)
        self.assertEqual(s.migrate_runs, 1)


class Qa525AutoE2eGoAuthorization(E2eBase):
    """Independent signed GO: exact nonce, bound pins, one-shot across restarts and processes, fail-closed when unsigned."""

    def stop_untouched(self, release=None):
        before = self.stack.snapshot()
        r = self.go(release=release)
        self.assertEqual(r["outcome"], auto.OUT_STOP)
        self.assertEqual(self.stack.snapshot(), before)
        self.assertEqual(self.stack.nonces, set())
        self.assert_invariants()
        return r

    def test_g1_changed_nonce_is_not_spendable_and_burns_nothing(self):
        r = self.stop_untouched(dict(RELEASE, nonce="x" * 32))
        self.assertEqual(r["failure_class"], "NONCE_NOT_SPENDABLE")

    def test_g2_unsigned_go_fails_closed_before_any_effect(self):
        self.stack.go["sig_ok"] = False
        self.stop_untouched()

    def test_g3_go_bound_to_another_release_or_plan_is_not_attested(self):
        for key, value in (("release_sha", "b" * 40), ("plan_sha256", "sha256:" + "0" * 64)):
            self.stack = self.new_world(self.clock)
            self.stack.go[key] = value
            self.stop_untouched()

    def test_g4_nonce_is_one_shot_after_success_and_after_restart(self):
        r = self.go()
        self.assertEqual(r["outcome"], auto.OUT_APPLIED)
        self.assertEqual(self.stack.nonces, {RELEASE["nonce"]})
        for _ in range(2):                                       # a "restarted" process: fresh host over the same world
            again = self.go(host=self.new_host(self.stack))
            self.assertNotEqual(again["outcome"], auto.OUT_APPLIED)
        self.assertEqual(self.stack.migrate_runs, 1)
        self.assert_invariants()

    def test_g5_two_concurrent_processes_spend_the_nonce_exactly_once(self):
        results, barrier = [], threading.Barrier(8)

        def worker():
            host = self.new_host(self.stack)
            barrier.wait()
            results.append(host.spend_nonce(RELEASE["nonce"]))

        threads = [threading.Thread(target=worker) for _ in range(8)]
        [t.start() for t in threads]
        [t.join() for t in threads]
        self.assertEqual(results.count(True), 1)
        self.assertEqual(self.stack.nonces, {RELEASE["nonce"]})

    def test_g6_crash_after_spend_before_any_mutation_leaves_nonce_spent_and_blocks_replay(self):
        probe = self.new_host(self.new_world(E2eClock()))
        auto.run(probe, RELEASE, clock=self.clock, sleep=self.clock.sleep)
        crash_at = probe.trace.index("journal:PENDING") + 1
        self.assertLess(probe.trace.index("spend_nonce"), crash_at - 1)
        host = self.new_host(self.stack, crash_at=crash_at)
        with self.assertRaises(E2eCrash):
            auto.run(host, RELEASE, clock=self.clock, sleep=self.clock.sleep)
        self.assertEqual(self.stack.nonces, {RELEASE["nonce"]})
        self.assertEqual((self.stack.migrate_runs, self.stack.history, self.stack.columns), (0, [], set()))
        self.stack.locks_held = False                            # the OS drops the lock with the process
        again = self.go()
        self.assertNotEqual(again["outcome"], auto.OUT_APPLIED)  # spent nonce / PENDING journal: never a blind rerun
        self.assertEqual(self.stack.migrate_runs, 0)


class Qa525AutoE2eRealPolicyBridge(E2eBase):
    """The REAL #1036 policy behind host.plan_attested, with pins independent of the host world."""

    def _pins(self):
        return {"release_sha": SHA, "plan_sha256": policy.plan_digest(additive_plan()),
                "baseline_images": dict(BASE), "expected_images": dict(TARGET)}

    def test_exact_plan_release_and_pins_are_attested(self):
        self.assertIs(policy.plan_attested(additive_plan(), dict(RELEASE), self._pins()), True)

    def test_real_migration_id_of_workshop_registry_is_attested(self):
        mig = "20261118150000_AddWorkshopRegistryState"
        plan = additive_plan(mig)
        plan["ordered_set_sha256"] = "sha256:" + hashlib.sha256((mig + "\n").encode()).hexdigest()
        pins = self._pins()
        pins["plan_sha256"] = policy.plan_digest(plan)
        self.assertIs(policy.plan_attested(plan, dict(RELEASE), pins), True)

    def test_altered_plan_foreign_pin_and_other_release_are_not_attested(self):
        self.assertIsNot(policy.plan_attested(additive_plan(arbitrary_sql=True), dict(RELEASE), self._pins()), True)
        pins = self._pins()
        pins["plan_sha256"] = "sha256:" + "0" * 64
        self.assertIsNot(policy.plan_attested(additive_plan(), dict(RELEASE), pins), True)
        self.assertIsNot(policy.plan_attested(additive_plan(), dict(RELEASE, sha="b" * 40), self._pins()), True)
        self.assertIsNot(policy.plan_attested(additive_plan(), dict(RELEASE, expected_images={"api": "sha256:" + "9" * 64, "web": TARGET["web"]}), self._pins()), True)

    def test_trailing_newline_in_migration_id_is_never_additive(self):
        plan = additive_plan(MIG + "\n")
        # consistent set hash, so ONLY the id check can reject it
        plan["ordered_set_sha256"] = "sha256:" + hashlib.sha256(((MIG + "\n") + "\n").encode()).hexdigest()
        self.assertEqual(auto.classify(plan), "MANUAL")


if __name__ == "__main__":
    unittest.main()
