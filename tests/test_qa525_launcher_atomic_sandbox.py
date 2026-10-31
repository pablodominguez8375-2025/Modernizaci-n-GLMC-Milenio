"""Hermetic crash-safety tests: no QA host, Docker, signature bypass or deploy."""
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import signal
import subprocess
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "scripts/qa/operators/qa525-launcher-atomic-sandbox.py"
spec = importlib.util.spec_from_file_location("qa525_atomic_sandbox", SOURCE)
swap = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = swap
spec.loader.exec_module(swap)

OLD = b"#!/usr/bin/python3\nprint('old trusted launcher')\n"
NEW = b"#!/usr/bin/python3\nprint('new trusted launcher')\n"
OLD_SHA = hashlib.sha256(OLD).hexdigest()
NEW_SHA = hashlib.sha256(NEW).hexdigest()
NONCE = "f" * 64
SOURCE_SHA = "a" * 40
DIGEST = "sha256:" + "b" * 64


class AtomicSandboxTests(unittest.TestCase):
    def setUp(self):
        self.home = tempfile.TemporaryDirectory(prefix=swap.TEST_PREFIX)
        self.addCleanup(self.home.cleanup)
        self.base = Path(self.home.name)
        for name in ("live", "signed", "state"):
            path = self.base / name
            path.mkdir(mode=0o700)
            path.chmod(0o700)
        self.live = self.base / swap.LIVE_REL
        self.candidate = self.base / swap.SIGNED_REL
        self.live.write_bytes(OLD)
        self.candidate.write_bytes(NEW)
        self.live.chmod(0o700)
        self.candidate.chmod(0o700)

    def execute(self, *, checkpoint=lambda phase: None, **change):
        args = dict(source_sha=SOURCE_SHA, digest=DIGEST, nonce=NONCE,
                    old_hash=OLD_SHA, new_hash=NEW_SHA, checkpoint=checkpoint)
        args.update(change)
        return swap.simulate(self.base, **args)

    def status(self):
        return swap.inspect(self.base, NONCE, old_hash=OLD_SHA, new_hash=NEW_SHA)

    def test_normal_sandbox_cut_commits_with_durable_backup_and_receipt(self):
        seen = []
        result = self.execute(checkpoint=seen.append)
        self.assertEqual("SIMULATED_LAUNCHER_COMMITTED_NO_DEPLOYMENT",
                         result["status"])
        self.assertEqual(NEW, self.live.read_bytes())
        self.assertEqual(OLD, (self.base / "state" / ("old-" + NONCE + ".py")).read_bytes())
        receipt = json.loads((self.base / "state" / ("receipt-" + NONCE + ".json")).read_text())
        self.assertEqual(NEW_SHA, receipt["new_launcher_sha256"])
        self.assertEqual(OLD_SHA, receipt["old_launcher_sha256"])
        self.assertFalse(result["pending_intent"])
        self.assertTrue(result["backup_exists"])
        self.assertTrue(result["receipt_exists"])
        self.assertFalse(result["may_install"])
        self.assertFalse(result["may_apply"])
        self.assertFalse(result["may_recover_automatically"])
        self.assertEqual(
            ["before_intent", "after_durable_intent", "after_durable_backup",
             "before_atomic_swap", "after_atomic_swap",
             "after_durable_receipt", "after_intent_unlink"], seen)

    def test_crash_injection_every_checkpoint_never_implicitly_replays(self):
        expectations = {
            "before_intent": ("SIMULATION_NOT_STARTED_NO_AUTHORIZATION", OLD, False),
            "after_durable_intent": ("PENDING_BEFORE_DURABLE_BACKUP_MANUAL_RECONCILIATION", OLD, True),
            "after_durable_backup": ("PENDING_OLD_LAUNCHER_MANUAL_RECONCILIATION", OLD, True),
            "before_atomic_swap": ("PENDING_OLD_LAUNCHER_MANUAL_RECONCILIATION", OLD, True),
            "after_atomic_swap": ("PENDING_NEW_LAUNCHER_MANUAL_RECONCILIATION", NEW, True),
            "after_durable_receipt": ("PENDING_NEW_LAUNCHER_MANUAL_RECONCILIATION", NEW, True),
            "after_intent_unlink": ("SIMULATED_LAUNCHER_COMMITTED_NO_DEPLOYMENT", NEW, False),
        }
        for step,(expected,content,pending) in expectations.items():
            with self.subTest(phase=step):
                with tempfile.TemporaryDirectory(prefix=swap.TEST_PREFIX) as home:
                    base = Path(home)
                    for name in ("live","signed","state"):
                        (base/name).mkdir(mode=0o700)
                        (base/name).chmod(0o700)
                    (base/swap.LIVE_REL).write_bytes(OLD)
                    (base/swap.SIGNED_REL).write_bytes(NEW)
                    (base/swap.LIVE_REL).chmod(0o700)
                    (base/swap.SIGNED_REL).chmod(0o700)
                    def break_at(phase):
                        if phase == step:
                            raise RuntimeError("synthetic interruption at " + phase)
                    with self.assertRaisesRegex(RuntimeError, step):
                        swap.simulate(base,source_sha=SOURCE_SHA,digest=DIGEST,
                                      nonce=NONCE,old_hash=OLD_SHA,new_hash=NEW_SHA,
                                      checkpoint=break_at)
                    observed = swap.inspect(base,NONCE,old_hash=OLD_SHA,new_hash=NEW_SHA)
                    self.assertEqual(expected,observed["status"])
                    self.assertEqual(content,(base/swap.LIVE_REL).read_bytes())
                    self.assertEqual(pending,observed["pending_intent"])
                    self.assertFalse(observed["may_recover_automatically"])
                    self.assertFalse(observed["may_apply"])

    @unittest.skipUnless(os.name=="posix", "SIGKILL is POSIX-only")
    def test_actual_kill_between_durable_intent_and_atomic_swap(self):
        # Each subprocess writes only in its own tempfile root. SIGKILL cannot
        # run finally/exception handlers; journal must survive process death.
        for phase in ("after_durable_intent","after_durable_backup","after_atomic_swap"):
            with self.subTest(phase=phase):
                with tempfile.TemporaryDirectory(prefix=swap.TEST_PREFIX) as home:
                    base=Path(home)
                    for name in ("live","signed","state"):
                        (base/name).mkdir(mode=0o700)
                        (base/name).chmod(0o700)
                    for path,data in ((base/swap.LIVE_REL,OLD),
                                      (base/swap.SIGNED_REL,NEW)):
                        path.write_bytes(data)
                        path.chmod(0o700)
                    payload = """
import importlib.util, os, pathlib, signal, sys
spec=importlib.util.spec_from_file_location("kill_test",sys.argv[1])
module=importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
def kill(checkpoint):
    if checkpoint==sys.argv[3]:
        os.kill(os.getpid(),signal.SIGKILL)
module.simulate(pathlib.Path(sys.argv[2]),source_sha=sys.argv[4],
                digest=sys.argv[5],nonce=sys.argv[6],old_hash=sys.argv[7],
                new_hash=sys.argv[8],checkpoint=kill)
"""
                    proc=subprocess.run([sys.executable,"-c",payload,str(SOURCE),str(base),
                                         phase,SOURCE_SHA,DIGEST,NONCE,OLD_SHA,NEW_SHA],
                                         capture_output=True,text=True,timeout=10)
                    self.assertEqual(-signal.SIGKILL,proc.returncode,proc.stderr)
                    state=swap.inspect(base,NONCE,old_hash=OLD_SHA,new_hash=NEW_SHA)
                    self.assertTrue(state["pending_intent"])
                    self.assertIn("MANUAL_RECONCILIATION",state["status"])
                    self.assertFalse(state["may_recover_automatically"])

    def test_replay_and_colliding_nonce_block_even_with_correct_bytes(self):
        self.execute()
        with self.assertRaisesRegex(swap.Blocked,
                                    "SANDBOX_SIGNED_CANDIDATE_HASH_MISMATCH"):
            self.execute()
        self.live.write_bytes(OLD)
        self.live.chmod(0o700)
        with self.assertRaisesRegex(swap.Blocked,
                                    "SANDBOX_REPLAY_OR_PENDING_BLOCKED"):
            self.execute()

    def test_old_hash_new_hash_and_source_digest_mutation_block_before_intent(self):
        bad = [
            dict(old_hash=NEW_SHA),
            dict(new_hash=OLD_SHA),
            dict(source_sha="wrong"),
            dict(digest="sha256:invalid"),
            dict(nonce="wrong"),
        ]
        for kwargs in bad:
            with self.subTest(kwargs=kwargs),self.assertRaises(swap.Blocked):
                self.execute(**kwargs)
            self.assertFalse(list((self.base/"state").iterdir()))

    def test_tampered_backup_receipt_and_intent_block_inspection(self):
        self.execute()
        backup=self.base/"state"/("old-"+NONCE+".py")
        backup.write_bytes(b"tampered backup")
        with self.assertRaisesRegex(swap.Blocked,"SANDBOX_BACKUP_HASH_DRIFT"):
            self.status()
        backup.write_bytes(OLD)
        receipt=self.base/"state"/("receipt-"+NONCE+".json")
        contents=json.loads(receipt.read_text())
        contents["old_launcher_sha256"]="c"*64
        receipt.write_text(json.dumps(contents))
        with self.assertRaisesRegex(swap.Blocked,"SANDBOX_COMMIT_RECEIPT_INVALID"):
            self.status()

    def test_symlink_hardlink_and_unauthorized_root_are_blocked(self):
        fake=self.base/"signed"/"suspicious"
        fake.write_bytes(NEW)
        self.candidate.unlink()
        self.candidate.symlink_to(fake)
        with self.assertRaisesRegex(swap.Blocked,"SANDBOX_FILE_UNTRUSTED"):
            self.execute()
        self.candidate.unlink()
        os.link(fake,self.candidate)
        self.candidate.chmod(0o700)
        with self.assertRaisesRegex(swap.Blocked,"SANDBOX_FILE_UNTRUSTED"):
            self.execute()
        with self.assertRaisesRegex(swap.Blocked,"SANDBOX_ROOT_NOT_WHITELISTED"):
            swap.trust_sandbox(Path("/usr/local/sbin"))

    def test_world_writable_test_directory_blocks(self):
        (self.base/"state").chmod(0o777)
        with self.assertRaisesRegex(swap.Blocked,"SANDBOX_DIRECTORY_UNTRUSTED"):
            self.execute()

    def test_no_production_installer_cli(self):
        run=subprocess.run([sys.executable,str(SOURCE)],capture_output=True,
                           text=True,timeout=10)
        self.assertNotEqual(0,run.returncode)
        self.assertIn("SANDBOX_LIBRARY_ONLY_NO_PRODUCTION_ENTRYPOINT",
                      run.stderr)
        self.assertNotIn("docker",SOURCE.read_text().lower())
        self.assertNotIn("/usr/local/sbin/pmgm-qa-update",SOURCE.read_text().split("def simulate")[1])

    def test_pre_existing_intent_blocks_without_touching_launcher(self):
        intent=self.base/"state"/("intent-"+NONCE+".json")
        intent.write_text("{}")
        with self.assertRaisesRegex(swap.Blocked,
                                    "SANDBOX_REPLAY_OR_PENDING_BLOCKED"):
            self.execute()
        self.assertEqual(OLD,self.live.read_bytes())

if __name__ == "__main__":
    unittest.main()
