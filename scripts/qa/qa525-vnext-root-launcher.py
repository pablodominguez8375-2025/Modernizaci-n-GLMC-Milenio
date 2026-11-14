#!/usr/bin/env python3
"""QA525 pre-root LAUNCHER composition. INACTIVE library; grants NO authority, installs nothing.

Loaded ONLY by the PRE-4 bootstrap (#978) from bytes it already authenticated, and called as
`launch(load, record)`. Every module it uses (`load(name)`) and its configuration module come from the
same Sponsor-pinned manifest; nothing here imports from disk or accepts a caller-supplied callback.

Order (each step a fixed STOP):
  1. root; configuration module (pins, paths, child table) parsed against an exact key set;
  2. pinned `ssh-keygen` from the bootstrap's verified `helpers`, converted ONCE to `sha256:<hex>`;
  3. inbox files (GO, signature, OPS v3 receipt, APP subject) read without following links; the receipts'
     digests must equal the digests pinned in the configuration;
  4. closed dispatch on the signed GO's `schema`: `qa525-go-launcher-v1` (LAUNCHER_ONLY) or `qa525-go-g6-v1`
     (G6_V2_ONLY, honoured only for the ONE command the release config pins); anything else STOP;
  5. the child module for that scope is resolved BEFORE any nonce can be spent;
  6. the one composed flight: 5 legacy locks + dedicated lease (#982) -> GO verified (#981 + `check_cut`)
     -> sealed FD received (#966) -> nonce spent (#967) -> fixed child -> plane re-checked (#975/#976).
The OPS v3 / APP receipts are CLAIMS until the authenticity port (`qa525-vnext-root-authenticity.py`:
OPS v3 signature in its own namespace + offline Sigstore of the inner ZIP by the pinned `gh`, run unprivileged)
confirms them. The launcher feeds it ONLY digest-pinned inputs (inbox files whose sha256 the Sponsor-signed
config fixes, the `ssh-keygen` and `gh` helpers of the verified manifest, the release archive path) and stops
before the flight if the port is absent or says anything but True. `check_cut` only proves the GO's claims are consistent with the pins; the AUTHENTICITY is #981's
fingerprint-pinned `ssh-keygen -Y verify`. A GO's validity is never read from the host, the sender or Git.
"""
import datetime as dt
import json
import os
import re
import socket
import stat
import sys

CONFIG = "qa525-vnext-root-launch-config.py"
LOCKS, POSIX, PLANE = ("qa525-vnext-root-locks.py", "qa525-vnext-posix-port.py", "qa525-vnext-root-plane.py")
AUTH = "qa525-vnext-root-authenticity.py"
OPSSIG = "qa525-vnext-ops-signature.py"
ENTRY, VERIFY, CUT = ("qa525-vnext-root-entry.py", "qa525-vnext-root-go-verify.py",
                      "qa525-vnext-go-cut-contract.py")
REPO_ID = re.compile(r"[1-9][0-9]{0,19}\Z")  # stable numeric GitHub repository id (Sponsor pin)
HEX64 = re.compile(r"[0-9a-f]{64}\Z")
SHA256 = re.compile(r"sha256:[0-9a-f]{64}\Z")
FP = re.compile(r"SHA256:[A-Za-z0-9+/]{43}\Z")
CODE = re.compile(r"[A-Z0-9_]{3,64}\Z")
MODULE = re.compile(r"qa525-vnext-[a-z0-9-]{1,60}\.py\Z")
MAX_INPUT = 65536
INBOX_FILES = ("go.json", "go.json.sig", "ops-v3.json", "app-subject.json",
               "ops-v3.json.sig", "app-bundle.json", "trusted-root.jsonl")
INBOX_LIMITS = {"ops-v3.json.sig": 4096, "app-bundle.json": 1_000_000,
                "trusted-root.jsonl": 262144, "g6-operation.json": 4096}
MAX_ARCHIVE = 512 * 1024 * 1024
G6_OPERATION_FILE = "g6-operation.json"
G6_RUN = "qa525-vnext-g6-v2-run.py"
G6_CHILD_CORE = "qa525-vnext-root-g6-child-core.py"
APIWEB_HOST = "qa525-vnext-root-apiweb-host.py"  # pinned host-effects module (readers/runner/target); fail closed if absent
APIWEB_MODULES = {"root_locks": LOCKS, "bridge": "qa525-vnext-admission-bridge.py",
                  "boundary": "qa525-vnext-b7-root-boundary.py", "b7": "qa525-vnext-b7-held-readonly.py",
                  "observer": "qa525-vnext-observer.py", "scopes": "qa525-vnext-scope-store.py",
                  "switch": "qa525-vnext-switch.py", "flight": "qa525-vnext-flight.py",
                  "g6_anchor": "qa525-vnext-g6-anchor.py", "run_held": "qa525-vnext-held-flight-bridge.py"}
