#!/usr/bin/env python3
"""QA525 root-launcher config assembly (INACTIVE). No keys, pins or authority are stored here.

CONFIG is deliberately None: merely importing this file, even as root, can never
enable the launcher. An authenticated PRE-4 loader may call materialize() ONLY
after it has independently checked the Sponsor's out-of-band trust anchor BEFORE
the first privileged byte. This function validates shapes and makes a detached
copy; it DOES NOT authenticate the Sponsor, release, host, filesystem or caller.

No disk, network, environment variables, command-line values, defaults or
repository-controlled bytes can supply the live config. The next binding PR
must prove that provenance explicitly; a returned namespace is not a GO.
"""
import copy
import math
import os
import re
import types

CONFIG = None  # FAIL CLOSED until a separately authenticated loader owns the values.

SHA = re.compile(r"sha256:[0-9a-f]{64}\Z")
HEX = re.compile(r"[0-9a-f]{64}\Z")
SOURCE = re.compile(r"[0-9a-f]{40}\Z")
FP = re.compile(r"SHA256:[A-Za-z0-9+/]{43}\Z")
REPO_ID = re.compile(r"[1-9][0-9]{0,19}\Z")
CONFIG_KEYS = frozenset({
    "pins", "launcher_pins", "allowed_signers", "go_fingerprint", "ops_v3_sha256",
    "app_subject_sha256", "inbox", "temp_root", "socket_path", "journal_root",
    "lease_dir", "peer_uid", "timeout", "children", "g6_pins", "authenticity",
})
PINS_KEYS = frozenset({
    "ops_source_sha", "app_source_sha", "app_archive_sha256",
    "plan_sha256", "admission_sha256", "policy_hash",
})
LAUNCHER_PIN_KEYS = frozenset({
    "release_asset_sha256", "old_launcher_sha256", "candidate_operator_sha256",
})
AUTH_KEYS = frozenset({
    "ops_allowed_signers", "ops_signature_sha256", "app_bundle_sha256",
    "trusted_root_sha256", "app_archive_path", "drop_uid", "drop_gid",
    "source_repository_id",
})
CHILD_MODULES = dict(
    {"LAUNCHER_ONLY": "qa525-vnext-root-launcher-child.py",
     "API_WEB_QA_ONLY": "qa525-vnext-root-apiweb-child.py"},
    **{"G6:" + cmd: "qa525-vnext-root-g6-%s.py" % cmd
       for cmd in ("preflight", "init", "backup", "restore", "review")}
)


class Stop(Exception):
    """Fixed code only. Never echo candidate signing material."""


def _shape(value, keys):
    return type(value) is dict and set(value) == keys


def _matches(value, pattern):
    return type(value) is str and pattern.fullmatch(value) is not None


def _path(value):
    return (type(value) is str and value.startswith("/") and
            "\0" not in value and os.path.normpath(value) == value)


def _signers(value):
    return type(value) is bytes and 0 < len(value) <= 4096


def _validated(data):
    if not _shape(data, CONFIG_KEYS):
        raise Stop("LAUNCH_CONFIG_INVALID")
    if (not _shape(data["pins"], PINS_KEYS)
            or not _shape(data["launcher_pins"], LAUNCHER_PIN_KEYS)
            or not _shape(data["authenticity"], AUTH_KEYS)
            or data["children"] != CHILD_MODULES
            or type(data["children"]) is not dict):
        raise Stop("LAUNCH_CONFIG_INVALID")
    g6 = data["g6_pins"]
    if g6 is not None and (not _shape(g6, {"g6_command", "g6_operation_sha256"})
                            or type(g6["g6_command"]) is not str
                            or g6["g6_command"] not in ("preflight", "init", "backup", "restore", "review")
                            or not _matches(g6["g6_operation_sha256"], SHA)):
        raise Stop("LAUNCH_CONFIG_INVALID")

    pins, launch, auth = data["pins"], data["launcher_pins"], data["authenticity"]
    if (not all(_matches(pins[k], SOURCE) for k in ("ops_source_sha", "app_source_sha"))
            or not all(_matches(pins[k], SHA) for k in (
                "app_archive_sha256", "plan_sha256", "admission_sha256", "policy_hash"))
            or not _matches(launch["release_asset_sha256"], SHA)
            or not all(_matches(launch[k], HEX) for k in (
                "old_launcher_sha256", "candidate_operator_sha256"))
            or not _signers(data["allowed_signers"])
            or not _matches(data["go_fingerprint"], FP)
            or not all(_matches(data[k], SHA) for k in ("ops_v3_sha256", "app_subject_sha256"))
            or not all(_path(data[k]) for k in (
                "inbox", "temp_root", "socket_path", "journal_root", "lease_dir"))):
        raise Stop("LAUNCH_CONFIG_INVALID")

    if (not _signers(auth["ops_allowed_signers"])
            or not all(_matches(auth[k], SHA) for k in (
                "ops_signature_sha256", "app_bundle_sha256", "trusted_root_sha256"))
            or not _path(auth["app_archive_path"])
            or not _matches(auth["source_repository_id"], REPO_ID)):
        raise Stop("LAUNCH_CONFIG_INVALID")

    for n in ("peer_uid",):
        if type(data[n]) is not int or data[n] <= 0:
            raise Stop("LAUNCH_CONFIG_INVALID")
    for n in ("drop_uid", "drop_gid"):
        if type(auth[n]) is not int or auth[n] <= 0:
            raise Stop("LAUNCH_CONFIG_INVALID")
    if (auth["drop_uid"] == data["peer_uid"]
            or type(data["timeout"]) not in (int, float)
            or not math.isfinite(data["timeout"])
            or not 0 < data["timeout"] <= 60):
        raise Stop("LAUNCH_CONFIG_INVALID")
    return data


def materialize(*, preverified_config):
    """Return a detached module-like CONFIG for the *already authenticated* loader.

    This shape gate is NOT a signature verifier. PRE-4 must establish the
    independent Sponsor pin, exact release bytes and provenance before calling
    or loading this module as root; there is deliberately no 'trust_me' flag.
    """
    candidate = _validated(preverified_config)
    return types.SimpleNamespace(CONFIG=copy.deepcopy(candidate))


if __name__ == "__main__":
    raise SystemExit("QA525_ROOT_CONFIG_LIBRARY_ONLY_NO_AUTHORITY_NO_ROOT_NO_GO")
