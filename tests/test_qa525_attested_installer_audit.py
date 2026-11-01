"""Hermetic attestation and signed-manifest guard tests, no root/network/QA."""
import hashlib
import importlib.util
import io
from pathlib import Path
import stat
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import Mock, patch
import zipfile

SOURCE = (Path(__file__).resolve().parents[1] /
          "scripts/qa/operators/qa525-attested-installer-audit.py")
spec = importlib.util.spec_from_file_location("qa525_attested_installer_audit", SOURCE)
m = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = m
spec.loader.exec_module(m)
SHA = "a" * 40


def archive_bytes(files):
    b = io.BytesIO()
    with zipfile.ZipFile(b, "w", compression=zipfile.ZIP_STORED) as z:
        for name, content in files.items():
            z.writestr(name, content)
    return b.getvalue()


def fixture(*, missing=None, tampered=None, unexpected=None, malicious=None,
            wrong_build=False):
    root = f"Proyecto-Centenario-QA-srv01-{SHA[:12]}"
    data = {path: ("synthetic code " + path).encode() for path in m.SOURCES}
    data[m.BOOTSTRAP_SCRIPT] = b"synthetic independently authenticated bootstrap"
    data.update({"BUILD-INFO.txt":
                     ("SOURCE_SHA=" + ("f" * 40 if wrong_build else SHA) +
                      "\nSTORAGE=SIGNED_REBUILT_SILO_ARCHIVE_SYNTHETIC_QA\n").encode(),
                 "docs/readme.txt": b"test-only body"})
    if missing:
        del data[missing]
    manifest = "".join(hashlib.sha256(value).hexdigest() + "  ./" + key + "\n"
                       for key, value in sorted(data.items()))
    data["MANIFEST.sha256"] = manifest.encode()
    if tampered:
        data[tampered] = b"changed after manifest generation"
    inner = archive_bytes({root + "/" + k: v for k, v in data.items()})
    if malicious:
        inner = archive_bytes({root + "/" + k: v for k, v in data.items()} |
                              {malicious: b"unsafe"})
    outer_name = root + ".zip"
    envelope = {outer_name: inner,
                "installable-provenance.sigstore.json": b"test attestation data",
                outer_name + ".verification.json": b"{}"}
    if unexpected:
        envelope[unexpected] = b"extra"
    return archive_bytes(envelope)


class AttestedSourceAuditTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory(prefix="qa525-unprivileged-audit-test-")
        self.addCleanup(self.tmp.cleanup)
        self.file = Path(self.tmp.name) / "release.zip"

    def run_audit(self, data, *, expected=None, runner=None):
        self.file.write_bytes(data)
        expected = expected or hashlib.sha256(data).hexdigest()
        runner = runner or Mock()
        with patch.object(m.os, "geteuid", return_value=1000):
            return m.audit(self.file, SHA, expected, runner=runner), runner

    def test_verified_exact_manifest_has_twelve_sources_and_no_install(self):
        answer, runner = self.run_audit(fixture())
        self.assertEqual("SIGNED_INSTALLER_AUDITED_UNPRIVILEGED_NO_INSTALL",
                         answer["status"])
        self.assertEqual(set(m.SOURCES), set(answer["privileged_sources_sha256"]))
        self.assertEqual(hashlib.sha256(
            b"synthetic independently authenticated bootstrap").hexdigest(),
            answer["bootstrap_sha256"])
        self.assertFalse(answer["bootstrap_admission_created"])
        self.assertFalse(answer["may_apply"])
        self.assertFalse(answer["may_install"])
        args = runner.call_args.args[0]
        self.assertEqual(["gh", "attestation", "verify"], args[:3])
        self.assertIn("--deny-self-hosted-runners", args)
        self.assertEqual(SHA, args[args.index("--source-digest") + 1])
        self.assertEqual(m.WORKFLOW, args[args.index("--signer-workflow") + 1])
        self.assertEqual(m.REPO, args[args.index("--repo") + 1])

    def test_bad_digest_fails_before_gh_or_extraction(self):
        verifier = Mock()
        with self.assertRaisesRegex(m.Blocked, "ENVELOPE_SHA256_MISMATCH"):
            self.run_audit(fixture(), expected="0" * 64, runner=verifier)
        verifier.assert_not_called()

    def test_signature_failure_refuses_any_positive_result(self):
        verifier = Mock(side_effect=subprocess.CalledProcessError(1, "gh"))
        with self.assertRaisesRegex(m.Blocked, "GITHUB_SIGSTORE_ATTESTATION_FAILED"):
            self.run_audit(fixture(), runner=verifier)
        self.assertEqual(1, verifier.call_count)

    def test_no_installer_in_dev_release_is_blocked_even_if_attested(self):
        with self.assertRaisesRegex(m.Blocked, "SIGNED_INSTALLER_OR_HELPERS_ABSENT"):
            self.run_audit(fixture(missing=m.SOURCES[0]))

    def test_missing_root_bootstrap_source_is_blocked(self):
        with self.assertRaisesRegex(m.Blocked, "SIGNED_INSTALLER_OR_HELPERS_ABSENT"):
            self.run_audit(fixture(missing=m.BOOTSTRAP_SCRIPT))

    def test_manifest_tampering_is_blocked(self):
        with self.assertRaisesRegex(m.Blocked, "SIGNED_MEMBER_SHA256_MISMATCH"):
            self.run_audit(fixture(tampered=m.SOURCES[1]))

    def test_wrong_signed_source_sha_is_blocked(self):
        with self.assertRaisesRegex(m.Blocked, "SIGNED_SOURCE_BUILD_INFO_MISMATCH"):
            self.run_audit(fixture(wrong_build=True))

    def test_extra_envelope_or_archive_path_escape_blocked(self):
        with self.assertRaisesRegex(m.Blocked, "UNEXPECTED_ENVELOPE_MEMBERS"):
            self.run_audit(fixture(unexpected="evil.py"))
        with self.assertRaisesRegex(m.Blocked, "UNSAFE_OR_DUPLICATE_ZIP_PATH"):
            self.run_audit(fixture(malicious="../escape.py"))

    def test_requires_unprivileged_runner(self):
        self.file.write_bytes(fixture())
        with patch.object(m.os, "geteuid", return_value=0):
            with self.assertRaisesRegex(m.Blocked, "ROOT_FORBIDDEN_FOR_AUDIT"):
                m.audit(self.file, SHA, hashlib.sha256(self.file.read_bytes()).hexdigest())

    def test_reject_symlink_zip_member(self):
        info = zipfile.ZipInfo("evil-link")
        info.create_system = 3
        info.external_attr = (stat.S_IFLNK | 0o777) << 16
        f = io.BytesIO()
        with zipfile.ZipFile(f, "w") as z:
            z.writestr(info, b"/etc/passwd")
        with zipfile.ZipFile(io.BytesIO(f.getvalue())) as z:
            with self.assertRaisesRegex(m.Blocked, "UNSAFE_ZIP_MEMBER_TYPE"):
                m.members(z)


if __name__ == "__main__":
    unittest.main()
