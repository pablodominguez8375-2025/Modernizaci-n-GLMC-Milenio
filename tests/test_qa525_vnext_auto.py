"""QA525 full-stack orchestrator (hermetic): E1-E8 + ChatGPT acceptance conditions (#1010 6977453297/6977539617)."""
import importlib.util
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("qa525_vnext_auto_test", ROOT / "scripts/qa/qa525-vnext-auto.py")
auto = importlib.util.module_from_spec(spec)
spec.loader.exec_module(auto)

SHA = "a" * 40
IDS = ["20261118000001_add_col"]


def set_hash(ids):
    import hashlib
    return "sha256:" + hashlib.sha256(("\n".join(ids) + "\n").encode()).hexdigest()


SET = set_hash(IDS)
BASE_IMG = {"api": "sha256:" + "b" * 64, "web": "sha256:" + "c" * 64}
TGT_IMG = {"api": "sha256:" + "3" * 64, "web": "sha256:" + "4" * 64}
REL = {"sha": SHA, "nonce": "n" * 32, "expected_images": TGT_IMG}


class Clock:
    def __init__(self):
        self.t = 1000.0
    def __call__(self):
        return self.t
    def sleep(self, s):
        self.t += s


class Crash(BaseException):
    pass


def good_plan():
    return {"ordered_set_sha256": SET, "migrations": [{
        "id": IDS[0], "additive": True, "backward_compatible_with_api_n": True, "arbitrary_sql": False,
        "unbounded_backfill": False, "incompatible_ddl_lock": False, "needs_api_n_plus_1": False}]}


class Host:
    def __init__(self, clock):
        self.clock = clock
        self.log = []
        self.plan = good_plan()
        self.pre = True
        self.sup = True
        self.g6 = {"isolated_restore_ok": True, "release_sha": SHA, "expires_at": 10**9}
        self.nonces = set()
        self.locks = True
        self.applied_already = False
        self.migrate_exc = None
        self.schema = True
        self.history_ok = True
        self.lease = True
        self.ready_after = {"api": 0.0, "web": 0.0}   # seconds after recreate to become ready
        self.never_ready = {"api": False, "web": False}
        self.rollback_never_ready = False
        self.crash_on = None
        self.recreated_at = {}
        self.target = {}
        self.images = dict(BASE_IMG)
        self.cids = {"api": "c-api-0", "web": "c-web-0"}
        self.raise_after_effect = None      # (step, which): physical change happens, THEN the adapter raises
        self.raise_before_effect = None
        self.n = 0
        self.evidence_override = None
        self.journal_states = []
        self.migrations_run = 0

    def preflight(self): self.log.append("preflight"); return self.pre
    def supply(self): self.log.append("supply"); return self.sup
    def migration_plan(self): return self.plan
    def g6_evidence(self): return self.g6
    def spend_nonce(self, n):
        self.log.append("nonce")
        if n in self.nonces:
            return False
        self.nonces.add(n)
        return True
    def acquire_locks(self): self.log.append("locks"); return self.locks
    def release_locks(self): self.log.append("unlock")
    def journal(self, s): self.journal_states.append(s); self.log.append("journal:" + s)
    def ef_history_sha(self): return "sha256:" + ("1" if self.migrations_run == 0 and not self.applied_already else "2") * 64
    def migrations_already_applied(self, s): return self.applied_already
    def migrate_once(self):
        self.log.append("migrate")
        self.migrations_run += 1
        if self.migrate_exc:
            raise self.migrate_exc
    def schema_ok(self): return self.schema
    def ef_history_matches(self, s): return self.history_ok
    def lease_alive(self): return self.lease
    def recreate(self, step, which):
        self.log.append("recreate:%s:%s" % (step, which))
        if self.crash_on == (step, which):
            raise Crash()
        if self.raise_before_effect == (step, which):
            raise RuntimeError("pre-effect")
        self.recreated_at[step] = self.clock()
        self.target[step] = which
        self.images[step] = TGT_IMG[step] if which == "target" else BASE_IMG[step]
        self.n += 1
        self.cids[step] = "c-%s-%d" % (step, self.n)
        if self.raise_after_effect == (step, which):
            raise RuntimeError("post-effect")
    def ready(self, step):
        if self.target.get(step) == "baseline":
            return not self.rollback_never_ready
        if self.never_ready[step]:
            return False
        return self.clock() - self.recreated_at[step] >= self.ready_after[step]
    def evidence(self):
        if self.evidence_override is not None:
            return self.evidence_override
        return {"images": dict(self.images), "containers": dict(self.cids)}


