"""Issue #470: IAM S3 real contra SILO corregido en contenedor efímero.

Solo datos sintéticos. No publicar puertos, crear volúmenes, usar secretos reales
ni aceptar S3Mock como prueba de ACL. Fallar en CI si pull o S3 real fallan.
"""
import datetime
import hashlib
import hmac
import ipaddress
import json
import os
from pathlib import Path
import secrets
import shutil
import subprocess
import tempfile
import time
import unittest
import urllib.error
import urllib.parse
import urllib.request
import uuid

IMAGE = ("docker.io/pgsty/silo@sha256:"
         "aaac6bce13eb99316b3c88bd20143fc3c38ab607585256a411a095c44ee57f62")
BUCKET = "pmgm-documents"
FORBIDDEN_BUCKET = "pmgm-iam-forbidden"

# Compare with the contract of PR #479 without reading unmerged policy files.
POLICY = {
    "Version": "2012-10-17",
    "Statement": [
        {
            "Sid": "BucketListingForNotFoundHead", "Effect": "Allow",
            "Action": ["s3:ListBucket"], "Resource": ["arn:aws:s3:::pmgm-documents"]
        },
        {
            "Sid": "AllowDocumentObjectsOnly", "Effect": "Allow",
            "Action": ["s3:GetObject", "s3:PutObject", "s3:DeleteObject"],
            "Resource": ["arn:aws:s3:::pmgm-documents/*"]
        }
    ]
}


def run(args, *, timeout=60, check=True):
    try:
        r = subprocess.run(args, capture_output=True, text=True,
                           timeout=timeout, check=False)
    except (OSError, subprocess.TimeoutExpired):
        if check:
            raise AssertionError("No fue posible ejecutar un paso Docker Silo IAM.") from None
        return None
    if check and r.returncode:
        # Nunca imprimir los argumentos (pueden contener contraseñas),
        # ni stdout/stderr originales del proveedor.
        raise AssertionError("Fallo un paso Docker/S3 IAM real en laboratorio.")
    return r


def signature(secret, date, region, service, string_to_sign):
    key = ("AWS4" + secret).encode()
    for piece in (date, region, service, "aws4_request"):
        key = hmac.new(key, piece.encode(), hashlib.sha256).digest()
    return hmac.new(key, string_to_sign.encode(), hashlib.sha256).hexdigest()


def s3_request(ip, method, bucket, key, access, secret, body=b""):
    """SigV4 S3 REST sin dependencias de boto3 ni credenciales AWS reales."""
    host = ip + ":9000"
    now = datetime.datetime.now(datetime.timezone.utc)
    amz_date = now.strftime("%Y%m%dT%H%M%SZ")
    short_date = now.strftime("%Y%m%d")
    scope = short_date + "/us-east-1/s3/aws4_request"
    payload_hash = hashlib.sha256(body).hexdigest()
    canonical_uri = "/" + urllib.parse.quote(bucket + "/" + key, safe="/-_.~")
    canonical_headers = (
        "host:" + host + "\n" +
        "x-amz-content-sha256:" + payload_hash + "\n" +
        "x-amz-date:" + amz_date + "\n"
    )
    signed_headers = "host;x-amz-content-sha256;x-amz-date"
    canonical_request = (
        method + "\n" + canonical_uri + "\n\n" + canonical_headers +
        "\n" + signed_headers + "\n" + payload_hash
    )
    to_sign = (
        "AWS4-HMAC-SHA256\n" + amz_date + "\n" + scope + "\n" +
        hashlib.sha256(canonical_request.encode()).hexdigest()
    )
    authorization = (
        "AWS4-HMAC-SHA256 Credential=" + access + "/" + scope +
        ", SignedHeaders=" + signed_headers +
        ", Signature=" + signature(secret, short_date, "us-east-1", "s3", to_sign)
    )
    req = urllib.request.Request(
        "http://" + host + canonical_uri,
        data=body if method in ("PUT", "POST") else None,
        method=method,
        headers={
            "Host": host,
            "X-Amz-Date": amz_date,
            "X-Amz-Content-Sha256": payload_hash,
            "Authorization": authorization
        }
    )
    opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))
    try:
        with opener.open(req, timeout=8) as response:
            return response.status, response.read() if method != "HEAD" else b""
    except urllib.error.HTTPError as err:
        return err.code, b""


