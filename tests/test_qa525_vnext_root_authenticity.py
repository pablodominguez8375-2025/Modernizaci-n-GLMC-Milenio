"""QA525 root authenticity port: real ssh-keygen for OPS; a pinned stub (and the real gh) for APP."""
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest
import unittest.mock

QA = Path(__file__).resolve().parents[1] / "scripts/qa"
KEYGEN = "/usr/bin/ssh-keygen"
REAL_GH = shutil.which("gh")
UID, GID = os.geteuid(), os.getegid()
OPS_NS = "qa525-ops-v2@pmgm"
GO_NS = "qa525-go-cut-v1@pmgm"
DIG = lambda c: "sha256:" + c * 64
ARCHIVE_BYTES = b"PK\x03\x04 pretend inner release zip " * 40
ARCHIVE = "sha256:" + hashlib.sha256(ARCHIVE_BYTES).hexdigest()
OPS_SRC, APP_SRC = "a" * 40, "b" * 40
POLICY = DIG("4")
BUNDLE, ROOT = b'{"mediaType":"bundle"}', b'{"mediaType":"trusted-root"}\n'


def load(name, alias):
    spec = importlib.util.spec_from_file_location(alias, QA / name)
    m = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(m)
    return m


def run(*argv, **kw):
    return subprocess.run(argv, check=True, capture_output=True, **kw)


def sha(path):
    return "sha256:" + hashlib.sha256(Path(path).read_bytes()).hexdigest()


def receipt(archive=ARCHIVE, ops_src=OPS_SRC):
    return ('{"app_archive_sha256":"%s","ops_source_sha":"%s","policy_hash":"%s","program_sha256":"%s",'
            '"readiness_sha256":"%s","schema":"qa525-ops-v3","staging_sha256":"%s",'
            '"status":"OPS_SIGNED_VERIFIED"}' % (archive, ops_src, POLICY, DIG("1"), DIG("2"), DIG("3"))).encode()


def subject(**over):
    doc = {"source_sha": APP_SRC, "subject_sha256": ARCHIVE,
           "signer_workflow": "pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/.github/workflows/qa-silo-rebuild.yml",
           "oidc_issuer": "https://token.actions.githubusercontent.com"}
    doc.update(over)
    return json.dumps(doc).encode()


REPO_ID = "424242"
ENTRY = '{"verificationResult":{"signature":{"certificate":{"sourceRepositoryIdentifier":"%s"}}}}'
OK_JSON = "echo '[" + ENTRY % REPO_ID + "]'"
STUB = """#!/bin/sh
# Oracle gh: it only says yes when the three inputs arrive, through inherited fds, with the pinned bytes.
OUT=%(out)s
echo "$@" > $OUT/argv
/usr/bin/env > $OUT/env
/usr/bin/id -u > $OUT/uid
zipf=$3
shift 3
while [ $# -gt 0 ]; do
  case "$1" in
    --bundle) b=$2;; --custom-trusted-root) r=$2;;
  esac
  shift
done
sum() { /usr/bin/sha256sum "$1" | /usr/bin/cut -d' ' -f1; }
[ "$(sum $zipf)" = "%(zipsum)s" ] || exit 11
[ "$(sum $b)" = "%(bsum)s" ] || exit 12
[ "$(sum $r)" = "%(rsum)s" ] || exit 13
%(tail)s
"""


