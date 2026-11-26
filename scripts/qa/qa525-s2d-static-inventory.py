#!/usr/bin/env python3
"""QA525 S2d static inventory of the five unversioned host scripts (stdlib only, READ-ONLY, closed output).

AUXILIARY evidence only: it is NOT a certification and NEVER proves exclusion of writers.  Ambiguity => UNKNOWN.
Closed list of five fixed ids with expected sha256.  Each file is opened O_NOFOLLOW|O_NONBLOCK, must be a regular
root-owned (or --expect-uid) file with nlink 1 and no group/other write, bounded size, identical fstat before/after the
read, and its sha256 must equal the expected one BEFORE it is parsed.  The file is parsed (ast) and never imported or
executed.  Output is a fixed-schema JSON with ONLY: fixed id, sha256, size, enumerated status/verdict and integer
counts per enumerated category.  No literals, identifiers, paths, fragments or exception messages are ever emitted.
Exit: 0 = report emitted (see verdicts), 1 = at least one file not inspectable, 2 = bad invocation.
"""
import argparse
import ast
import hashlib
import json
import os
import stat
import sys

EXPECTED = {
    "run-with-backup-20261022": "b31b67a2a0d35cecd8e12e8b5fbcc072814a7fbb308aa9bf41c6213af6a38ce7",
    "qa-status": "c2f6a0734330aca7e5425bd07f13e1246dd01f188892ba10fb8c41924cd59a4d",
    "activate-local": "d2982b1fcb212f87550237580cfef07fd2b3a5d99106797a04e992f6afeff713",
    "prepare-local": "1e47858b20f8cbe51befdefb716205d70ade431f3aab5d23565c8a0cc7055d91",
    "inventory-local": "b3a59e76513926fa40179ac36211d23942244f4230a7bbc9e4fdfcfcafa7f71d",
}
MAX_SIZE = 1 << 20
CATEGORIES = ("import_stdlib", "import_other", "import_star", "dynamic_exec", "dynamic_import", "subprocess",
              "os_exec", "network", "fs_write", "fs_destructive", "fs_perm", "lock_flock", "service_ref",
              "docker_ref", "env_access", "indirect_call", "syntax_extras")
STDLIB = set(getattr(sys, "stdlib_module_names", ())) | {"__future__"}
DYN = {"exec", "eval", "compile"}
NET = {"socket", "ssl", "http", "urllib", "ftplib", "smtplib", "requests", "xmlrpc", "asyncio"}
EXEC_OS = {"system", "popen", "execv", "execve", "execvp", "execl", "execlp", "spawnl", "spawnv", "fork"}
DESTR = {("os", "remove"), ("os", "unlink"), ("os", "rmdir"), ("os", "rename"), ("os", "replace"),
         ("os", "removedirs"), ("shutil", "rmtree"), ("shutil", "move")}
PERM = {("os", "chmod"), ("os", "chown"), ("os", "fchmod"), ("os", "fchown"), ("os", "umask")}
WRITE_FS = {("os", "mkdir"), ("os", "makedirs"), ("os", "symlink"), ("os", "link"), ("os", "truncate"),
            ("shutil", "copy"), ("shutil", "copy2"), ("shutil", "copyfile"), ("shutil", "copytree")}
PATH_WRITE = {"write_text", "write_bytes", "mkdir", "touch", "unlink", "rmdir", "rename", "replace", "chmod",
              "symlink_to", "hardlink_to"}


def identity(st):
    return (st.st_dev, st.st_ino, st.st_mode, st.st_uid, st.st_nlink, st.st_size, st.st_mtime_ns, st.st_ctime_ns)


