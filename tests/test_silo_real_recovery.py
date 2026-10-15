"""#470: restauración lógica de objeto S3 entre servidores Silo efímeros reales.

No es respaldo completo de servidor: NO preserva metadata del objeto,
versionado/retención, políticas IAM ni KMS. Datos 100% sintéticos.
"""
import hashlib
import ipaddress
import json
import os
from pathlib import Path
import secrets
import shutil
import tempfile
import time
import unittest
import uuid

from test_silo_real_iam import (
    BUCKET, FORBIDDEN_BUCKET, IMAGE, POLICY, run, s3_request
)


def _instance_address(container, network):
    inspection = json.loads(run(["docker", "inspect", container]).stdout)[0]
    address = inspection["NetworkSettings"]["Networks"][network]["IPAddress"]
    parsed = ipaddress.ip_address(address)
    if not isinstance(parsed, ipaddress.IPv4Address) or not parsed.is_private:
        raise AssertionError("Silo no usa una IP privada IPv4 de laboratorio.")
    return address


def _start(container, network, root, password):
    run([
        "docker", "run", "-d", "--name", container, "--network", network,
        "-e", "MINIO_ROOT_USER=" + root,
        "-e", "MINIO_ROOT_PASSWORD=" + password,
        IMAGE, "server", "/data", "--console-address", ":9001"
    ], timeout=90)
    for _ in range(40):
        state = run(["docker", "exec", container, "silo",
                     "healthcheck", "ready"], timeout=7, check=False)
        if state is not None and state.returncode == 0:
            return _instance_address(container, network)
        time.sleep(2)
    raise AssertionError("La instancia Silo efímera no inicia a tiempo.")


def _provision(container, policy, admin, admin_password, app, app_password):
    def cli(*args):
        return run(["docker", "exec", container, "mcli", *args], timeout=45)

    # Alias solo dentro del contenedor efímero.
    cli("alias", "set", "admin", "http://127.0.0.1:9000",
        admin, admin_password)
    cli("mb", "admin/" + BUCKET)
    cli("mb", "admin/" + FORBIDDEN_BUCKET)
    run(["docker", "cp", str(policy), container + ":/tmp/pmgm-policy.json"])
    cli("admin", "policy", "create", "admin",
        "pmgm-documents-only", "/tmp/pmgm-policy.json")
    cli("admin", "user", "add", "admin", app, app_password)
    cli("admin", "policy", "attach", "admin",
        "pmgm-documents-only", "--user", app)