APIWEB_HOST_MODULES = {"root_locks": LOCKS, "boundary": "qa525-vnext-b7-root-boundary.py",
                       "scopes": "qa525-vnext-scope-store.py", "containment": "qa525-vnext-containment.py",
                       "host_adapters": "qa525-vnext-host-adapters.py", "state_reader": "qa525-vnext-state-reader.py",
                       "health": "qa525-vnext-health.py", "plan": "qa525-vnext-baseline-plan.py",
                       "b7": "qa525-vnext-b7-held-readonly.py", "physical_runner": "qa525-vnext-held-physical-runner.py"}
APIWEB_EFFECTS = "qa525-vnext-root-apiweb-effects.py"
CONFIG_KEYS = frozenset({
    "pins", "launcher_pins", "allowed_signers", "go_fingerprint", "ops_v3_sha256", "app_subject_sha256",
    "inbox", "temp_root", "socket_path", "journal_root", "lease_dir", "peer_uid", "timeout", "children",
    "g6_pins", "authenticity", "apiweb"})
AUTH_KEYS = frozenset({"ops_allowed_signers", "ops_signature_sha256", "app_bundle_sha256", "trusted_root_sha256",
                       "app_archive_path", "drop_uid", "drop_gid", "source_repository_id"})
PINS_KEYS = frozenset({"ops_source_sha", "app_source_sha", "app_archive_sha256", "plan_sha256",
                       "admission_sha256", "policy_hash"})
# schema -> scope. One signed GO authorizes exactly one scope; a G6 GO authorizes exactly ONE G6 command.
SCOPES = {"qa525-go-launcher-v1": "LAUNCHER_ONLY", "qa525-go-g6-v1": "G6_V2_ONLY",
          "qa525-go-cut-v1": "API_WEB_QA_ONLY"}
G6_COMMANDS = ("preflight", "init", "backup", "restore", "review")
# The ONLY scope/command -> child mapping. It lives in code, not in the configuration: an authentic but
# mis-bound config (swap, duplicate, alternative child) is refused before authentication, nonce or child.
CHILD_MODULES = dict({"LAUNCHER_ONLY": "qa525-vnext-root-launcher-child.py",
                      "API_WEB_QA_ONLY": "qa525-vnext-root-apiweb-child.py"},
                     **{"G6:" + c: "qa525-vnext-root-g6-%s.py" % c for c in G6_COMMANDS})


class Stop(Exception):
    """Opaque STOP code."""


def _abs(path):
    return type(path) is str and os.path.isabs(path) and path == os.path.normpath(path) and "\0" not in path


