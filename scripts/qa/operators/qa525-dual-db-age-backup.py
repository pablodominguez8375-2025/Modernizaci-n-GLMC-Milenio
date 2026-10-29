#!/usr/bin/env python3
"""QA525 encrypted local PostgreSQL backup for synthetic glmdch ONLY.

Explicit --backup writes ONLY new encrypted .age archives and root-owned
manifests; PostgreSQL is queried using pg_dump (no schema/data mutation).
This does NOT verify off-host custody or complete restoration, and NEVER
authorizes deployment. No service restart, compose, docker cp, or raw dump.
"""
import contextlib
from datetime import datetime, timezone
import fcntl
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import resource
import secrets
import shutil
import socket
import stat
import subprocess
import sys

HERE = Path(__file__).resolve().parent
INVENTORY = HERE / "qa525-host-inventory-readonly.py"
ADAPTER = HERE.parent / "qa-lite-compose-adapter.py"
STATE = Path("/opt/centenario/qa525-dual-db-backups")
RECIPIENT = Path("/etc/pmgm/qa525-offhost-age-recipient.txt")
DOCKER = Path("/usr/bin/docker")
AGE = Path("/usr/bin/age")
HOST = "glmdch"
PROJECT = "centenario-qa-lab2"
POSTGRES = PROJECT + "-postgres-1"
DBS = ("pmgm", "pmgm_keycloak")
SERVICES = ("api", "web", "keycloak", "postgres",
            "minio", "gateway", "clamav")
JOBS = ("db-grants", "migrator", "minio-init")
SCOPE = "ENCRYPTED_LOCAL_DUAL_DB_BACKUP_NO_OFFHOST_NO_RESTORE_NO_APPLY"
MAX_ENCRYPTED_FILE_BYTES = 2 * 1024**3
FREE_SPACE_RESERVE_BYTES = 2 * 1024**3
RECIPIENT_PATTERN = re.compile(r"age1[a-z0-9]{58}\Z")
HASH = re.compile(r"[0-9a-f]{64}\Z")
LOCKS = (
    Path("/opt/centenario/qa-api-refresh/operation.lock"),
    Path("/opt/centenario/qa-auto-lite/web-update.lock"),
    Path("/run/lock/pmgm-master-qa-604.lock"),
    Path("/run/lock/pmgm-qa754-web-hotfix.lock"),
    Path("/run/lock/pmgm-qa754-api-activation.lock"),
)


class Blocked(Exception):
    pass


def require(ok, code):
    if not ok:
        raise Blocked(code)


def trusted(path, *, directory=False, private=False):
    path = Path(path)
    require(path.is_absolute(), "UNTRUSTED_PATH")
    for current in (path, *path.parents):
        try:
            details = current.lstat()
        except OSError:
            raise Blocked("TRUSTED_PATH_MISSING") from None
        require(not stat.S_ISLNK(details.st_mode) and details.st_uid == 0
                and not stat.S_IMODE(details.st_mode) & 0o022,
                "PATH_NOT_ROOT_CONTROLLED")
    st = path.lstat()
    require((stat.S_ISDIR(st.st_mode) if directory else stat.S_ISREG(st.st_mode)),
            "TRUSTED_FILE_TYPE_INVALID")
    if private:
        require(stat.S_IMODE(st.st_mode) == (0o700 if directory else 0o600)
                and (directory or st.st_nlink == 1),
                "TRUSTED_PATH_NOT_PRIVATE")
    return path


def lock_parent(path):
    path = Path(path)
    if path == Path("/run/lock"):
        trusted(path.parent, directory=True)
        st = path.lstat()
        require(stat.S_ISDIR(st.st_mode) and st.st_uid == 0
                and stat.S_IMODE(st.st_mode) in (0o700, 0o755, 0o1777),
                "LOCK_DIRECTORY_UNTRUSTED")
    else:
        trusted(path, directory=True)


