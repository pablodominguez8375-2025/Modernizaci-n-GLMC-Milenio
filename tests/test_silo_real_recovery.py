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



# Diagnostics are deliberately projections, never redacted raw provider output.
# Unknown values must collapse to a constant rather than becoming log content.
def _failure_category(result):
    if result is None:
        return "timeout-or-exec-unavailable"
    error = ((result.stderr or "") + "\n" + (result.stdout or "")).lower()
    for needle, label in (
        ("connection refused", "transport-refused"),
        ("connection reset", "transport-reset"),
        ("no such host", "dns-resolution"),
        ("temporary failure in name resolution", "dns-resolution"),
        ("not initialized", "not-initialized"),
        ("not ready", "not-ready"),
        ("context deadline exceeded", "timeout"),
        ("timed out", "timeout"),
        ("access denied", "access-denied"),
        ("invalid credentials", "invalid-credentials"),
        ("permission denied", "permission-denied"),
        ("already exists", "already-exists"),
        ("error response from daemon", "docker-daemon"),
        ("no space left on device", "disk-exhausted"),
        ("out of memory", "memory-exhausted"),
        ("unknown command", "cli-version"),
    ):
        if needle in error:
            return label
    return "other"


def _safe_exit(result):
    code = getattr(result, "returncode", None)
    return code if type(code) is int and -255 <= code <= 255 else None


def _mcli_error_code(result):
    # --json output differs between CLI versions. Never emit a value supplied
    # by the provider unless it is one of these known protocol codes.
    allowed = {"AccessDenied", "InvalidAccessKeyId", "SignatureDoesNotMatch",
               "ServerNotInitialized", "ServiceUnavailable", "InternalError",
               "RequestTimeTooSkewed", "BucketAlreadyOwnedByYou",
               "BucketAlreadyExists", "XMinioAdminNoSuchUser"}
    if result is not None:
        for line in (result.stdout or "").splitlines():
            try:
                entry = json.loads(line)
                code = entry["error"]["cause"]["error"]["Code"]
                if type(code) is str and code in allowed:
                    return code
            except (ValueError, KeyError, TypeError):
                pass
    return "unclassified"


def _provision_snapshot(container):
    # State-only inspection avoids Config.Env, command arguments and logs.
    state_result = run(["docker", "inspect", "--format", "{{json .State}}",
                        container], timeout=7, check=False)
    snapshot = {"container_state": "unknown", "oom_killed": None,
                "container_exit": None}
    if state_result is not None and state_result.returncode == 0:
        try:
            state = json.loads(state_result.stdout)
            if isinstance(state, dict):
                status = state.get("Status")
                if type(status) is str and status in {
                    "created", "running", "paused", "restarting", "removing", "exited", "dead"
                }:
                    snapshot["container_state"] = status
                oom = state.get("OOMKilled")
                if type(oom) is bool:
                    snapshot["oom_killed"] = oom
                code = state.get("ExitCode")
                if type(code) is int and -255 <= code <= 255:
                    snapshot["container_exit"] = code
        except (ValueError, TypeError):
            pass
    health = run(["docker", "exec", container, "silo", "healthcheck", "ready"],
                 timeout=7, check=False)
    snapshot["ready"] = health is not None and health.returncode == 0
    snapshot["health_exit"] = _safe_exit(health)
    return snapshot


def _provision(container, policy, admin, admin_password, app, app_password):
    # Silo's readiness probe may pass a few seconds before the admin API is
    # able to serve mcli requests on loaded GitHub runners. Retry ONLY the
    # ephemeral provisioning operations for a bounded period, and always
    # fail if none succeeds. Real IAM positive/negative checks stay mandatory.
    def cli(step, *args):
        last_result = None
        history = []
        for attempt in range(6):
            started = time.monotonic()
            last_result = run(
                ["docker", "exec", container, "mcli", "--json", *args],
                timeout=45, check=False
            )
            elapsed_ms = round((time.monotonic() - started) * 1000)
            if last_result is not None and last_result.returncode == 0:
                if history:
                    print(json.dumps({"event": "silo-provision-recovered",
                                      "step": step, "attempt": attempt + 1}), flush=True)
                return
            diagnostic = {
                "event": "silo-provision-failure", "step": step,
                "attempt": attempt + 1, "exit": _safe_exit(last_result),
                "elapsed_ms": elapsed_ms,
                "category": _failure_category(last_result),
                "mcli_code": _mcli_error_code(last_result),
                **_provision_snapshot(container),
            }
            history.append(diagnostic)
            print(json.dumps(diagnostic, sort_keys=True), flush=True)
            if attempt < 5:
                time.sleep(2)
        # Only allowlisted values; no exception/args/provider text interpolation.
        raise AssertionError(
            "Silo provisioning failed (six attempts): " +
            json.dumps({"step": step, "attempts": history}, sort_keys=True)
        )

    # Alias is local to the disposable Silo container, not a host config.
    cli("alias", "alias", "set", "admin", "http://127.0.0.1:9000",
        admin, admin_password)
    cli("admin-readiness", "admin", "info", "admin")
    cli("bucket-docs", "mb", "admin/" + BUCKET)
    cli("bucket-forbidden", "mb", "admin/" + FORBIDDEN_BUCKET)
    run(["docker", "cp", str(policy), container + ":/tmp/pmgm-policy.json"])
    cli("policy-create", "admin", "policy", "create", "admin",
        "pmgm-documents-only", "/tmp/pmgm-policy.json")
    cli("user-add", "admin", "user", "add", "admin", app, app_password)
    cli("policy-attach", "admin", "policy", "attach", "admin",
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