class SiloRealIamLaboratoryTests(unittest.TestCase):
    def test_patched_silo_enforces_document_bucket_policy(self):
        if not shutil.which("docker"):
            if os.getenv("GITHUB_ACTIONS"):
                self.fail("Docker es obligatorio para la evidencia IAM real de CI.")
            self.skipTest("Solo se ejecuta prueba real en runner con Docker.")

        suffix = uuid.uuid4().hex[:11]
        network, server = "pmgm-silo-ci-" + suffix, "pmgm-silo-ci-srv-" + suffix
        root_user = "root" + secrets.token_hex(8)
        app_user = "docs" + secrets.token_hex(8)
        root_pw = secrets.token_urlsafe(30)
        app_pw = secrets.token_urlsafe(30)

        try:
            with tempfile.TemporaryDirectory(prefix="pmgm-silo-iam-") as tmp:
                policy = Path(tmp) / "silo-policy.json"
                policy.write_text(json.dumps(POLICY), encoding="utf-8")
                run(["docker", "network", "create", network])
                run(["docker", "pull", IMAGE], timeout=240)
                run([
                    "docker", "run", "--detach", "--name", server,
                    "--network", network,
                    "-e", "MINIO_ROOT_USER=" + root_user,
                    "-e", "MINIO_ROOT_PASSWORD=" + root_pw,
                    IMAGE, "server", "/data", "--console-address", ":9001"
                ], timeout=90)
                ready = False
                for _ in range(40):
                    r = run(["docker", "exec", server, "silo",
                             "healthcheck", "ready"], check=False, timeout=7)
                    if r is not None and r.returncode == 0:
                        ready = True
                        break
                    time.sleep(2)
                self.assertTrue(ready, "Servidor Silo corregido no alcanza readiness.")

                inspect = json.loads(run(["docker", "inspect", server]).stdout)[0]
                ip = inspect["NetworkSettings"]["Networks"][network]["IPAddress"]
                self.assertTrue(ipaddress.ip_address(ip).is_private)
                def cli(*cmd, check=True):
                    return run(["docker", "exec", server, "mcli", *cmd],
                               timeout=45, check=check)

                cli("alias", "set", "admin", "http://127.0.0.1:9000",
                    root_user, root_pw)
                cli("mb", "admin/" + BUCKET)
                cli("mb", "admin/" + FORBIDDEN_BUCKET)
                run(["docker", "cp", str(policy), server + ":/tmp/policy.json"])
                cli("admin", "policy", "create", "admin",
                    "pmgm-documents-app", "/tmp/policy.json")
                cli("admin", "user", "add", "admin", app_user, app_pw)
                cli("admin", "policy", "attach", "admin", "pmgm-documents-app",
                    "--user", app_user)
                cli("alias", "set", "documents", "http://127.0.0.1:9000",
                    app_user, app_pw)

                # La cuenta documental tiene operaciones sobre SU bucket.
                status, _ = s3_request(
                    ip, "PUT", BUCKET, "iam-synthetic.txt",
                    app_user, app_pw, b"synthetic-document-470"
                )
                self.assertEqual(200, status, "PutObject documental denegado.")
                status, _ = s3_request(
                    ip, "HEAD", BUCKET, "iam-synthetic.txt", app_user, app_pw
                )
                self.assertEqual(200, status, "HeadObject documental denegado.")
                status, content = s3_request(
                    ip, "GET", BUCKET, "iam-synthetic.txt", app_user, app_pw
                )
                self.assertEqual(200, status, "GetObject documental denegado.")
                self.assertEqual(b"synthetic-document-470", content)
                status, _ = s3_request(
                    ip, "HEAD", BUCKET, "missing-synthetic.txt", app_user, app_pw
                )
                self.assertEqual(404, status,
                    "HeadObject ausente debe ser 404 (ListBucket limitado).")
                self.assertEqual(0, cli("ls", "documents/" + BUCKET).returncode)
                status, _ = s3_request(
                    ip, "DELETE", BUCKET, "iam-synthetic.txt", app_user, app_pw
                )
                self.assertEqual(204, status, "DeleteObject documental denegado.")

                # Otra carpeta/bucket y superficies administrativas NUNCA.
                for method in ("PUT", "GET", "DELETE"):
                    with self.subTest(method=method):
                        status, _ = s3_request(
                            ip, method, FORBIDDEN_BUCKET, "no-access.txt",
                            app_user, app_pw, b"fake" if method == "PUT" else b""
                        )
                        self.assertEqual(403, status,
                            "IAM concedió acceso a bucket ajeno.")
                self.assertNotEqual(
                    0, cli("ls", "documents/" + FORBIDDEN_BUCKET,
                           check=False).returncode
                )
                # Silo puede responder 200 a ListBuckets devolviendo únicamente
                # los buckets visibles para este usuario. La condición de
                # confidencialidad es NO enumerar ni revelar buckets ajenos,
                # independientemente del código de retorno del cliente.
                bucket_listing = cli("ls", "documents", check=False)
                self.assertIsNotNone(bucket_listing)
                if bucket_listing.returncode == 0:
                    self.assertNotIn(
                        FORBIDDEN_BUCKET, bucket_listing.stdout,
                        "Un listado global expuso el bucket prohibido."
                    )
                self.assertNotEqual(
                    0, cli("admin", "info", "documents", check=False).returncode
                )
                self.assertEqual(
                    0, cli("ls", "admin/" + FORBIDDEN_BUCKET).returncode,
                    "El bucket ajeno debe existir para que la negativa sea válida."
                )
        finally:
            run(["docker", "rm", "-f", server], check=False, timeout=30)
            run(["docker", "network", "rm", network], check=False, timeout=30)


if __name__ == "__main__":
    unittest.main()
