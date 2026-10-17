"""Synthetic signed QA staging tests. No host, Docker, network or real credentials."""
import hashlib
import importlib.util
import io
from pathlib import Path
import stat
import subprocess
import tempfile
import unittest
from unittest.mock import patch
import zipfile

ROOT = Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location("qa_signed_staging", ROOT / "scripts/qa/qa-signed-staging.py")
qa = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(qa)
SHA = "a" * 40
PACK = "Proyecto-Centenario-QA-srv01-" + SHA[:12]
INNER = PACK + ".zip"
REQUIRED = {
    "BUILD-INFO.txt": "SOURCE_SHA=" + SHA + "\n" + qa.STORAGE_MARK + "\n",
    "storage-bundle/project-provenance.sigstore.json": "{}",
    "storage-bundle/rebuild-report.json": "{}",
    "storage-bundle/server-fixed.openvex.json": "{}",
    "storage-bundle/server-cve-triage.json": "{}",
    "storage-bundle/silo-qa-review-only.tar": "synthetic QA fixture, not runnable",
}


def fixture(where, *, changes=None, wrong_manifest=False, outer_member=None):
    changes = changes or {}
    files = {**REQUIRED, **changes}
    original_manifest = {k: hashlib.sha256(v.encode()).hexdigest() for k, v in REQUIRED.items()}
    actual_manifest = {k: hashlib.sha256(v.encode()).hexdigest() for k, v in files.items()}
    recorded = original_manifest if wrong_manifest else actual_manifest
    manifest = "".join(v + "  ./" + k + "\n" for k, v in sorted(recorded.items()))
    content = io.BytesIO()
    with zipfile.ZipFile(content, "w", compression=zipfile.ZIP_DEFLATED) as z:
        for name, value in files.items():
            z.writestr(PACK + "/" + name, value)
        z.writestr(PACK + "/MANIFEST.sha256", manifest)
    artifact = where / "signed-actions-artifact.zip"
    with zipfile.ZipFile(artifact, "w", compression=zipfile.ZIP_DEFLATED) as z:
        z.writestr(INNER, content.getvalue())
        z.writestr("installable-provenance.sigstore.json", "{}")
        z.writestr(INNER + ".verification.json", "{}")
        if outer_member is not None:
            name, body = outer_member
            z.writestr(name, body)
    return artifact, "sha256:" + qa.sha256_file(artifact)


def attestation_ok(command, **kwargs):
    if command[:3] != ["gh", "attestation", "verify"]:
        raise AssertionError("unexpected execution")
    return subprocess.CompletedProcess(command, 0)


class QaSignedStagingTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="qa-signed-staging-test-")
        self.addCleanup(self.temp.cleanup)
        self.dir = Path(self.temp.name)
        self.out = self.dir / "verified-synthetic"

    def test_valid_fixture_is_staged_without_installing(self):
        archive, digest = fixture(self.dir)
        with patch.object(qa.subprocess, "run", side_effect=attestation_ok) as executed:
            status = qa.stage(archive, digest, SHA, self.out)
        self.assertEqual("SIGNED_CODE_STAGED_NOT_INSTALLED", status["status"])
        self.assertEqual(1, executed.call_count)
        self.assertEqual("gh", executed.call_args.args[0][0])
        self.assertTrue((self.out / "storage-bundle" / "rebuild-report.json").is_file())
        self.assertFalse((self.dir / "INSTALAR.sh").exists())

    def test_rejects_outer_digest_change_before_attestation(self):
        archive, digest = fixture(self.dir)
        archive.write_bytes(archive.read_bytes() + b"tamper")
        with patch.object(qa.subprocess, "run") as run, self.assertRaisesRegex(qa.Blocked, "DIGEST_MISMATCH"):
            qa.stage(archive, digest, SHA, self.out)
        run.assert_not_called()
        self.assertFalse(self.out.exists())

    def test_rejects_missing_signature_evidence(self):
        archive, digest = fixture(self.dir)
        with zipfile.ZipFile(archive, "r") as z:
            data = {name: z.read(name) for name in z.namelist()
                    if name != "installable-provenance.sigstore.json"}
        with zipfile.ZipFile(archive, "w") as z:
            for name, body in data.items():
                z.writestr(name, body)
        with self.assertRaisesRegex(qa.Blocked, "UNEXPECTED_OUTER"):
            qa.stage(archive, "sha256:" + qa.sha256_file(archive), SHA, self.out)

    def test_manifest_file_content_tampering_blocks(self):
        archive, digest = fixture(self.dir, changes={"BUILD-INFO.txt": "SOURCE_SHA=" + SHA + "\n" + qa.STORAGE_MARK + "\nHACKED\n"},
                                  wrong_manifest=True)
        with patch.object(qa.subprocess, "run", side_effect=attestation_ok):
            with self.assertRaisesRegex(qa.Blocked, "CHECKSUM_MISMATCH"):
                qa.stage(archive, digest, SHA, self.out)
        self.assertFalse(self.out.exists())

    def test_rejects_non_matching_source_sha(self):
        archive, digest = fixture(self.dir)
        with patch.object(qa.subprocess, "run", side_effect=attestation_ok):
            with self.assertRaisesRegex(qa.Blocked, "SOURCE_OR_SILO_BINDING"):
                qa.stage(archive, digest, "b" * 40, self.out)

    def test_rejects_extra_outer_file_and_traversal(self):
        for value in [("unexpected.txt", "extra"), ("../escape.txt", "escape")]:
            with self.subTest(name=value[0]):
                archive, digest = fixture(self.dir, outer_member=value)
                with self.assertRaises(qa.Blocked):
                    qa.stage(archive, digest, SHA, self.out)
                self.assertFalse(self.out.exists())

    def test_symlink_zip_member_is_blocked(self):
        with zipfile.ZipFile(self.dir / "bad.zip", "w") as z:
            link = zipfile.ZipInfo("signed-link")
            link.create_system = 3
            link.external_attr = (stat.S_IFLNK | 0o777) << 16
            z.writestr(link, "other")
        with zipfile.ZipFile(self.dir / "bad.zip") as z:
            with self.assertRaisesRegex(qa.Blocked, "SYMLINK"):
                qa.checked_names(z)

    def test_broken_sigstore_attestation_blocks_without_staging(self):
        archive, digest = fixture(self.dir)
        with patch.object(qa.subprocess, "run", side_effect=subprocess.CalledProcessError(1, "gh")):
            with self.assertRaisesRegex(qa.Blocked, "ATTESTATION_FAILED"):
                qa.stage(archive, digest, SHA, self.out)
        self.assertFalse(self.out.exists())

    def test_existing_staging_is_not_overwritten(self):
        archive, digest = fixture(self.dir)
        self.out.mkdir()
        (self.out / "sentinel").write_text("do not erase")
        with self.assertRaisesRegex(qa.Blocked, "MUST_NOT_EXIST"):
            qa.stage(archive, digest, SHA, self.out)
        self.assertEqual("do not erase", (self.out / "sentinel").read_text())

    def test_missing_silo_evidence_blocks_after_manifest_verification(self):
        archive, _ = fixture(self.dir)
        with zipfile.ZipFile(archive) as outer:
            contents = {n: outer.read(n) for n in outer.namelist()}
        inner_data = contents[INNER]
        with zipfile.ZipFile(io.BytesIO(inner_data)) as nested:
            files = {n: nested.read(n) for n in nested.namelist()
                     if not n.endswith("storage-bundle/silo-qa-review-only.tar")}
        inner_output = io.BytesIO()
        with zipfile.ZipFile(inner_output, "w") as z:
            for name, value in files.items():
                z.writestr(name, value)
        contents[INNER] = inner_output.getvalue()
        with zipfile.ZipFile(archive, "w") as z:
            for name, value in contents.items():
                z.writestr(name, value)
        with patch.object(qa.subprocess, "run", side_effect=attestation_ok):
            with self.assertRaisesRegex(qa.Blocked, "FILE_SET_MISMATCH"):
                qa.stage(archive, "sha256:" + qa.sha256_file(archive), SHA, self.out)


if __name__ == "__main__":
    unittest.main()
