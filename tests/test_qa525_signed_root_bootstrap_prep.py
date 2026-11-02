"""Offline adversarial tests for independently authenticated root preparation.

Only disposable temp files; the real host and sudo are NEVER used.
"""
import hashlib
import importlib.util
import io
import json
import os
from pathlib import Path
from types import SimpleNamespace
import stat
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import Mock, patch
import zipfile

ROOT = Path(__file__).resolve().parents[1]
FILE = ROOT / "scripts/qa/operators/qa525-signed-root-bootstrap-prep.py"
spec = importlib.util.spec_from_file_location("qa525_signed_root_bootstrap", FILE)
m = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = m
spec.loader.exec_module(m)
SHA = "a" * 40
ORIGINAL_ISOLATION = m.require_isolated_root_python
BOOTSTRAP_HASH = hashlib.sha256(FILE.read_bytes()).hexdigest()


def make_zip(files):
    b = io.BytesIO()
    with zipfile.ZipFile(b, "w", zipfile.ZIP_STORED) as z:
        for name, data in files.items():
            z.writestr(name, data)
    return b.getvalue()


def build_release(*, missing=None, tampered=None, wrong_sha=False,
                  extra=None, source_sha=SHA):
    base = f"Proyecto-Centenario-QA-srv01-{source_sha[:12]}"
    members = {
        p: ("signed synthetic " + p).encode() for p in m.SOURCES
    }
    members[m.BOOTSTRAP_SCRIPT] = FILE.read_bytes()
    members.update({
        "BUILD-INFO.txt": ("SOURCE_SHA=" + ("b" * 40 if wrong_sha else source_sha) +
                           "\nSTORAGE=SIGNED_REBUILT_SILO_ARCHIVE_SYNTHETIC_QA\n").encode(),
        "README.txt": b"synthetic docs",
    })
    if missing:
        members.pop(missing)
    manifest = "".join(
        hashlib.sha256(value).hexdigest() + "  ./" + name + "\n"
        for name, value in sorted(members.items())
    )
    members["MANIFEST.sha256"] = manifest.encode()
    if tampered:
        members[tampered] = b"tampered after signing"
    inner = make_zip({base + "/" + name: value
                      for name, value in members.items()})
    if extra:
        inner = make_zip({base + "/" + name: value
                          for name, value in members.items()} |
                         {extra: b"invalid member"})
    return make_zip({
        base + ".zip": inner,
        "installable-provenance.sigstore.json": b"synthetic sigstore",
        base + ".zip.verification.json": b"{}",
    })