@contextlib.contextmanager
def exclusive():
    with contextlib.ExitStack() as stack:
        for path in LOCKS:
            lock_parent(path.parent)
            fd = os.open(path, os.O_RDWR | os.O_CREAT | os.O_NOFOLLOW, 0o600)
            stack.callback(os.close, fd)
            info = os.fstat(fd)
            require(stat.S_ISREG(info.st_mode) and info.st_uid == 0
                    and info.st_nlink == 1
                    and stat.S_IMODE(info.st_mode) == 0o600,
                    "OPERATOR_LOCK_UNTRUSTED")
            try:
                fcntl.flock(fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
            except BlockingIOError:
                raise Blocked("ANOTHER_QA_OPERATION_IN_PROGRESS") from None
        yield


def public_recipient():
    trusted(RECIPIENT, private=True)
    raw = RECIPIENT.read_bytes()
    require(0 < len(raw) <= 128, "RECIPIENT_FILE_INVALID")
    try:
        text = raw.decode("ascii").strip()
    except UnicodeError:
        raise Blocked("RECIPIENT_FILE_INVALID") from None
    require(RECIPIENT_PATTERN.fullmatch(text)
            and raw in (text.encode("ascii"), (text + "\n").encode("ascii")),
            "RECIPIENT_FILE_INVALID")
    return hashlib.sha256(raw).hexdigest()  # not the recipient itself


def load_inventory():
    for path in (Path(__file__), INVENTORY, ADAPTER):
        trusted(path)
    spec = importlib.util.spec_from_file_location("qa525_backups_inventory", INVENTORY)
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


def limit_age_output():
    """Kernel file size cap on age ciphertext; protects the QA system volume."""
    resource.setrlimit(resource.RLIMIT_FSIZE,
                       (MAX_ENCRYPTED_FILE_BYTES, MAX_ENCRYPTED_FILE_BYTES))


def fixed_process(args, *, stdin=None, stdout=None):
    options = {"preexec_fn": limit_age_output} if args[0] == str(AGE) else {}
    return subprocess.Popen(
        args, stdin=stdin, stdout=stdout, stderr=subprocess.DEVNULL,
        env={"PATH": "/usr/bin:/bin", "HOME": "/root"},
        close_fds=True, **options,
    )


def check_postgres():
    # Only selected Docker labels/state. NEVER emit docker inspect/config/env.
    def inspect(field):
        completed = subprocess.run(
            [str(DOCKER), "--host", "unix:///var/run/docker.sock",
             "inspect", "--format", field, POSTGRES],
            stdin=subprocess.DEVNULL, stdout=subprocess.PIPE,
            stderr=subprocess.DEVNULL, timeout=15, check=False,
            env={"PATH": "/usr/bin:/bin", "HOME": "/root"},
        )
        require(completed.returncode == 0 and len(completed.stdout) < 300,
                "POSTGRES_INSPECT_FAILED")
        return completed.stdout.decode("ascii", errors="replace").strip()
    require(inspect('{{index .Config.Labels "com.docker.compose.project"}}') == PROJECT
            and inspect('{{index .Config.Labels "com.docker.compose.service"}}') == "postgres"
            and inspect("{{.State.Status}}") == "running",
            "POSTGRES_NOT_EXPECTED_QA_CONTAINER")


def snapshot(inv):
    # Metadata returned by trusted adapter is redacted in output below.
    data = inv.adapter.metadata() if hasattr(inv, "adapter") else inv.load_adapter().metadata()
    require(type(data) is dict and set(data) == set(SERVICES + JOBS),
            "PROJECT_CONTAINER_SET_CHANGED")
    for service, entry in data.items():
        require(type(entry) is list and len(entry) == 12
                and entry[5] == service,
                "CONTAINER_METADATA_MALFORMED")
        if service in SERVICES:
            require(entry[2] == "running", "ACTIVE_SERVICE_NOT_RUNNING")
        else:
            require(entry[2] == "exited" and entry[11] == 0,
                    "PREVIOUS_ONESHOT_JOB_UNEXPECTED")
    return {
        name: {"container_id": data[name][0],
               "image_id": data[name][1],
               "status": data[name][2],
               "exit_code": data[name][11]}
        for name in SERVICES + JOBS
    }


def sha256_file(path):
    h = hashlib.sha256()
    with Path(path).open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            h.update(block)
    return h.hexdigest()


def ciphertext_is_root_private(info):
    """Pure trust check, deliberately not bypassable through the host CLI."""
    return (stat.S_ISREG(info.st_mode) and info.st_uid == 0
            and stat.S_IMODE(info.st_mode) == 0o600
            and info.st_nlink == 1
            and 1024 < info.st_size <= MAX_ENCRYPTED_FILE_BYTES)


def dump_age(db, destination, *, run=fixed_process):
    """Stream pg_dump directly into age; no unencrypted dump on host disk."""
    require(db in DBS and destination.parent.is_dir()
            and not destination.exists(), "INVALID_BACKUP_TARGET")
    fd = os.open(destination, os.O_WRONLY | os.O_EXCL | os.O_CREAT | os.O_NOFOLLOW,
                 0o600)
    pg = enc = None
    try:
        with os.fdopen(fd, "wb") as out:
            pg = run([str(DOCKER), "--host", "unix:///var/run/docker.sock",
                      "exec", "--user", "postgres", POSTGRES,
                      "pg_dump", "--format=custom", "--no-owner", "--no-acl",
                      "--dbname", db], stdout=subprocess.PIPE)
            require(pg.stdout is not None, "POSTGRES_DUMP_PIPE_MISSING")
            enc = run([str(AGE), "-R", str(RECIPIENT)],
                      stdin=pg.stdout, stdout=out)
            pg.stdout.close()
            enc_status = enc.wait(timeout=1200)
            pg_status = pg.wait(timeout=1200)
            out.flush()
            os.fsync(out.fileno())
        require(enc_status == 0 and pg_status == 0,
                "ENCRYPTED_PG_DUMP_FAILED")
        info = destination.lstat()
        require(ciphertext_is_root_private(info), "BACKUP_CIPHERTEXT_INVALID")
        return {"file": db + ".pgdump.age",
                "encrypted_bytes": info.st_size,
                "sha256": sha256_file(destination)}
    except Exception:
        for proc in (enc, pg):
            if proc is not None and proc.poll() is None:
                proc.kill()
        for proc in (enc, pg):
            if proc is not None:
                try:
                    proc.wait(timeout=10)
                except Exception:
                    pass
        raise


def save_manifest(path, record):
    require(not path.exists(), "MANIFEST_ALREADY_EXISTS")
    fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW,
                 0o600)
    with os.fdopen(fd, "w", encoding="utf-8") as out:
        json.dump(record, out, sort_keys=True, separators=(",", ":"))
        out.flush()
        os.fsync(out.fileno())