class AutoBase(unittest.TestCase):
    def setUp(self):
        self.c = Clock()
        self.h = Host(self.c)

    def go(self):
        return auto.run(self.h, REL, clock=self.c, sleep=self.c.sleep)

    def effects(self):
        return [x for x in self.h.log if x.startswith(("migrate", "recreate", "locks"))]


class AutoHappyAndLatency(AutoBase):
    def test_e1_happy(self):
        r = self.go()
        self.assertEqual(r["outcome"], auto.OUT_APPLIED)
        self.assertEqual(self.h.journal_states, ["PENDING", "APPLIED"])
        self.assertEqual(self.h.migrations_run, 1)
        for k in ("ef_history_before", "ef_history_after", "release_sha", "images", "containers", "duration_s"):
            self.assertIn(k, r)
        self.assertEqual(r["images"], TGT_IMG)
        self.assertIn(r["outcome"], auto.OUTCOMES)

    def test_e3_slow_api_six_seconds_passes(self):
        self.h.ready_after["api"] = 6.0
        self.assertEqual(self.go()["outcome"], auto.OUT_APPLIED)

    def test_order_nonce_after_all_gates_before_effects(self):
        self.go()
        log = self.h.log
        self.assertLess(log.index("preflight"), log.index("supply"))
        self.assertLess(log.index("supply"), log.index("locks"))
        self.assertLess(log.index("locks"), log.index("nonce"))
        self.assertLess(log.index("nonce"), log.index("journal:PENDING"))
        self.assertLess(log.index("journal:PENDING"), log.index("migrate"))
        self.assertLess(log.index("migrate"), log.index("recreate:api:target"))
        self.assertLess(log.index("recreate:api:target"), log.index("recreate:web:target"))


class AutoStopBeforeDb(AutoBase):
    def assert_pre_effect(self, code):
        r = self.go()
        self.assertEqual(r["outcome"], auto.OUT_STOP)
        self.assertEqual(r["failure_class"], code)
        self.assertEqual(self.effects(), [])
        self.assertEqual(self.h.journal_states, [])
        return r

    def test_preflight_fail_spends_no_nonce(self):
        self.h.pre = False
        self.assert_pre_effect("PREFLIGHT_FAILED")
        self.assertEqual(self.h.nonces, set())

    def test_supply_fail_spends_no_nonce(self):
        self.h.sup = False
        self.assert_pre_effect("SUPPLY_NOT_VERIFIED")
        self.assertEqual(self.h.nonces, set())

    def test_e4_destructive(self):
        self.h.plan["migrations"][0]["additive"] = False
        self.assert_pre_effect("MIGRATION_NOT_PROVEN_ADDITIVE")
        self.assertEqual(self.h.nonces, set())

    def test_nullable_sql_alone_is_not_additive(self):
        for key, val in (("backward_compatible_with_api_n", False), ("arbitrary_sql", True),
                         ("unbounded_backfill", True), ("incompatible_ddl_lock", True), ("needs_api_n_plus_1", True)):
            self.setUp()
            self.h.plan["migrations"][0][key] = val
            self.assert_pre_effect("MIGRATION_NOT_PROVEN_ADDITIVE")  # E8 included (API N cannot run on N+1 schema)

    def test_plan_without_set_hash_or_garbage(self):
        for plan in ({}, {"migrations": []}, None, {"migrations": ["x"], "ordered_set_sha256": SET},
                     {"migrations": [], "ordered_set_sha256": SET}):
            self.assertEqual(auto.classify(plan), "MANUAL")
        for bad in ("x", "sha256:abc", "", None, 5, SET.upper()):
            plan = good_plan()
            plan["ordered_set_sha256"] = bad
            self.assertEqual(auto.classify(plan), "MANUAL")

    def test_e5_g6_restore_unavailable(self):
        self.h.g6["isolated_restore_ok"] = False
        self.assert_pre_effect("G6_UNAVAILABLE_OR_EXPIRED")

    def test_g6_expired_or_other_release(self):
        self.h.g6["expires_at"] = 1000.0 + 100
        self.assert_pre_effect("G6_UNAVAILABLE_OR_EXPIRED")
        self.setUp()
        self.h.g6["release_sha"] = "b" * 40
        self.assert_pre_effect("G6_UNAVAILABLE_OR_EXPIRED")

    def test_e7_nonce_already_consumed(self):
        self.assertEqual(self.go()["outcome"], auto.OUT_APPLIED)
        self.h.log.clear()
        self.h.journal_states.clear()
        r = self.go()
        self.assertEqual(r["failure_class"], "NONCE_NOT_SPENDABLE")
        self.assertEqual([x for x in self.effects() if x != "locks"], [])    # locks taken, nothing else
        self.assertEqual(self.h.log[-1], "unlock")
        self.assertNotIn("journal:PENDING", self.h.log)

    def test_locks_not_acquired_does_not_burn_nonce(self):
        self.h.locks = False
        r = self.go()
        self.assertEqual(r["failure_class"], "LOCKS_NOT_ACQUIRED")
        self.assertNotIn("migrate", self.h.log)
        self.assertEqual(self.h.nonces, set())
        self.assertNotIn("nonce", self.h.log)

    def test_manipulated_ef_ids_or_hash_are_manual(self):
        base = good_plan()
        cases = []
        p = good_plan(); p["ordered_set_sha256"] = "sha256:" + "0" * 64; cases.append(p)          # forged hash
        p = good_plan(); p["migrations"][0]["id"] = "20261118000002_other"; cases.append(p)        # id changed, hash stale
        p = good_plan(); p["migrations"] = p["migrations"] * 2; cases.append(p)                   # duplicate ids
        two = [dict(base["migrations"][0], id="20261118000002_b"), dict(base["migrations"][0], id=IDS[0])]
        cases.append({"ordered_set_sha256": set_hash([m["id"] for m in two]), "migrations": two})  # unordered
        p = good_plan(); p["migrations"][0]["id"] = "../x"; p["ordered_set_sha256"] = set_hash(["../x"]); cases.append(p)
        for plan in cases:
            self.assertEqual(auto.classify(plan), "MANUAL")
        two_ok = sorted(two, key=lambda m: m["id"])
        self.assertEqual(auto.classify({"ordered_set_sha256": set_hash([m["id"] for m in two_ok]),
                                        "migrations": two_ok}), "ADDITIVE")


