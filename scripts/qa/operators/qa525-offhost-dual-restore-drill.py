#!/usr/bin/env python3
"""QA525 off-host, network-isolated dual database restore drill.

ONLY restores into two pre-created EMPTY databases of the separately
provisioned qa525-restore-lab postgres container, network_mode=none and named
test volume. It never connects to glmdch, never touches QA/production, never
prints passwords, data, schema names or private keys. No deploy authorization.
"""
import hashlib
import contextlib
import fcntl
import json
import os
from pathlib import Path
import re
import socket
import stat
import subprocess
import sys
from datetime import datetime, timezone

DOCKER = Path("/usr/bin/docker")
AGE = Path("/usr/bin/age")
IDENTITY = Path("/etc/pmgm/qa525-restore-lab-age-identity.txt")
ROOT = Path("/var/lib/qa525-restore-lab/incoming")
LOCK = Path("/var/lib/qa525-restore-lab/restore.lock")
CONTAINER = "qa525-restore-lab-postgres-1"
PROJECT = "qa525-restore-lab"
RESTORE_DATABASES = (("pmgm", "qa525_verify_pmgm"),
                     ("pmgm_keycloak", "qa525_verify_pmgm_keycloak"))
BACKUP_NAME = re.compile(r"backup-\d{8}T\d{6}Z-[a-f0-9]{16}\Z")
DIGEST = re.compile(r"[a-f0-9]{64}\Z")
PG_IMAGE_REFERENCE = re.compile(r"(?:docker\.io/library/)?postgres:17@sha256:[a-f0-9]{64}\Z")
OCI_IMAGE_ID = re.compile(r"sha256:[a-f0-9]{64}\Z")
SCOPE = "OFFHOST_ISOLATED_DUAL_RESTORE_DRILL_NO_APPLY"
SQL_EMPTY = ("SELECT count(*) FROM pg_class c JOIN pg_namespace n ON "
             "n.oid=c.relnamespace WHERE c.relkind IN ('r','p') "
             "AND n.nspname NOT IN ('pg_catalog','information_schema') "
             "AND n.nspname NOT LIKE 'pg_toast%';")


class Blocked(Exception):
    pass


def require(value, code):
    if not value:
        raise Blocked(code)


def trusted(path, *, directory=False, private=False):
    path = Path(path)
    require(path.is_absolute(), "UNTRUSTED_PATH")
    for item in (path, *path.parents):
        try:
            st = item.lstat()
        except OSError:
            raise Blocked("PATH_NOT_PROVISIONED") from None
        require(not stat.S_ISLNK(st.st_mode) and st.st_uid == 0
                and not stat.S_IMODE(st.st_mode) & 0o022, "PATH_NOT_ROOT_CONTROLLED")
    st = path.lstat()
    require((stat.S_ISDIR(st.st_mode) if directory else stat.S_ISREG(st.st_mode)),
            "PATH_TYPE_UNEXPECTED")
    if private:
        require(stat.S_IMODE(st.st_mode) == (0o700 if directory else 0o600)
                and (directory or st.st_nlink == 1),
                "PATH_NOT_PRIVATE")
    return path


def digest_file(path):
    h = hashlib.sha256()
    with Path(path).open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def read_manifest(bundle):
    trusted(bundle, directory=True, private=True)
    manifest = bundle / "manifest.json"
    trusted(manifest, private=True)
    require(0 < manifest.stat().st_size <= 8192, "MANIFEST_SIZE_INVALID")
    data = json.loads(manifest.read_bytes())
    require(type(data) is dict
            and data.get("status") == "TWO_DATABASES_ENCRYPTED_LOCALLY_UNVERIFIED_OFFHOST"
            and data.get("environment") == "QA_SYNTHETIC"
            and data.get("scope") == "ENCRYPTED_LOCAL_DUAL_DB_BACKUP_NO_OFFHOST_NO_RESTORE_NO_APPLY"
            and data.get("may_apply") is False
            and data.get("restoration_drill_verified") is False
            and data.get("offhost_custody_verified") is False
            and data.get("databases") == ["pmgm", "pmgm_keycloak"],
            "UNTRUSTED_BACKUP_RECEIPT")
    rows = data.get("archives")
    require(type(rows) is list and len(rows) == 2
            and set(bundle.iterdir()) ==
            {manifest, bundle / "pmgm.pgdump.age", bundle / "pmgm_keycloak.pgdump.age"},
            "INCOMPLETE_OR_EXTRA_BACKUP_FILES")
    validated = {}
    for row in rows:
        require(type(row) is dict
                and set(row) == {"file", "encrypted_bytes", "sha256"}
                and row["file"] in ("pmgm.pgdump.age", "pmgm_keycloak.pgdump.age")
                and row["file"] not in validated
                and type(row["sha256"]) is str
                and DIGEST.fullmatch(row["sha256"])
                and type(row["encrypted_bytes"]) is int
                and 1024 < row["encrypted_bytes"] <= 2 * 1024**3,
                "INVALID_BACKUP_FILE_ENTRY")
        path = bundle / row["file"]
        trusted(path, private=True)
        require(path.stat().st_size == row["encrypted_bytes"]
                and digest_file(path) == row["sha256"],
                "OFFHOST_CIPHERTEXT_DIGEST_MISMATCH")
        validated[row["file"]] = path
    require(set(validated) == {"pmgm.pgdump.age", "pmgm_keycloak.pgdump.age"},
            "ONE_DATABASE_BACKUP_MISSING")
    return validated