def _config(module):
    cfg = getattr(module, "CONFIG", None)
    if type(cfg) is not dict or set(cfg) != CONFIG_KEYS:
        raise Stop("LAUNCH_CONFIG_INVALID")
    if (type(cfg["pins"]) is not dict or set(cfg["pins"]) != PINS_KEYS
            or type(cfg["launcher_pins"]) is not dict or type(cfg["children"]) is not dict
            or type(cfg["allowed_signers"]) is not bytes or not 0 < len(cfg["allowed_signers"]) <= 4096
            or type(cfg["go_fingerprint"]) is not str or FP.fullmatch(cfg["go_fingerprint"]) is None
            or any(type(cfg[k]) is not str or SHA256.fullmatch(cfg[k]) is None
                   for k in ("ops_v3_sha256", "app_subject_sha256"))
            or any(not _abs(cfg[k]) for k in ("inbox", "temp_root", "socket_path", "journal_root", "lease_dir"))
            or type(cfg["peer_uid"]) is not int or isinstance(cfg["peer_uid"], bool) or cfg["peer_uid"] <= 0
            or type(cfg["timeout"]) not in (int, float) or isinstance(cfg["timeout"], bool)
            or not 0 < cfg["timeout"] <= 60
            or cfg["children"] != CHILD_MODULES):  # exact, closed value mapping (dict equality: keys AND values)
        raise Stop("LAUNCH_CONFIG_INVALID")
    auth = cfg["authenticity"]
    if (type(auth) is not dict or set(auth) != AUTH_KEYS
            or type(auth["ops_allowed_signers"]) is not bytes or not 0 < len(auth["ops_allowed_signers"]) <= 4096
            or any(type(auth[k]) is not str or SHA256.fullmatch(auth[k]) is None
                   for k in ("ops_signature_sha256", "app_bundle_sha256", "trusted_root_sha256"))
            or not _abs(auth["app_archive_path"])
            or type(auth["source_repository_id"]) is not str or REPO_ID.fullmatch(auth["source_repository_id"]) is None
            or any(type(auth[k]) is not int or isinstance(auth[k], bool) or auth[k] <= 0
                   for k in ("drop_uid", "drop_gid"))
            or auth["drop_uid"] == cfg["peer_uid"]):  # the verifier never runs as the peer that sends the archive
        raise Stop("LAUNCH_CONFIG_INVALID")
    g6 = cfg["g6_pins"]
    if g6 is not None and (type(g6) is not dict or set(g6) != {"g6_command", "g6_operation_sha256"}
                           or g6["g6_command"] not in G6_COMMANDS
                           or type(g6["g6_operation_sha256"]) is not str
                           or SHA256.fullmatch(g6["g6_operation_sha256"]) is None):
        raise Stop("LAUNCH_CONFIG_INVALID")
    if cfg["apiweb"] is not None and type(cfg["apiweb"]) is not dict:
        raise Stop("LAUNCH_CONFIG_INVALID")
    return cfg


def _read_regular(directory, name, uid, anchor="/"):
    """Root-owned regular file directly inside `directory`, opened without following links or blocking."""
    if name not in INBOX_FILES and name != G6_OPERATION_FILE:
        raise Stop("LAUNCH_INPUT_NAME")
    limit = INBOX_LIMITS.get(name, MAX_INPUT)
    try:
        _chain(directory, uid, anchor)  # every ancestor, before any root I/O on the inbox
        dfd = os.open(directory, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW | os.O_CLOEXEC)
    except (OSError, Stop):
        raise Stop("LAUNCH_INBOX_UNSAFE") from None
    try:
        dst = os.fstat(dfd)
        if dst.st_uid != uid or dst.st_mode & 0o022 or not stat.S_ISDIR(dst.st_mode):
            raise Stop("LAUNCH_INBOX_UNSAFE")
        try:
            fd = os.open(name, os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC | os.O_NONBLOCK, dir_fd=dfd)
        except OSError:
            raise Stop("LAUNCH_INPUT_MISSING") from None
        try:
            info = os.fstat(fd)
            if (not stat.S_ISREG(info.st_mode) or info.st_uid != uid or info.st_nlink != 1
                    or info.st_mode & 0o022 or not 0 < info.st_size <= limit):
                raise Stop("LAUNCH_INPUT_UNSAFE")
            data = os.read(fd, limit + 1)
            if len(data) != info.st_size or os.read(fd, 1) != b"":
                raise Stop("LAUNCH_INPUT_UNSAFE")
            return data
        finally:
            os.close(fd)
    finally:
        os.close(dfd)


def _json(raw, code):
    def pairs(items):
        out = {}
        for k, v in items:
            if k in out:
                raise ValueError
            out[k] = v
        return out
    try:
        value = json.loads(raw.decode("utf-8"), object_pairs_hook=pairs)
    except (ValueError, UnicodeDecodeError, RecursionError):
        raise Stop(code) from None
    if type(value) is not dict:
        raise Stop(code)
    return value


def _g6_operation(raw, pins, runner):
    """Canonical, GO-bound options from a root-owned file, never the peer's FD."""
    doc = _json(raw, "LAUNCH_G6_OPERATION_INVALID")
    if (set(doc) != {"schema", "g6_command", "argv"}
            or doc["schema"] != "qa525-g6-operation-v1"
            or doc["g6_command"] != pins["g6_command"]
            or type(doc["argv"]) is not list or len(doc["argv"]) > 24
            or any(type(v) is not str or len(v) > 256 for v in doc["argv"])
            or json.dumps(doc, sort_keys=True, separators=(",", ":"), ensure_ascii=True).encode() != raw):
        raise Stop("LAUNCH_G6_OPERATION_INVALID")
    argv = [pins["g6_command"], *doc["argv"]]
    try:
        cmd, _ = runner.parse(argv)
    except Exception:
        raise Stop("LAUNCH_G6_OPERATION_INVALID") from None
    if cmd != pins["g6_command"]:
        raise Stop("LAUNCH_G6_OPERATION_INVALID")
    return argv