class RootBootstrapTests(unittest.TestCase):
    def setUp(self):
        self.context = tempfile.TemporaryDirectory(prefix="qa525-bootstrap-test-")
        self.addCleanup(self.context.cleanup)
        root = Path(self.context.name)
        self.stage = root / "stage"
        self.stage.mkdir(mode=0o700)
        self.lock = self.stage / "bootstrap.lock"
        self.lock.write_bytes(b"")
        self.lock.chmod(0o600)
        self.admission = root / "admission.json"
        self.envelope = root / "envelope.zip"
        original_fstat = os.fstat
        def as_root_fd(fd):
            info = list(original_fstat(fd))
            info[4] = 0  # st_uid on POSIX, fake root in disposable CI files
            return os.stat_result(info)
        self.patchers = [
            patch.object(m.os, "fstat", side_effect=as_root_fd),
            patch.object(m, "STORE", self.stage),
            patch.object(m, "LOCK", self.lock),
            patch.object(m, "ADMISSION", self.admission),
            patch.object(m, "root_owned"),
            patch.object(m.os, "geteuid", return_value=0),
            patch.object(m, "require_isolated_root_python"),
            patch.object(m.socket, "gethostname", return_value="glmdch"),
        ]
        for p in self.patchers:
            p.start()
            self.addCleanup(p.stop)

    def verify(self, data, *, digest=None, mock=None):
        self.envelope.write_bytes(data)
        self.envelope.chmod(0o600)
        fake = mock or Mock()
        expected = digest or hashlib.sha256(data).hexdigest()
        return m.prepare(self.envelope, SHA, expected, BOOTSTRAP_HASH,
                         now=1_800_000_000, verify=fake), fake

    def test_root_bootstrap_requires_isolated_python_before_staging(self):
        for isolated, no_site in ((0, 0), (1, 0), (0, 1)):
            with self.subTest(isolated=isolated, no_site=no_site):
                with patch.object(m.sys, "flags", SimpleNamespace(
                        isolated=isolated, no_site=no_site)):
                    with self.assertRaisesRegex(
                            m.Blocked, "BOOTSTRAP_ROOT_PYTHON_ISOLATION_REQUIRED"):
                        ORIGINAL_ISOLATION()
        with patch.object(m.sys, "flags", SimpleNamespace(isolated=1, no_site=1)):
            ORIGINAL_ISOLATION()

    def test_signed_source_is_staged_without_running_and_admitted_900_seconds(self):
        answer, runner = self.verify(build_release())
        self.assertEqual("QA525_SIGNED_SOURCES_PREPARED_NO_LAUNCHER_CHANGE",
                         answer["status"])
        self.assertEqual(12, answer["prepared_source_count"])
        self.assertFalse(answer["launcher_replaced"])
        self.assertFalse(answer["may_apply"])
        packet = json.loads(self.admission.read_text())
        self.assertEqual("EXTERNAL_SIGSTORE_BOOTSTRAP_ADMISSION_ONLY",
                         packet["status"])
        self.assertEqual(1_800_000_000, packet["issued_at"])
        self.assertEqual(1_800_000_900, packet["expires_at"])
        self.assertEqual(0o600, stat.S_IMODE(self.admission.stat().st_mode))
        self.assertEqual(set(packet["files"]),
                         {str(self.stage / SHA / s) for s in m.SOURCES})
        for path, hexdigest in packet["files"].items():
            self.assertEqual(hexdigest,
                             hashlib.sha256(Path(path).read_bytes()).hexdigest())
            self.assertEqual(0o600, stat.S_IMODE(Path(path).stat().st_mode))
        cmd = runner.call_args.args[0]
        self.assertEqual([str(m.GH_BINARY), "attestation", "verify"], cmd[:3])
        environment = runner.call_args.kwargs["env"]
        self.assertEqual("/usr/bin:/bin", environment["PATH"])
        self.assertEqual("C", environment["LC_ALL"])
        self.assertEqual(set(environment), {"PATH", "HOME", "GH_CONFIG_DIR", "LC_ALL"})
        self.assertNotEqual(os.environ.get("HOME"), environment["HOME"])
        self.assertFalse(environment.get("GH_TOKEN"))
        self.assertIn("--deny-self-hosted-runners", cmd)
        self.assertEqual(m.SIGNER, cmd[cmd.index("--signer-workflow") + 1])
        self.assertEqual(SHA, cmd[cmd.index("--source-digest") + 1])

    def test_prepared_admission_is_accepted_by_real_installer_contract(self):
        """Exercise real installer admission reader against signed synthetic bytes."""
        self.verify(build_release())
        installer_source = (ROOT / "scripts/qa/operators" /
                            "qa525-launcher-supervised-root-install.py")
        spec = importlib.util.spec_from_file_location(
            "qa525_bootstrap_contract_consumer", installer_source)
        consumer = importlib.util.module_from_spec(spec)
        sys.modules[spec.name] = consumer
        spec.loader.exec_module(consumer)
        source_paths = tuple(self.stage / SHA / key for key in m.SOURCES)
        # This test validates receipt interoperability only; an independent
        # negative test verifies that the real root entrypoint refuses Python
        # without -I -S. Do not weaken the production isolation guard.
        with patch.object(consumer, "require_host"), \
             patch.object(consumer, "ADMISSION", self.admission), \
             patch.object(consumer, "SOURCES", source_paths), \
             patch.object(consumer, "root_chain"), \
             patch.object(consumer, "read_bound",
                          side_effect=lambda p, maximum=4096, exact_mode=None:
                          Path(p).read_bytes()), \
             patch.object(consumer.time, "time",
                          return_value=1_800_000_001):
            packet = consumer.bootstrap_trusted_sources()
        self.assertEqual("EXTERNAL_SIGSTORE_BOOTSTRAP_ADMISSION_ONLY",
                         packet["status"])
        self.assertEqual(12, len(packet["files"]))

    def test_untrusted_pinned_gh_binary_blocks_before_verification(self):
        original = m.root_owned
        def require_system_gh(path, **kwargs):
            if Path(path) == m.GH_BINARY:
                raise m.Blocked("UNTRUSTED_SYSTEM_GH")
            return original(path, **kwargs)
        with patch.object(m, "root_owned", side_effect=require_system_gh):
            with self.assertRaisesRegex(m.Blocked, "UNTRUSTED_SYSTEM_GH"):
                self.verify(build_release())
        self.assertFalse(self.admission.exists())

    def test_wrong_envelope_digest_refused_without_sigstore(self):
        fake = Mock()
        with self.assertRaisesRegex(m.Blocked, "BOOTSTRAP_ENVELOPE_DIGEST_MISMATCH"):
            self.verify(build_release(), digest="0" * 64, mock=fake)
        fake.assert_not_called()
        self.assertFalse(self.admission.exists())

    def test_signed_missing_root_source_never_issues_admission(self):
        with self.assertRaisesRegex(
                m.Blocked, "BOOTSTRAP_SIGNED_INSTALLER_OR_HELPERS_MISSING"):
            self.verify(build_release(missing=m.SOURCES[0]))
        self.assertFalse(self.admission.exists())
        self.assertFalse((self.stage / SHA).exists())

    def test_missing_signed_bootstrap_itself_never_issues_admission(self):
        with self.assertRaisesRegex(
                m.Blocked, "BOOTSTRAP_SIGNED_INSTALLER_OR_HELPERS_MISSING"):
            self.verify(build_release(missing=m.BOOTSTRAP_SCRIPT))
        self.assertFalse(self.admission.exists())

    def test_changed_signed_helper_never_stages(self):
        with self.assertRaisesRegex(m.Blocked, "BOOTSTRAP_SIGNED_MEMBER_MISMATCH"):
            self.verify(build_release(tampered=m.SOURCES[-1]))
        self.assertFalse(self.admission.exists())
        self.assertFalse((self.stage / SHA).exists())

    def test_signature_fail_closed_before_any_staging(self):
        failure = Mock(side_effect=subprocess.CalledProcessError(1, "gh"))
        with self.assertRaisesRegex(m.Blocked, "BOOTSTRAP_ATTESTATION_FAILED"):
            self.verify(build_release(), mock=failure)
        self.assertFalse(self.admission.exists())

    def test_wrong_source_and_zip_path_escape_fail_closed(self):
        with self.assertRaisesRegex(m.Blocked, "BOOTSTRAP_BUILD_SOURCE_MISMATCH"):
            self.verify(build_release(wrong_sha=True))
        with self.assertRaisesRegex(m.Blocked, "BOOTSTRAP_UNSAFE_ZIP_PATH"):
            self.verify(build_release(extra="../escape.py"))
        self.assertFalse(self.admission.exists())

    def test_second_prepare_requires_manual_reconciliation_no_overwrite(self):
        self.verify(build_release())
        with self.assertRaisesRegex(m.Blocked, "BOOTSTRAP_ADMISSION_ALREADY_EXISTS"):
            self.verify(build_release())
        self.assertTrue(self.admission.is_file())

    def test_unauthorized_bootstrap_code_digest_is_blocked(self):
        self.envelope.write_bytes(build_release())
        self.envelope.chmod(0o600)
        with self.assertRaisesRegex(
                m.Blocked, "BOOTSTRAP_TRUSTED_VERIFIER_HASH_MISMATCH"):
            m.prepare(self.envelope, SHA,
                      hashlib.sha256(self.envelope.read_bytes()).hexdigest(),
                      "0" * 64, verify=Mock())
        self.assertFalse(self.admission.exists())

    def test_concurrent_bootstrap_requires_separate_lock(self):
        with m.locked_bootstrap():
            with self.assertRaisesRegex(m.Blocked, "BOOTSTRAP_ALREADY_RUNNING"):
                with m.locked_bootstrap():
                    self.fail("Concurrent bootstrap unexpectedly obtained lock")
        self.assertTrue(self.lock.exists())

    def test_partial_os_write_is_retried_without_corrupting_admission(self):
        actual_write = os.write
        with patch.object(m.os, "write",
                          side_effect=lambda fd, data:
                          actual_write(fd, data[:min(17, len(data))])):
            self.verify(build_release())
        packet = json.loads(self.admission.read_text())
        self.assertEqual(len(m.SOURCES), len(packet["files"]))

    def test_no_deploy_or_force_flags_and_root_guard(self):
        code = FILE.read_text()
        self.assertNotIn('os.system(', code)
        self.assertNotIn('subprocess.run(["docker"', code)
        self.assertNotIn('write_once(LAUNCHER', code)
        self.assertNotIn('os.replace(staged, LAUNCHER)', code)
        self.assertNotIn('"--force"', code)
        with patch.object(m.os, "geteuid", return_value=1001):
            with self.assertRaisesRegex(m.Blocked, "BOOTSTRAP_ROOT_GLMDCH_ONLY"):
                m.prepare(self.envelope, SHA, "c" * 64, BOOTSTRAP_HASH)




