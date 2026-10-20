#!/usr/bin/env python3
"""Local Docker adapter for qa-lite-web-transaction.py (#525).

No apply CLI: the supervised installer must supply independent provenance,
backup and health verifiers and a root-owned physical baseline. Importing this
module never contacts Docker. The sole CLI is a read-only metadata probe.
"""
import argparse
from contextlib import contextmanager
from dataclasses import dataclass
import fcntl
import hashlib
import json
import os
from pathlib import Path
import re
import socket
import stat
import subprocess
import sys

PROJECT = "centenario-qa-lab2"
IMAGE = re.compile(r"sha256:[a-f0-9]{64}\Z")
CID = re.compile(r"[a-f0-9]{64}\Z")
SHA = re.compile(r"[a-f0-9]{40}\Z")
DOCKER = "/usr/bin/docker"


class Blocked(Exception):
    """Static codes only; never echo Docker output or configuration."""


def require(ok, code):
    if not ok:
        raise Blocked(code)


def docker(args, *, cwd=None, timeout=150):
    """Fixed local Docker socket, sanitized environment, no shell or logs."""
    try:
        result = subprocess.run(
            [DOCKER, "--host", "unix:///var/run/docker.sock", *args],
            cwd=cwd, env={"PATH": "/usr/bin:/bin", "HOME": "/root"},
            stdin=subprocess.DEVNULL, stdout=subprocess.PIPE,
            stderr=subprocess.PIPE, text=True, timeout=timeout, check=True,
        )
        return result.stdout.strip()
    except (OSError, subprocess.SubprocessError):
        raise Blocked("DOCKER_OPERATION_FAILED") from None


def trusted_path(path, *, directory=False):
    """No symlinks or non-root writers in any path component."""
    path = Path(path)
    require(path.is_absolute(), "PATH_NOT_ABSOLUTE")
    for component in (path, *path.parents):
        info = component.lstat()
        require(not stat.S_ISLNK(info.st_mode) and info.st_uid == 0
                and not info.st_mode & 0o022, "PATH_NOT_ROOT_CONTROLLED")
    info = path.stat()
    require(stat.S_ISDIR(info.st_mode) if directory else stat.S_ISREG(info.st_mode),
            "PATH_TYPE_INVALID")
    return path


@dataclass(frozen=True)
class Binding:
    """Installed locally, never taken from a downloaded release JSON.

    files includes every Compose file, env file and referenced runtime file.
    compose_files preserves the order of the actual physical stack.
    """
    working_dir: str
    compose_files: tuple
    env_file: str
    files: tuple  # (absolute path, SHA256) tuples, pinned by installer
    state_dir: str
    initial_image_id: str | None = None  # supervised first install ONLY; no invented Git SHA


def service_ids(run=docker):
    ids = run(["ps", "-a", "--filter", "label=com.docker.compose.project=" + PROJECT,
               "--format", "{{.ID}}", "--no-trunc"]).splitlines()
    require(ids and all(CID.fullmatch(x) for x in ids), "PROJECT_NOT_FOUND")
    return ids


def metadata(run=docker):
    """Only selected fields; Config.Env and container logs are never read."""
    result = {}
    fields = [".Id", ".Image", ".State.Status", ".State.StartedAt", ".RestartCount",
              '.Config.Labels "com.docker.compose.service"',
              '.Config.Labels "com.docker.compose.project.working_dir"',
              '.Config.Labels "com.docker.compose.project.config_files"']
    template = "[" + ",".join(
        "{{json " + ("(index " + f + ")" if f.startswith(".Config.Labels") else f) + "}}"
        for f in fields) + ",{{json .Mounts}},{{json .NetworkSettings.Networks}}," \
        "{{json .NetworkSettings.Ports}}]"
    for cid in service_ids(run):
        value = json.loads(run(["inspect", "--format", template, cid]))
        require(isinstance(value, list) and len(value) == 11, "METADATA_INVALID")
        service = value[5]
        require(isinstance(service, str) and service not in result,
                "DUPLICATE_OR_UNNAMED_SERVICE")
        result[service] = value
    require("web" in result, "WEB_NOT_FOUND")
    return result


