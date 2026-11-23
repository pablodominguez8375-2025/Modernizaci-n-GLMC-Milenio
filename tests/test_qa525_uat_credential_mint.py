"""UAT credential minting: synthetic JWTs, fake token endpoint, private temp dir. No network, no root, no host."""
import base64
import datetime as dt
import importlib.util
import json
import os
from pathlib import Path
import stat
import tempfile
import unittest

BASE = Path(__file__).resolve().parents[1] / "scripts" / "qa"


def load(n, f):
    s = importlib.util.spec_from_file_location(n, BASE / f)
    m = importlib.util.module_from_spec(s)
    s.loader.exec_module(m)
    return m


mint = load("uatmint_t", "qa525-uat-credential-mint.py")
uat = load("uatreader_t", "qa525-vnext-root-schema-uat.py")
NOW = dt.datetime(2026, 11, 22, 21, 0, tzinfo=dt.timezone.utc)
UID = os.geteuid()


def b64(obj):
    return base64.urlsafe_b64encode(json.dumps(obj).encode()).decode().rstrip("=")


def jwt(**over):
    claims = {"iss": mint.ISSUER, "exp": int(NOW.timestamp()) + 7000, "pmgm_scope": "order",
              "aud": ["pmgm-api", "account"], "azp": "pmgm-web",
              "pmgm_role": ["platform_superadmin"], "jti": over.pop("jti", "a")}
    claims.update(over)
    return b64({"alg": "RS256"}) + "." + b64(claims) + "." + "sig"


def operator(**over):
    base = {"pmgm_role": ["grand_lodge_admin"], "pmgm_account_kind": "superuser", "jti": "b"}
    base.update(over)
    return jwt(**base)


class Endpoint:
    def __init__(self, master=None, oper=None, status=200):
        self.tokens = {"admin": master or jwt(), "pablo": oper or operator()}
        self.status = status
        self.calls = []

    def post(self, body):
        form = dict(x.split("=", 1) for x in body.decode().split("&"))
        self.calls.append(form)
        user = form["username"]
        if self.status != 200:
            return self.status, b'{"error":"invalid_grant","detail":"SECRET-BODY"}'
        return 200, json.dumps({"access_token": self.tokens[user]}).encode()


class MintTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.target = os.path.join(self.tmp.name, ".qa525-uat-credential")
        self.out = []
        self.asked = []

    def ask(self, prompt):
        self.asked.append(prompt)
        return "PASSWORD-SENTINEL-" + str(len(self.asked))

    def run_mint(self, endpoint=None, **kw):
        endpoint = endpoint or Endpoint()
        self.endpoint = endpoint
        self.api_calls = []
        status = kw.pop("api_status", 200)

        def get(path, token):
            self.api_calls.append((path, token))
            return status
        mint.run("admin", "pablo", ask=self.ask, post=endpoint.post, clock=lambda: NOW,
                 target=self.target, uid=UID, euid=UID, out=self.out.append, get=get, **kw)

    def assertStop(self, code, **kw):
        with self.assertRaises(mint.Stop) as cm:
            self.run_mint(**kw)
        self.assertEqual(str(cm.exception), code)
        self.assertFalse(os.path.lexists(self.target))
        self.assertEqual([n for n in os.listdir(self.tmp.name)], [])

    def test_success_file_shape_mode_and_reader_compat(self):
        self.run_mint()
        st = os.stat(self.target)
        self.assertEqual(stat.S_IMODE(st.st_mode), 0o600)
        data = json.loads(Path(self.target).read_bytes())
        self.assertEqual(set(data), {"master_token", "operator_token"})
        self.assertNotEqual(data["master_token"], data["operator_token"])
        self.assertLessEqual(st.st_size, 4096)
        self.assertEqual(os.listdir(self.tmp.name), [".qa525-uat-credential"])
        # the real UAT reader accepts the file we wrote
        uat._tokens(lambda: Path(self.target).read_bytes(), lambda: NOW)

    def test_output_never_contains_tokens_or_passwords(self):
        self.run_mint()
        text = "\n".join(self.out)
        data = json.loads(Path(self.target).read_bytes())
        for secret in list(data.values()) + ["PASSWORD-SENTINEL"]:
            self.assertNotIn(secret, text)
        self.assertEqual(len(self.asked), 2)
        for call in self.endpoint.calls:
            self.assertEqual((call["grant_type"], call["client_id"]), ("password", "pmgm-web"))

    def test_short_lifespan_rejected(self):
        self.assertStop("UATMINT_LIFESPAN_TOO_SHORT", endpoint=Endpoint(master=jwt(exp=int(NOW.timestamp()) + 300)))

    def test_boundary_just_under_minimum(self):
        self.assertStop("UATMINT_LIFESPAN_TOO_SHORT",
                        endpoint=Endpoint(oper=operator(exp=int(NOW.timestamp()) + mint.MIN_REMAINING)))

    def test_wrong_issuer(self):
        self.assertStop("UATMINT_ISSUER_INVALID", endpoint=Endpoint(master=jwt(iss="https://evil/realms/pmgm")))

    def test_missing_scope_claim(self):
        self.assertStop("UATMINT_CLAIM_SCOPE_MISSING", endpoint=Endpoint(oper=operator(pmgm_scope="organization")))

    def test_missing_account_kind_claim_is_not_operator(self):
        claims = json.loads(base64.urlsafe_b64decode(operator().split(".")[1] + "=="))
        del claims["pmgm_account_kind"]
        token = b64({"alg": "x"}) + "." + b64(claims) + ".sig"
        self.assertStop("UATMINT_NOT_OPERATOR", endpoint=Endpoint(oper=token))

    def test_operator_must_not_be_master(self):
        self.assertStop("UATMINT_NOT_OPERATOR", endpoint=Endpoint(oper=jwt(pmgm_account_kind="superuser", jti="z")))

    def test_master_needs_platform_superadmin(self):
        self.assertStop("UATMINT_NOT_MASTER", endpoint=Endpoint(master=jwt(pmgm_role=["grand_lodge_admin"])))

    def test_auth_rejected_hides_body(self):
        with self.assertRaises(mint.Stop) as cm:
            self.run_mint(endpoint=Endpoint(status=401))
        self.assertEqual(str(cm.exception), "UATMINT_AUTH_REJECTED")
        self.assertNotIn("SECRET-BODY", "\n".join(self.out))
        self.assertFalse(os.path.lexists(self.target))

    def test_existing_target_untouched_without_replace(self):
        Path(self.target).write_bytes(b"old")
        os.chmod(self.target, 0o600)
        with self.assertRaises(mint.Stop) as cm:
            self.run_mint()
        self.assertEqual(str(cm.exception), "UATMINT_TARGET_EXISTS")
        self.assertEqual(Path(self.target).read_bytes(), b"old")
        self.assertEqual(self.asked, [])  # fails before asking for passwords

    def test_replace_overwrites_atomically(self):
        Path(self.target).write_bytes(b"old")
        os.chmod(self.target, 0o600)
        self.run_mint(replace=True)
        self.assertIn(b"master_token", Path(self.target).read_bytes())
        self.assertEqual(os.listdir(self.tmp.name), [".qa525-uat-credential"])

    def test_symlink_target_refused_and_not_followed(self):
        victim = os.path.join(self.tmp.name, "victim")
        Path(victim).write_bytes(b"keep")
        os.symlink(victim, self.target)
        with self.assertRaises(mint.Stop):
            self.run_mint()
        self.assertEqual(Path(victim).read_bytes(), b"keep")

    def test_requires_root(self):
        with self.assertRaises(mint.Stop) as cm:
            mint.run("admin", "pablo", ask=self.ask, post=Endpoint().post, clock=lambda: NOW,
                     target=self.target, uid=UID + 1, euid=UID, out=self.out.append, get=lambda p, t: 200)
        self.assertEqual(str(cm.exception), "UATMINT_ROOT_REQUIRED")
        self.assertEqual(self.asked, [])

    def test_username_validation(self):
        with self.assertRaises(mint.Stop):
            mint.run("admin&x=1", "pablo", ask=self.ask, post=Endpoint().post, clock=lambda: NOW,
                     target=self.target, uid=UID, euid=UID, out=self.out.append, get=lambda p, t: 200)

    def test_empty_password_stops_before_network(self):
        endpoint = Endpoint()
        with self.assertRaises(mint.Stop):
            mint.run("admin", "pablo", ask=lambda p: "", post=endpoint.post, clock=lambda: NOW,
                     target=self.target, uid=UID, euid=UID, out=self.out.append, get=lambda p, t: 200)
        self.assertEqual(endpoint.calls, [])

    def test_identical_tokens_rejected(self):
        same = jwt(pmgm_account_kind="superuser", pmgm_role=["platform_superadmin"])
        with self.assertRaises(mint.Stop) as cm:
            self.run_mint(endpoint=Endpoint(master=same, oper=same))
        self.assertIn(str(cm.exception), ("UATMINT_NOT_OPERATOR", "UATMINT_TOKENS_EQUAL"))
        self.assertFalse(os.path.lexists(self.target))




class CheckOnlyAndCleanupTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.target = os.path.join(self.tmp.name, ".qa525-uat-credential")
        self.out = []

    def check(self, endpoint, status=200, **kw):
        calls = []

        def get(path, token):
            calls.append((path, token))
            return status
        mint.run("admin", "pablo", ask=lambda p: "PASSWORD-SENTINEL", post=endpoint.post, clock=lambda: NOW,
                 target=self.target, uid=UID + 1, euid=UID, out=self.out.append, check_only=True, get=get, **kw)
        return calls

    def test_check_only_writes_nothing_needs_no_root_and_accepts_short_tokens(self):
        short = Endpoint(master=jwt(exp=int(NOW.timestamp()) + 300), oper=operator(exp=int(NOW.timestamp()) + 300))
        calls = self.check(short)
        self.assertEqual([p for p, _ in calls], ["/api/session/me", "/api/session/me"])
        self.assertFalse(os.path.lexists(self.target))
        self.assertEqual(os.listdir(self.tmp.name), [])
        text = "\n".join(self.out)
        for _, token in calls:
            self.assertNotIn(token, text)
        self.assertIn("No se escribio ningun archivo", text)

    def test_check_only_still_requires_claims(self):
        with self.assertRaises(mint.Stop) as cm:
            self.check(Endpoint(oper=operator(pmgm_scope="organization")))
        self.assertEqual(str(cm.exception), "UATMINT_CLAIM_SCOPE_MISSING")

    def test_check_only_rejects_expired_token(self):
        with self.assertRaises(mint.Stop) as cm:
            self.check(Endpoint(master=jwt(exp=int(NOW.timestamp()) + 30)))
        self.assertEqual(str(cm.exception), "UATMINT_LIFESPAN_TOO_SHORT")

    def test_check_only_api_rejection_is_fixed_code(self):
        with self.assertRaises(mint.Stop) as cm:
            self.check(Endpoint(), status=403)
        self.assertEqual(str(cm.exception), "UATMINT_API_REJECTED_403")

    def test_check_only_requires_get_and_forbids_replace(self):
        with self.assertRaises(mint.Stop):
            mint.run("admin", "pablo", ask=lambda p: "x", post=Endpoint().post, clock=lambda: NOW,
                     target=self.target, uid=UID, euid=UID, out=self.out.append, check_only=True)
        with self.assertRaises(mint.Stop):
            self.check(Endpoint(), replace=True)

    def test_replace_refuses_symlink_directory_and_hardlink(self):
        victim = os.path.join(self.tmp.name, "victim")
        Path(victim).write_bytes(b"keep")
        os.symlink(victim, self.target)
        with self.assertRaises(mint.Stop) as cm:
            mint.write_atomic(b"{}", self.target, True, UID)
        self.assertEqual(str(cm.exception), "UATMINT_TARGET_UNSAFE")
        self.assertEqual(Path(victim).read_bytes(), b"keep")
        os.unlink(self.target)
        os.link(victim, self.target)
        with self.assertRaises(mint.Stop) as cm:
            mint.write_atomic(b"{}", self.target, True, UID)
        self.assertEqual(str(cm.exception), "UATMINT_TARGET_UNSAFE")
        os.unlink(self.target)
        os.mkdir(self.target)
        with self.assertRaises(mint.Stop) as cm:
            mint.write_atomic(b"{}", self.target, True, UID)
        self.assertEqual(str(cm.exception), "UATMINT_TARGET_UNSAFE")
        os.rmdir(self.target)
        Path(self.target).write_bytes(b"x")  # regular file but mode 0644
        os.chmod(self.target, 0o644)
        with self.assertRaises(mint.Stop) as cm:
            mint.write_atomic(b"{}", self.target, True, UID)
        self.assertEqual(str(cm.exception), "UATMINT_TARGET_UNSAFE")

    def test_remove_deletes_plain_file_only(self):
        Path(self.target).write_bytes(b"secret")
        os.chmod(self.target, 0o600)
        mint.remove_credential(self.target, uid=UID, euid=UID, out=self.out.append)
        self.assertIn("NO revoca", "\n".join(self.out))
        self.assertFalse(os.path.lexists(self.target))
        mint.remove_credential(self.target, uid=UID, euid=UID, out=self.out.append)  # idempotent
        victim = os.path.join(self.tmp.name, "victim")
        Path(victim).write_bytes(b"keep")
        os.symlink(victim, self.target)
        with self.assertRaises(mint.Stop):
            mint.remove_credential(self.target, uid=UID, euid=UID, out=self.out.append)
        self.assertEqual(Path(victim).read_bytes(), b"keep")

    def test_remove_requires_root(self):
        Path(self.target).write_bytes(b"secret")
        os.chmod(self.target, 0o600)
        with self.assertRaises(mint.Stop) as cm:
            mint.remove_credential(self.target, uid=UID + 1, euid=UID, out=self.out.append)
        self.assertEqual(str(cm.exception), "UATMINT_ROOT_REQUIRED")
        self.assertTrue(os.path.exists(self.target))

    def test_main_argument_rules(self):
        self.assertEqual(mint.main(["--remove", "--check-only"]), 2)
        self.assertEqual(mint.main(["--check-only"]), 2)


