"""Test support: an EPHEMERAL ssh-ed25519 key pair (never a real key) that signs canonical G6 GOs with the real
`ssh-keygen -Y sign`, plus the verifier config the driver takes in `deps['go_cfg']`.  Hermetic: tmp dir only."""
import hashlib
import json
import os
import secrets
import shutil
import subprocess
import tempfile

KEYGEN = "/usr/bin/ssh-keygen"
NAMESPACE = "qa525-go-g6-v1@pmgm"


def have_keygen():
    return os.path.isfile(KEYGEN) and shutil.which("ssh-keygen") is not None


class Signer:
    def __init__(self, cut, namespace=NAMESPACE):
        self.cut, self.namespace = cut, namespace
        self.dir = tempfile.mkdtemp(prefix="g6go-test-")
        os.chmod(self.dir, 0o700)
        self.key = os.path.join(self.dir, "ops")
        subprocess.run([KEYGEN, "-q", "-t", "ed25519", "-N", "", "-C", "test", "-f", self.key], check=True)
        pub = open(self.key + ".pub").read().split()
        self.signers = ('qa525-ops namespaces="%s" %s %s\n' % (NAMESPACE, pub[0], pub[1])).encode()
        self.fingerprint = cut._g6_signers_fingerprint(self.signers)
        self.work = os.path.join(self.dir, "work")
        os.mkdir(self.work, 0o700)
        self.n = 0

    def cfg(self, **over):
        st = os.stat(KEYGEN)
        cfg = {"keygen_path": KEYGEN, "keygen_sha256": "sha256:" + hashlib.sha256(open(KEYGEN, "rb").read()).hexdigest(),
               "signers": self.signers, "fingerprint": self.fingerprint, "temp_root": self.work,
               "required_euid": os.geteuid(), "binary_uid": st.st_uid, "temp_uid": os.geteuid()}
        cfg.update(over)
        return cfg

    def go(self, command, op_sha, driver_hex, now, ttl=600, nonce=None, extra=None, raw=None, key=None, namespace=None, **over):
        """Write go.json + go.json.sig (0600) and return their paths.  `raw` = exact bytes to sign (non-canonical cases)."""
        self.n += 1
        body = {"command": command, "op_sha256": op_sha, "nonce": nonce or secrets.token_hex(16),
                "expires_at": now + ttl, "driver_sha256": driver_hex}
        body.update(over)
        body.update(extra or {})
        data = raw if raw is not None else self.cut.g6_go_canonical(body)
        path = os.path.join(self.dir, "go%d.json" % self.n)
        with open(path, "wb") as fh:
            fh.write(data)
        os.chmod(path, 0o600)
        subprocess.run([KEYGEN, "-Y", "sign", "-q", "-f", key or self.key, "-n", namespace or self.namespace, path],
                       check=True, stdin=subprocess.DEVNULL)
        os.chmod(path + ".sig", 0o600)
        return path, path + ".sig"

    def other_key(self):
        p = os.path.join(self.dir, "other%d" % self.n)
        subprocess.run([KEYGEN, "-q", "-t", "ed25519", "-N", "", "-C", "x", "-f", p], check=True)
        return p

    def close(self):
        shutil.rmtree(self.dir, ignore_errors=True)