def backup():
    require(os.geteuid() == 0 and socket.gethostname() == HOST,
            "HOST_NOT_AUTHORIZED")
    for script in (DOCKER, AGE):
        trusted(script)
    trusted(STATE, directory=True, private=True)
    recipient_fingerprint = public_recipient()
    inv = load_inventory()
    with exclusive():
        before = snapshot(inv)
        check_postgres()
        volume = os.statvfs(STATE)
        require(volume.f_bavail * volume.f_frsize
                > len(DBS) * MAX_ENCRYPTED_FILE_BYTES + FREE_SPACE_RESERVE_BYTES,
                "BACKUP_DISK_HEADROOM_INSUFFICIENT")
        date = datetime.now(timezone.utc)
        ident = date.strftime("%Y%m%dT%H%M%SZ") + "-" + secrets.token_hex(8)
        pending = STATE / (".pending-" + ident)
        done = STATE / ("backup-" + ident)
        require(not done.exists() and not pending.exists(),
                "BACKUP_DIRECTORY_ALREADY_EXISTS")
        pending.mkdir(mode=0o700)
        completed = False
        try:
            archives = []
            for db in DBS:
                archives.append(dump_age(db, pending / (db + ".pgdump.age")))
            require(snapshot(inv) == before,
                    "SERVICE_OR_ONESHOT_CHANGED_DURING_BACKUP")
            record = {
                "status": "TWO_DATABASES_ENCRYPTED_LOCALLY_UNVERIFIED_OFFHOST",
                "scope": SCOPE,
                "environment": "QA_SYNTHETIC",
                "created_utc": date.isoformat(),
                "recipient_public_key_sha256": recipient_fingerprint,
                "archives": archives,
                "databases": list(DBS),
                "container_ids": {n: before[n]["container_id"] for n in SERVICES},
                "offhost_custody_verified": False,
                "restoration_drill_verified": False,
                "age_decrypt_and_pg_restore_list_verified": False,
                "may_apply": False,
            }
            save_manifest(pending / "manifest.json", record)
            os.rename(pending, done)
            completed = True
        finally:
            if not completed and pending.exists():
                shutil.rmtree(pending)
        return {
            "status": "ENCRYPTED_LOCAL_DUAL_DB_BACKUP_ONLY",
            "scope": SCOPE,
            "databases_backed_up": list(DBS),
            "backup_directory_created": True,
            "offhost_custody_verified": False,
            "restoration_drill_verified": False,
            "may_apply": False,
        }


def main(argv=None):
    args = sys.argv[1:] if argv is None else argv
    try:
        require(args == ["--backup"], "ONLY_EXPLICIT_LOCAL_BACKUP_SUPPORTED")
        print(json.dumps(backup(), sort_keys=True))
        return 0
    except Blocked as e:
        print(json.dumps({"status": "BLOCKED_QA525_DUAL_DB_BACKUP",
                          "scope": SCOPE, "code": str(e),
                          "may_apply": False}, sort_keys=True))
        return 2
    except Exception:
        print(json.dumps({"status": "BLOCKED_QA525_DUAL_DB_BACKUP",
                          "scope": SCOPE, "code": "BACKUP_EXECUTION_FAILED",
                          "may_apply": False}, sort_keys=True))
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