def read_safe(path, uid):
    """Returns (status, data).  status is an enumerated code; never an exception message."""
    try:
        if os.path.realpath(path) != path or not os.path.isabs(path):
            return "PATH_NOT_CANONICAL", None
        fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW | os.O_NONBLOCK | os.O_CLOEXEC)
    except OSError:
        return "UNREADABLE", None
    try:
        st = os.fstat(fd)
        if not stat.S_ISREG(st.st_mode):
            return "NOT_REGULAR", None
        if st.st_uid != uid or st.st_nlink != 1 or stat.S_IMODE(st.st_mode) & 0o022:
            return "METADATA_VIOLATION", None
        if st.st_size > MAX_SIZE:
            return "TOO_LARGE", None
        data = os.read(fd, MAX_SIZE + 1)
        if len(data) != st.st_size:
            return "SHORT_READ", None
        if identity(os.fstat(fd)) != identity(st):
            return "CHANGED_DURING_READ", None
        return "OK", data
    except OSError:
        return "UNREADABLE", None
    finally:
        os.close(fd)


class Scan(ast.NodeVisitor):
    def __init__(self):
        self.c = dict.fromkeys(CATEGORIES, 0)
        self.alias = {}  # local name -> dotted target

    def imp(self, mod):
        top = mod.split(".")[0]
        self.c["import_stdlib" if top in STDLIB else "import_other"] += 1
        if top in NET:
            self.c["network"] += 1
        if top == "importlib":
            self.c["dynamic_import"] += 1

    def visit_Import(self, n):
        for a in n.names:
            self.imp(a.name)
            self.alias[a.asname or a.name.split(".")[0]] = a.name if a.asname else a.name.split(".")[0]

    def visit_ImportFrom(self, n):
        if n.level or not n.module:
            self.c["import_other"] += 1
            return
        self.imp(n.module)
        for a in n.names:
            if a.name == "*":
                self.c["import_star"] += 1
            else:
                self.alias[a.asname or a.name] = n.module + "." + a.name

    def dotted(self, node):
        parts = []
        while isinstance(node, ast.Attribute):
            parts.append(node.attr)
            node = node.value
        if isinstance(node, ast.Name):
            base = self.alias.get(node.id, node.id)
            return ".".join([base] + parts[::-1])
        return None

    def has_marker(self, node, words):
        for sub in ast.walk(node):
            if isinstance(sub, ast.Constant) and isinstance(sub.value, (str, bytes)):
                v = sub.value.decode("latin-1") if isinstance(sub.value, bytes) else sub.value
                if any(w in v for w in words):
                    return True
        return False

    def visit_Call(self, n):
        name = self.dotted(n.func)
        if name is None and isinstance(n.func, ast.Attribute) and n.func.attr in PATH_WRITE:
            self.c["fs_write"] += 1  # e.g. Path(x).write_text(...): chained receiver, still a write
        elif name is None:
            self.c["indirect_call"] += 1
        else:
            mod, _, attr = name.rpartition(".")
            top = name.split(".")[0]
            if name in DYN:
                self.c["dynamic_exec"] += 1
            elif name in ("__import__", "importlib.import_module", "importlib.util.spec_from_file_location"):
                self.c["dynamic_import"] += 1
            elif top == "subprocess" or name.startswith("subprocess."):
                self.c["subprocess"] += 1
                if self.has_marker(n, ("systemctl", "service ")):
                    self.c["service_ref"] += 1
                if self.has_marker(n, ("docker",)):
                    self.c["docker_ref"] += 1
            elif mod == "os" and attr in EXEC_OS:
                self.c["os_exec"] += 1
            elif top in NET:
                self.c["network"] += 1
            elif name == "open" or name in ("io.open", "os.open", "pathlib.Path.open", "os.fdopen"):
                self.open_call(n, name)
            elif (mod, attr) in DESTR:
                self.c["fs_destructive"] += 1
            elif (mod, attr) in PERM:
                self.c["fs_perm"] += 1
            elif (mod, attr) in WRITE_FS:
                self.c["fs_write"] += 1
            elif name in ("fcntl.flock", "fcntl.lockf") or attr in ("flock", "lockf"):
                self.c["lock_flock"] += 1
            elif attr in ("getenv", "putenv", "unsetenv") and mod == "os":
                self.c["env_access"] += 1
            elif attr in PATH_WRITE and mod not in ("", None):
                self.c["fs_write"] += 1
            elif attr in PATH_WRITE:
                self.c["fs_write"] += 1
        self.generic_visit(n)

    def open_call(self, n, name):
        mode = None
        if len(n.args) > 1:
            mode = n.args[1]
        for kw in n.keywords:
            if kw.arg in ("mode", "flags"):
                mode = kw.value
        if mode is None:
            if name == "os.open":
                self.c["indirect_call"] += 1  # flags missing => unresolved
            return
        if isinstance(mode, ast.Constant) and isinstance(mode.value, str):
            if any(ch in mode.value for ch in "wax+"):
                self.c["fs_write"] += 1
        else:  # flags expression (os.open) or non-literal mode: any of the write flags or unresolved
            src = ast.dump(mode)
            if any(f in src for f in ("O_WRONLY", "O_RDWR", "O_CREAT", "O_TRUNC", "O_APPEND")):
                self.c["fs_write"] += 1
            elif not any(f in src for f in ("O_RDONLY",)):
                self.c["indirect_call"] += 1

    def visit_Attribute(self, n):
        if self.dotted(n) in ("os.environ",):
            self.c["env_access"] += 1
        self.generic_visit(n)

    def visit_Subscript(self, n):
        self.generic_visit(n)