class HardeningTests(MintTests):
    """Reviewer findings: audience/azp/nbf, API gate in mint mode, unsafe directory, link/unlink failure."""

    def test_missing_audience_or_wrong_azp_or_future_nbf(self):
        self.assertStop("UATMINT_AUDIENCE_INVALID", endpoint=Endpoint(master=jwt(aud=["account"])))
        self.assertStop("UATMINT_CLIENT_INVALID", endpoint=Endpoint(oper=operator(azp="other")))
        self.assertStop("UATMINT_NOT_YET_VALID",
                        endpoint=Endpoint(master=jwt(nbf=int(NOW.timestamp()) + 3600)))

    def test_mint_mode_requires_api_acceptance_before_writing(self):
        self.assertStop("UATMINT_API_REJECTED_401", api_status=401)
        self.assertEqual(len(self.api_calls), 1)  # stops at the first rejected token

    def test_mint_mode_calls_api_for_both_tokens(self):
        self.run_mint()
        self.assertEqual([p for p, _ in self.api_calls], ["/api/session/me", "/api/session/me"])

    def test_unsafe_directory_refused(self):
        os.chmod(self.tmp.name, 0o777)
        with self.assertRaises(mint.Stop) as cm:
            self.run_mint()
        self.assertEqual(str(cm.exception), "UATMINT_DIRECTORY_UNSAFE")
        os.chmod(self.tmp.name, 0o700)

    def test_unlink_failure_after_link_leaves_no_credential(self):
        real = os.unlink
        state = {"hit": False}

        def flaky(path, *a, **k):
            if not state["hit"] and ".tmp-" in str(path):
                state["hit"] = True
                raise OSError("boom")
            return real(path, *a, **k)
        os.unlink = flaky
        try:
            with self.assertRaises(mint.Stop) as cm:
                self.run_mint()
        finally:
            os.unlink = real
        self.assertEqual(str(cm.exception), "UATMINT_WRITE_FAILED")
        self.assertFalse(os.path.lexists(self.target))
        self.assertEqual(os.listdir(self.tmp.name), [])


if __name__ == "__main__":
    unittest.main()