class ComposeWebAdapter:
    """Use under exclusive() then pass to the existing coordinate(plan, adapter).

    verify_evidence(plan) must RE-RUN signed release/CI/backup verification;
    public_health() must check HTTPS, API readiness and OIDC, without redirects.
    They are local trusted code, not command strings or flags from a manifest.
    """
    def __init__(self, binding, verify_evidence, public_health, *, run=docker):
        self.binding = binding
        self.verify_evidence = verify_evidence
        self.public_health = public_health
        self.run = run
        self.locked = False
        self.plan = None
        self.dependencies = None
        self.web_boundary = None

    def check_files(self):
        b = self.binding
        require(isinstance(b, Binding) and b.compose_files and b.files,
                "BINDING_INVALID")
        trusted_path(b.working_dir, directory=True)
        trusted_path(b.state_dir, directory=True)
        expected = dict(b.files)
        require(len(expected) == len(b.files) and
                all(x in expected for x in (*b.compose_files, b.env_file)),
                "UNPINNED_CONFIG_FILE")
        for filename, digest in b.files:
            require(re.fullmatch(r"[a-f0-9]{64}", digest) is not None,
                    "FILE_DIGEST_INVALID")
            path = trusted_path(filename)
            require(hashlib.sha256(path.read_bytes()).hexdigest() == digest,
                    "CONFIG_CHANGED")

    @contextmanager
    def exclusive(self):
        require(os.geteuid() == 0 and socket.gethostname() == "glmdch",
                "HOST_NOT_AUTHORIZED")
        self.check_files()
        path = Path(self.binding.state_dir) / "web-update.lock"
        fd = os.open(path, os.O_CREAT | os.O_NOFOLLOW | os.O_RDWR, 0o600)
        try:
            info = os.fstat(fd)
            require(stat.S_ISREG(info.st_mode) and info.st_uid == 0
                    and not info.st_mode & 0o077, "LOCK_NOT_PRIVATE")
            try:
                fcntl.flock(fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
            except BlockingIOError:
                raise Blocked("ANOTHER_UPDATE_RUNNING") from None
            self.locked = True
            yield self
        finally:
            self.locked = False
            self.plan = None
            self.dependencies = None
            self.web_boundary = None
            os.close(fd)

    def image_exists(self, image, sha=None):
        require(isinstance(image, str) and IMAGE.fullmatch(image), "IMAGE_NOT_PINNED")
        image_id = self.run(["image", "inspect", "--format", "{{.Id}}", image])
        if image_id != image:
            return False
        if sha is not None:
            label = self.run(["image", "inspect", "--format",
                             '{{index .Config.Labels "org.opencontainers.image.revision"}}', image])
            return label == sha
        return True

    def reverify(self, plan):
        require(self.locked, "EXCLUSIVE_SESSION_REQUIRED")
        known_source = isinstance(plan.previous_sha, str) and SHA.fullmatch(plan.previous_sha)
        initial = (plan.previous_sha is None and self.binding.initial_image_id == plan.previous_image_id
                   and not (Path(self.binding.state_dir) / "installed.json").exists())
        require(plan.project == PROJECT and plan.action == "WEB_ONLY_NO_DB_NO_SILO"
                and (known_source or initial) and SHA.fullmatch(plan.candidate_sha)
                and plan.previous_sha != plan.candidate_sha,
                "PLAN_SCOPE_INVALID")
        self.check_files()
        require(self.verify_evidence(plan) is True, "EVIDENCE_NOT_REVERIFIED")
        require(self.image_exists(plan.previous_image_id)
                and plan.previous_image_id != plan.candidate_image_id
                and self.image_exists(plan.candidate_image_id, plan.candidate_sha),
                "LOCAL_IMAGES_UNVERIFIED")
        state = metadata(self.run)
        web = state["web"]
        b = self.binding
        require(web[1] == plan.previous_image_id and web[2] == "running"
                and web[6] == b.working_dir, "PHYSICAL_WEB_CHANGED")
        actual_files = (web[7] or "").split(",")
        allowed = list(b.compose_files)
        # A prior activation leaves an immutable override in Compose labels.
        if len(actual_files) == len(allowed) + 1:
            require(actual_files[-1] == str(self.override_path(plan.previous_image_id)),
                    "COMPOSE_BINDING_CHANGED")
            actual_files = actual_files[:-1]
        require(actual_files == allowed, "COMPOSE_BINDING_CHANGED")
        require(all(v[2] == "running" for v in state.values()), "DEPENDENCY_NOT_RUNNING")
        self.dependencies = {k: v for k, v in state.items() if k != "web"}
        require(self.dependencies, "DEPENDENCY_BASELINE_EMPTY")
        self.web_boundary = self.boundary(web)
        self.plan = plan
        return True

    def override_path(self, image):
        require(isinstance(image, str) and IMAGE.fullmatch(image), "IMAGE_NOT_PINNED")
        return Path(self.binding.state_dir) / ("web-" + image[7:] + ".json")

    def change(self, plan, image):
        require(self.locked and self.plan == plan, "PLAN_NOT_REVERIFIED")
        self.check_files()
        path = self.override_path(image)
        payload = json.dumps({"services": {"web": {"image": image}}}, sort_keys=True)
        # Persistent, immutable per-image override: also usable after process death.
        try:
            fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
        except FileExistsError:
            require(trusted_path(path).read_text() == payload, "OVERRIDE_CHANGED")
        else:
            with os.fdopen(fd, "w") as stream:
                stream.write(payload)
                stream.flush()
                os.fsync(stream.fileno())
        args = ["compose", "--project-name", PROJECT,
                "--project-directory", self.binding.working_dir,
                "--env-file", self.binding.env_file]
        for filename in (*self.binding.compose_files, str(path)):
            args.extend(["--file", filename])
        # No build, pull, dependency recreation, volumes, down or migrations.
        args.extend(["up", "--detach", "--no-deps", "--no-build", "--pull", "never",
                     "--force-recreate", "--wait", "--wait-timeout", "60", "web"])
        self.run(args, cwd=self.binding.working_dir)

    def activate_web(self, plan):
        require(self.verify_running_web(plan.previous_image_id)
                and self.verify_untouched_dependencies(), "RUNTIME_CHANGED_BEFORE_APPLY")
        self.change(plan, plan.candidate_image_id)

    def restore_previous_web(self, plan):
        self.change(plan, plan.previous_image_id)

    def verify_running_web(self, image):
        web = metadata(self.run)["web"]
        return (web[1] == image and web[2] == "running"
                and self.web_boundary == self.boundary(web))

    @staticmethod
    def boundary(web):
        # Recreated containers get new endpoint/IP IDs, but retain the same
        # network objects, mount permissions/sources and published bindings.
        return (web[8], {k: v.get("NetworkID") for k, v in web[9].items()}, web[10])

    def verify_untouched_dependencies(self):
        state = metadata(self.run)
        return self.dependencies is not None and self.dependencies == {
            k: v for k, v in state.items() if k != "web"}

    def verify_public_health(self):
        return self.public_health() is True


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--probe", action="store_true", required=True,
                        help="Read only project, web image and Compose paths; never env")
    parser.parse_args()
    try:
        require(socket.gethostname() == "glmdch", "HOST_NOT_AUTHORIZED")
        web = metadata()["web"]
        print(json.dumps({"status": "READ_ONLY_METADATA_NOT_DEPLOYMENT", "project": PROJECT,
                          "web_image_id": web[1], "web_state": web[2],
                          "working_dir": web[6], "config_files": web[7]}))
        return 0
    except Exception:
        print("BLOCKED:METADATA_PROBE_FAILED", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