@unittest.skipUnless(os.path.exists(KEYGEN), "ssh-keygen not installed")
class AuthenticityTests(unittest.TestCase):
    def setUp(self):
        self.m = load("qa525-vnext-root-authenticity.py", "qa525_auth_test")
        self.v = load("qa525-vnext-root-go-verify.py", "qa525_auth_gorv")
        self.ops = load("qa525-vnext-ops-signature.py", "qa525_auth_ops")
        self.dir = tempfile.mkdtemp(prefix="auth")
        self.addCleanup(shutil.rmtree, self.dir, True)
        os.chmod(self.dir, 0o755)
        self.out = os.path.join(self.dir, "out")
        os.mkdir(self.out, 0o777)
        os.chmod(self.out, 0o777)
        self.tmp = os.path.join(self.dir, "t")
        os.mkdir(self.tmp, 0o700)
        self.keygen = os.path.join(self.dir, "ssh-keygen")
        shutil.copy(KEYGEN, self.keygen)
        os.chmod(self.keygen, 0o755)
        self.key, self.other = self.keypair("k1"), self.keypair("k2")
        self.zip = os.path.join(self.dir, "inner.zip")
        Path(self.zip).write_bytes(ARCHIVE_BYTES)
        os.chmod(self.zip, 0o644)

    def keypair(self, name):
        path = os.path.join(self.dir, name)
        run(KEYGEN, "-q", "-t", "ed25519", "-N", "", "-f", path)
        pub = Path(path + ".pub").read_text().split()
        fp = run(KEYGEN, "-lf", path + ".pub").stdout.decode().split()[1]
        return {"path": path, "fp": fp,
                "ops_allowed": ('qa525-ops namespaces="%s" ssh-ed25519 %s\n' % (OPS_NS, pub[1])).encode()}

    def sign(self, key, data, ns=OPS_NS):
        doc = os.path.join(self.dir, "doc")
        Path(doc).write_bytes(data)
        if os.path.exists(doc + ".sig"):
            os.unlink(doc + ".sig")
        run(KEYGEN, "-Y", "sign", "-f", key["path"], "-n", ns, doc)
        return Path(doc + ".sig").read_bytes()

    def gh(self, tail=OK_JSON, bundle=BUNDLE, root=ROOT, zipbytes=ARCHIVE_BYTES):
        path = os.path.join(self.dir, "gh")
        text = STUB % dict(out=self.out, zipsum=hashlib.sha256(zipbytes).hexdigest(),
                           bsum=hashlib.sha256(bundle).hexdigest(), rsum=hashlib.sha256(root).hexdigest(), tail=tail)
        Path(path).write_text(text)
        os.chmod(path, 0o755)
        return path

    def open_archive(self, path=None):
        def opener():
            fd = os.open(path or self.zip, os.O_RDONLY | os.O_NOFOLLOW)
            return fd, os.fstat(fd).st_size
        return opener

    def app(self, gh_path=None, **over):
        gh_path = gh_path or self.gh()
        kw = dict(open_binary=self.v.open_pinned_binary, gh_path=gh_path, gh_sha256=sha(gh_path),
                  trusted_root=ROOT, bundle=BUNDLE, open_archive=self.open_archive(), temp_root=self.tmp, source_repository_id=REPO_ID, drop_uid=UID or 30033,
                  drop_gid=GID or 30033, required_euid=UID, os_uid=UID)
        kw.update(over)
        return self.m.app_check_for(**kw)

    def ops_check(self, signature=None, **over):
        ops_port = self.v.ops_port_for(
            keygen_path=self.keygen, trusted_binary_sha256=sha(self.keygen), allowed_signers=self.key["ops_allowed"],
            expected_fingerprint=self.key["fp"], temp_root=self.tmp, required_euid=UID, os_uid=UID)
        kw = dict(ops_signature=self.ops, ops_port=ops_port, signers=self.key["ops_allowed"],
                  ops_fingerprint=self.key["fp"], signature=signature or self.sign(self.key, receipt()),
                  policy_hash=POLICY)
        kw.update(over)
        return self.m.ops_check_for(**kw)

    def port(self, ops_check=None, app_check=None):
        return self.m.port_for(ops_check=ops_check or self.ops_check(), app_check=app_check or self.app())

    def claims(self, **over):
        doc = {"app_archive_sha256": ARCHIVE, "ops_source_sha": OPS_SRC, "app_source_sha": APP_SRC,
               "purpose": "LAUNCHER_ONLY"}
        doc.update(over)
        return doc

    def call(self, port=None, ops=None, subj=None, claims=None):
        return (port or self.port())(ops_bytes=receipt() if ops is None else ops,
                                     app_subject_bytes=subject() if subj is None else subj,
                                     claims=self.claims() if claims is None else claims)

    def stop(self, code, fn, *a, **kw):
        with self.assertRaises(self.m.Stop) as ctx:
            fn(*a, **kw)
        self.assertEqual(str(ctx.exception), code)

    # ---- positive -----------------------------------------------------------------------------------
    def test_both_halves_authentic_and_bound_to_the_go_claims(self):
        self.assertIs(self.call(), True)
        self.assertEqual(os.listdir(self.tmp), [])
        argv = Path(self.out, "argv").read_text().split()
        self.assertEqual(argv[:2], ["attestation", "verify"])
        self.assertRegex(argv[2], r"^/proc/self/fd/\d+$")
        for flag, value in (("--repo", "pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio"),
                            ("--source-digest", APP_SRC), ("--predicate-type", "https://slsa.dev/provenance/v1"),
                            ("--cert-oidc-issuer", "https://token.actions.githubusercontent.com"),
                            ("--signer-workflow", "pablodominguez8375-2025/Modernizaci-n-GLMC-Milenio/.github/workflows/qa-silo-rebuild.yml")):
            self.assertEqual(argv[argv.index(flag) + 1], value)
        for flag, leaf in (("--bundle", "bundle.json"), ("--custom-trusted-root", "trusted-root.jsonl")):
            self.assertRegex(argv[argv.index(flag) + 1], r"^/proc/self/fd/\d+/" + leaf.replace(".", r"\.") + "$")
        self.assertEqual(os.listdir(self.tmp), [])
        self.assertIn("--deny-self-hosted-runners", argv)
        env = dict(line.split("=", 1) for line in Path(self.out, "env").read_text().splitlines() if "=" in line)
        self.assertNotIn("GH_TOKEN", env)
        self.assertNotIn("GITHUB_TOKEN", env)
        self.assertEqual(env.get("HOME"), "/nonexistent")

    @unittest.skipUnless(os.geteuid() == 0, "needs root to drop privileges")
    def test_verifier_runs_as_the_unprivileged_identity_when_root(self):
        gh = self.gh()
        app = self.app(gh_path=gh, drop_uid=30033, drop_gid=30033, required_euid=0, os_uid=0)
        self.assertIs(app(ARCHIVE, APP_SRC), True)
        self.assertEqual(Path(self.out, "uid").read_text().strip(), "30033")

    # ---- OPS half -----------------------------------------------------------------------------------
    def test_ops_receipt_for_another_archive_is_refused(self):
        other = receipt(archive=DIG("9"))
        check = self.ops_check(signature=self.sign(self.key, other))
        self.stop("AUTH_OPS_DENIED", self.call, port=self.port(ops_check=check), ops=other)

    def test_ops_signed_by_another_key_or_in_the_go_namespace_is_refused(self):
        for sig in (self.sign(self.other, receipt()), self.sign(self.key, receipt(), ns=GO_NS)):
            self.stop("AUTH_OPS_DENIED", self.call, port=self.port(ops_check=self.ops_check(signature=sig)))

    def test_tampered_ops_receipt_is_refused(self):
        self.stop("AUTH_OPS_DENIED", self.call, ops=receipt(ops_src="c" * 40))

    def test_receipt_that_disagrees_with_the_go_claims_is_refused(self):
        sig = self.sign(self.key, receipt(ops_src="c" * 40))
        check = self.ops_check(signature=sig)
        self.stop("AUTH_OPS_CLAIMS_MISMATCH", self.call, port=self.port(ops_check=check),
                  ops=receipt(ops_src="c" * 40))

    # ---- APP claims ---------------------------------------------------------------------------------
    def test_app_subject_must_match_the_go_claims_and_the_fixed_identity(self):
        for over in (dict(source_sha="c" * 40), dict(subject_sha256=DIG("9")),
                     dict(signer_workflow="evil/repo/.github/workflows/x.yml"),
                     dict(oidc_issuer="https://evil.example")):
            self.stop("AUTH_APP_CLAIMS_MISMATCH", self.call, subj=subject(**over))
        self.stop("AUTH_SUBJECT_INVALID", self.call, subj=b"[]")
        self.stop("AUTH_SUBJECT_INVALID", self.call, subj=b'{"source_sha":"a","source_sha":"b"}')

    def test_claims_must_be_complete_and_well_formed(self):
        for bad in ({}, self.claims(app_archive_sha256="x"), self.claims(ops_source_sha="Z" * 40),
                    {k: v for k, v in self.claims().items() if k != "app_source_sha"}):
            self.stop("AUTH_CLAIMS_INVALID", self.call, claims=bad)
        self.stop("AUTH_INPUT_INVALID", self.port(), ops_bytes="x", app_subject_bytes=b"", claims={})

    # ---- APP execution ------------------------------------------------------------------------------
    def test_gh_rejection_or_garbage_output_is_refused(self):
        for tail in ("exit 1", "echo 'not json'", "echo '[]'", "echo '{}'", "exit 0",
                     "echo '[{}]'",                                              # no certificate fields at all
                     "echo '[" + ENTRY % "1" + "]'",                             # another repository id
                     "echo '[" + ENTRY % REPO_ID + "," + ENTRY % "1" + "]'",     # one good entry hides a bad one
                     "echo '[" + (ENTRY % REPO_ID).replace('"' + REPO_ID + '"', REPO_ID) + "]'",  # id as a number
                     "echo '[null]'", "echo '[" + ENTRY % "" + "]'"):
            gh = self.gh(tail=tail)
            self.stop("AUTH_APP_DENIED", self.app(gh_path=gh), ARCHIVE, APP_SRC)

    def test_inputs_reach_the_verifier_byte_exact_or_it_says_no(self):
        for bad in (dict(bundle=b'{"mediaType":"other"}'), dict(trusted_root=b"{}\n")):
            self.stop("AUTH_APP_DENIED", self.app(**bad), ARCHIVE, APP_SRC)  # stub pinned the real ones: exit 12/13

    def test_archive_that_is_not_the_signed_release_never_reaches_the_verifier(self):
        Path(self.zip).write_bytes(ARCHIVE_BYTES + b"x")
        self.stop("AUTH_ARCHIVE_DIGEST_MISMATCH", self.app(), ARCHIVE, APP_SRC)
        self.assertFalse(os.path.exists(os.path.join(self.out, "argv")))

    def test_archive_changing_while_read_is_refused(self):
        def opener():
            fd = os.open(self.zip, os.O_RDONLY)
            return fd, os.fstat(fd).st_size + 1
        self.stop("AUTH_ARCHIVE_CHANGED", self.app(open_archive=opener), ARCHIVE, APP_SRC)

    def test_oversized_archive_is_refused(self):
        with unittest.mock.patch.object(self.m, "MAX_ZIP", 16):
            self.stop("AUTH_INPUT_TOO_LARGE", self.app(), ARCHIVE, APP_SRC)

    def test_archive_opener_failure_is_a_fixed_code(self):
        def opener():
            raise OSError("boom /secret/path")
        self.stop("AUTH_ARCHIVE_UNAVAILABLE", self.app(open_archive=opener), ARCHIVE, APP_SRC)

    def test_gh_binary_must_match_its_pin_and_be_untouched(self):
        gh = self.gh()
        app = self.app(gh_path=gh, gh_sha256=DIG("0"))
        with self.assertRaises(self.v.Stop) as ctx:
            app(ARCHIVE, APP_SRC)
        self.assertEqual(str(ctx.exception), "GORV_BINARY_PIN_MISMATCH")
        self.assertFalse(os.path.exists(os.path.join(self.out, "argv")))
        app = self.app(gh_path=gh)
        Path(gh).write_text(Path(gh).read_text() + "\n# swapped\n")
        with self.assertRaises(self.v.Stop):
            app(ARCHIVE, APP_SRC)

    def test_gh_that_changes_identity_while_running_is_refused(self):
        def opener(path, uid, want, limit):
            fd, ident = self.v.open_pinned_binary(path, uid, want, limit)
            return fd, ident[:-1] + (ident[-1] + 1,)  # what was judged is no longer what ran
        self.stop("AUTH_APP_DENIED", self.app(open_binary=opener), ARCHIVE, APP_SRC)

    def test_output_flood_is_refused(self):
        gh = self.gh(tail="/usr/bin/head -c 5000000 /dev/zero | /usr/bin/tr '\\0' 'a'")
        self.stop("AUTH_APP_DENIED", self.app(gh_path=gh), ARCHIVE, APP_SRC)

    def test_timeout_is_a_denial(self):
        with unittest.mock.patch.object(self.m, "TIMEOUT", 1):
            self.stop("AUTH_APP_DENIED", self.app(gh_path=self.gh(tail="/usr/bin/sleep 5")), ARCHIVE, APP_SRC)

    def test_drop_identity_and_root_requirement_are_enforced(self):
        for uid, gid in ((0, 1), (1, 0), (True, 1), (-1, 1), ("1", 1)):
            self.stop("AUTH_DROP_IDENTITY_INVALID", self.app, drop_uid=uid, drop_gid=gid)
        self.stop("AUTH_ROOT_REQUIRED", self.app, required_euid=UID + 1)
        app = self.app()
        with unittest.mock.patch.object(os, "geteuid", return_value=UID + 7):
            self.stop("AUTH_ROOT_REQUIRED", app, ARCHIVE, APP_SRC)

    def test_bad_static_inputs_are_refused_before_anything_runs(self):
        for over in (dict(trusted_root=b""), dict(bundle=b""), dict(bundle=b"x" * 2_000_000),
                     dict(gh_sha256="nope"), dict(open_archive=None), dict(open_binary=None),
                     dict(source_repository_id=""), dict(source_repository_id="0"),
                     dict(source_repository_id="12ab"), dict(source_repository_id=424242),
                     dict(source_repository_id="1" * 21)):
            self.stop("AUTH_APP_INPUT_INVALID", self.app, **over)
        self.stop("AUTH_APP_INPUT_INVALID", self.app(), "x", APP_SRC)
        self.stop("AUTH_APP_INPUT_INVALID", self.app(), ARCHIVE, "short")

    def test_port_needs_two_callables(self):
        self.stop("AUTH_PORT_INVALID", self.m.port_for, ops_check=None, app_check=lambda a, b: True)
        self.stop("AUTH_PORT_INVALID", self.m.port_for, ops_check=lambda a, b: {}, app_check=None)

    def test_a_truthy_but_not_true_app_result_is_not_authentic(self):
        port = self.port(app_check=lambda a, s: 1)
        self.stop("AUTH_APP_DENIED", self.call, port=port)

    def test_ops_check_input_validation(self):
        self.stop("AUTH_OPS_INPUT_INVALID", self.m.ops_check_for, ops_signature=self.ops, ops_port=None,
                  signers=b"", ops_fingerprint="", signature=b"", policy_hash=POLICY)
        check = self.ops_check()
        self.stop("AUTH_OPS_INPUT_INVALID", check, "str", ARCHIVE)
        self.stop("AUTH_OPS_INPUT_INVALID", check, receipt(), "bad")

    def release(self, **over):
        gh = self.gh()
        auth = {"ops_allowed_signers": self.key["ops_allowed"], "drop_uid": UID or 30033, "drop_gid": GID or 30033}
        kw = dict(ops_signature=self.ops, go_verify=self.v, keygen=(self.keygen, sha(self.keygen)),
                  gh=(gh, sha(gh)), policy_hash=POLICY, authenticity=auth,
                  ops_signature_bytes=self.sign(self.key, receipt()), app_bundle=BUNDLE, trusted_root=ROOT,
                  open_archive=self.open_archive(), temp_root=self.tmp, go_fingerprint=self.key["fp"],
                  required_euid=UID, os_uid=UID)
        kw.update(over)
        return self.m.port_from_release(**kw)

    def test_release_assembly_end_to_end_with_real_ssh_keygen_and_the_oracle_gh(self):
        self.assertIs(self.call(port=self.release()), True)

    def test_release_assembly_denies_what_each_pinned_input_would_deny(self):
        cases = (
            dict(ops_signature_bytes=self.sign(self.other, receipt())),            # OPS signed by another key
            dict(ops_signature_bytes=self.sign(self.key, receipt(), ns=GO_NS)),    # GO namespace, not OPS
            dict(app_bundle=b'{"mediaType":"other"}'),                             # bundle not the pinned one
            dict(trusted_root=b"{}\n"),                                            # root not the pinned one
            dict(open_archive=self.open_archive(os.path.join(self.dir, "gone.zip"))),
        )
        for over in cases:
            with self.assertRaises(Exception):
                self.call(port=self.release(**over))

    def test_release_assembly_refuses_incomplete_or_malformed_release_material(self):
        self.stop("AUTH_RELEASE_INVALID", self.release, authenticity=[])
        self.stop("AUTH_RELEASE_INVALID", self.release, keygen=[self.keygen])
        self.stop("AUTH_RELEASE_INVALID", self.release, authenticity={"drop_uid": 1, "drop_gid": 1})
        self.stop("AUTH_DROP_IDENTITY_INVALID", self.release,
                  authenticity={"ops_allowed_signers": self.key["ops_allowed"], "drop_uid": 0, "drop_gid": 0})

    @unittest.skipUnless(REAL_GH, "gh not installed")
    def test_real_gh_refuses_a_forged_bundle_offline(self):
        gh = os.path.join(self.dir, "realgh")
        shutil.copy(REAL_GH, gh)
        os.chmod(gh, 0o755)
        app = self.app(gh_path=gh, bundle=b'{"mediaType":"application/vnd.dev.sigstore.bundle.v0.3+json"}')
        self.stop("AUTH_APP_DENIED", app, ARCHIVE, APP_SRC)