class SiloObjectRecoveryLaboratoryTests(unittest.TestCase):
    def test_silo_recovers_synthetic_document_after_instance_loss(self):
        if not shutil.which("docker"):
            if os.getenv("GITHUB_ACTIONS"):
                self.fail("CI debe tener Docker para la prueba real de recuperación S3.")
            self.skipTest("Docker no disponible; no se falsifica una prueba de recuperación.")

        suffix = uuid.uuid4().hex[:11]
        network = "pmgm-recover-ci-" + suffix
        original = "pmgm-recover-before-" + suffix
        restored = "pmgm-recover-after-" + suffix
        root_before = "root" + secrets.token_hex(8)
        root_after = "root" + secrets.token_hex(8)
        app_before = "docs" + secrets.token_hex(8)
        app_after = "docs" + secrets.token_hex(8)
        pw_before = secrets.token_urlsafe(28)
        pw_after = secrets.token_urlsafe(28)
        app_pw_before = secrets.token_urlsafe(28)
        app_pw_after = secrets.token_urlsafe(28)
        object_key = "synthetic/recovery-" + suffix + ".bin"
        payload = secrets.token_bytes(128 * 1024)

        try:
            with tempfile.TemporaryDirectory(prefix="pmgm-ci-recover-") as tmp:
                backup = Path(tmp) / "synthetic-object.bin"
                policy = Path(tmp) / "policy.json"
                policy.write_text(json.dumps(POLICY), encoding="utf-8")
                run(["docker", "network", "create", network])
                run(["docker", "pull", IMAGE], timeout=240)

                first_ip = _start(original, network, root_before, pw_before)
                _provision(original, policy, root_before, pw_before,
                           app_before, app_pw_before)

                status, _ = s3_request(
                    first_ip, "PUT", BUCKET, object_key,
                    app_before, app_pw_before, payload
                )
                self.assertEqual(200, status, "No se almacenó el documento inicial.")
                status, original_bytes = s3_request(
                    first_ip, "GET", BUCKET, object_key,
                    app_before, app_pw_before
                )
                self.assertEqual(200, status)
                expected_sha = hashlib.sha256(payload).hexdigest()
                self.assertEqual(expected_sha,
                                 hashlib.sha256(original_bytes).hexdigest())

                # Respaldo LÓGICO del objeto en un directorio temporal del runner.
                # El contenido es sintético y se eliminará en finally/context manager.
                backup.write_bytes(original_bytes)
                self.assertEqual(expected_sha,
                                 hashlib.sha256(backup.read_bytes()).hexdigest())

                # Falla total intencional: no hay volúmenes compartidos ni estado
                # de la antigua instancia; se remueve ANTES de crear la segunda.
                run(["docker", "rm", "-f", original], timeout=40)
                missing = run(["docker", "inspect", original], check=False)
                self.assertIsNotNone(missing)
                self.assertNotEqual(0, missing.returncode)

                # Nuevo servidor/identidades, misma versión fijada por digest,
                # recreando expresamente política y bucket desde fuente declarativa.
                second_ip = _start(restored, network, root_after, pw_after)
                _provision(restored, policy, root_after, pw_after,
                           app_after, app_pw_after)

                status, _ = s3_request(
                    second_ip, "HEAD", BUCKET, object_key,
                    app_after, app_pw_after
                )
                self.assertEqual(404, status,
                    "La segunda instancia no debe heredar datos sin restauración.")

                # Comprobar integridad de respaldo ANTES de subirlo a servidor nuevo.
                recovered = backup.read_bytes()
                self.assertEqual(expected_sha, hashlib.sha256(recovered).hexdigest())
                status, _ = s3_request(
                    second_ip, "PUT", BUCKET, object_key,
                    app_after, app_pw_after, recovered
                )
                self.assertEqual(200, status, "Restauración lógica del objeto falló.")
                status, restored_bytes = s3_request(
                    second_ip, "GET", BUCKET, object_key,
                    app_after, app_pw_after
                )
                self.assertEqual(200, status)
                self.assertEqual(payload, restored_bytes)
                self.assertEqual(expected_sha,
                                 hashlib.sha256(restored_bytes).hexdigest())
                status, _ = s3_request(
                    second_ip, "HEAD", BUCKET, object_key,
                    app_after, app_pw_after
                )
                self.assertEqual(200, status)

                # La nueva instancia jamás acepta la cuenta vieja.
                old_status, _ = s3_request(
                    second_ip, "GET", BUCKET, object_key,
                    app_before, app_pw_before
                )
                self.assertEqual(403, old_status,
                    "Credenciales de la instancia perdida siguieron válidas.")

                # Restaurar no debe ampliar permisos.
                for method in ("GET", "PUT", "DELETE"):
                    with self.subTest(method=method):
                        code, _ = s3_request(
                            second_ip, method, FORBIDDEN_BUCKET,
                            "synthetic-unauthorized.bin",
                            app_after, app_pw_after,
                            b"nonsecret" if method == "PUT" else b""
                        )
                        self.assertEqual(403, code,
                            "Cuenta documental accedió a un bucket ajeno.")
                admin_denial = run(
                    ["docker", "exec", restored, "mcli", "alias", "set",
                     "documents", "http://127.0.0.1:9000",
                     app_after, app_pw_after], check=False
                )
                self.assertEqual(0, admin_denial.returncode)
                admin_check = run(
                    ["docker", "exec", restored, "mcli",
                     "admin", "info", "documents"], check=False
                )
                self.assertIsNotNone(admin_check)
                self.assertNotEqual(0, admin_check.returncode,
                                    "Permisos administrativos indebidos tras restore.")
        finally:
            for container in (original, restored):
                run(["docker", "rm", "-f", container], check=False, timeout=40)
            run(["docker", "network", "rm", network], check=False, timeout=40)


if __name__ == "__main__":
    unittest.main()
