#!/usr/bin/env python3
"""QA525 PRE-4 kit (INACTIVE, AUTHORITY=NONE). Offline, no network, never root.

Subcommands (run as python3 -I pre4_kit.py <cmd> ...):
  facts                               public facts of cut 8e851bc3 this kit pins
  check-config CONFIG                 shape + cross-checks of the 17-key launch-config
  verify-manifest MANIFEST CONFIG ASSET ASSET_SHA256
                                      INDEPENDENT re-derivation (own code, not the generator)
  check-admission FILE                shape/canonical check of a PROPOSED LAUNCHER_ONLY admission (no authority)
  stage ASSET ASSET_SHA256 MANIFEST CONFIG OUTDIR
                                      literal-name extraction of 48 modules + bootstrap
Nothing here signs, admits, authorizes or spends a GO. A printed hash is a CANDIDATE
until the Sponsor recomputes it on an independent device and pins it out of band.
"""
import hashlib
import importlib.util
import io
import json
import os
import re
import subprocess
import sys
import zipfile

HERE = os.path.dirname(os.path.abspath(__file__))
GENERATOR = os.path.join(HERE, "qa525-vnext-installer-manifest-build.py")
GENERATOR_SHA256 = "fd678da2bceabb8636d928481dd89ea1dcae1d25f8e322a9481971ceac2839aa"
SOURCE = "8e851bc3d2f1615a94009591f0781f88d6d3fe6f"
BOOTSTRAP = "qa525-vnext-root-bootstrap.py"
BOOTSTRAP_SHA256 = "40f201d9d84fdfcf72630090d8f591dc0f7dfb204d0e17644871d6a3107017bf"
OUTER_SHA256 = "f0a018c81150b096d13b048424baa9d3a59b0d9a2e38a3ecb5a8a49284270176"
NAME = re.compile(r"[A-Za-z0-9][A-Za-z0-9._-]{0,63}\Z")
HISTORICAL_INBOX = "/var/lib/pmgm-qa525/inbox-launcher"
OPS_FP = "SHA256:DN64Bjj/7SMjP7LX9Nwrcp3lPRBpT2cKPs1NmaL1Y5k"

# public, already-verified facts (comments #525; none is authority by itself)
FACTS = {
    "pins.ops_source_sha": SOURCE,
    "pins.app_source_sha": SOURCE,
    "pins.app_archive_sha256": "sha256:9dbd8d5bec1aa86c07664d1c78137b8798ecfdbd49972f9de531d26f0a0207a5",
    "pins.plan_sha256": "sha256:769c53b872fb44d01cd17d1151a70d00744c2c5ea6d39187c2a5a0da95626063",
    "pins.policy_hash": "sha256:ec3dbd693bd54144bb18c53d347987d0bfbaa251cde3db179b82d136a50e9837",
    "launcher_pins.release_asset_sha256": "sha256:" + OUTER_SHA256,
    "launcher_pins.old_launcher_sha256": "67f0c3932fd2cde5c71c13dc912c81653af41182ad078d0490f7d1d2afb6bcae",
    "launcher_pins.candidate_operator_sha256": "1071b7b46d58dc2869fe9fe1481ef49b0bf5db3aad14ca0265f3ec1d72865a5f",
    "ops_v3_sha256": "sha256:3771195a23cbd25578100c97edb5c1eb22c867ead7163dfa6c070f6c76cab1f4",
    "authenticity.ops_signature_sha256": "sha256:36875914c364edd1815e44a5c988cde9a15003706b9a46ec04b6fb4b3b012af2",
    "authenticity.app_bundle_sha256": "sha256:6da41f302ab7eaa4bd3a095d76cbb421538ab537df4b3f77420a1c475c6164c4",
    "authenticity.trusted_root_sha256": "sha256:65ca537f6ed8a47fd0e560c421baa1f6c1efb8b25fc200d8c5c02c0e92eb2b9c",
    "authenticity.source_repository_id": "1356543647",
}
# values only an authority can supply: reported OPEN while placeholders, never invented here
AUTHORITY = ("pins.admission_sha256", "app_subject_sha256", "allowed_signers", "go_fingerprint",
             "authenticity.ops_allowed_signers", "authenticity.app_archive_path", "inbox",
             "temp_root", "socket_path", "journal_root", "lease_dir", "peer_uid",
             "authenticity.drop_uid", "authenticity.drop_gid")


