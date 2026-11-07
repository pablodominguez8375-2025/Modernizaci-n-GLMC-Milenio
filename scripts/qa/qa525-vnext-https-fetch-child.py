#!/usr/bin/env python3
"""QA525 vNext: one-shot child entry for ``qa525-vnext-bounded-child``. INACTIVE.

Usage (only ever spawned by the bounded runner): ``python3 -I <this> <exact URL>``.
Prints ONE ASCII JSON line: ``{"status","final_url","body"(base64)}`` on success,
or ``{"error": "HTTPSFETCH_..."}`` with exit 2 on a fixed fetch code; any other
failure exits 3 with no output.  Nothing is read from the environment, no
credentials are used and the URL must be one of the five allowed health URLs.
"""
import base64
import importlib.util
import json
from pathlib import Path
import sys

HERE = Path(__file__).resolve().parent


def _fetch_lib():
    spec = importlib.util.spec_from_file_location(
        "qa525_vnext_https_fetch_child_lib", HERE / "qa525-vnext-https-fetch.py")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def main(argv, *, lib=None, out=sys.stdout):
    if len(argv) != 2:
        return 3
    lib = lib or _fetch_lib()
    try:
        status, final, body = lib.build_fetch()(argv[1])
        line = json.dumps({"status": status, "final_url": final,
                           "body": base64.b64encode(body).decode("ascii")},
                          separators=(",", ":"))
        code = 0
    except lib.Stop as exc:
        line, code = json.dumps({"error": str(exc)}, separators=(",", ":")), 2
    except Exception:
        return 3
    out.write(line + "\n")
    out.flush()
    return code


if __name__ == "__main__":
    sys.exit(main(sys.argv))