def _epoch(text):
    return int(dt.datetime.strptime(text, "%Y-%m-%dT%H:%M:%SZ").replace(tzinfo=dt.timezone.utc).timestamp())


def _helper_pin(record, name="ssh-keygen"):
    helper = (record.get("helpers") or {}).get(name) if type(record) is dict else None
    if (type(helper) is not dict or not _abs(helper.get("path"))
            or type(helper.get("sha256")) is not str or HEX64.fullmatch(helper["sha256"]) is None):
        raise Stop("LAUNCH_HELPER_PIN_INVALID")
    return helper["path"], "sha256:" + helper["sha256"]  # the ONE bare-hex -> "sha256:" conversion


def _chain(path, uid, anchor="/"):
    """Every directory from `anchor` down to `path`: real (no symlink), owned by `uid`, not group/other writable."""
    if not _abs(path) or not _abs(anchor) or not (path == anchor or path.startswith(anchor.rstrip("/") + "/")):
        raise Stop("LAUNCH_DIR_UNSAFE")
    current = anchor
    parts = [] if path == anchor else path[len(anchor.rstrip("/")) + 1:].split("/")
    for part in [None] + parts:
        if part is not None:
            current = os.path.join(current, part)
        try:
            info = os.lstat(current)
        except OSError:
            raise Stop("LAUNCH_DIR_UNSAFE") from None
        if not stat.S_ISDIR(info.st_mode) or info.st_uid != uid or info.st_mode & 0o022:
            raise Stop("LAUNCH_DIR_UNSAFE")


def _open_dir(path, uid, anchor):
    _chain(path, uid, anchor)
    try:
        fd = os.open(path, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW | os.O_CLOEXEC)
    except OSError:
        raise Stop("LAUNCH_DIR_UNSAFE") from None
    info = os.fstat(fd)
    try:
        same = os.lstat(path)
    except OSError:
        os.close(fd)
        raise Stop("LAUNCH_DIR_UNSAFE") from None
    if (info.st_dev, info.st_ino) != (same.st_dev, same.st_ino) or info.st_uid != uid or info.st_mode & 0o022:
        os.close(fd)
        raise Stop("LAUNCH_DIR_UNSAFE")
    return fd


def _open_archive(path, uid, anchor):
    """The release's inner archive: a root-owned regular file in a link-free root-owned chain; returns (fd, size)."""
    if not _abs(path):
        raise Stop("LAUNCH_ARCHIVE_UNSAFE")
    try:
        _chain(os.path.dirname(path), uid, anchor)
        fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW | os.O_CLOEXEC | os.O_NONBLOCK)
    except (OSError, Stop):
        raise Stop("LAUNCH_ARCHIVE_UNSAFE") from None
    info = os.fstat(fd)
    if (not stat.S_ISREG(info.st_mode) or info.st_uid != uid or info.st_nlink != 1 or info.st_mode & 0o022
            or not 0 < info.st_size <= MAX_ARCHIVE):
        os.close(fd)
        raise Stop("LAUNCH_ARCHIVE_UNSAFE")
    return fd, info.st_size