def analyze(data):
    try:
        tree = ast.parse(data)
    except (SyntaxError, ValueError, RecursionError, MemoryError):
        return "PARSE_ERROR", None
    s = Scan()
    s.visit(tree)
    for n in ast.walk(tree):
        if isinstance(n, (ast.Global, ast.Nonlocal)) or isinstance(n, ast.AsyncFunctionDef):
            s.c["syntax_extras"] += 1
    return "PARSED", s.c


UNKNOWN_IF = ("import_other", "import_star", "dynamic_exec", "dynamic_import", "os_exec", "indirect_call")


def inspect(fid, path, uid):
    rec = {"id": fid, "status": None, "sha256": None, "size": None, "verdict": "UNKNOWN", "counts": None}
    status, data = read_safe(path, uid)
    if status != "OK":
        rec["status"] = status
        return rec
    rec["size"] = len(data)
    digest = hashlib.sha256(data).hexdigest()
    rec["sha256"] = digest
    if digest != EXPECTED[fid]:
        rec["status"] = "HASH_MISMATCH"
        return rec
    st, counts = analyze(data)
    if st != "PARSED":
        rec["status"] = st
        return rec
    rec["status"], rec["counts"] = "ANALYZED", counts
    # Never PASS: even without indicators this is only a static, partial view.
    rec["verdict"] = "UNKNOWN" if any(counts[k] for k in UNKNOWN_IF) else "NO_UNRESOLVED_INDICATORS_STATIC_ONLY"
    return rec


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--file", action="append", default=[], metavar="ID=ABSOLUTE_PATH")
    ap.add_argument("--expect-uid", type=int, default=0)
    ap.add_argument("--allow-non-root", action="store_true")
    try:
        a = ap.parse_args(argv)
    except SystemExit:
        return 2
    if a.expect_uid != 0 and not a.allow_non_root:
        return 2
    given = {}
    for item in a.file:
        fid, sep, path = item.partition("=")
        if not sep or fid not in EXPECTED or fid in given:
            return 2
        given[fid] = path
    if set(given) != set(EXPECTED):
        return 2
    report = {"tool": "qa525-s2d-static-inventory", "limits": "AUXILIARY_STATIC_EVIDENCE_NOT_CERTIFICATION_"
              "NEVER_PROVES_EXCLUSION", "files": [inspect(f, given[f], a.expect_uid) for f in sorted(EXPECTED)]}
    sys.stdout.write(json.dumps(report, sort_keys=True, separators=(",", ":")) + "\n")
    return 0 if all(r["status"] == "ANALYZED" for r in report["files"]) else 1


if __name__ == "__main__":
    sys.exit(main())