def command(args, timeout=25):
    try:
        p = subprocess.run(args, stdin=subprocess.DEVNULL,
                           stdout=subprocess.PIPE, stderr=subprocess.DEVNULL,
                           check=False, timeout=timeout,
                           env={"PATH": "/usr/bin:/bin", "HOME": "/root"})
        require(p.returncode == 0 and len(p.stdout) <= 4096,
                "ISOLATED_LAB_PROBE_FAILED")
        return p.stdout.decode("utf-8", errors="replace").strip()
    except (OSError, subprocess.TimeoutExpired):
        raise Blocked("ISOLATED_LAB_PROBE_UNAVAILABLE") from None


def check_lab():
    """Validate a separate Docker project with NO container network or binds."""
    field = lambda query: command([str(DOCKER), "--host", "unix:///var/run/docker.sock",
                                   "inspect", "--format", query, CONTAINER])
    require(field('{{index .Config.Labels "com.docker.compose.project"}}') == PROJECT
            and field('{{index .Config.Labels "com.docker.compose.service"}}') == "postgres"
            and field("{{.State.Status}}") == "running"
            and field("{{.HostConfig.NetworkMode}}") == "none",
            "RESTORE_CONTAINER_NOT_ISOLATED")
    require(field("{{.HostConfig.Privileged}}") == "false"
            and field("{{.HostConfig.PidMode}}") in ("", "<no value>")
            and field("{{.HostConfig.IpcMode}}") in ("", "private", "<no value>")
            and field("{{json .HostConfig.Devices}}") in ("[]", "null")
            and field("{{json .HostConfig.Binds}}") in ("[]", "null")
            and field("{{json .HostConfig.PortBindings}}") in ("{}", "null"),
            "RESTORE_LAB_HOST_ESCAPE_RISK")
    image_ref = field("{{.Config.Image}}")
    image_id = field("{{.Image}}")
    require(PG_IMAGE_REFERENCE.fullmatch(image_ref)
            and OCI_IMAGE_ID.fullmatch(image_id),
            "RESTORE_LAB_POSTGRES_IMAGE_UNPINNED")
    mounts = json.loads(field("{{json .Mounts}}"))
    require(type(mounts) is list and len(mounts) == 1
            and type(mounts[0]) is dict
            and mounts[0].get("Type") == "volume"
            and mounts[0].get("Destination") == "/var/lib/postgresql/data"
            and mounts[0].get("RW") is True
            and mounts[0].get("Name") == PROJECT + "_pgdata",
            "RESTORE_VOLUME_NOT_ISOLATED")
    # A matching name alone is not proof that the volume was created by
    # this disposable lab: inspect its independent Compose project label.
    volume = mounts[0]["Name"]
    labels = command([str(DOCKER), "--host", "unix:///var/run/docker.sock",
                      "volume", "inspect", "--format",
                      "{{json .Labels}}", volume])
    values = json.loads(labels)
    require(type(values) is dict
            and values.get("com.docker.compose.project") == PROJECT
            and values.get("com.docker.compose.volume") == "pgdata",
            "RESTORE_VOLUME_PROVENANCE_INVALID")
    # No other container, even stopped, may share this disposable volume.
    owners = command([str(DOCKER), "--host", "unix:///var/run/docker.sock",
                      "ps", "-a", "--filter", "volume=" + volume,
                      "--format", "{{.Names}}"])
    require(owners.splitlines() == [CONTAINER],
            "RESTORE_VOLUME_SHARED_OR_UNVERIFIED")


def lab_table_count(database):
    require(database in {v for _, v in RESTORE_DATABASES},
            "UNEXPECTED_RESTORE_DB_NAME")
    data = command([str(DOCKER), "--host", "unix:///var/run/docker.sock",
                    "exec", "--user", "postgres", CONTAINER,
                    "psql", "--no-psqlrc", "--tuples-only", "--no-align",
                    "--dbname", database, "--command", SQL_EMPTY], timeout=30)
    require(data.isdecimal() and len(data) < 10,
            "RESTORE_DB_CATALOG_NOT_VERIFIABLE")
    return int(data)