def _accept(path, peer_uid, timeout, *, owner=0, anchor="/"):
    """Listen on a socket this attempt creates inside a root-owned, link-free directory; take ONE peer.

    Nothing outside the inode THIS attempt created is ever touched: a pre-existing path makes bind fail and is
    left alone; owner/mode are set through a descriptor to the exact inode (never the path); the file is
    unlinked only if the name still refers to that same (dev, ino)."""
    if not _abs(path) or len(os.path.basename(path)) > 60 or os.path.basename(path) in ("", ".", ".."):
        raise Stop("LAUNCH_SOCKET_FAILED")
    directory, name = os.path.dirname(path), os.path.basename(path)
    dfd = _open_dir(directory, owner, anchor)
    server = socket.socket(socket.AF_UNIX, socket.SOCK_SEQPACKET)
    created, pfd = None, None
    try:
        try:
            server.bind("/proc/self/fd/%d/%s" % (dfd, name))  # EADDRINUSE on any pre-existing entry: untouched
            info = os.lstat(name, dir_fd=dfd)
            if not stat.S_ISSOCK(info.st_mode) or info.st_uid != owner:
                raise Stop("LAUNCH_SOCKET_FAILED")
            created = (info.st_dev, info.st_ino)
            pfd = os.open(name, os.O_PATH | os.O_NOFOLLOW | os.O_CLOEXEC, dir_fd=dfd)
            held = os.fstat(pfd)
            if (held.st_dev, held.st_ino) != created:
                raise Stop("LAUNCH_SOCKET_FAILED")
            proc = "/proc/self/fd/%d" % pfd  # resolves to the inode we hold, immune to renames/swaps
            os.chown(proc, peer_uid, -1)
            os.chmod(proc, 0o600)
            # Set BEFORE listen: the accepted socket inherits it, so a frame sent before accept() still
            # carries SCM_CREDENTIALS (verified on Linux); setting it only after accept would lose them.
            server.setsockopt(socket.SOL_SOCKET, socket.SO_PASSCRED, 1)
            server.listen(1)
            server.settimeout(timeout)
            conn, _ = server.accept()
        except OSError:
            raise Stop("LAUNCH_SOCKET_FAILED") from None
    finally:
        server.close()
        if pfd is not None:
            os.close(pfd)
        try:
            if created is not None:
                now = os.lstat(name, dir_fd=dfd)
                if (now.st_dev, now.st_ino) == created:
                    os.unlink(name, dir_fd=dfd)
        except OSError:
            pass
        os.close(dfd)
    conn.setsockopt(socket.SOL_SOCKET, socket.SO_PASSCRED, 1)
    return conn