class DelayedRecoveryBootstrapTests(unittest.TestCase):
    """Real A/B ZIP checks with disposable root-mocked staging, no QA host."""

    B = "b" * 40
    verify = RootBootstrapTests.verify

    def setUp(self):
        super().setUp()
        self.pending = Path(self.context.name) / "pending"
        self.pending.mkdir(mode=0o700)
        self.launcher = Path(self.context.name) / "bin" / "pmgm-qa-update"
        self.launcher.parent.mkdir(mode=0o700)
        self.original = b"synthetic original launcher\n"
        self.launcher.write_bytes(self.original)
        self.launcher.chmod(0o700)
        self.new = b"synthetic target launcher\n"
        self.nonce = "9" * 64
        self.b_admission = Path(self.context.name) / "recovery-admission.json"
        for patcher in (
            patch.object(m, "PENDING_STATE", self.pending),
            patch.object(m, "LAUNCHER", self.launcher),
            patch.object(m, "RECOVERY_ADMISSION", self.b_admission),
        ):
            patcher.start()
            self.addCleanup(patcher.stop)
        original_zip = build_release()
        self.verify(original_zip)
        a_envelope = self.stage / ("qa-signed-" + SHA + ".zip")
        a_envelope.write_bytes(original_zip)
        a_envelope.chmod(0o600)
        self.a_original_admission = self.admission.read_bytes()
        self.intent = {
            "status": "SWAP_INTENT_NO_DEPLOYMENT",
            "source_sha": SHA,
            "release_asset_sha256":
                "sha256:" + hashlib.sha256(original_zip).hexdigest(),
            "old_launcher_sha256": hashlib.sha256(self.original).hexdigest(),
            "new_launcher_sha256": hashlib.sha256(self.new).hexdigest(),
            "nonce": self.nonce,
        }
        self.intent_path = self.pending / ("intent-" + self.nonce + ".json")
        self.intent_path.write_text(json.dumps(self.intent))
        self.intent_path.chmod(0o600)
        self.backup = self.pending / ("old-" + self.nonce + ".bin")
        self.backup.write_bytes(self.original)
        self.backup.chmod(0o700)
        self.b_envelope = Path(self.context.name) / "B.zip"
        self.b_bytes = build_release(source_sha=self.B)
        self.b_envelope.write_bytes(self.b_bytes)
        self.b_envelope.chmod(0o600)

    def admit_b(self, **kwargs):
        return m.prepare_delayed_recovery(
            kwargs.pop("envelope", self.b_envelope),
            kwargs.pop("sha", self.B),
            kwargs.pop("digest", hashlib.sha256(self.b_bytes).hexdigest()),
            BOOTSTRAP_HASH, kwargs.pop("nonce", self.nonce),
            now=kwargs.pop("now", 1_900_000_000),
            verify=kwargs.pop("verify", Mock()),
            **kwargs)

    def test_b_admission_verifies_both_signed_envelopes_and_preserves_A(self):
        before = self.intent_path.read_bytes()
        after = self.admit_b()
        self.assertEqual(
            "QA525_DUAL_SIGNED_RECOVERY_B_PREPARED_NO_ACTION",
            after["status"])
        self.assertFalse(after["may_recover"])
        self.assertFalse(after["may_install"])
        self.assertFalse(after["may_apply"])
        self.assertEqual(self.a_original_admission, self.admission.read_bytes())
        self.assertEqual(before, self.intent_path.read_bytes())
        self.assertEqual(self.original, self.backup.read_bytes())
        self.assertEqual(self.original, self.launcher.read_bytes())
        b_packet = json.loads(self.b_admission.read_text())
        self.assertEqual("EXTERNAL_SIGSTORE_RECOVERY_B_ADMISSION_ONLY",
                         b_packet["status"])
        self.assertEqual(1_900_000_000, b_packet["issued_at"])
        self.assertEqual(1_900_000_900, b_packet["expires_at"])
        self.assertEqual(SHA, b_packet["cut_source_sha"])
        self.assertEqual(self.B, b_packet["source_sha"])
        self.assertEqual(hashlib.sha256(json.dumps(
            self.intent, sort_keys=True, separators=(",", ":"),
            ensure_ascii=True).encode()).hexdigest(),
            b_packet["cut_intent_sha256"])
        self.assertEqual(set(b_packet["files"]), {
            str(self.stage / ("recovery-" + self.B) / s) for s in m.SOURCES})
        for path, digest in b_packet["files"].items():
            self.assertEqual(hashlib.sha256(Path(path).read_bytes()).hexdigest(),
                             digest)
        self.assertFalse(
            (self.pending / ("receipt-" + self.nonce + ".json")).exists())

    def test_expired_original_A_admission_cannot_be_reissued_or_overwritten(self):
        self.assertLess(json.loads(
            self.admission.read_text())["expires_at"], 1_900_000_000)
        self.admit_b()
        with self.assertRaisesRegex(
                m.Blocked, "RECOVERY_BOOTSTRAP_ALREADY_ADMITTED"):
            self.admit_b()
        self.assertEqual(self.a_original_admission, self.admission.read_bytes())

    def test_refuses_tampered_original_sources_and_archive(self):
        source = self.stage / SHA / m.SOURCES[0]
        source.write_bytes(b"tampered")
        with self.assertRaisesRegex(
                m.Blocked, "RECOVERY_BOOTSTRAP_HISTORICAL_A_SOURCE_DRIFT"):
            self.admit_b()
        self.assertFalse(self.b_admission.exists())

    def test_refuses_missing_original_intent_backup_and_commit_ambiguity(self):
        for target, error in (
            (self.intent_path, "BOOTSTRAP_ROOT_PATH_MISSING"),
            (self.backup, "BOOTSTRAP_ROOT_PATH_MISSING"),
        ):
            target.rename(target.with_suffix(".hidden"))
            try:
                with self.subTest(target=target):
                    with self.assertRaises(m.Blocked):
                        self.admit_b()
                    self.assertFalse(self.b_admission.exists())
            finally:
                target.with_suffix(".hidden").rename(target)
        receipt = self.pending / ("receipt-" + self.nonce + ".json")
        receipt.write_text('{"status":"FAKE"}')
        with self.assertRaisesRegex(
                m.Blocked, "RECOVERY_BOOTSTRAP_ORIGINAL_CUT_ALREADY_COMMITTED"):
            self.admit_b()
        self.assertFalse(self.b_admission.exists())

    def test_refuses_bad_B_sigstore_and_no_recovery_admission(self):
        failure = Mock(side_effect=subprocess.CalledProcessError(1, "gh"))
        with self.assertRaisesRegex(
                m.Blocked, "BOOTSTRAP_ATTESTATION_FAILED"):
            self.admit_b(verify=failure)
        self.assertFalse(self.b_admission.exists())
        self.assertFalse((self.stage / ("recovery-" + self.B)).exists())

    def test_refuses_reusing_A_source_as_B(self):
        with self.assertRaisesRegex(
                m.Blocked, "RECOVERY_BOOTSTRAP_B_MUST_DIFFER_FROM_A"):
            self.admit_b(sha=SHA, digest=hashlib.sha256(
                (self.stage / ("qa-signed-" + SHA + ".zip")).read_bytes()
                ).hexdigest())
        self.assertFalse(self.b_admission.exists())

    def test_refuses_unrecognized_launcher_and_wrong_nonce(self):
        self.launcher.write_bytes(b"unexpected launcher")
        with self.assertRaisesRegex(
                m.Blocked, "RECOVERY_BOOTSTRAP_UNKNOWN_LIVE_LAUNCHER"):
            self.admit_b()
        self.launcher.write_bytes(self.original)
        with self.assertRaises(m.Blocked):
            self.admit_b(nonce="0" * 64)
        self.assertFalse(self.b_admission.exists())


if __name__ == "__main__":
    unittest.main()