class Stop(Exception):
    pass


def _sha(b):
    return hashlib.sha256(b).hexdigest()


def _read(path, limit=1 << 28):
    fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW | os.O_NONBLOCK)
    try:
        st = os.fstat(fd)
        if not os.path.isfile(path) or st.st_size > limit or st.st_nlink < 1:
            raise Stop("KIT_INPUT_NOT_REGULAR_OR_TOO_BIG")
        with os.fdopen(os.dup(fd), "rb") as f:
            data = f.read(limit + 1)
        if len(data) != st.st_size:
            raise Stop("KIT_INPUT_CHANGED_WHILE_READING")
        return data
    finally:
        os.close(fd)


def _get(cfg, dotted):
    cur = cfg
    for p in dotted.split("."):
        cur = cur[p]
    return cur


def _generator():
    if _sha(_read(GENERATOR)) != GENERATOR_SHA256:
        raise Stop("KIT_GENERATOR_HASH_MISMATCH")
    spec = importlib.util.spec_from_file_location("qa525_manifest_build", GENERATOR)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def _key_fp(line_bytes):
    """Fingerprint of the ssh public key inside an allowed_signers line (uses ssh-keygen)."""
    parts = line_bytes.decode().strip().split()
    for i, tok in enumerate(parts):
        if tok.startswith("ssh-") and i + 1 < len(parts):
            pub = (tok + " " + parts[i + 1] + "\n").encode()
            r = subprocess.run(["ssh-keygen", "-lf", "/dev/stdin"], input=pub, capture_output=True, timeout=15)
            if r.returncode == 0 and r.stdout.split()[:2]:
                return r.stdout.decode().split()[1]
    return None


def cmd_facts(_):
    for k in sorted(FACTS):
        print("%s=%s" % (k, FACTS[k]))
    print("bootstrap_sha256=" + BOOTSTRAP_SHA256)
    print("generator_sha256=" + GENERATOR_SHA256)
    print("ops_key_fingerprint=" + OPS_FP)


def cmd_check_config(args):
    raw = _read(args[0], 65536)
    if b"@@" in raw:
        open_fields = sorted(set(re.findall(rb"@@REQUIRED:([^@]*)@@", raw)))
        print("CONFIG_OPEN placeholders=%d" % raw.count(b"@@REQUIRED"))
        for f in open_fields:
            print("  OPEN " + f.decode())
        raise Stop("CONFIG_HAS_PLACEHOLDERS")
    gen = _generator()
    try:
        gen.validate_config(raw)
    except Exception as e:  # generator Stop carries a fixed code
        raise Stop("CONFIG_SHAPE_INVALID:%s" % (e.args[0] if e.args else "?")) from None
    cfg = json.loads(raw)
    bad = [k for k, v in FACTS.items() if _get(cfg, k) != v]
    for k in sorted(FACTS):
        print("%-46s %s" % (k, "MATCH_PUBLIC_FACT" if k not in bad else "MISMATCH"))
    for k in AUTHORITY:
        v = _get(cfg, k)
        tag = "PRESENT_UNVERIFIED sha256:%s" % _sha(json.dumps(v, sort_keys=True).encode())[:12]
        print("%-46s %s" % (k, tag))
    extra = []
    if cfg["inbox"] == HISTORICAL_INBOX or cfg["inbox"].startswith(HISTORICAL_INBOX + "/"):
        extra.append("INBOX_IS_HISTORICAL")
    if len({cfg[k] for k in ("inbox", "temp_root", "socket_path", "journal_root", "lease_dir")}) != 5:
        extra.append("PATHS_NOT_DISTINCT")
    if cfg["g6_pins"] is not None or cfg["apiweb"] is not None:
        extra.append("NOT_LAUNCHER_ONLY_SHAPE")
    ops_fp = _key_fp(cfg["authenticity"]["ops_allowed_signers"].encode())
    if ops_fp != OPS_FP:
        extra.append("OPS_SIGNERS_KEY_NOT_THE_PINNED_FINGERPRINT")
    if _key_fp(cfg["allowed_signers"].encode()) != cfg["go_fingerprint"]:
        extra.append("GO_FINGERPRINT_NOT_THE_ALLOWED_SIGNERS_KEY")
    for e in extra:
        print("CROSSCHECK_FAIL " + e)
    if bad or extra:
        raise Stop("CONFIG_CROSSCHECK_FAILED")
    print("CONFIG_OK keys=17 sha256=%s AUTHORITY=NONE (shape+public facts only; admission/APP-subject/paths NOT authenticated)" % _sha(raw))