def _compose(load, record, env):
    """`env` is built ONLY by launch() below; tests build their own for the private seam."""
    if env["euid"]() != env["root_euid"]:
        raise Stop("LAUNCH_NOT_ROOT")
    cfg = _config(load(CONFIG))
    keygen_path, keygen_sha = _helper_pin(record)
    gh_path, gh_sha = _helper_pin(record, "gh")
    raw = {n: _read_regular(cfg["inbox"], n, env["os_uid"], env["anchor"]) for n in INBOX_FILES}
    auth_cfg = cfg["authenticity"]
    if (env["sha256"](raw["ops-v3.json"]) != cfg["ops_v3_sha256"]
            or env["sha256"](raw["app-subject.json"]) != cfg["app_subject_sha256"]
            or env["sha256"](raw["ops-v3.json.sig"]) != auth_cfg["ops_signature_sha256"]
            or env["sha256"](raw["app-bundle.json"]) != auth_cfg["app_bundle_sha256"]
            or env["sha256"](raw["trusted-root.jsonl"]) != auth_cfg["trusted_root_sha256"]):
        raise Stop("LAUNCH_RECEIPT_PIN_MISMATCH")
    go_doc = _json(raw["go.json"], "LAUNCH_GO_INVALID")
    schema = go_doc.get("schema")
    if type(schema) is not str or schema not in SCOPES:
        raise Stop("LAUNCH_SCOPE_UNKNOWN")
    purpose = SCOPES[schema]
    g6_pins = None
    if purpose == "G6_V2_ONLY":
        if cfg["g6_pins"] is None:
            raise Stop("LAUNCH_G6_NOT_CONFIGURED")  # this release pins no G6 command: a G6 GO is not honoured
        g6_pins = dict(cfg["g6_pins"])
    child_key = "G6:" + g6_pins["g6_command"] if g6_pins else purpose
    g6_runner = g6_bridge = g6_argv = None
    if g6_pins is not None:
        operation = _read_regular(cfg["inbox"], G6_OPERATION_FILE, env["os_uid"], env["anchor"])
        if env["sha256"](operation) != g6_pins["g6_operation_sha256"]:
            raise Stop("LAUNCH_G6_OPERATION_PIN_MISMATCH")
        # Both modules come exclusively from the already Sponsor-pinned PRE-4 loader.
        g6_runner, g6_bridge = load(G6_RUN), load(G6_CHILD_CORE)
        g6_argv = _g6_operation(operation, g6_pins, g6_runner)
    ops, subject = _json(raw["ops-v3.json"], "LAUNCH_OPS_INVALID"), _json(raw["app-subject.json"], "LAUNCH_APP_INVALID")
    cut, verify, entry = load(CUT), load(VERIFY), load(ENTRY)
    build = getattr(load(AUTH), "port_from_release", None)  # Sigstore APP + OPS v3 under the Sponsor root
    if not callable(build):
        raise Stop("LAUNCH_AUTHENTICITY_PORT_MISSING")
    ops_signature = load(OPSSIG)
    child_mod = load(cfg["children"][child_key])  # resolved BEFORE any nonce can be spent
    granted = {}  # filled by verify_go() with the AUTHENTICATED claims, inside the plane, before the nonce is spent
    if purpose == "LAUNCHER_ONLY":
        act_for = getattr(child_mod, "act_for", None)  # the launcher child is built FROM the signed claims
        if not callable(act_for):
            raise Stop("LAUNCH_CHILD_INVALID")

        def act(fd):
            claims = granted.get("claims")
            if claims is None:
                raise Stop("LAUNCH_CLAIMS_MISSING")
            return act_for(claims=dict(claims), launcher_pins=dict(cfg["launcher_pins"]))(fd)
    elif purpose == "API_WEB_QA_ONLY":
        act_for = getattr(child_mod, "act_for", None)
        if not callable(act_for):
            raise Stop("LAUNCH_CHILD_INVALID")
        apiweb_modules = {k: load(v) for k, v in APIWEB_MODULES.items()}  # ALL pinned before any nonce is spent
        if cfg["apiweb"] is None:
            raise Stop("LAUNCH_APIWEB_NOT_CONFIGURED")  # this release pins no API_WEB authority: its GO is not honoured
        apiweb_host = load(APIWEB_HOST)
        apiweb_host_modules = {k: load(v) for k, v in APIWEB_HOST_MODULES.items()}
        apiweb_effects = load(APIWEB_EFFECTS)
        apiweb_authority = dict(cfg["apiweb"], admission_sha256=cfg["pins"]["admission_sha256"],
                                policy_hash=cfg["pins"]["policy_hash"])
        apiweb_port = getattr(load(POSIX), "PosixPort")()
        outer = {}

        def act(fd):
            claims = granted.get("claims")
            if claims is None or outer.get("fp") is None:
                raise Stop("LAUNCH_CLAIMS_MISSING")
            return act_for(claims=dict(claims), modules=apiweb_modules, host=apiweb_host,
                           outer_plane=lambda: outer["fp"]._plane, port=apiweb_port,
                           authority=dict(apiweb_authority), host_modules=apiweb_host_modules,
                           effects=apiweb_effects)(fd)
    else:
        factory = getattr(child_mod, "act_for", None)
        verified = getattr(load, "_qa525_verified", None)
        verified_loader = getattr(load, "_qa525_load_module", None)
        if (not callable(factory) or verified is None or not callable(verified_loader)
                or getattr(verified, "record", None) != record):
            raise Stop("LAUNCH_G6_VERIFIED_REQUIRED")
        # Bind/check every G6 dependency and the closed argv before GO/nonce.
        bound = factory(bridge=g6_bridge, runner=g6_runner, argv=g6_argv,
                        g6_pins=dict(g6_pins), verified=verified, verified_loader=verified_loader)
        if not callable(bound):
            raise Stop("LAUNCH_CHILD_INVALID")

        def act(fd):
            claims = granted.get("claims")
            if claims is None:
                raise Stop("LAUNCH_CLAIMS_MISSING")
            return bound(claims=dict(claims), fd=fd)
    verifier = verify.verifier_for(
        keygen_path=keygen_path, trusted_binary_sha256=keygen_sha,
        allowed_signers=cfg["allowed_signers"], expected_fingerprint=cfg["go_fingerprint"],
        temp_root=cfg["temp_root"], required_euid=env["root_euid"], os_uid=env["os_uid"])
    pins = dict(cfg["pins"], go_fingerprint=cfg["go_fingerprint"])
    port = build(
        ops_signature=ops_signature, go_verify=verify, keygen=(keygen_path, keygen_sha), gh=(gh_path, gh_sha),
        policy_hash=cfg["pins"]["policy_hash"], authenticity=dict(auth_cfg),
        ops_signature_bytes=raw["ops-v3.json.sig"], app_bundle=raw["app-bundle.json"],
        trusted_root=raw["trusted-root.jsonl"],
        open_archive=lambda: _open_archive(auth_cfg["app_archive_path"], env["os_uid"], env["anchor"]),
        temp_root=cfg["temp_root"], go_fingerprint=cfg["go_fingerprint"], required_euid=env["root_euid"],
        os_uid=env["os_uid"])
    if not callable(port):
        raise Stop("LAUNCH_AUTHENTICITY_PORT_MISSING")

    def verify_go():
        now = env["now"]()
        result = cut.check_cut(
            go_bytes=raw["go.json"], go_signature_bytes=raw["go.json.sig"], pins=pins,
            ops_v3_receipt=ops, app_subject=subject, now_utc=dt.datetime.fromtimestamp(now, dt.timezone.utc),
            verify_go_signature=verifier, nonce_unused=lambda _n, _a: True,  # #967 consume is the ONLY marker
            purpose=purpose, launcher_pins=dict(cfg["launcher_pins"]) if purpose == "LAUNCHER_ONLY" else None, g6_pins=g6_pins)
        try:
            authentic = port(ops_bytes=raw["ops-v3.json"], app_subject_bytes=raw["app-subject.json"], claims=dict(result))
        except Exception:  # noqa: BLE001
            authentic = False
        if authentic is not True:  # check_cut is consistency only; receipts are claims until this port says so
            raise Stop("LAUNCH_AUTHENTICITY_DENIED")
        if g6_pins and result.get("g6_command") != g6_pins["g6_command"]:
            raise Stop("LAUNCH_GO_CLAIMS_INVALID")
        if (result.get("purpose") != purpose or type(result.get("nonce")) is not str
                or HEX64.fullmatch(result["nonce"]) is None):
            raise Stop("LAUNCH_GO_CLAIMS_INVALID")
        granted["claims"] = {"purpose": result["purpose"], "nonce": result["nonce"],
                             "app_source_sha": result["app_source_sha"],
                             "app_archive_sha256": result["app_archive_sha256"]}
        if g6_pins is not None:
            granted["claims"]["g6_command"] = result["g6_command"]
        if purpose == "API_WEB_QA_ONLY":
            granted["claims"]["expires_at"] = _epoch(result["expires_at"])  # the host gate refuses any stage after it
        return {"nonce": result["nonce"], "app_archive_sha256": result["app_archive_sha256"],
                # the nonce journal binds DIGESTS only: the OPS half is the pinned sha256 of the signed v3 receipt
                # (a 40-hex source sha here made every real consume() stop with NONCE_BINDING_INVALID)
                "ops": cfg["ops_v3_sha256"], "policy": cfg["pins"]["policy_hash"],
                "plan": result["plan_sha256"], "admission": result["admission_sha256"],
                "expires_at": _epoch(result["expires_at"])}

    locks, posix, plane_mod = load(LOCKS), load(POSIX), load(PLANE)
    flight = plane_mod.launcher_flight(locks, posix.PosixPort(), cfg["lease_dir"], uid=0)  # genuine view only
    sock = env["accept"](cfg["socket_path"], cfg["peer_uid"], cfg["timeout"])
    try:
        flight_plane = entry.FlightPlane(flight)
        if purpose == "API_WEB_QA_ONLY":
            outer["fp"] = flight_plane
        return entry.run(
            load, plane=flight_plane, verify_go=verify_go, sock=sock,
            peer_uid=cfg["peer_uid"], peer_pid=None, timeout=cfg["timeout"],
            journal_root=cfg["journal_root"], uid=0, clock=env["now"], child=act)
    finally:
        sock.close()


def _code(exc):
    text = str(exc)
    return text if CODE.fullmatch(text or "") else "LAUNCH_UNEXPECTED"


def launch(load, record):
    """Called by the bootstrap. Any failure prints ONE fixed code and exits non-zero (no traceback)."""
    import hashlib
    import time
    env = {"euid": os.geteuid, "root_euid": 0, "os_uid": 0, "anchor": "/", "now": lambda: int(time.time()), "accept": _accept,
           "sha256": lambda b: "sha256:" + hashlib.sha256(b).hexdigest()}
    try:
        _compose(load, record, env)
    except BaseException as exc:  # noqa: BLE001 - nothing may escape with a traceback or a partial state
        if isinstance(exc, (KeyboardInterrupt, SystemExit)):
            print("LAUNCH_INTERRUPTED")
        else:
            print(_code(exc))
        sys.exit(1)
    print("LAUNCH_DONE")
    return 0


if __name__ == "__main__":
    raise SystemExit("QA525_LAUNCHER_LIBRARY_ONLY_NO_ROOT")