FX = Path(__file__).resolve().parent / "fixtures" / "sigstore-offline"
FX_ID = dict(REPO="sigstore/sigstore-js", WORKFLOW="sigstore/sigstore-js/.github/workflows/release.yml",
             ISSUER="https://token.actions.githubusercontent.com", DIGEST_ALG="sha512")
FX_SOURCE = "26d16513386ffaa790b1c32f927544f1322e4194"


@unittest.skipUnless(REAL_GH and os.path.exists(FX / "sigstore-js-2.1.0.tgz"), "real gh and fixtures required")
class RealGhOfflineTests(unittest.TestCase):
    """The pinned REAL gh, a REAL public Sigstore attestation, a local trusted root, no network needed:
    positive and negative, fail-closed. (Run once under `unshare -n` to prove no network is used.)"""

    def setUp(self):
        self.m = load("qa525-vnext-root-authenticity.py", "qa525_auth_real")
        self.v = load("qa525-vnext-root-go-verify.py", "qa525_auth_real_gorv")
        self.dir = tempfile.mkdtemp(prefix="authreal")
        self.addCleanup(shutil.rmtree, self.dir, True)
        os.chmod(self.dir, 0o755)
        self.tmp = os.path.join(self.dir, "t")
        os.mkdir(self.tmp, 0o700)
        self.gh = os.path.join(self.dir, "gh")
        shutil.copy(REAL_GH, self.gh)
        os.chmod(self.gh, 0o755)
        self.tgz = FX / "sigstore-js-2.1.0.tgz"
        self.bundle = (FX / "sigstore-js-2.1.0-bundle.json").read_bytes()
        self.root = (FX / "trusted-root-public-good.jsonl").read_bytes()
        for name, value in FX_ID.items():
            patcher = unittest.mock.patch.object(self.m, name, value)
            patcher.start()
            self.addCleanup(patcher.stop)

    def opener(self, path=None):
        def go():
            fd = os.open(path or self.tgz, os.O_RDONLY)
            return fd, os.fstat(fd).st_size
        return go

    def check(self, **over):
        kw = dict(open_binary=self.v.open_pinned_binary, gh_path=self.gh, gh_sha256=sha(self.gh),
                  trusted_root=self.root, bundle=self.bundle, open_archive=self.opener(), temp_root=self.tmp,
                  source_repository_id="495574555", drop_uid=UID or 30033, drop_gid=GID or 30033, required_euid=UID, os_uid=UID)
        kw.update(over)
        return self.m.app_check_for(**kw)

    @property
    def digest(self):
        return "sha256:" + hashlib.sha256(self.tgz.read_bytes()).hexdigest()

    def denied(self, check, digest=None, source=FX_SOURCE):
        with self.assertRaises(self.m.Stop) as ctx:
            check(digest or self.digest, source)
        self.assertEqual(str(ctx.exception), "AUTH_APP_DENIED")

    def test_positive_real_attestation_verifies_offline(self):
        self.assertIs(self.check()(self.digest, FX_SOURCE), True)

    def test_wrong_source_digest_workflow_issuer_or_repo_is_denied(self):
        self.denied(self.check(), source="0" * 40)
        for name, value in (("WORKFLOW", "sigstore/sigstore-js/.github/workflows/other.yml"),
                            ("ISSUER", "https://evil.example"), ("REPO", "evil/repo")):
            with unittest.mock.patch.object(self.m, name, value):
                self.denied(self.check())

    def test_bundle_for_another_artifact_is_denied(self):
        other = os.path.join(self.dir, "other.tgz")
        Path(other).write_bytes(self.tgz.read_bytes() + b"\0")
        digest = "sha256:" + hashlib.sha256(Path(other).read_bytes()).hexdigest()  # digest claim matches the bytes...
        self.denied(self.check(open_archive=self.opener(other)), digest=digest)    # ...but the attestation is not for them

    def test_tampered_bundle_or_missing_or_forged_root_is_denied(self):
        bad = bytearray(self.bundle)
        bad[len(bad) // 2] ^= 0x01
        self.denied(self.check(bundle=bytes(bad)))
        self.denied(self.check(bundle=b'{"mediaType":"application/vnd.dev.sigstore.bundle.v0.3+json"}'))
        self.denied(self.check(trusted_root=b'{"mediaType":"application/vnd.dev.sigstore.trustedroot+json;version=0.1"}\n'))
        for edit in (lambda d: d.update(certificateAuthorities=[]),               # no CA can vouch for the certificate
                     lambda d: d.update(tlogs=[]),                                 # no log can vouch for the entry
                     lambda d: [t["publicKey"].update(rawBytes=t["publicKey"]["rawBytes"][::-1]) for t in d.get("tlogs", [])]):
            lines = []
            for line in self.root.splitlines():
                doc = json.loads(line)
                edit(doc)
                lines.append(json.dumps(doc))
            self.denied(self.check(trusted_root=("\n".join(lines) + "\n").encode()))

    def test_renamed_or_reused_repository_name_is_not_enough_without_the_stable_id(self):
        self.denied(self.check(source_repository_id="495574556"))   # same name, different repository id
        self.denied(self.check(source_repository_id="71096353"))    # the OWNER id is not the repository id

    def test_the_wrong_digest_algorithm_is_denied(self):
        with unittest.mock.patch.object(self.m, "DIGEST_ALG", "sha256"):
            self.denied(self.check())  # subject is sha512: a sha256 policy must not accept it


if __name__ == "__main__":
    unittest.main()
