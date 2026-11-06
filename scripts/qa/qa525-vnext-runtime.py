#!/usr/bin/env python3
"""QA525 vNext runtime reader wiring -- INACTIVE library, READ-ONLY, NO Docker.

Builds the ``StateReader`` of ``qa525-vnext-state-reader.py`` from the
EXISTING ``metadata(run)`` of ``qa-lite-compose-adapter.py`` plus three
caller-injected effects. This file imports no subprocess, socket, os or Docker
client: the only way it can reach a host is the injected ``run`` callable
(argv list -> stdout text) and the injected health callables, which the future
engine supplies. Passing these tests proves wiring and fail-closed behaviour
with fakes, never a physical result or permission to APPLY.

Rules enforced here:
* ``run`` is wrapped so the reader can only issue read verbs: ``ps``,
  ``inspect`` and ``image inspect``. Any other argv (``rm``, ``exec``, ``run``,
  ``compose``, ``stop`` ...) is refused BEFORE the injected runner is called.
* The web revision label comes from the image id of the SAME metadata read that
  produced the rows (no second, possibly different, container listing).
* Health is the combination of an injected boolean check (HTTPS, API
  readiness, OIDC) and an injected served-web-revision reader; an exception or
  a non-True answer yields ``ok=False``, never a pass.
* Exceptions are replaced by fixed codes; external text is never echoed.
"""
import importlib.util
from pathlib import Path

HERE = Path(__file__).resolve().parent
STATUS = "RUNTIME_READER_NOT_AUTHENTICATED_NOT_AUTHORIZED_FOR_PHYSICAL_APPLY"
READ_VERBS = ("ps", "inspect")
REVISION_FORMAT = '{{index .Config.Labels "org.opencontainers.image.revision"}}'


class Stop(Exception):
    """Fixed diagnostic code; never carries external content."""


def _load(name: str, filename: str):
    spec = importlib.util.spec_from_file_location(name, HERE / filename)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


state_reader = _load("qa525_vnext_state_reader_for_runtime", "qa525-vnext-state-reader.py")


def read_only(run):
    """Wrap ``run`` so only read-only docker verbs reach it."""
    if not callable(run):
        raise Stop("RUNTIME_RUN_INVALID")

    def guarded(args, **kwargs):
        if (type(args) is not list or not args
                or not all(type(item) is str and item and "\0" not in item for item in args)):
            raise Stop("RUNTIME_ARGV_INVALID")
        allowed = (args[0] in READ_VERBS
                   or (len(args) >= 2 and args[0] == "image" and args[1] == "inspect"))
        if not allowed or kwargs:
            raise Stop("RUNTIME_VERB_REFUSED")
        return run(args)

    return guarded


def build_reader(compose_adapter, run, health_check, served_web_revision):
    """Return a ``StateReader`` over the injected, read-only effects.

    ``compose_adapter`` exposes ``metadata(run)`` (module of
    ``qa-lite-compose-adapter.py``); ``health_check()`` must return ``True``
    only after HTTPS, API readiness and OIDC pass; ``served_web_revision()``
    returns the 40-hex revision the public web actually serves.
    """
    if not (callable(getattr(compose_adapter, "metadata", None))
            and callable(health_check) and callable(served_web_revision)):
        raise Stop("RUNTIME_PROVIDERS_INVALID")
    guarded = read_only(run)
    last = {"web_image": None}

    def metadata():
        try:
            rows = compose_adapter.metadata(guarded)
        except Stop:
            raise
        except Exception:
            raise Stop("RUNTIME_METADATA_FAILED") from None
        try:
            last["web_image"] = rows["web"][1]
        except Exception:
            last["web_image"] = None
        return rows

    def web_revision():
        image = last["web_image"]
        last["web_image"] = None  # one use: a stale id must never be reused
        if type(image) is not str or state_reader.IMAGE_ID.fullmatch(image) is None:
            raise Stop("RUNTIME_WEB_IMAGE_UNKNOWN")
        try:
            label = guarded(["image", "inspect", "--format", REVISION_FORMAT, image])
        except Stop:
            raise
        except Exception:
            raise Stop("RUNTIME_WEB_LABEL_FAILED") from None
        return state_reader.web_revision_label(label.strip() if type(label) is str else label)

    def health():
        try:
            ok = health_check() is True
            served = served_web_revision()
        except Exception:
            return {"ok": False, "web_sha": ""}
        if type(served) is not str:
            return {"ok": False, "web_sha": ""}
        return {"ok": ok, "web_sha": served}

    return state_reader.StateReader(metadata, web_revision, health)


if __name__ == "__main__":
    raise SystemExit("QA525_RUNTIME_LIBRARY_ONLY_NO_EXECUTION")
