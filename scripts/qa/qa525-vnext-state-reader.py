#!/usr/bin/env python3
"""QA525 vNext runtime state reader -- PURE CONVERSION, INACTIVE, READ-ONLY.

Turns the 12-field per-service rows that the existing ``metadata()`` of
``qa-lite-compose-adapter.py`` already produces into the two shapes the
vNext verifier (``qa525-vnext-verify.py``) consumes:

* a sealed *baseline* (before any mutation), and
* an *observation* (during/after).

It imports no subprocess, socket, Docker client or os module. The three data
sources (rows, web revision, health) are injected callables supplied by the
future engine; this file never starts a command and never reads a container's
env vars or output (neither is part of the row shape). Anything
malformed, missing or extra is a ``Stop`` (fail-closed).

Row layout (from ``metadata()``): 0 id, 1 image id, 2 state, 3 started_at,
4 restart_count, 5 service, 6 working_dir, 7 compose config_files (comma
list), 8 mounts, 9 networks, 10 ports, 11 exit_code.
"""
import re

IMAGE_ID = re.compile(r"^(?:sha256:)?[a-f0-9]{64}$")
CONTAINER_ID = re.compile(r"^[a-f0-9]{64}$")
SHA40 = re.compile(r"^[a-f0-9]{40}$")
TARGETS = ("api", "web")
DEPENDENCIES = ("keycloak", "postgres", "minio", "gateway", "clamav")
ONESHOTS = ("db-grants", "migrator", "minio-init")
ROW_LENGTH = 12
MAX_LAYERS = 64


class Stop(Exception):
    """Fail-closed diagnostic code; never carries external text."""


def _row(rows: dict, name: str) -> list:
    row = rows[name]
    if type(row) is not list or len(row) != ROW_LENGTH or row[5] != name:
        raise Stop("ROW_MALFORMED_" + name.upper())
    if type(row[0]) is not str or CONTAINER_ID.fullmatch(row[0]) is None:
        raise Stop("ROW_ID_INVALID_" + name.upper())
    if type(row[1]) is not str or IMAGE_ID.fullmatch(row[1]) is None:
        raise Stop("ROW_IMAGE_INVALID_" + name.upper())
    if type(row[2]) is not str or type(row[11]) is not int:
        raise Stop("ROW_STATE_INVALID_" + name.upper())
    return row


def _check_rows(rows, config) -> None:
    # The only QA host has a FIXED 2+5+3 service set; a caller can never relax it.
    if config != default_config():
        raise Stop("CONFIG_NOT_DEFAULT")
    if type(rows) is not dict:
        raise Stop("ROWS_NOT_OBJECT")
    expected = set(config["targets"] + config["dependencies"] + config["oneshots"])
    if set(rows) != expected:
        raise Stop("PROJECT_CONTAINER_SET_CHANGED")


def _layers(row: list, name: str) -> list:
    value = row[7]
    if type(value) is not str or not value:
        raise Stop("LAYERS_MISSING_" + name.upper())
    paths = value.split(",")
    if not 0 < len(paths) <= MAX_LAYERS or len(set(paths)) != len(paths):
        raise Stop("LAYERS_INVALID_" + name.upper())
    for path in paths:
        if (not path.startswith("/") or "\0" in path
                or any(part in ("", ".", "..") for part in path[1:].split("/"))):
            raise Stop("LAYERS_INVALID_" + name.upper())
    return paths


def default_config() -> dict:
    return {"targets": TARGETS, "dependencies": DEPENDENCIES, "oneshots": ONESHOTS}


def web_revision_label(label_value) -> str:
    """Validate the ``org.opencontainers.image.revision`` label of the web image."""
    if type(label_value) is not str or SHA40.fullmatch(label_value) is None:
        raise Stop("WEB_PROVENANCE_INVALID")
    return label_value


def baseline_from_rows(rows: dict, web_sha: str, config: dict = None) -> dict:
    """Baseline for the verifier. Targets must be running; one-shots exited 0."""
    config = default_config() if config is None else config
    _check_rows(rows, config)
    images, layers, deps, ones = {}, {}, {}, {}
    for name in config["targets"]:
        row = _row(rows, name)
        if row[2] != "running":
            raise Stop("BASELINE_TARGET_NOT_RUNNING_" + name.upper())
        images[name] = row[1]
        layers[name] = _layers(row, name)
    for name in config["dependencies"]:
        row = _row(rows, name)
        if row[2] != "running":
            raise Stop("BASELINE_DEPENDENCY_NOT_RUNNING_" + name.upper())
        deps[name] = row[0]
    for name in config["oneshots"]:
        row = _row(rows, name)
        if row[2] != "exited" or row[11] != 0:
            raise Stop("BASELINE_ONESHOT_NOT_CLEAN_" + name.upper())
        ones[name] = {"id": row[0], "exit_code": row[11]}
    return {"images": images, "dependencies": deps, "oneshots": ones,
            "layers": layers, "web_sha": web_revision_label(web_sha)}


def observation_from_rows(rows: dict, health: dict, config: dict = None) -> dict:
    """Observation for the verifier. ``running`` is exactly state == 'running'."""
    config = default_config() if config is None else config
    _check_rows(rows, config)
    images, running, layers, deps, ones = {}, {}, {}, {}, {}
    for name in config["targets"]:
        row = _row(rows, name)
        images[name] = row[1]
        running[name] = row[2] == "running"
        layers[name] = _layers(row, name)
    for name in config["dependencies"]:
        row = _row(rows, name)
        if row[2] != "running":  # same id but stopped/restarting is NOT healthy
            raise Stop("DEPENDENCY_NOT_RUNNING_" + name.upper())
        deps[name] = row[0]
    for name in config["oneshots"]:
        row = _row(rows, name)
        if row[2] != "exited":  # a one-shot that is running again is not terminal
            raise Stop("ONESHOT_NOT_EXITED_" + name.upper())
        ones[name] = {"id": row[0], "exit_code": row[11]}
    if (type(health) is not dict or set(health) != {"ok", "web_sha"}
            or type(health["ok"]) is not bool or type(health["web_sha"]) is not str):
        raise Stop("HEALTH_INVALID")
    return {"images": images, "running": running, "dependencies": deps,
            "oneshots": ones, "layers": layers,
            "health": {"ok": health["ok"], "web_sha": health["web_sha"]}}


class StateReader:
    """Adapter for ``verify``: ``observe()``; plus ``capture_baseline()``.

    ``metadata`` -> rows dict, ``web_revision`` -> label string and ``health``
    -> {"ok", "web_sha"} are trusted local callables injected by the engine.
    ``health`` must cover API+WEB, OIDC and the image/dependency/one-shot
    state; nothing in this module proves any of that on a host.
    """

    def __init__(self, metadata, web_revision, health, config: dict = None):
        if not (callable(metadata) and callable(web_revision) and callable(health)):
            raise Stop("PROVIDERS_INVALID")
        self._metadata, self._web_revision, self._health = metadata, web_revision, health
        self._config = default_config() if config is None else config
        if self._config != default_config():
            raise Stop("CONFIG_NOT_DEFAULT")

    def capture_baseline(self) -> dict:
        return baseline_from_rows(self._metadata(), self._web_revision(), self._config)

    def observe(self) -> dict:
        return observation_from_rows(self._metadata(), self._health(), self._config)