def decrypt_to_restore(archive, database, *, list_only=False, start=subprocess.Popen):
    """Only pipes decrypted data to lab pg_restore, never to disk or terminal."""
    require(database in {v for _, v in RESTORE_DATABASES},
            "RESTORE_DATABASE_INVALID")
    age_args = [str(AGE), "--decrypt", "--identity", str(IDENTITY), str(archive)]
    restore_args = [str(DOCKER), "--host", "unix:///var/run/docker.sock",
                    "exec", "--interactive", "--user", "postgres", CONTAINER,
                    "pg_restore"]
    if list_only:
        restore_args.extend(["--list"])
    else:
        restore_args.extend(["--exit-on-error", "--no-owner", "--no-acl",
                             "--dbname", database])
    def launch(args, *, stdin=None, stdout=None):
        return start(args, stdin=stdin, stdout=stdout, stderr=subprocess.DEVNULL,
                     env={"PATH": "/usr/bin:/bin", "HOME": "/root"}, close_fds=True)
    dec = restore = None
    try:
        dec = launch(age_args, stdout=subprocess.PIPE)
        require(dec.stdout is not None, "DECRYPTED_PIPE_UNAVAILABLE")
        restore = launch(restore_args, stdin=dec.stdout,
                         stdout=subprocess.DEVNULL)
        dec.stdout.close()
        rc_restore = restore.wait(timeout=1200)
        rc_decrypt = dec.wait(timeout=1200)
        require(rc_restore == 0 and rc_decrypt == 0,
                "DECRYPT_OR_RESTORE_FAILED")
    except Exception:
        for proc in (restore, dec):
            if proc is not None and proc.poll() is None:
                proc.kill()
        for proc in (restore, dec):
            if proc is not None:
                try:
                    proc.wait(timeout=10)
                except Exception:
                    pass
        raise


@contextlib.contextmanager
def lab_exclusive():
    trusted(LOCK, private=True)
    fd = os.open(LOCK, os.O_RDWR | os.O_NOFOLLOW)
    try:
        st = os.fstat(fd)
        require(stat.S_ISREG(st.st_mode) and st.st_uid == 0
                and stat.S_IMODE(st.st_mode) == 0o600 and st.st_nlink == 1,
                "RESTORE_LOCK_UNTRUSTED")
        try:
            fcntl.flock(fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError:
            raise Blocked("CONCURRENT_RESTORE_LAB_OPERATION") from None
        yield
    finally:
        os.close(fd)


def drill(backup_id):
    require(os.geteuid() == 0 and socket.gethostname() != "glmdch",
            "OFFHOST_ROOT_ONLY")
    require(type(backup_id) is str and BACKUP_NAME.fullmatch(backup_id),
            "BACKUP_IDENTIFIER_INVALID")
    with lab_exclusive():
        return drill_locked(backup_id)


def drill_locked(backup_id):
    trusted(Path(__file__))
    for path in (DOCKER, AGE):
        trusted(path)
    trusted(IDENTITY, private=True)
    trusted(ROOT, directory=True, private=True)
    bundle = ROOT / backup_id
    archives = read_manifest(bundle)
    check_lab()
    # A restoring lab is provisioned with two empty, dedicated databases.
    # A second invocation must never overwrite an already restored database.
    require(all(lab_table_count(db) == 0 for _, db in RESTORE_DATABASES),
            "RESTORE_LAB_DATABASE_NOT_EMPTY")
    for name, database in RESTORE_DATABASES:
        check_lab()
        decrypt_to_restore(archives[name + ".pgdump.age"], database, list_only=True)
    for name, database in RESTORE_DATABASES:
        check_lab()
        decrypt_to_restore(archives[name + ".pgdump.age"], database)
        require(lab_table_count(database) > 0,
                "RESTORED_DATABASE_HAS_NO_USER_TABLES")
    check_lab()
    return {
        "status": "OFFHOST_DUAL_RESTORE_DRILL_LOCAL_EVIDENCE_ONLY",
        "scope": SCOPE,
        "databases_restored": ["pmgm", "pmgm_keycloak"],
        "catalog_and_full_restore_attempted": True,
        "restore_lab_isolated_by_local_checks": True,
        "repository_or_host_signed_evidence": False,
        "independent_offhost_custody_attested": False,
        "may_apply": False,
    }


def main(argv=None):
    args = sys.argv[1:] if argv is None else argv
    try:
        require(len(args) == 2 and args[0] == "--restore-drill",
                "ONLY_EXPLICIT_LAB_RESTORE_SUPPORTED")
        print(json.dumps(drill(args[1]), sort_keys=True))
        return 0
    except Blocked as e:
        print(json.dumps({"status": "BLOCKED_QA525_OFFHOST_RESTORE",
                          "scope": SCOPE, "code": str(e),
                          "may_apply": False}, sort_keys=True))
        return 2
    except Exception:
        print(json.dumps({"status": "BLOCKED_QA525_OFFHOST_RESTORE",
                          "scope": SCOPE, "code": "OFFHOST_DRILL_UNVERIFIED",
                          "may_apply": False}, sort_keys=True))
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
