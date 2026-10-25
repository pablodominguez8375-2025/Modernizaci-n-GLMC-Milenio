#!/usr/bin/env python3
"""Real Docker exercise, ONLY disposable GitHub-hosted runner, never glmdch.

Independent evidence/health stubs are deliberately synthetic here: this tests
the transport/rollback, not Sigstore, G6 or public HTTPS acceptance.
"""
import hashlib
from dataclasses import asdict, replace
import importlib.util
import json
import os
from pathlib import Path
import shutil
import socket
import sys
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]


def load(name, filename):
    spec = importlib.util.spec_from_file_location(name, ROOT / "scripts/qa" / filename)
    value = importlib.util.module_from_spec(spec)
    sys.modules[name] = value
    spec.loader.exec_module(value)
    return value


def main():
    if (os.environ.get("GITHUB_ACTIONS") != "true" or os.geteuid() != 0
            or socket.gethostname() == "glmdch"):
        raise SystemExit("CI_DISPOSABLE_RUNNER_REQUIRED")
    mod = load("docker_ci_adapter", "qa-lite-compose-adapter.py")
    tx = load("docker_ci_transaction", "qa-lite-web-transaction.py")
    builder = load("docker_ci_builder", "qa-lite-web-build.py")
    installer = load("docker_ci_installer", "qa-lite-install.py")
    if mod.docker(["ps", "-a", "--filter", "label=com.docker.compose.project=" + mod.PROJECT,
                   "--format", "{{.ID}}"]):
        raise SystemExit("REFUSE_EXISTING_PROJECT")
    work = Path("/root/pmgm-qa-adapter-ci")
    if work.exists():
        raise SystemExit("REFUSE_EXISTING_TEST_DIRECTORY")
    work.mkdir(mode=0o700)
    compose = work / "compose.json"
    env = work / "synthetic.env"
    env.write_text("")
    old_sha, new_sha = "c" * 40, "d" * 40
    base = ["compose", "--project-name", mod.PROJECT, "--project-directory", str(work),
            "--env-file", str(env), "--file", str(compose)]
    try:
        # Same nginx family as the existing frontend runtime; no app or real data.
        mod.docker(["pull", "nginx:stable-alpine"])
        (work / "Dockerfile").write_text(
            'FROM nginx:stable-alpine\nLABEL org.opencontainers.image.revision="' + old_sha + '"\n')
        mod.docker(["build", "--tag", "pmgm-adapter-ci-old", str(work)])
        old = mod.docker(["image", "inspect", "--format", "{{.Id}}", "pmgm-adapter-ci-old"])
        package = work / new_sha
        (package / "frontend").mkdir(parents=True)
        (package / "frontend/Dockerfile").write_text("FROM nginx:stable-alpine\n")
        build_config = work / "build-inputs.json"
        build_config.write_text(json.dumps({"services": {"web": {"build": {
            "context": builder.OLD_CONTEXT, "args": {
                "VITE_USE_MOCKS": "false", "VITE_API_BASE_URL": "",
                "VITE_OIDC_AUTHORITY": builder.AUTHORITY, "VITE_OIDC_CLIENT_ID": "pmgm-web"}
        }}}}))
        build_config.chmod(0o600)
        with patch.object(builder.socket, "gethostname", return_value="glmdch"), \
                patch.object(builder, "COMPOSE", build_config), \
                patch.object(builder, "COMPOSE_SHA256", hashlib.sha256(build_config.read_bytes()).hexdigest()):
            prepared = builder.prepare_web(package, new_sha, lambda *a: True)
        assert prepared["status"] == "LOCAL_WEB_IMAGE_PREPARED_NOT_DEPLOYED"
        new = prepared["web_image_id"]
        compose.write_text(json.dumps({"services": {
            "web": {"image": old},
            "postgres": {"image": old, "volumes": ["synthetic_pg:/synthetic"]},
            "keycloak": {"image": old},
            "minio": {"image": old, "volumes": ["synthetic_objects:/synthetic"]},
            **{name: {"image": old, "entrypoint": ["/bin/sh"],
                      "command": ["-c", "exit 0"], "restart": "no"}
               for name in mod.COMPLETED_JOBS},
        }, "volumes": {"synthetic_pg": {}, "synthetic_objects": {}}}))
        mod.docker([*base, "up", "--detach", "--wait", "--wait-timeout", "60",
                    "web", "postgres", "keycloak", "minio"])
        mod.docker([*base, "up", "--detach", *sorted(mod.COMPLETED_JOBS)])
        for name in sorted(mod.COMPLETED_JOBS):
            cid = mod.docker([*base, "ps", "--all", "--quiet", name])
            assert mod.docker(["wait", cid]) == "0"
            record = mod.metadata()[name]
            assert record[2] == "exited" and record[11] == 0
        binding = mod.Binding(str(work), (str(compose),), str(env),
                              tuple((str(p), hashlib.sha256(p.read_bytes()).hexdigest())
                                    for p in (compose, env)), str(work))
        plan = tx.WebReleasePlan(mod.PROJECT, old_sha, new_sha, old, new, "e" * 64)
        dependencies = {k: v for k, v in mod.metadata().items() if k != "web"}
        adapter = mod.ComposeWebAdapter(binding, lambda p: True, lambda: True)
        # Hostname patch scoped ONLY to disposable runner transport exercise.
        with patch.object(mod.socket, "gethostname", return_value="glmdch"):
            with adapter.exclusive():
                result = tx.coordinate(plan, adapter)
                assert result["status"] == "WEB_HEALTHY_PENDING_AUTHENTICATED_QA"
        assert mod.metadata()["web"][1] == new
        marker = json.loads(mod.docker(["exec", mod.metadata()["web"][0], "cat", "/usr/share/nginx/html/qa-version.json"]))
        assert marker == {"environment": "QA_SYNTHETIC", "source_sha": new_sha}
        assert dependencies == {k: v for k, v in mod.metadata().items() if k != "web"}
        # Real second recreation fails synthetic health, real rollback to new.
        reverse = tx.WebReleasePlan(mod.PROJECT, new_sha, old_sha, new, old, "e" * 64)
        health = iter([False, True])
        adapter = mod.ComposeWebAdapter(binding, lambda p: True, lambda: next(health))
        with patch.object(mod.socket, "gethostname", return_value="glmdch"):
            with adapter.exclusive():
                try:
                    tx.coordinate(reverse, adapter)
                    raise AssertionError("failure must not be reported as success")
                except tx.Blocked as error:
                    assert str(error) == "APPLY_FAILED_ROLLBACK_VERIFIED", str(error)
        assert mod.metadata()["web"][1] == new
        assert dependencies == {k: v for k, v in mod.metadata().items() if k != "web"}
        # Exercise the real operational installer and journal against Docker.
        # Signature/G6/public-HTTPS inputs remain EXPLICIT synthetic fixtures;
        # their actual verifiers have separate tests and are not claimed here.
        initial_binding = replace(binding, initial_image_id=new)
        selection = {"source_sha": old_sha}
        with patch.object(installer, "STATE", work), \
                patch.object(installer, "FIRST_IMAGE", new), \
                patch.object(installer, "adapter", mod), \
                patch.object(installer, "tx", tx), \
                patch.object(installer, "builder", builder), \
                patch.object(installer.socket, "gethostname", return_value="glmdch"), \
                patch.object(installer.ready, "evaluate_latest_validated", return_value=selection), \
                patch.object(installer, "backup_verified", return_value=True), \
                patch.object(installer, "public_health", return_value=True), \
                patch.object(installer, "binding_for", return_value=initial_binding), \
                patch.object(installer, "verified_candidate", return_value=(work, lambda *a: True)), \
                patch.object(installer, "selection_still_valid", return_value=True), \
                patch.object(builder, "prepare_web", return_value={"web_image_id": old}):
            installed = installer.install(bootstrap=True)
            assert installed["source_sha"] == old_sha
            recorded = json.loads((work / "installed.json").read_text())
            assert recorded["previous_source_sha"] is None  # never invent old Git SHA
            assert mod.metadata()["web"][1] == old
            assert installer.install(False)["status"] == "QA_WEB_UP_TO_DATE"
            # Simulate a crash after apply/installed-write, before clearing journal.
            initial_plan = tx.WebReleasePlan(mod.PROJECT, None, old_sha, new, old, "e" * 64)
            installer.write_json(work / "pending.json", {
                "plan": asdict(initial_plan), "dependencies": dependencies,
                "web_boundary": mod.ComposeWebAdapter.boundary(mod.metadata()["web"]),
                "previous_installed": None})
            recovered = installer.recover(initial_binding)
            assert recovered["status"] == "PREVIOUS_WEB_RESTORED"
            assert mod.metadata()["web"][1] == new
            assert not (work / "installed.json").exists()
            assert not (work / "pending.json").exists()
        assert dependencies == {k: v for k, v in mod.metadata().items() if k != "web"}
        print("PASS:REAL_DOCKER_BUILD_BOOTSTRAP_APPLY_ROLLBACK_CRASH_RECOVERY_DEPENDENCIES_UNTOUCHED")
    finally:
        # Teardown is exclusive to this isolated synthetic CI fixture, never adapter.
        if compose.exists():
            mod.docker([*base, "down", "--volumes", "--remove-orphans"])
        shutil.rmtree(work)


if __name__ == "__main__":
    main()
