#!/usr/bin/env python3
"""Build qa525-host-prep.py = template + embedded 11 pinned libraries (reproducible: same inputs -> same bytes)."""
import base64
import hashlib
import pathlib
import sys

NAMES = (
    "qa525-vnext-schema-cut-executor.py", "qa525-vnext-schema-cut-ports.py", "qa525-vnext-schema-adapter.py",
    "qa525-vnext-schema-cut-journal-store.py", "qa525-vnext-schema-cut-journal-contract.py", "qa525-vnext-schema-cut-plan.py",
    "qa525-vnext-go-nonce-journal.py", "qa525-vnext-g6-v2-run.py", "qa525-vnext-g6-v2-producer.py",
    "qa525-vnext-g6-v2-captor.py", "qa525-vnext-g6-v2-verifier.py",
)
MARK = "BUNDLE = {}  # @@BUNDLE@@"


def build(template, srcdir):
    entries = []
    for name in sorted(NAMES):
        data = (pathlib.Path(srcdir) / name).read_bytes()
        b64 = base64.b64encode(data).decode()
        lines = [b64[i:i + 76] for i in range(0, len(b64), 76)]
        entries.append("    %r: (%r, %d, (\n%s\n    ))," % (
            name, hashlib.sha256(data).hexdigest(), len(data), "\n".join("        %r," % l for l in lines)))
    body = "BUNDLE = {\n" + "\n".join(entries) + "\n}"
    if template.count(MARK) != 1:
        raise SystemExit("template marker missing")
    return template.replace(MARK, body)


if __name__ == "__main__":
    here = pathlib.Path(__file__).resolve().parent
    out = build((here / "qa525-host-prep.template.py").read_text(), here.parent)
    (here / "qa525-host-prep.py").write_text(out)
    print(hashlib.sha256(out.encode()).hexdigest())