ADMISSION_KEYS = {"schema", "purpose", "source_sha", "release_asset_sha256", "app_archive_sha256",
                  "plan_sha256", "ops_v3_sha256", "issuer", "issued_at", "expires_at"}


def cmd_check_admission(args):
    """PROPOSED schema (pending ChatGPT/PO ruling). The launcher never opens this document for
    LAUNCHER_ONLY: it only compares its sha256 with pins.admission_sha256, so validity is a human
    contract, not enforced code. The sha256 printed is a CANDIDATE for the PO to pin."""
    raw = _read(args[0], 16384)
    d = json.loads(raw)
    if raw != (json.dumps(d, sort_keys=True, separators=(",", ":")) + "\n").encode():
        raise Stop("ADMISSION_NOT_CANONICAL")
    if type(d) is not dict or set(d) != ADMISSION_KEYS:
        raise Stop("ADMISSION_KEYS")
    want = {"schema": "qa525-launcher-admission-v1", "purpose": "LAUNCHER_ONLY", "source_sha": SOURCE,
            "release_asset_sha256": FACTS["launcher_pins.release_asset_sha256"],
            "app_archive_sha256": FACTS["pins.app_archive_sha256"], "plan_sha256": FACTS["pins.plan_sha256"],
            "ops_v3_sha256": FACTS["ops_v3_sha256"]}
    for k, v in want.items():
        if d[k] != v:
            raise Stop("ADMISSION_FIELD_MISMATCH:" + k)
    ia, ea = d["issued_at"], d["expires_at"]
    if type(ia) is not int or type(ea) is not int or not 900 <= ea - ia <= 86400:
        raise Stop("ADMISSION_VALIDITY_OUT_OF_RANGE")
    if type(d["issuer"]) is not str or not re.fullmatch(r"[A-Za-z0-9@._ -]{3,64}", d["issuer"]):
        raise Stop("ADMISSION_ISSUER")
    import time
    state = "VALID_NOW" if ia <= time.time() < ea else "NOT_VALID_NOW"
    print("ADMISSION_SHAPE_OK %s admission_sha256=sha256:%s AUTHORITY=NONE (candidate for the PO to pin)" % (state, _sha(raw)))


def _inner(asset_path, asset_sha):
    outer = _read(asset_path)
    if _sha(outer) != asset_sha or asset_sha != OUTER_SHA256:
        raise Stop("KIT_ASSET_SHA_MISMATCH")
    z = zipfile.ZipFile(io.BytesIO(outer))
    names = [n for n in z.namelist() if n.endswith(".zip")]
    if len(names) != 1 or z.getinfo(names[0]).file_size > 1 << 28:
        raise Stop("KIT_INNER_ZIP_NOT_UNIQUE_OR_TOO_BIG")
    inner = z.read(names[0])
    if "sha256:" + _sha(inner) != FACTS["pins.app_archive_sha256"]:
        raise Stop("KIT_INNER_ZIP_NOT_ATTESTED_SUBJECT")
    return zipfile.ZipFile(io.BytesIO(inner))


def _member(zi, name):
    if not NAME.match(name):
        raise Stop("KIT_BAD_MODULE_NAME")
    tops = {n.split("/", 1)[0] for n in zi.namelist()}
    if len(tops) != 1:
        raise Stop("KIT_INNER_ZIP_MULTIPLE_ROOTS")
    path = "%s/scripts/qa/%s" % (next(iter(tops)), name)
    info = zi.getinfo(path)  # KeyError => STOP
    if info.file_size > 1 << 22 or (info.external_attr >> 16) & 0o170000 not in (0, 0o100000):
        raise Stop("KIT_MODULE_NOT_REGULAR_OR_TOO_BIG")
    return zi.read(path)