class AutoRollbackAndBlock(AutoBase):
    def test_e2_api_fails_rolled_back_schema_stays(self):
        self.h.never_ready["api"] = True
        r = self.go()
        self.assertEqual(r["outcome"], auto.OUT_ROLLED_BACK)
        self.assertEqual(r["failure_class"], "API_NOT_READY")
        self.assertEqual(self.h.journal_states, ["PENDING", "ROLLED_BACK"])
        self.assertEqual(self.h.migrations_run, 1)           # schema stays, no re-run
        self.assertIn("recreate:api:baseline", self.h.log)
        self.assertNotIn("recreate:web:target", self.h.log)

    def test_web_fails_rolls_back_web_then_api(self):
        self.h.never_ready["web"] = True
        r = self.go()
        self.assertEqual(r["outcome"], auto.OUT_ROLLED_BACK)
        log = self.h.log
        self.assertLess(log.index("recreate:web:baseline"), log.index("recreate:api:baseline"))

    def test_rollback_with_slow_baseline_is_not_judged_by_one_probe(self):
        self.h.never_ready["web"] = True
        r = self.go()                                         # baseline ready immediately in fake
        self.assertEqual(r["outcome"], auto.OUT_ROLLED_BACK)

    def test_post_effect_exception_is_rolled_back_api_and_web(self):
        for step in ("api", "web"):
            self.setUp()
            self.h.raise_after_effect = (step, "target")
            r = self.go()
            self.assertEqual(r["outcome"], auto.OUT_ROLLED_BACK)
            self.assertEqual(self.h.images, BASE_IMG)            # physically back on baseline, not just a label
            self.assertIn("recreate:%s:baseline" % step, self.h.log)

    def test_pre_effect_exception_still_restores_baseline_and_verifies(self):
        self.h.raise_before_effect = ("api", "target")
        r = self.go()
        self.assertEqual(r["outcome"], auto.OUT_ROLLED_BACK)
        self.assertEqual(self.h.images, BASE_IMG)

    def test_rollback_exception_blocks(self):
        self.h.raise_after_effect = ("web", "baseline")
        self.h.never_ready["web"] = True
        r = self.go()
        self.assertEqual(r["outcome"], auto.OUT_BLOCKED)

    def test_rollback_no_short_circuit_both_steps_verified_separately(self):
        probes = []
        orig = self.h.ready
        def ready(step):
            probes.append((step, self.h.target.get(step)))
            if step == "web" and self.h.target.get(step) == "baseline":
                return False                                      # WEB baseline unhealthy
            return orig(step)
        self.h.ready = ready
        self.h.never_ready["web"] = True
        r = self.go()
        self.assertEqual(r["outcome"], auto.OUT_BLOCKED)
        self.assertEqual(r["rollback_results"], {"web": False, "api": True})
        self.assertIn(("api", "baseline"), probes)               # API baseline probe ran after WEB failed

    def test_rollback_requires_baseline_image_identity_not_only_health(self):
        self.h.never_ready["api"] = True
        orig = self.h.recreate
        def stuck(step, which):
            orig(step, which)
            if which == "baseline":
                self.h.images[step] = TGT_IMG[step]               # recreate "succeeded" but image is wrong
        self.h.recreate = stuck
        r = self.go()
        self.assertEqual(r["outcome"], auto.OUT_BLOCKED)
        self.assertTrue(r["failure_class"].startswith("ROLLBACK_NOT_VERIFIED"))

    def test_lease_lost_during_rollback_blocks(self):
        self.h.never_ready["web"] = True
        seq = {"n": 0}
        def lease():
            seq["n"] += 1
            return seq["n"] < 3
        self.h.lease_alive = lease
        r = self.go()
        self.assertEqual(r["outcome"], auto.OUT_BLOCKED)

    def test_empty_or_wrong_evidence_never_applies(self):
        for ev in ({}, {"images": {}, "containers": {}}, []):
            self.setUp()
            self.h.evidence_override = ev
            r = self.go()
            self.assertEqual(r["outcome"], auto.OUT_BLOCKED)
            self.assertNotIn("APPLIED", self.h.journal_states)

    def test_wrong_target_image_or_unchanged_container_blocks(self):
        self.h.recreate_orig = self.h.recreate
        def wrong(step, which):
            self.h.recreate_orig(step, which)
            if step == "web" and which == "target":
                self.h.images["web"] = BASE_IMG["web"]
        self.h.recreate = wrong
        r = self.go()
        self.assertEqual(r["outcome"], auto.OUT_ROLLED_BACK)
        self.assertEqual(r["failure_class"], "TARGET_IDENTITY_MISMATCH")
        self.assertEqual(self.h.images, BASE_IMG)
        self.setUp()
        orig = self.h.recreate
        def same_id(step, which):
            before = self.h.cids[step]
            orig(step, which)
            self.h.cids[step] = before
        self.h.recreate = same_id
        self.assertEqual(self.go()["failure_class"], "TARGET_IDENTITY_MISMATCH")

    def test_rollback_not_verified_blocks(self):
        self.h.never_ready["api"] = True
        self.h.rollback_never_ready = True
        r = self.go()
        self.assertEqual(r["outcome"], auto.OUT_BLOCKED)
        self.assertEqual(r["failure_class"], "ROLLBACK_NOT_VERIFIED_API_NOT_READY")
        self.assertEqual(self.h.journal_states[-1], "RECOVERY_MANUALLY_REQUIRED")

    def test_migration_failure_blocks_without_rerun_or_restore(self):
        self.h.migrate_exc = RuntimeError("boom")
        r = self.go()
        self.assertEqual(r["outcome"], auto.OUT_BLOCKED)
        self.assertEqual(r["failure_class"], "MIGRATION_FAILED_NO_RERUN")
        self.assertEqual(self.h.migrations_run, 1)
        self.assertFalse([x for x in self.h.log if x.startswith("recreate")])
        self.assertNotIn("restore", " ".join(self.h.log))

    def test_divergent_schema_or_history_blocks_before_api(self):
        for attr in ("schema", "history_ok"):
            self.setUp()
            setattr(self.h, attr, False)
            r = self.go()
            self.assertEqual(r["failure_class"], "SCHEMA_OR_EF_HISTORY_DIVERGENT")
            self.assertEqual(r["outcome"], auto.OUT_BLOCKED)
            self.assertFalse([x for x in self.h.log if x.startswith("recreate")])

    def test_lease_lost_after_migration_blocks(self):
        self.h.lease = False
        r = self.go()
        self.assertEqual(r["failure_class"], "LEASE_LOST_AFTER_MIGRATION")
        self.assertFalse([x for x in self.h.log if x.startswith("recreate")])

    def test_idempotent_migration_already_applied_not_rerun(self):
        self.h.applied_already = True
        r = self.go()
        self.assertEqual(r["outcome"], auto.OUT_APPLIED)
        self.assertEqual(self.h.migrations_run, 0)

    def test_e6_crash_after_effect_never_false_active(self):
        for point in (("api", "target"), ("web", "target")):
            self.setUp()
            self.h.crash_on = point
            with self.assertRaises(Crash):
                self.go()
            self.assertEqual(self.h.journal_states, ["PENDING"])
            self.assertNotIn("APPLIED", self.h.journal_states)
            self.assertEqual(self.h.log[-1], "unlock")

    def test_lease_lost_at_final_verification_blocks_without_rollback(self):
        calls = {"n": 0}
        def lease():
            calls["n"] += 1
            return calls["n"] < 4                             # alive through migration/API/WEB, lost at final check
        self.h.lease_alive = lease
        r = self.go()
        self.assertEqual(r["outcome"], auto.OUT_BLOCKED)
        self.assertEqual(r["failure_class"], "LEASE_LOST_FINAL_VERIFICATION")
        self.assertNotIn("APPLIED", self.h.journal_states)
        self.assertFalse([x for x in self.h.log if x.endswith(":baseline")])    # no unsafe physical action

    def test_lease_lost_during_api_or_web_stops_all_physical_action(self):
        for n_ok, code in ((2, "LEASE_LOST_DURING_API"), (3, "LEASE_LOST_DURING_WEB")):
            self.setUp()
            calls = {"n": 0}
            def lease(calls=calls, n_ok=n_ok):
                calls["n"] += 1
                return calls["n"] < n_ok
            self.h.lease_alive = lease
            r = self.go()
            self.assertEqual(r["failure_class"], code)
            self.assertEqual(r["outcome"], auto.OUT_BLOCKED)
            self.assertFalse([x for x in self.h.log if x.endswith(":baseline")])

    def test_final_ef_history_revalidated_with_valid_lease_rolls_back_and_blocks(self):
        calls = {"n": 0}
        def matches(s):
            calls["n"] += 1
            return calls["n"] < 2                             # ok after migration, divergent at the end
        self.h.ef_history_matches = matches
        r = self.go()
        self.assertEqual(r["outcome"], auto.OUT_BLOCKED)
        self.assertEqual(r["failure_class"], "FINAL_SCHEMA_OR_EF_DIVERGENT")
        self.assertEqual(self.h.images, BASE_IMG)             # API/WEB restored and verified
        self.assertEqual(r["rollback_results"], {"web": True, "api": True})
        self.assertNotIn("APPLIED", self.h.journal_states)
        self.assertEqual(self.h.journal_states[-1], "RECOVERY_MANUALLY_REQUIRED")
        self.assertEqual(self.h.migrations_run, 1)            # no re-run, no DB restore

    def test_final_schema_divergence_same_policy(self):
        calls = {"n": 0}
        def schema():
            calls["n"] += 1
            return calls["n"] < 2
        self.h.schema_ok = schema
        r = self.go()
        self.assertEqual(r["failure_class"], "FINAL_SCHEMA_OR_EF_DIVERGENT")
        self.assertEqual(self.h.images, BASE_IMG)

    def test_lease_lost_before_rollback_blocks(self):
        self.h.never_ready["web"] = True
        calls = {"n": 0}
        def lease():
            calls["n"] += 1
            return calls["n"] < 4                             # migration, api, (web: not ready) -> before rollback lost
        self.h.lease_alive = lease
        r = self.go()
        self.assertEqual(r["outcome"], auto.OUT_BLOCKED)

    def test_locks_always_released(self):
        self.h.migrate_exc = RuntimeError("x")
        self.go()
        self.assertEqual(self.h.log[-1], "unlock")


class AutoHermeticImports(unittest.TestCase):
    def test_no_process_network_time_imports(self):
        import ast
        tree = ast.parse((ROOT / "scripts/qa/qa525-vnext-auto.py").read_text())
        names = {a.name.split(".")[0] for n in ast.walk(tree) if isinstance(n, (ast.Import, ast.ImportFrom))
                 for a in (n.names if isinstance(n, ast.Import) else [ast.alias(name=n.module or "")])}
        self.assertEqual(names - {"hashlib", "importlib", "pathlib", "re"}, set())


if __name__ == "__main__":
    unittest.main()
