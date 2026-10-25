#!/usr/bin/env python3
"""Prepare WEB image only; never change a container or accept release JSON flags.

The trusted local installer must provide a cryptographic source verifier. An
image label or the build receipt below is NOT an OCI signature/attestation.
"""
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import socket
import sys
import tempfile

spec = importlib.util.spec_from_file_location(
    "qa_lite_build_transport", Path(__file__).with_name("qa-lite-compose-adapter.py"))
transport = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = transport
spec.loader.exec_module(transport)

COMPOSE = Path("/opt/centenario/qa-lab-firstboot/recovery-v2/compose.recovery.json")
COMPOSE_SHA256 = "bed395ce374659b98ede0637afcbbe428feaf7c2c4bdeb4b351d2b6015aa1b71"
OLD_CONTEXT = "/opt/centenario/qa-staging/4ef456fee3a578a9e6418f3f7072af5dd79eb045/frontend"
AUTHORITY = "https://sistema.granlogiamixta.cl/identity/realms/pmgm"
ALLOWED_ARGS = {
    "VITE_API_BASE_URL", "VITE_USE_MOCKS", "VITE_OIDC_AUTHORITY", "VITE_OIDC_CLIENT_ID",
    "VITE_OIDC_SCOPE", "VITE_OIDC_ALLOW_HTTP_LOCAL", "VITE_OIDC_ALLOW_HTTP_QA",
}


def build_inputs():
    """Read only pinned JSON, never print config, env, build arguments or logs."""
    path = transport.trusted_path(COMPOSE)
    info = path.stat()
    transport.require(info.st_mode & 0o777 == 0o600, "COMPOSE_MODE_CHANGED")
    raw = path.read_bytes()
    transport.require(hashlib.sha256(raw).hexdigest() == COMPOSE_SHA256,
                      "PHYSICAL_COMPOSE_CHANGED")
    web = json.loads(raw)["services"]["web"]
    build = web.get("build")
    transport.require(web.get("image") is None and not web.get("env_file")
                      and isinstance(build, dict) and build.get("context") == OLD_CONTEXT
                      and set(build) <= {"context", "dockerfile", "args"}
                      and build.get("dockerfile", "Dockerfile") == "Dockerfile",
                      "WEB_BUILD_LAYOUT_CHANGED")
    args = build.get("args", {})
    transport.require(isinstance(args, dict) and set(args) <= ALLOWED_ARGS
                      and all(isinstance(v, str) and len(v) <= 256
                              and not any(c in v for c in "\r\n\x00") for v in args.values()),
                      "UNSUPPORTED_WEB_BUILD_INPUT")
    transport.require(args.get("VITE_USE_MOCKS") == "false"
                      and args.get("VITE_API_BASE_URL", "") == ""
                      and args.get("VITE_OIDC_AUTHORITY") == AUTHORITY
                      and args.get("VITE_OIDC_CLIENT_ID") == "pmgm-web"
                      and args.get("VITE_OIDC_SCOPE", "openid profile") == "openid profile"
                      and args.get("VITE_OIDC_ALLOW_HTTP_LOCAL", "false") == "false"
                      and args.get("VITE_OIDC_ALLOW_HTTP_QA", "false") in {"true", "false"},
                      "WEB_IDENTITY_CONFIGURATION_CHANGED")
    return args


def prepare_web(package, source_sha, verify_source, *, run=transport.docker):
    """Verify signed package+manifest before AND after a local frontend build.

    verify_source(package, sha) is trusted installed code that independently
    verifies signature identity, source digest, full file manifest and CI gates.
    No JSON boolean may be substituted for that code. No apply CLI is exposed.
    """
    transport.require(os.geteuid() == 0 and socket.gethostname() == "glmdch",
                      "HOST_NOT_AUTHORIZED")
    transport.require(isinstance(source_sha, str) and re.fullmatch(r"[a-f0-9]{40}", source_sha),
                      "SOURCE_SHA_INVALID")
    package = transport.trusted_path(package, directory=True)
    frontend = transport.trusted_path(package / "frontend", directory=True)
    transport.trusted_path(frontend / "Dockerfile")
    transport.require(verify_source(package, source_sha) is True,
                      "SIGNED_SOURCE_NOT_VERIFIED")
    inputs = build_inputs()
    inputs_sha = hashlib.sha256(json.dumps(inputs, sort_keys=True).encode()).hexdigest()
    with tempfile.TemporaryDirectory(prefix=".web-build-", dir=package.parent) as tmp:
        iid = Path(tmp) / "image-id"
        # Add public QA version metadata as a trusted build layer, without
        # editing the attested source tree or exposing any runtime information.
        recipe = Path(tmp) / "Dockerfile"
        marker = json.dumps({"environment": "QA_SYNTHETIC", "source_sha": source_sha}, separators=(",", ":"))
        recipe.write_text((frontend / "Dockerfile").read_text() +
                          "\nRUN printf '%s\\n' '" + marker +
                          "' > /usr/share/nginx/html/qa-version.json && chmod 644 /usr/share/nginx/html/qa-version.json\n")
        command = ["build", "--pull=false", "--iidfile", str(iid),
                   "--label", "org.opencontainers.image.revision=" + source_sha,
                   "--label", "cl.pmgm.qa.compose-sha256=" + COMPOSE_SHA256,
                   "--label", "cl.pmgm.qa.public-build-inputs-sha256=" + inputs_sha,
                   "--file", str(recipe)]
        for key, value in sorted(inputs.items()):
            command.extend(["--build-arg", key + "=" + value])
        command.append(str(frontend))
        run(command, cwd=str(package), timeout=900)
        image = iid.read_text().strip()
    transport.require(transport.IMAGE.fullmatch(image), "BUILD_IMAGE_ID_INVALID")
    transport.require(build_inputs() == inputs and verify_source(package, source_sha) is True,
                      "SOURCE_OR_CONFIG_CHANGED_DURING_BUILD")
    transport.require(run(["image", "inspect", "--format", "{{.Id}}", image]) == image
                      and run(["image", "inspect", "--format",
                               '{{index .Config.Labels "org.opencontainers.image.revision"}}',
                               image]) == source_sha,
                      "BUILT_IMAGE_IDENTITY_MISMATCH")
    return {"status": "LOCAL_WEB_IMAGE_PREPARED_NOT_DEPLOYED", "source_sha": source_sha,
            "web_image_id": image, "compose_sha256": COMPOSE_SHA256,
            "public_build_inputs_sha256": inputs_sha,
            "scope": "NO_CONTAINER_CHANGE_NO_OCI_SIGNATURE_NO_UAT"}