def _parse_manifest(mraw, craw):
    m = json.loads(mraw)
    if mraw != json.dumps(m, sort_keys=True, separators=(",", ":")).encode():
        raise Stop("MANIFEST_NOT_CANONICAL")
    if set(m) != {"config", "files", "helpers", "interpreter"} or set(m["config"]) != {"name", "sha256"}:
        raise Stop("MANIFEST_SHAPE")
    if m["config"]["sha256"] != _sha(craw) or not NAME.match(m["config"]["name"]):
        raise Stop("MANIFEST_CONFIG_HASH_MISMATCH")
    if set(m["helpers"]) != {"gh", "ssh-keygen"} or len(m["files"]) != 48:
        raise Stop("MANIFEST_HELPERS_OR_FILECOUNT")
    return m


def cmd_verify_manifest(args):
    mpath, cpath, apath, asha = args
    mraw, craw = _read(mpath, 1 << 20), _read(cpath, 65536)
    m = _parse_manifest(mraw, craw)
    zi = _inner(apath, asha)
    for name, h in sorted(m["files"].items()):
        if _sha(_member(zi, name)) != h:
            raise Stop("MANIFEST_FILE_HASH_MISMATCH:" + name)
    if _sha(_member(zi, BOOTSTRAP)) != BOOTSTRAP_SHA256 or BOOTSTRAP in m["files"]:
        raise Stop("BOOTSTRAP_PIN_MISMATCH")
    if any(not m[k]["path"].startswith("/") for k in ("interpreter",)) or \
            any(not v["path"].startswith("/") for v in m["helpers"].values()):
        raise Stop("MANIFEST_PATHS_NOT_ABSOLUTE")
    print("MANIFEST_INDEPENDENT_OK files=48 bootstrap=%s MANIFEST_SHA256=%s AUTHORITY=NONE (compare with the Sponsor's device)" % (BOOTSTRAP_SHA256[:12], _sha(mraw)))


def cmd_stage(args):
    apath, asha, mpath, cpath, outdir = args
    mraw, craw = _read(mpath, 1 << 20), _read(cpath, 65536)
    m = _parse_manifest(mraw, craw)
    zi = _inner(apath, asha)
    if os.path.lexists(outdir) or not os.path.isabs(outdir):
        raise Stop("STAGE_DIR_EXISTS_OR_NOT_ABSOLUTE")
    os.mkdir(outdir, 0o700)
    out = {}
    for name in sorted(m["files"]) + [BOOTSTRAP]:
        want = m["files"].get(name, BOOTSTRAP_SHA256)
        data = _member(zi, name)
        if _sha(data) != want:
            raise Stop("STAGE_HASH_MISMATCH:" + name)
        out[name] = data
    out[m["config"]["name"]] = craw
    out["manifest-launcher.json"] = mraw
    if len(out) != 51:
        raise Stop("STAGE_FILECOUNT_NOT_51")
    for name, data in out.items():
        fd = os.open(os.path.join(outdir, name), os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600)
        with os.fdopen(fd, "wb") as f:
            f.write(data)
    print("STAGE_OK files=51 dir=%s AUTHORITY=NONE" % outdir)


def main(argv):
    cmds = {"facts": (cmd_facts, 0), "check-config": (cmd_check_config, 1),
            "verify-manifest": (cmd_verify_manifest, 4), "stage": (cmd_stage, 5),
            "check-admission": (cmd_check_admission, 1)}
    if os.geteuid() == 0:
        print("KIT_MUST_NOT_RUN_AS_ROOT")
        return 1
    if len(argv) < 2 or argv[1] not in cmds or len(argv) - 2 != cmds[argv[1]][1]:
        print(__doc__)
        return 2
    try:
        cmds[argv[1]][0](argv[2:])
    except Stop as e:
        print("STOP " + str(e))
        return 3
    except (OSError, KeyError, ValueError, zipfile.BadZipFile) as e:
        print("STOP KIT_INPUT_ERROR:" + type(e).__name__)
        return 3
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
